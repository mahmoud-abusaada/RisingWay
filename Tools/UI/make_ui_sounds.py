"""Small UI sounds for the menus' switches and sliders, in the same soft style as the turn pluck:
a short rising blip for a switch turned on, falling for off, and a tiny tick for a slider let go.
Written to Assets/Resources/Audio/UI (SoundManager loads them by name).

    python Tools/UI/make_ui_sounds.py
"""
import os
import wave
import numpy as np

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Resources', 'Audio', 'UI')
RATE = 44100


def blip(f0, f1, dur, level=0.5, second=None):
    n = int(RATE * dur)
    t = np.arange(n) / RATE
    # glide from f0 to f1, exponential
    f = f0 * (f1 / f0) ** (t / dur)
    phase = 2 * np.pi * np.cumsum(f) / RATE
    tone = np.sin(phase) + 0.25 * np.sin(2 * phase) + 0.08 * np.sin(3 * phase)
    env = np.minimum(1, t / 0.004) * np.exp(-t / (dur * 0.35))
    out = tone * env * level
    if second is not None:  # a softer second note right after: "on" is a little two-step
        g0, delay = second
        m = int(RATE * delay)
        tail = blip(g0, g0 * 1.02, dur * 0.8, level * 0.55)
        res = np.zeros(max(n, m + len(tail)))
        res[:n] += out
        res[m:m + len(tail)] += tail
        out = res
    return out


def save(name, x):
    os.makedirs(OUT, exist_ok=True)
    x = np.clip(x / max(1e-6, np.abs(x).max()) * 0.6, -1, 1)
    # a few ms of fade at the end, no click
    x[-200:] *= np.linspace(1, 0, 200)
    with wave.open(os.path.join(OUT, name + '.wav'), 'w') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print('wrote', name, '%.0f ms' % (len(x) / RATE * 1000))


save('Toggle On', blip(880, 1320, 0.07, second=(1760, 0.055)))
save('Toggle Off', blip(1320, 740, 0.09))
save('Slider Tick', blip(2200, 2000, 0.03, level=0.35))
