"""
Rising Way ambient beds - seamless loops, synthesised from scratch (no samples, no licences).

    python ambient.py <out_dir> [variant ...]      variants: orbit, nebula, drift

Every modulation is periodic over the loop length and the reverb is applied as a circular
convolution, so the end of the file flows straight into its start: no seam, no crossfade.
Oscillator frequencies are rounded to whole cycles per loop for the same reason (at most
1/(2*LOOP) Hz off - inaudible).

Layers: chord pads (additive partials, each doubled with detuned copies spread in stereo, a slow
"breathing" brightness), a sub drone, sparse bell-like glints and a long dark stereo reverb. No
noise layer: the filtered-noise "wind" of the first version read as radio hiss or a waterfall
under play. Rolled off above 4.5 kHz and set 5 dB below the first version, which was tiring. No sub drone
and nothing below 80 Hz: phone speakers do not play it, and it only took up the loudness. No rhythm, no melody: it sits under the game's sounds.
"""
import sys
import os
import numpy as np
from scipy.io import wavfile

SR = 48000
LOOP = 120.0                      # seconds
N = int(SR * LOOP)
t = np.arange(N) / SR
RNG = np.random.default_rng(7)


def hz(note):
    """'D2' / 'F#3' / 'Bb1' -> Hz, rounded to whole cycles per loop."""
    names = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}
    n = names[note[0]]
    rest = note[1:]
    if rest[0] == '#':
        n += 1; rest = rest[1:]
    elif rest[0] == 'b':
        n -= 1; rest = rest[1:]
    midi = 12 * (int(rest) + 1) + n
    f = 440.0 * 2 ** ((midi - 69) / 12)
    return periodic(f)


def periodic(f):
    return max(1, round(f * LOOP)) / LOOP


def lfo(cycles, phase=0.0):
    """A sine that completes a whole number of cycles over the loop (0..1 range)."""
    return 0.5 + 0.5 * np.sin(2 * np.pi * cycles * t / LOOP + phase)


def chord_envelopes(count, fade):
    """Raised-cosine windows: chord i sounds in slot i, crossfading `fade` seconds each side.
    Circular, so the last chord fades into the first."""
    slot = LOOP / count
    envs = []
    for i in range(count):
        centre = (i + 0.5) * slot
        d = (t - centre + LOOP / 2) % LOOP - LOOP / 2      # circular distance to the slot centre
        half = slot / 2
        e = np.clip((half + fade / 2 - np.abs(d)) / fade, 0, 1)
        envs.append(np.sin(e * np.pi / 2) ** 2)             # equal-power-ish
    total = np.sum(envs, axis=0)
    return [e / np.maximum(total, 1e-6) ** 0.5 for e in envs]


def pad_voice(freq, level, brightness, spread=0.35, partials=8, tilt=1.6):
    """One held note: additive partials, each as three detuned copies spread across the stereo
    field, with their own slow amplitude drift. `brightness` (0..1 array) opens the upper partials."""
    L = np.zeros(N); R = np.zeros(N)
    for k in range(1, partials + 1):
        fk = freq * k
        if fk > 9000:
            break
        amp = level / k ** tilt
        # upper partials follow the breathing brightness
        open_ = np.clip(brightness * 1.6 - (k - 1) / partials, 0, 1) if k > 2 else 1.0
        drift = 0.65 + 0.35 * lfo(RNG.integers(1, 5), RNG.uniform(0, 6.28))
        for cents, pan in ((-7, -spread), (0, 0.0), (6, spread)):
            f = periodic(fk * 2 ** (cents / 1200))
            ph = RNG.uniform(0, 2 * np.pi)
            s = np.sin(2 * np.pi * f * t + ph) * amp * drift * open_ / 3
            L += s * np.cos((pan + 1) * np.pi / 4)
            R += s * np.sin((pan + 1) * np.pi / 4)
    return L, R


def noise_band(lo, hi, level, cycles_a=1, cycles_b=3):
    """Stereo noise between lo and hi Hz (circular, so periodic), gently swelling."""
    out = []
    for _ in range(2):
        spec = np.fft.rfft(RNG.standard_normal(N))
        f = np.fft.rfftfreq(N, 1 / SR)
        shape = np.exp(-0.5 * ((np.log(np.maximum(f, 1)) - np.log(np.sqrt(lo * hi))) / (np.log(hi / lo) / 2.5)) ** 2)
        x = np.fft.irfft(spec * shape, N)
        out.append(x / np.std(x))
    swell = 0.35 + 0.65 * lfo(cycles_a, 1.1) * (0.6 + 0.4 * lfo(cycles_b, 0.3))
    return out[0] * level * swell, out[1] * level * swell


def glints(notes, count, level, decay=(2.5, 5.0)):
    """Sparse soft bells - distant stars. Circular: a tail past the end wraps to the start."""
    L = np.zeros(N); R = np.zeros(N)
    for _ in range(count):
        start = int(RNG.uniform(0, N))
        f = hz(RNG.choice(notes))
        dur = RNG.uniform(*decay)
        n = int(dur * SR * 1.5)
        tt = np.arange(n) / SR
        env = (1 - np.exp(-tt / 0.012)) * np.exp(-tt / (dur / 3))
        tone = (np.sin(2 * np.pi * f * tt) + 0.25 * np.sin(2 * np.pi * f * 2.01 * tt) * np.exp(-tt / 0.6)) * env
        pan = RNG.uniform(-0.8, 0.8)
        g = level * RNG.uniform(0.4, 1.0)
        idx = (start + np.arange(n)) % N
        np.add.at(L, idx, tone * g * np.cos((pan + 1) * np.pi / 4))
        np.add.at(R, idx, tone * g * np.sin((pan + 1) * np.pi / 4))
    return L, R


def reverb(L, R, seconds=7.0, predelay=0.03, dark=0.6):
    """Long stereo reverb as a circular convolution with decaying, decorrelated noise; highs die
    away faster than lows."""
    n = int(seconds * SR)
    tt = np.arange(n) / SR
    f = np.fft.rfftfreq(n, 1 / SR)
    outs = []
    for x, seed in ((L, 11), (R, 23)):
        rng = np.random.default_rng(seed)
        ir = np.zeros(n)
        for lo, hi, t60 in ((20, 400, seconds), (400, 3000, seconds * 0.8), (3000, 16000, seconds * 0.45 * (1 - dark) + 0.4)):
            spec = np.fft.rfft(rng.standard_normal(n))
            spec[(f < lo) | (f >= hi)] = 0
            band = np.fft.irfft(spec, n)
            ir += band * np.exp(-6.9 * tt / t60)
        ir = np.concatenate([np.zeros(int(predelay * SR)), ir])[:n]
        ir /= np.sqrt(np.sum(ir ** 2))
        full = np.zeros(N); full[:n] = ir
        outs.append(np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(full), N))
    return outs


def tone_shape(x, low_cut=80, high_cut=4500):
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(N, 1 / SR)
    g = 1 / np.sqrt(1 + (low_cut / np.maximum(f, 1e-3)) ** 4) / np.sqrt(1 + (f / high_cut) ** 4)
    return np.fft.irfft(spec * g, N)


def master(L, R, rms_db=-27.0):
    L = tone_shape(L); R = tone_shape(R)
    rms = np.sqrt(np.mean((L ** 2 + R ** 2) / 2))
    gain = 10 ** (rms_db / 20) / rms
    L *= gain; R *= gain
    peak = max(np.max(np.abs(L)), np.max(np.abs(R)))
    if peak > 0.89:                                   # keep -1 dBFS without clipping: soft knee
        L = np.tanh(L / 0.89) * 0.89; R = np.tanh(R / 0.89) * 0.89
    return np.stack([L, R], axis=1)


def render(chords, fade, pad_level, brightness, sub, wind, glint_notes, glint_count, glint_level,
           verb_seconds, wet, dark):
    envs = chord_envelopes(len(chords), fade)
    L = np.zeros(N); R = np.zeros(N)
    for chord, env in zip(chords, envs):
        for i, note in enumerate(chord):
            l, r = pad_voice(hz(note), pad_level * (1.0 if i else 0.8), brightness)
            L += l * env; R += r * env
    if sub:
        s = np.sin(2 * np.pi * hz(sub) * t) * 0.10 * (0.6 + 0.4 * lfo(2, 0.5))
        L += s; R += s
    if wind:
        wl, wr = noise_band(*wind)
        L += wl; R += wr
    gl, gr = glints(glint_notes, glint_count, glint_level)
    wl, wr = reverb(L + gl * 2.5, R + gr * 2.5, verb_seconds, dark=dark)
    return master(L * (1 - wet * 0.6) + gl * 0.25 + wl * wet, R * (1 - wet * 0.6) + gr * 0.25 + wr * wet)


VARIANTS = {
    # Deep and warm: D minor colours, slow, wide. The default candidate.
    'orbit': dict(
        chords=[['D2', 'A2', 'F3', 'C4', 'E4'], ['Bb1', 'F2', 'D3', 'A3', 'C4'],
                ['F2', 'C3', 'A3', 'E4', 'G4'], ['C2', 'G2', 'E3', 'A3', 'D4']],
        fade=14, pad_level=0.055, brightness=0.35 + 0.35 * lfo(3, 0.7), sub=None,
        wind=None, glint_notes=['A5', 'D6', 'E6', 'F5', 'C6'], glint_count=16, glint_level=0.05,
        verb_seconds=7.5, wet=0.75, dark=0.65),
    # Brighter and floating: E Lydian shimmer, more glints.
    'nebula': dict(
        chords=[['E2', 'B2', 'G#3', 'D#4', 'A#4'], ['C#2', 'G#2', 'E3', 'B3', 'D#4'],
                ['A1', 'E2', 'C#3', 'G#3', 'D#4'], ['B1', 'F#2', 'D#3', 'G#3', 'C#4']],
        fade=16, pad_level=0.05, brightness=0.45 + 0.35 * lfo(4, 2.0), sub=None,
        wind=None, glint_notes=['B5', 'E6', 'F#6', 'G#5', 'D#6', 'A#5'], glint_count=26, glint_level=0.055,
        verb_seconds=8.5, wet=0.8, dark=0.45),
    # Minimal: a drone of open fifths that barely moves, wind and a few far glints.
    'drift': dict(
        chords=[['D2', 'A2', 'E3', 'A3'], ['D2', 'A2', 'D3', 'G3'], ['D2', 'G2', 'D3', 'A3'], ['D2', 'A2', 'E3', 'B3']],
        fade=24, pad_level=0.06, brightness=0.25 + 0.25 * lfo(2, 1.3), sub=None,
        wind=None, glint_notes=['A5', 'D6', 'E6'], glint_count=9, glint_level=0.045,
        verb_seconds=9.0, wet=0.8, dark=0.75),
}


if __name__ == '__main__':
    out_dir = sys.argv[1]
    names = sys.argv[2:] or list(VARIANTS)
    os.makedirs(out_dir, exist_ok=True)
    for name in names:
        audio = render(**VARIANTS[name])
        path = os.path.join(out_dir, 'ambient_' + name + '.wav')
        wavfile.write(path, SR, (audio * 32767).astype(np.int16))
        rms = 20 * np.log10(np.sqrt(np.mean(audio ** 2)))
        seam = np.max(np.abs(audio[0] - audio[-1]))
        print(f'{path}: {LOOP:.0f}s, rms {rms:.1f} dBFS, peak {20*np.log10(np.max(np.abs(audio))):.1f} dBFS, '
              f'seam jump {seam:.4f}')
