# The "Nebula" UI kit: white shapes the game tints and shades in code (UiKit.cs), so one
# sprite serves every colour. Written to Assets/Resources/UI/Nebula; the .meta files there set
# the 9-slice borders.
#
#   python Tools/UI/make_nebula_ui.py
import os
import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Resources', 'UI', 'Nebula')
SS = 4  # supersampling, for clean edges


def rounded_rect_sdf(w, h, r, inset=0.0):
    """Signed distance (pixels, negative inside) to a rounded rectangle filling w x h less inset."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    xs += 0.5
    ys += 0.5
    cx, cy = w / 2.0, h / 2.0
    hx, hy = w / 2.0 - inset - r, h / 2.0 - inset - r
    qx = np.abs(xs - cx) - hx
    qy = np.abs(ys - cy) - hy
    outside = np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2)
    inside = np.minimum(np.maximum(qx, qy), 0)
    return outside + inside - r


def circle_sdf(size, radius):
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float32)
    c = size / 2.0
    return np.sqrt((xs + 0.5 - c) ** 2 + (ys + 0.5 - c) ** 2) - radius


def save(name, alpha, value=None):
    """alpha (and optional grey value) at SS x resolution, box-filtered down."""
    h, w = alpha.shape
    a = alpha.reshape(h // SS, SS, w // SS, SS).mean(axis=(1, 3))
    v = np.ones_like(a) if value is None else value.reshape(h // SS, SS, w // SS, SS).mean(axis=(1, 3))
    rgba = np.zeros(a.shape + (4,), np.uint8)
    rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = np.clip(v * 255, 0, 255).astype(np.uint8)
    rgba[..., 3] = np.clip(a * 255, 0, 255).astype(np.uint8)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(rgba, 'RGBA').save(os.path.join(OUT, name + '.png'))
    print('wrote', name, rgba.shape[1], 'x', rgba.shape[0])


def fill(d):
    return np.clip(0.5 - d, 0, 1)


# Rounded panel, 128 px, corner 36: cards, buttons, chips (9-slice 40).
d = rounded_rect_sdf(128 * SS, 128 * SS, 36 * SS)
save('round_fill', fill(d))
# The same with a light top and a darker bottom, for glass panels and buttons.
ys = np.mgrid[0:128 * SS, 0:128 * SS][0].astype(np.float32) / (128 * SS)
save('round_sheen', fill(d), 1.0 - 0.28 * ys)
# Its outline, 3 px.
save('round_outline', fill(np.abs(d + 1.5 * SS) - 1.5 * SS))
# A soft glow round the same shape, 32 px out (192 px sprite, 9-slice 72).
d = rounded_rect_sdf(192 * SS, 192 * SS, 36 * SS, inset=32 * SS)
g = np.exp(-np.maximum(d, 0) / (9.0 * SS)) * (d > -2 * SS)
save('round_glow', g * 0.9)

# Small pill for the level segments and badges (48 x 24, corner 12; 9-slice 12).
d = rounded_rect_sdf(48 * SS, 48 * SS, 12 * SS)
save('pill', fill(d))

# Circles: filled, ring, a thick ring for timers (Image.Filled, radial), a soft glow.
S = 256
save('circle', fill(circle_sdf(S * SS, 124 * SS)))
save('ring', fill(np.abs(circle_sdf(S * SS, 121 * SS)) - 3 * SS))
save('ring_thick', fill(np.abs(circle_sdf(S * SS, 113 * SS)) - 11 * SS))
r = (circle_sdf(S * SS, 0) / (128 * SS)).clip(0, 1)
save('glow', np.exp(-(r ** 2) * 6.0) * (1 - r) ** 2)

# A vertical shade for behind a menu: darker at the top and bottom, clear in the middle.
H = 256
t = (np.arange(H * SS, dtype=np.float32) + 0.5) / (H * SS)
shade = 0.75 * np.clip(1 - t / 0.35, 0, 1) ** 1.5 + 0.55 * np.clip((t - 0.7) / 0.3, 0, 1) ** 1.5 + 0.18
save('shade', np.tile(shade[:, None], (1, 4 * SS)))

# A cut gem, facets in shades of white (tinted violet in the game): the diamonds count.
from PIL import ImageDraw
G = 128 * SS
gem = Image.new('LA', (G, G), (0, 0))
dr = ImageDraw.Draw(gem)
P = lambda pts: [(x * SS, y * SS) for x, y in pts]
facets = [
    ([(36, 26), (92, 26), (78, 50), (50, 50)], 255),          # table
    ([(36, 26), (50, 50), (12, 50)], 215),                   # crown, left
    ([(92, 26), (116, 50), (78, 50)], 170),                  # crown, right
    ([(12, 50), (50, 50), (64, 112)], 190),                  # pavilion, left
    ([(50, 50), (78, 50), (64, 112)], 235),                  # pavilion, middle
    ([(78, 50), (116, 50), (64, 112)], 140),                 # pavilion, right
]
for pts, v in facets:
    dr.polygon(P(pts), fill=(v, 255))
a = np.asarray(gem, np.float32) / 255.0
save('gem', a[..., 1], a[..., 0])
