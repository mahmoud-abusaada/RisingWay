"""
A look at audio I cannot hear: a spectrogram image plus numbers that flag what an ambient bed must
not have - drums or other sharp hits, silence gaps, clipping.

    python inspect_audio.py <file.wav|flac> [more ...]     writes <file>.png next to each
"""
import sys
import numpy as np
from PIL import Image


def load(path):
    """(channels, samples) float32 in -1..1, and the sample rate. PyAV reads wav and flac alike."""
    import av
    with av.open(path) as f:
        stream = f.streams.audio[0]
        sr = stream.rate
        ch = stream.channels
        chunks = []
        for fr in f.decode(stream):
            a = fr.to_ndarray()
            if a.shape[0] == 1 and ch > 1:          # packed (interleaved) sample format
                a = a.reshape(-1, ch).T
            chunks.append(a)
    x = np.concatenate(chunks, axis=1).astype(np.float32)
    if np.issubdtype(chunks[0].dtype, np.integer):
        x /= np.iinfo(chunks[0].dtype).max
    return x, sr


def analyse(path):
    x, sr = load(path)
    mono = x.mean(axis=0)
    n_fft, hop = 4096, 1024
    win = np.hanning(n_fft)
    frames = np.lib.stride_tricks.sliding_window_view(mono, n_fft)[::hop] * win
    spec = np.abs(np.fft.rfft(frames, axis=1)) + 1e-9
    db = 20 * np.log10(spec)
    freqs = np.fft.rfftfreq(n_fft, 1 / sr)

    # log-frequency image, 20 Hz .. 16 kHz, 256 rows
    rows = 256
    edges = np.geomspace(20, 16000, rows + 1)
    img = np.zeros((rows, db.shape[0]))
    for i in range(rows):
        m = (freqs >= edges[i]) & (freqs < edges[i + 1])
        if m.any():
            img[rows - 1 - i] = db[:, m].max(axis=1)
    top = img.max()
    img = np.clip((img - (top - 80)) / 80, 0, 1)
    cols = min(img.shape[1], 1200)
    img = np.array(Image.fromarray((img * 255).astype(np.uint8)).resize((cols, rows * 2)))
    rgb = np.stack([img, (img * 0.6).astype(np.uint8), (255 - img) // 3], axis=2)
    Image.fromarray(rgb.astype(np.uint8)).save(path + '.png')

    # onset strength: positive spectral flux, normalised - high peaks mean hits / drums
    flux = np.maximum(np.diff(db, axis=0), 0).mean(axis=1)
    flux = (flux - np.median(flux)) / (np.std(flux) + 1e-9)
    hits = int(np.sum(flux > 6))
    rms = 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12)
    peak = 20 * np.log10(np.max(np.abs(x)) + 1e-12)
    # 1 s windows: loudness range and quiet gaps
    w = sr
    lv = [20 * np.log10(np.sqrt(np.mean(mono[i:i + w] ** 2)) + 1e-12) for i in range(0, len(mono) - w, w)]
    centroid = float((spec * freqs).sum() / spec.sum())
    print(f'{path}: {len(mono)/sr:.1f}s {sr}Hz ch={x.shape[0]}  rms {rms:.1f} dBFS  peak {peak:.1f}  '
          f'1s-level range {min(lv):.1f}..{max(lv):.1f}  sharp onsets {hits}  spectral centroid {centroid:.0f} Hz')


if __name__ == "__main__":
    for p in sys.argv[1:]:
        analyse(p)
