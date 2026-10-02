"""
Rising Way - the sound of a run. Not music: no melody, no drums. Three seamless layers that the
game mixes by how fast the ball is going (SoundManager), and one short sound for a turn.

    python run_layers.py <out_dir>

  run_bed.wav      a dark held drone (D and its fifth), always there during a run. It replaces the
                   menus' slow chords, which felt too calm for a ball that is racing.
  run_pulse.wav    the same harmony breathing in time - eight soft swells every 5 seconds - and a
                   low heartbeat under it. This is what gives the run a pace. The game plays it
                   faster as the ball speeds up (pitch and tempo rise together, a little).
  run_shimmer.wav  high, bright partials that flutter quickly. Silent at the start of a run, it
                   comes in as the speed climbs and is loudest at top speed and during a bolt.
  turn.wav         a soft glassy pluck (D5). The game plays it on each turn, a step higher up a
                   pentatonic scale every time, so a run of quick turns climbs.

All three layers are the same length and the same key, every modulation is periodic over the loop
and the reverb is circular, so each loops without a seam and they stay in step with each other.
Nothing below 90 Hz (a phone speaker cannot play it) and no noise (it read as hiss under play).
"""
import sys
import os
import numpy as np
from scipy.io import wavfile

SR = 44100
BPM = 96.0
BEAT = 60.0 / BPM
BEATS = 64                           # 16 bars of 4
LOOP = BEAT * BEATS                  # 40 s
N = int(round(SR * LOOP))
t = np.arange(N) / SR
RNG = np.random.default_rng(11)
NOTE = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}


def periodic(f):
    return max(1, round(f * LOOP)) / LOOP


def hz(note):
    n = NOTE[note[0]]
    rest = note[1:]
    if rest[0] == '#':
        n += 1; rest = rest[1:]
    elif rest[0] == 'b':
        n -= 1; rest = rest[1:]
    return periodic(440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12))


def lfo(cycles, phase=0.0):
    return 0.5 + 0.5 * np.sin(2 * np.pi * cycles * t / LOOP + phase)


def voice(freq, level, partials=8, tilt=1.6, spread=0.35, bright=1.0):
    L = np.zeros(N); R = np.zeros(N)
    for k in range(1, partials + 1):
        fk = freq * k
        if fk > 9000:
            break
        amp = level / k ** tilt * (bright if k > 2 else 1.0)
        drift = 0.7 + 0.3 * lfo(RNG.integers(1, 6), RNG.uniform(0, 6.28))
        for cents, pan in ((-6, -spread), (0, 0.0), (7, spread)):
            f = periodic(fk * 2 ** (cents / 1200))
            s = np.sin(2 * np.pi * f * t + RNG.uniform(0, 6.28)) * amp * drift / 3
            L += s * np.cos((pan + 1) * np.pi / 4)
            R += s * np.sin((pan + 1) * np.pi / 4)
    return L, R


def slots(count, fade):
    """Equal-power windows for `count` chords around the loop, crossfading over `fade` seconds."""
    slot = LOOP / count
    envs = []
    for i in range(count):
        d = (t - (i + 0.5) * slot + LOOP / 2) % LOOP - LOOP / 2
        e = np.clip((slot / 2 + fade / 2 - np.abs(d)) / fade, 0, 1)
        envs.append(np.sin(e * np.pi / 2) ** 2)
    total = np.sum(envs, axis=0)
    return [e / np.maximum(total, 1e-6) ** 0.5 for e in envs]


def reverb(L, R, seconds, dark=0.6):
    n = int(seconds * SR)
    tt = np.arange(n) / SR
    f = np.fft.rfftfreq(n, 1 / SR)
    outs = []
    for x, seed in ((L, 11), (R, 23)):
        rng = np.random.default_rng(seed)
        ir = np.zeros(n)
        for lo, hi, t60 in ((20, 400, seconds), (400, 3000, seconds * 0.8), (3000, 16000, seconds * 0.45 * (1 - dark) + 0.3)):
            spec = np.fft.rfft(rng.standard_normal(n))
            spec[(f < lo) | (f >= hi)] = 0
            ir += np.fft.irfft(spec, n) * np.exp(-6.9 * tt / t60)
        ir /= np.sqrt(np.sum(ir ** 2))
        full = np.zeros(N); full[:n] = ir
        outs.append(np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(full), N))
    return outs


def shape(x, low_cut, high_cut):
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(N, 1 / SR)
    g = 1 / np.sqrt(1 + (low_cut / np.maximum(f, 1e-3)) ** 4) / np.sqrt(1 + (f / high_cut) ** 4)
    return np.fft.irfft(spec * g, N)


def master(L, R, rms_db, low_cut=90, high_cut=6000):
    L = shape(L, low_cut, high_cut); R = shape(R, low_cut, high_cut)
    rms = np.sqrt(np.mean((L ** 2 + R ** 2) / 2))
    g = 10 ** (rms_db / 20) / rms
    L *= g; R *= g
    peak = max(np.max(np.abs(L)), np.max(np.abs(R)))
    if peak > 0.89:
        L = np.tanh(L / 0.89) * 0.89; R = np.tanh(R / 0.89) * 0.89
    return np.stack([L, R], axis=1)


def save(path, audio):
    wavfile.write(path, SR, (audio * 32767).astype(np.int16))
    rms = 20 * np.log10(np.sqrt(np.mean(audio ** 2)))
    print(f'{path}: {len(audio) / SR:.2f}s rms {rms:.1f} dBFS peak {20 * np.log10(np.max(np.abs(audio))):.1f} dBFS '
          f'seam {np.max(np.abs(audio[0] - audio[-1])):.4f}')


# The harmony all three layers share: four colours over a D that never leaves.
CHORDS = [['D3', 'A3', 'F4', 'C5'], ['D3', 'Bb3', 'F4', 'A4'], ['D3', 'A3', 'E4', 'G4'], ['D3', 'G3', 'D4', 'F4']]


def run_bed():
    L = np.zeros(N); R = np.zeros(N)
    for note, level in (('D2', 0.10), ('A2', 0.07), ('D3', 0.06), ('A3', 0.03)):
        l, r = voice(hz(note), level, partials=7, tilt=1.45, bright=0.5 + 0.5 * lfo(2, 1.0))
        L += l; R += r
    # A slow swell, twice a loop, so it is not a flat tone.
    swell = 0.75 + 0.25 * lfo(2, 0.4)
    L *= swell; R *= swell
    wl, wr = reverb(L, R, 5.0, dark=0.75)
    return master(L * 0.5 + wl * 0.7, R * 0.5 + wr * 0.7, -26.0, high_cut=3200)


def run_pulse():
    envs = slots(len(CHORDS), 3.0)
    L = np.zeros(N); R = np.zeros(N)
    for chord, env in zip(CHORDS, envs):
        for i, note in enumerate(chord):
            l, r = voice(hz(note), 0.05 if i else 0.04, partials=9, tilt=1.35, bright=0.55 + 0.45 * lfo(4, 0.3))
            L += l * env; R += r * env
    # The breathing: one swell per half beat (eighths), rounded - a pump, not a gate. Alternate
    # swells a little weaker, which is what makes it feel like a pace and not a tremolo.
    phase = (t / (BEAT / 2)) % 1.0
    swell = np.sin(np.pi * phase) ** 1.5
    strong = ((t / (BEAT / 2)).astype(int) % 2 == 0)
    pump = 0.30 + 0.70 * swell * np.where(strong, 1.0, 0.72)
    L *= pump; R *= pump
    # The heartbeat: a soft low thump on every beat, a second softer one after it on beats 1 and 3.
    beat = np.zeros(N)
    for b in range(BEATS):
        for offset, gain in ((0.0, 1.0), (0.5, 0.0 if b % 2 else 0.45)):
            if gain == 0.0:
                continue
            start = int(round((b + offset) * BEAT * SR))
            n = int(0.32 * SR)
            tt = np.arange(n) / SR
            f = 118 * np.exp(-tt * 9) + 96          # a quick drop in pitch: the thump
            tone = np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.35 * np.sin(4 * np.pi * np.cumsum(f) / SR)
            env = (1 - np.exp(-tt / 0.004)) * np.exp(-tt / 0.085)
            np.add.at(beat, (start + np.arange(n)) % N, tone * env * gain)
    wl, wr = reverb(L, R, 2.6, dark=0.5)
    L = L * 0.75 + wl * 0.45 + beat * 0.11
    R = R * 0.75 + wr * 0.45 + beat * 0.11
    return master(L, R, -25.0, high_cut=5200)


def run_shimmer():
    envs = slots(len(CHORDS), 3.0)
    tops = [['A5', 'E6'], ['F5', 'D6'], ['G5', 'E6'], ['F5', 'C6']]
    L = np.zeros(N); R = np.zeros(N)
    for chord, env in zip(tops, envs):
        for note in chord + ['D6']:
            l, r = voice(hz(note), 0.04, partials=4, tilt=1.2, spread=0.8)
            L += l * env; R += r * env
    # Flutter: sixteenths, out of step between left and right, so it moves across the stereo field.
    sixteenth = BEAT / 4
    fl = 0.35 + 0.65 * np.sin(np.pi * ((t / sixteenth) % 1.0)) ** 2
    fr = 0.35 + 0.65 * np.sin(np.pi * ((t / sixteenth + 0.5) % 1.0)) ** 2
    L *= fl; R *= fr
    wl, wr = reverb(L, R, 3.2, dark=0.2)
    return master(L * 0.6 + wl * 0.7, R * 0.6 + wr * 0.7, -28.0, low_cut=500, high_cut=9000)


def turn():
    """A short glassy pluck, D5: a few inharmonic partials that die at different rates."""
    dur = 0.55
    n = int(dur * SR)
    tt = np.arange(n) / SR
    f0 = 587.33
    x = np.zeros(n)
    for ratio, amp, decay in ((1.0, 1.0, 0.16), (2.0, 0.38, 0.09), (3.01, 0.16, 0.05), (4.2, 0.07, 0.03)):
        x += amp * np.sin(2 * np.pi * f0 * ratio * tt) * np.exp(-tt / decay)
    x *= 1 - np.exp(-tt / 0.0025)                       # no click at the start
    x *= np.clip((dur - tt) / 0.05, 0, 1)                # nor at the end
    x = x / np.max(np.abs(x)) * 0.5
    return np.stack([x, x], axis=1)


if __name__ == '__main__':
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    save(os.path.join(out, 'run_bed.wav'), run_bed())
    save(os.path.join(out, 'run_pulse.wav'), run_pulse())
    save(os.path.join(out, 'run_shimmer.wav'), run_shimmer())
    save(os.path.join(out, 'turn.wav'), turn())
