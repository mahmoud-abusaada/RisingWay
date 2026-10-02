"""
Turns a stretch of a generated piece into a seamless loop at the game's level.

    python make_loop.py <in.flac|wav> <start s> <end s> <out.wav> [crossfade s = 6]

The stretch's last `crossfade` seconds are blended into its first (equal power), so the file
ends exactly where it starts. Slow levelling (2 s windows) evens out the piece's swells so it
stays a background, then it is set to -22 dBFS RMS like the synthesised beds.
"""
import sys
import numpy as np
from scipy.io import wavfile
from scipy.ndimage import uniform_filter1d
from inspect_audio import load

src, start, end, out = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), sys.argv[4]
xf = float(sys.argv[5]) if len(sys.argv) > 5 else 6.0

x, sr = load(src)
if x.shape[0] == 1:
    x = np.vstack([x, x])
seg = x[:, int(start * sr):int(end * sr)].astype(np.float64)
n = int(xf * sr)

# Levelling: divide by a slow envelope (2 s), only partly (ratio ~2:1), so swells stay but shrink.
power = uniform_filter1d((seg ** 2).mean(axis=0), size=2 * sr, mode='wrap')
env = np.sqrt(power) + 1e-6
target = np.sqrt(np.mean(power))
seg *= (target / env) ** 0.5

# Seamless: the tail fades into the head.
fade = np.linspace(0, np.pi / 2, n)
head, tail = seg[:, :n], seg[:, -n:]
loop = seg[:, :-n].copy()
loop[:, :n] = head * np.sin(fade) + tail * np.cos(fade)

rms = np.sqrt(np.mean(loop ** 2))
loop *= 10 ** (-22 / 20) / rms
peak = np.max(np.abs(loop))
if peak > 0.89:
    loop = np.tanh(loop / 0.89) * 0.89
wavfile.write(out, sr, (loop.T * 32767).astype(np.int16))
print(f'{out}: {loop.shape[1] / sr:.1f}s loop from {start}-{end}s, peak {20 * np.log10(np.max(np.abs(loop))):.1f} dBFS')
