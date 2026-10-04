"""
The game's sound for a recorded clip, in step with its picture.

A clip is rendered frame by frame, much slower than real time, so Unity's own audio output while
recording is useless. Instead the game writes down what it plays (Assets/Scripts/Diagnostics/
SoundLog.cs -> <clip>/sound.jsonl) and this plays the same sound files back to that score:
  - one-shots (turns, diamonds, power-ups, menu clicks) at the moment they were played, at their
    volume and pitch;
  - every source playing a clip of its own (the ambient loop, the run's three layers, the bolt's
    loop), followed frame by frame: volume, pitch and low-pass muffling as the game set them.
Only the game's own sounds; no music is ever added.

    python mix_audio.py <clip folder> [<clip folder> ...]   ->  <clip folder>/audio.wav (48 kHz stereo)
"""
import json
import math
import os
import sys

import av
import numpy as np
from scipy.io import wavfile
from scipy.signal import lfilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SR = 48000

_clips = {}


def clip(path):
    """A sound file as float32 stereo at SR, shape (n, 2)."""
    if path not in _clips:
        box = av.open(os.path.join(ROOT, path))
        res = av.AudioResampler(format='flt', layout='stereo', rate=SR)
        parts = []
        for frame in box.decode(audio=0):
            for f in res.resample(frame):
                parts.append(f.to_ndarray().reshape(-1, 2))
        for f in res.resample(None):
            parts.append(f.to_ndarray().reshape(-1, 2))
        box.close()
        _clips[path] = np.concatenate(parts).astype(np.float32) if parts else np.zeros((1, 2), np.float32)
    return _clips[path]


def lowpass(cut, q):
    """Unity's AudioLowPassFilter as a biquad (RBJ), or None when it lets everything through."""
    if cut is None or cut >= 21000:
        return None
    w0 = 2 * math.pi * max(cut, 10.0) / SR
    alpha = math.sin(w0) / (2 * max(q or 1.0, 0.1))
    c = math.cos(w0)
    b = np.array([(1 - c) / 2, 1 - c, (1 - c) / 2])
    a = np.array([1 + alpha, -2 * c, 1 - alpha])
    return b / a[0], a / a[0]


def read_at(data, pos, step, n, loop):
    """n samples from data starting at sample pos, step clip samples per output sample (pitch)."""
    idx = pos + np.arange(n) * step
    length = len(data)
    if loop:
        idx = np.mod(idx, length)
        live = np.ones(n, bool)
    else:
        live = idx < length - 1
        idx = np.minimum(idx, length - 1.001)
    i0 = np.floor(idx).astype(np.int64)
    i1 = np.minimum(i0 + 1, length - 1) if not loop else (i0 + 1) % length
    frac = (idx - i0)[:, None]
    out = data[i0] * (1 - frac) + data[i1] * frac
    out[~live] = 0
    return out


def limit(x, ceiling=0.9, block=240, release=0.15):
    """Where many sounds pile up (a row of diamonds) the sum goes past full scale: turned down just
    there, at once, and back up slowly - a phone's speaker would have clipped. Elsewhere untouched."""
    n = len(x)
    nb = (n + block - 1) // block
    pad = np.zeros((nb * block, 2), np.float32)
    pad[:n] = x
    peaks = np.abs(pad).reshape(nb, block, 2).max(axis=(1, 2))
    want = np.minimum(1.0, ceiling / np.maximum(peaks, 1e-9))
    # look one block ahead, so the gain is already down when the peak arrives
    want = np.minimum(want, np.concatenate([want[1:], want[-1:]]))
    rise = 1 - math.exp(-block / (release * SR))
    gain = np.empty(nb)
    g = 1.0
    for i in range(nb):
        g = want[i] if want[i] < g else g + (want[i] - g) * rise
        gain[i] = g
    smooth = np.interp(np.arange(nb * block), np.arange(nb) * block + block / 2, gain)
    return np.clip(pad * smooth[:, None], -0.99, 0.99)[:n]


def mix(folder):
    lines = [json.loads(l) for l in open(os.path.join(folder, 'sound.jsonl'), encoding='utf-8') if l.strip()]
    frames = [l for l in lines if 'frame' in l]
    shots = [l for l in lines if 'shot' in l]
    stops = [l for l in lines if 'stop' in l]
    if not frames:
        raise SystemExit('no frames in ' + folder)
    dt = frames[1]['t'] - frames[0]['t'] if len(frames) > 1 else 1 / 60
    total = int(round((frames[-1]['t'] + dt) * SR)) + SR * 4  # room for the last one-shots' tails
    out = np.zeros((total, 2), np.float32)

    # Sources playing a clip of their own, frame by frame.
    state = {}  # src -> {'pos': clip samples, 'zi': filter state}
    for k, fr in enumerate(frames):
        s0 = int(round(fr['t'] * SR))
        s1 = int(round((frames[k + 1]['t'] if k + 1 < len(frames) else fr['t'] + dt) * SR))
        n = s1 - s0
        if n <= 0:
            continue
        listener = fr.get('listener', 1.0)
        nxt = {s['src']: s for s in frames[k + 1]['frame']} if k + 1 < len(frames) else {}
        seen = set()
        for s in fr['frame']:
            src = s['src']
            seen.add(src)
            data = clip(s['clip'])
            st = state.get(src)
            if st is None or st['clip'] != s['clip']:
                st = state[src] = {'clip': s['clip'], 'pos': s['pos'] * SR, 'zi': None}
            v0 = s['vol'] * listener
            v1 = (nxt[src]['vol'] if src in nxt else s['vol']) * listener
            chunk = read_at(data, st['pos'], s['pitch'], n, s['loop'])
            st['pos'] += n * s['pitch']
            if s['loop']:
                st['pos'] %= len(data)
            filt = lowpass(s.get('cut'), s.get('q'))
            if filt is not None:
                b, a = filt
                if st['zi'] is None:
                    st['zi'] = np.zeros((2, 2))
                chunk, st['zi'] = lfilter(b, a, chunk, axis=0, zi=st['zi'])
            out[s0:s1] += chunk * np.linspace(v0, v1, n, endpoint=False)[:, None]
        for src in list(state):
            if src not in seen:
                del state[src]  # stopped: starts again from its own place if it comes back

    # One-shots, each as a whole.
    for sh in shots:
        data = clip(sh['shot'])
        step = sh.get('pitch', 1.0) or 1.0
        n = int(len(data) / step)
        s0 = int(round(sh['t'] * SR))
        end = min(total, s0 + n)
        # cut short where its source was stopped
        for st in stops:
            if st['stop'] == sh['src'] and st['t'] >= sh['t']:
                end = min(end, int(round(st['t'] * SR)))
                break
        n = end - s0
        if n <= 0:
            continue
        chunk = read_at(data, 0.0, step, n, False)
        filt = lowpass(sh.get('cut'), sh.get('q'))
        if filt is not None:
            chunk = lfilter(filt[0], filt[1], chunk, axis=0)
        fade = min(n, int(0.01 * SR))
        if end < s0 + int(len(data) / step):  # stopped: a short fade, not a click
            chunk[-fade:] *= np.linspace(1, 0, fade)[:, None]
        out[s0:end] += chunk * sh['vol']

    # trim the silent tail, keep the peaks under full scale (only ever turned down)
    last = int(round((frames[-1]['t'] + dt) * SR))
    loud = np.nonzero(np.abs(out).max(axis=1) > 1e-4)[0]
    out = out[:max(last, loud[-1] + 1 if len(loud) else last)]
    peak = float(np.abs(out).max()) if len(out) else 0.0
    out = limit(out)
    path = os.path.join(folder, 'audio.wav')
    wavfile.write(path, SR, out.astype(np.float32))
    print('wrote %s  %.1f s  %d one-shots  peak %.2f' % (path, len(out) / SR, len(shots), peak))
    return path


if __name__ == '__main__':
    for f in sys.argv[1:]:
        mix(f)
