"""
The galaxy texture for the sky dome (Assets/Shaders/SpaceSky.shader): the glow of the Milky Way as
seen from inside it, without its stars. Painted from a model, not a photograph.

    python make_galaxy.py <project root> [seed]

Why not the 8k panorama already in the project (Textures/Ambient/Galaxy/8k_stars_milky_way.jpg):
it is an 8-bit JPEG whose whole galaxy sits in the bottom 15% of the range. Stretched far enough to
be seen it shows its compression blocks. And its stars are part of the picture; at any size a
phone can afford they are blurred squares, so the game draws stars as points (StarField.shader)
and the texture only has to carry what is smooth.

The model, in galactic coordinates (the band along the equator, the centre in the middle of the
picture, which is where SpaceSky.shader expects them):
  - a thin disc of unresolved stars, brighter and thicker towards the centre;
  - the central bulge, warmer in colour;
  - dust lanes along the plane that darken and redden what is behind them (the Great Rift);
  - star clouds: the disc is clumpy, not smooth;
  - a few pink emission nebulae near the plane, and the two Magellanic Clouds off it.
All the noise is 3D noise sampled on the sphere, so there is no seam and no pinching at the poles.
"""
import sys, os
import numpy as np
from PIL import Image
from scipy import ndimage

root = sys.argv[1]
seed = int(sys.argv[2]) if len(sys.argv) > 2 else 4
rng = np.random.default_rng(seed)
W, H = 2048, 1024
out = os.path.join(root, 'Assets/Textures/Ambient/Sky/MilkyWayGlow.png')

# Directions for every texel. l: galactic longitude, 0 in the middle; b: latitude.
u = (np.arange(W) + 0.5) / W
v = (np.arange(H) + 0.5) / H
l = (u - 0.5) * 2 * np.pi
b = (0.5 - v) * np.pi
L, B = np.meshgrid(l, b)
X = np.cos(B) * np.sin(L)
Y = np.sin(B)
Z = np.cos(B) * np.cos(L)


def noise3(freq, salt):
    """Smooth 3D value noise on the sphere, 0..1."""
    n = 32
    grid = np.random.default_rng(seed * 1000 + salt).random((n, n, n)).astype(np.float32)
    c = [((a * 0.5 + 0.5) * freq) % n for a in (X, Y, Z)]
    return ndimage.map_coordinates(grid, c, order=3, mode='grid-wrap')


def fbm(freq, octaves, salt, gain=0.5):
    total = np.zeros_like(X, dtype=np.float32)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        total += amp * noise3(freq * 2 ** o, salt + o)
        norm += amp
        amp *= gain
    return total / norm


def ridged(freq, octaves, salt):
    """Thin bright ridges: filaments."""
    total = np.zeros_like(X, dtype=np.float32)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        n = 1 - np.abs(noise3(freq * 2 ** o, salt + o) * 2 - 1)
        total += amp * n * n
        norm += amp
        amp *= 0.55
    return total / norm


deg = np.pi / 180
lat = B / deg
lon = L / deg

# The plane of the disc wanders a little (the real one is warped and we sit slightly above it).
lat_w = lat - 1.5 + 5.0 * (fbm(3, 2, 10) - 0.5)

# Disc: exponential in latitude; thicker and brighter towards the centre.
toward_centre = np.exp(-(lon / 75) ** 2)
thickness = 3.5 + 5.0 * toward_centre
disc = np.exp(-np.abs(lat_w) / thickness) * (0.22 + 0.78 * toward_centre)
# A wide faint skirt, so the band has no hard edge.
disc += 0.045 * np.exp(-(lat_w / 16) ** 2) * (0.3 + 0.7 * toward_centre)

# Star clouds.
clouds = fbm(12, 6, 20, gain=0.6)
clouds = np.clip((clouds - 0.28) / 0.45, 0, 1)
disc *= 0.18 + 1.9 * clouds ** 1.5

# Bulge.
bulge = np.exp(-((lon / 11) ** 2 + (lat_w / 7.5) ** 2))
bulge += 0.35 * np.exp(-((lon / 24) ** 2 + (lat_w / 13) ** 2))

# Dust: filaments hugging the plane, strongest through the centre; one long rift.
dust_field = ridged(11, 5, 40) * 0.5 + fbm(9, 5, 50, gain=0.6) * 0.5
near_plane = np.exp(-(lat_w / (6.0 + 7.0 * toward_centre)) ** 2)
rift = np.exp(-((lat_w - 0.5 - 3.0 * (fbm(2.5, 2, 60) - 0.5) * 2) / (1.2 + 2.2 * fbm(6, 3, 65))) ** 2) * (0.3 + 0.7 * np.exp(-(lon / 110) ** 2))
dust = np.clip((dust_field - 0.36) * 4.0, 0, 1) * near_plane * 1.1 + 0.8 * rift * np.clip((fbm(16, 4, 70, gain=0.6) - 0.25) * 2.2, 0, 1)
dust = np.clip(dust, 0, 1.4)

disc_col = np.array([0.62, 0.74, 1.00])
bulge_col = np.array([1.00, 0.84, 0.62])
light = disc[..., None] * disc_col * 0.62 + bulge[..., None] * bulge_col * 0.85
# Extinction is stronger in blue than in red: dust reddens as it darkens.
ext = np.array([1.35, 1.8, 2.4])
light *= np.exp(-dust[..., None] * ext)


def blob(lon0, lat0, size, colour, strength, ragged=0.6, salt=0):
    d2 = ((((lon - lon0 + 180) % 360) - 180) / size) ** 2 + ((lat - lat0) / size) ** 2
    shape = np.exp(-d2 * (0.6 + 1.6 * fbm(22, 4, 300 + salt))) * np.clip((fbm(30, 4, 200 + salt, gain=0.6) - 0.2) * 2.2, 0, 1.5) ** (ragged * 2)
    return shape[..., None] * np.array(colour) * strength


# Emission nebulae along the plane: pink hydrogen, a touch of teal oxygen.
for i in range(16):
    lon0 = rng.uniform(-170, 170)
    lat0 = rng.normal(0, 2.2)
    size = rng.uniform(0.7, 2.6)
    colour = (1.0, 0.32, 0.45) if rng.random() < 0.75 else (0.35, 0.85, 0.9)
    light += blob(lon0, lat0, size, colour, rng.uniform(0.10, 0.28), salt=i)

# The Magellanic Clouds.
light += blob(-79, -33, 2.8, (0.80, 0.86, 1.0), 0.12, 0.0, 90)
light += blob(-57, -44, 1.4, (0.80, 0.86, 1.0), 0.08, 0.0, 91)
# Andromeda, small and faint.
light += blob(121, -22, 1.1, (1.0, 0.92, 0.8), 0.16, 0.2, 92)

# Tone: keep the core from clipping, lift the faint outskirts a little.
light = 1 - np.exp(-light * 1.9)
img = np.clip(light, 0, 1) ** (1 / 2.2)
# Dither, so the long gradients do not band in 8 bits.
img += (rng.random(img.shape) - 0.5) / 255
img = (np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)
os.makedirs(os.path.dirname(out), exist_ok=True)
Image.fromarray(img).save(out)
Image.fromarray(img).resize((1200, 600), Image.LANCZOS).save(os.path.join(root, 'Builds', 'galaxy_preview.jpg'), quality=92)
print(out, 'mean', img.mean() / 255)
