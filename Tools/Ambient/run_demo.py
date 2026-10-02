"""
A listening demo of the run's sound, mixed the way SoundManager.mixRun mixes it in the game:
20 seconds each at the start of a run, halfway to top speed, and at top speed, with the turn
plucks at about the rate turns come at those speeds.

    python run_demo.py <folder with run_bed.wav, run_pulse.wav, run_shimmer.wav, turn.wav> <out.wav>
"""
import sys
import os
import numpy as np
from scipy.io import wavfile

folder, out = sys.argv[1], sys.argv[2]


def load(name):
    sr, x = wavfile.read(os.path.join(folder, name))
    return sr, x.astype(np.float32) / 32768.0


SR, bed = load('run_bed.wav')
_, pulse = load('run_pulse.wav')
_, shimmer = load('run_shimmer.wav')
_, pluck = load('turn.wav')
SCALE = [0, 3, 5, 7, 10, 12, 15, 17, 19, 22, 24, 22, 19, 17, 15, 12, 10, 7, 5, 3]


def play(x, pitch, seconds, start=0.0):
    """x looped, played `pitch` times faster, for `seconds`."""
    n = int(seconds * SR)
    pos = (start * SR + np.arange(n) * pitch) % len(x)
    i = pos.astype(int)
    f = (pos - i)[:, None]
    return x[i] * (1 - f) + x[(i + 1) % len(x)] * f


def smoothstep(a, b, v):
    u = np.clip((v - a) / (b - a), 0, 1)
    return u * u * (3 - 2 * u)


parts = []
step = 0
for intensity, turn_every in ((0.0, 1.9), (0.5, 1.25), (1.0, 0.85)):
    seconds = 20.0
    pitch = 1.0 + 0.16 * intensity
    mix = play(bed, pitch, seconds) * 0.9
    mix += play(pulse, pitch, seconds) * (0.5 + 0.5 * intensity)
    mix += play(shimmer, pitch, seconds) * smoothstep(0.25, 1.0, intensity) * 0.9
    at = 1.0
    while at < seconds - 1:
        p = 2 ** (SCALE[step % len(SCALE)] / 12) * pitch
        note = play(pluck, p, len(pluck) / SR / p * 0.999)[: int(len(pluck) / p) - 2] * 0.6
        a = int(at * SR)
        mix[a:a + len(note)] += note[: len(mix) - a]
        step += 1
        at += turn_every
    fade = int(0.5 * SR)
    mix[:fade] *= np.linspace(0, 1, fade)[:, None]
    mix[-fade:] *= np.linspace(1, 0, fade)[:, None]
    parts.append(mix)

audio = np.concatenate(parts)
audio = np.clip(audio, -1, 1)
wavfile.write(out, SR, (audio * 32767).astype(np.int16))
print(out, len(audio) / SR, 'seconds, peak', float(np.max(np.abs(audio))))
