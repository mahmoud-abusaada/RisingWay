"""Social media posts for Rising Way: reels/shorts, a YouTube trailer, its thumbnail and still
posts, made from the game's own footage (StoreCapture -shotVideo, see Assets/Scripts/Diagnostics/
VideoRecorder.cs) with the game's font, and the game's own sound (Tools/Social/mix_audio.py) -
never music. Best quality throughout: footage recorded larger than it is shown, lossless frames,
60 fps, near-lossless H.264 with 320 kb/s AAC, pictures at JPEG 100 with full colour.

    python Tools/Social/make_social.py [only-these-names...]

Reads Builds/Social/raw/<clip>/f_00000.png... and sound.jsonl, writes Builds/Social/out/. Needs
Pillow, PyAV, numpy and scipy.
"""
import os
import sys
from fractions import Fraction

import av
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from scipy.io import wavfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mix_audio  # noqa: E402

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
RAW = os.path.join(ROOT, 'Builds', 'Social', 'raw')
OUT = os.path.join(ROOT, 'Builds', 'Social', 'out')
FONT = os.path.join(ROOT, 'Assets', 'Fonts', 'Rexlia.otf')
ICON = os.path.join(ROOT, 'Assets', 'Textures', 'ic_launcher.png')
FPS = 60
SR = mix_audio.SR
# The landscape run is recorded at 4608x2592 with the camera 1.333x closer in (-videoZoom), and
# shown a further 1.2x closer at 3840x2160 - every picture pixel a real one.
LAND_ZOOM = 1.2

WHITE = (255, 255, 255)
CYAN = (63, 224, 255)
VIOLET = (140, 108, 255)
GOLD = (255, 212, 90)
NAVY = (8, 16, 52)
GLOW = (45, 107, 255)

_fonts = {}


def font(size):
    if size not in _fonts:
        _fonts[size] = ImageFont.truetype(FONT, size)
    return _fonts[size]


# ---------------------------------------------------------------------------------------------
# Text: the store art's look - white (or coloured) letters, a dark navy outline, a blue glow.
# A layer is made once and faded in and out per frame.
# ---------------------------------------------------------------------------------------------
def text_layer(lines, size, color=WHITE, max_width=None, glow=GLOW, spacing=1.15, stroke=None):
    if isinstance(lines, str):
        lines = [lines]
    f = font(size)
    # shrink to fit
    if max_width:
        while max(f.getbbox(l)[2] for l in lines) > max_width and size > 20:
            size -= 2
            f = font(size)
    stroke = stroke if stroke is not None else max(3, size // 14)
    pad = size
    widths = [f.getbbox(l, stroke_width=stroke)[2] for l in lines]
    line_h = int(size * spacing)
    w = max(widths) + pad * 2
    h = line_h * len(lines) + pad * 2
    base = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(base)
    for i, l in enumerate(lines):
        x = (w - widths[i]) // 2
        d.text((x, pad + i * line_h), l, font=f, fill=color + (255,), stroke_width=stroke, stroke_fill=NAVY + (255,))
    if glow:
        halo = Image.new('RGBA', (w, h), glow + (0,))
        a = base.getchannel('A').filter(ImageFilter.GaussianBlur(size / 5))
        a = a.point(lambda v: min(255, int(v * 1.6)))
        halo.putalpha(a)
        halo.alpha_composite(base)
        base = halo
    return base


def pill(text, size, fg=WHITE, bg=(10, 18, 50, 200), edge=CYAN, pad=(0.9, 0.45)):
    f = font(size)
    l, t, r, b = f.getbbox(text)
    w = r + int(size * pad[0] * 2)
    h = int(size * (1 + pad[1] * 2))
    im = Image.new('RGBA', (w + 8, h + 8), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((4, 4, w + 4, h + 4), radius=h // 2, fill=bg, outline=edge + (230,), width=max(2, size // 16))
    d.text(((w + 8 - r) // 2, (h + 8) // 2 - (t + b) // 2), text, font=f, fill=fg + (255,))
    return im


def place(frame, layer, cx, cy, alpha=1.0):
    """Lays <layer> centred at (cx, cy) - fractions of the frame - faded by alpha."""
    if alpha <= 0:
        return
    if alpha < 1:
        layer = layer.copy()
        layer.putalpha(layer.getchannel('A').point(lambda v: int(v * alpha)))
    x = int(cx * frame.width - layer.width / 2)
    y = int(cy * frame.height - layer.height / 2)
    frame.alpha_composite(layer, (x, y))


def fade(t, t0, t1, fin=0.25, fout=0.25):
    if t < t0 or t > t1:
        return 0.0
    return max(0.0, min(1.0, (t - t0) / fin if fin else 1.0, (t1 - t) / fout if fout else 1.0))


def ease(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


# ---------------------------------------------------------------------------------------------
# Footage
# ---------------------------------------------------------------------------------------------
def frames(clip):
    d = os.path.join(RAW, clip)
    return [os.path.join(d, n) for n in sorted(os.listdir(d)) if n.startswith('f_')]


def cover(im, w, h, zoom=1.0, cx=0.5, cy=0.5):
    """Scales <im> to cover w x h (times zoom) and crops around (cx, cy)."""
    s = max(w / im.width, h / im.height) * zoom
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    x = min(max(0, int(cx * im.width - w / 2)), im.width - w)
    y = min(max(0, int(cy * im.height - h / 2)), im.height - h)
    return im.crop((x, y, x + w, y + h))


class Video:
    def __init__(self, name, w, h):
        os.makedirs(OUT, exist_ok=True)
        self.path = os.path.join(OUT, name + '.mp4')
        self.box = av.open(self.path, 'w', options={'movflags': '+faststart'})
        self.s = self.box.add_stream('libx264', rate=FPS)
        self.s.width, self.s.height = w, h
        self.s.pix_fmt = 'yuv420p'
        self.s.time_base = Fraction(1, FPS)
        self.s.options = {'crf': '12', 'preset': 'slow', 'profile': 'high',
                          'level': '5.2' if w * h > 1920 * 1080 else '4.2', 'g': str(FPS * 2)}
        self.a = self.box.add_stream('aac', rate=SR)
        self.a.layout = 'stereo'
        self.a.bit_rate = 320000
        self.w, self.h, self.n = w, h, 0
        self.sounds = []

    def sound(self, clip, at, src_from, dur, fin=0.02, fout=0.15, gain=1.0):
        """The game's sound of <clip> from <src_from> s, laid at <at> s of this video for <dur> s."""
        self.sounds.append((clip, at, src_from, dur, fin, fout, gain))

    def add(self, im):
        f = av.VideoFrame.from_image(im.convert('RGB'))
        f.pts = self.n
        self.n += 1
        for p in self.s.encode(f):
            self.box.mux(p)

    def close(self):
        for p in self.s.encode():
            self.box.mux(p)
        n = int(round(self.n / FPS * SR))
        track = np.zeros((n, 2), np.float32)
        for clip, at, src_from, dur, fin, fout, gain in self.sounds:
            data = clip_sound(clip)
            s0, d0 = int(round(at * SR)), int(round(src_from * SR))
            m = max(0, min(int(round(dur * SR)), n - s0, len(data) - d0))
            if m <= 0:
                continue
            env = np.ones(m, np.float32)
            a, b = min(m, int(fin * SR)), min(m, int(fout * SR))
            if a:
                env[:a] *= np.linspace(0, 1, a)
            if b:
                env[-b:] *= np.linspace(1, 0, b)
            track[s0:s0 + m] += data[d0:d0 + m] * env[:, None] * gain
        track = np.clip(track, -0.99, 0.99)
        step = 1024
        for i in range(0, n, step):
            part = np.ascontiguousarray(track[i:i + step].T)
            f = av.AudioFrame.from_ndarray(part, format='fltp', layout='stereo')
            f.sample_rate = SR
            f.pts = i
            for p in self.a.encode(f):
                self.box.mux(p)
        for p in self.a.encode():
            self.box.mux(p)
        self.box.close()
        print('wrote', self.path, '%.1fs' % (self.n / FPS), 'with sound' if self.sounds else 'SILENT')


_sounds = {}


def clip_sound(clip):
    """A raw clip's game sound (mixed from its sound.jsonl when that is newer than the mix)."""
    if clip not in _sounds:
        d = os.path.join(RAW, clip)
        wav, log = os.path.join(d, 'audio.wav'), os.path.join(d, 'sound.jsonl')
        if not os.path.exists(log):
            raise SystemExit('no sound recorded for ' + clip + ' (record it again: VideoRecorder writes sound.jsonl)')
        if not os.path.exists(wav) or os.path.getmtime(wav) < os.path.getmtime(log):
            mix_audio.mix(d)
        sr, data = wavfile.read(wav)
        assert sr == SR
        _sounds[clip] = data.astype(np.float32)
    return _sounds[clip]


# ---------------------------------------------------------------------------------------------
# The end card: the icon, the name, where to get it - over the last picture, darkened.
# ---------------------------------------------------------------------------------------------
# Where to get it, on every end card and picture. (The black holes wait for their own
# campaign: nothing in this batch shows them.)
STORE_LINE = 'FREE ON GOOGLE PLAY AND APP STORE'


def icon(size):
    im = Image.open(ICON).convert('RGBA').resize((size, size), Image.LANCZOS)
    mask = Image.new('L', (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size - 1, size - 1), radius=size // 5, fill=255)
    im.putalpha(mask)
    halo = Image.new('RGBA', (size * 2, size * 2), CYAN + (0,))
    a = Image.new('L', (size * 2, size * 2), 0)
    a.paste(mask, (size // 2, size // 2))
    halo.putalpha(a.filter(ImageFilter.GaussianBlur(size / 7)).point(lambda v: int(v * 0.8)))
    halo.alpha_composite(im, (size // 2, size // 2))
    return halo


def fitted_pill(line, size, max_w):
    """The store pill, its words made smaller until it fits the width."""
    while True:
        p = pill(line, size, bg=(255, 255, 255, 235), fg=NAVY, edge=CYAN)
        if p.width <= max_w or size < 12:
            return p
        size -= 2


def end_card(bg, t, w, h, line=STORE_LINE, extra=None):
    """One frame of the end card, t seconds in."""
    k = ease(t / 0.6)
    frame = bg.filter(ImageFilter.GaussianBlur(4 + 14 * k)).convert('RGBA')
    shade = Image.new('RGBA', frame.size, (4, 6, 22, int(175 * k)))
    frame.alpha_composite(shade)
    portrait = h > w
    u = min(w, h)
    place(frame, _cache('icon', lambda: icon(int(u * (0.30 if portrait else 0.26)))), 0.5, 0.33 if portrait else 0.3, ease((t - 0.15) / 0.5))
    place(frame, _cache('title%d' % u, lambda: text_layer('RISING WAY', int(u * 0.11), max_width=w * 0.9)), 0.5, 0.52 if portrait else 0.6, ease((t - 0.3) / 0.5))
    place(frame, _cache('sub%d' % u, lambda: text_layer('SPACE BALL RUN', int(u * 0.05), CYAN)), 0.5, 0.585 if portrait else 0.72, ease((t - 0.4) / 0.5))
    place(frame, _cache('play%d%s' % (u, line), lambda: fitted_pill(line, int(u * 0.042), w * 0.86)),
          0.5, 0.68 if portrait else 0.86, ease((t - 0.7) / 0.4))
    if extra:
        place(frame, _cache('extra' + extra, lambda: text_layer(extra, int(u * 0.045), GOLD, max_width=w * 0.86)), 0.5, 0.77 if portrait else 0.95, ease((t - 1.0) / 0.4))
    return frame


_layers = {}


def _cache(key, make):
    if key not in _layers:
        _layers[key] = make()
    return _layers[key]


# ---------------------------------------------------------------------------------------------
# A clip: footage segments, captions over them, an end card.
#   segments: [(clip, from_s, to_s[, zoom, cy, cx])]  cut together (a quick dissolve between them)
#   captions: [(layer, cx, cy, t0, t1)]  times in the finished clip
# ---------------------------------------------------------------------------------------------
def make(name, w, h, segments, captions, end=2.6, end_line=STORE_LINE, end_extra=None, zoom=1.0, cy=0.5):
    v = Video(name, w, h)
    t = 0.0
    last = None
    XF = 8 / FPS  # the dissolve between segments: the sound crosses over in the same time
    for si, seg in enumerate(segments):
        clip, a, b = seg[:3]
        final = si == len(segments) - 1
        v.sound(clip, t, a, (b - a) + (end if final else XF), fin=XF if si else 0.02,
                fout=end * 0.9 if final else XF)
        z = seg[3] if len(seg) > 3 else zoom   # a segment can be closer in: (clip, from, to, zoom, cy)
        zy = seg[4] if len(seg) > 4 else cy
        zx = seg[5] if len(seg) > 5 else 0.5
        fs = frames(clip)
        i0, i1 = int(a * FPS), min(len(fs), int(b * FPS))
        before = last
        for i in range(i0, i1):
            im = cover(Image.open(fs[i]), w, h, z, zx, zy).convert('RGBA')
            # a short dissolve from the previous segment's last picture
            if before is not None and i - i0 < 8:
                im = Image.blend(before, im, (i - i0 + 1) / 9)
            fr = im.copy()
            for layer, cx, cy_, t0, t1 in captions:
                place(fr, layer, cx, cy_, fade(t, t0, t1))
            v.add(fr)
            last = im
            t += 1 / FPS
    for k in range(int(end * FPS)):
        v.add(end_card(last.convert('RGB'), k / FPS, w, h, end_line, end_extra))
    v.close()
    return v.path


def still(name, src, w, h, title, sub=None, cy=0.5, zoom=1.0, ty=0.12, title_size=None, badge=True, sub_color=CYAN, quality=100, band=True, foot_line=None, cx=0.5):
    im = cover(Image.open(src), w, h, zoom, cx, cy).convert('RGBA')
    # a soft dark band behind the words, top (not over a picture whose top must show)
    if band:
        shade = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        bd = ImageDraw.Draw(shade)
        for y in range(int(h * (ty + 0.2))):
            bd.line([(0, y), (w, y)], fill=(4, 6, 22, int(190 * (1 - y / (h * (ty + 0.2))) ** 1.2)))
        im.alpha_composite(shade)
    u = min(w, h)
    place(im, text_layer(title.split('\n'), title_size or int(u * 0.095), max_width=w * 0.92), 0.5, ty)
    if sub:
        place(im, text_layer(sub.split('\n'), int(u * 0.042), sub_color, max_width=w * 0.9), 0.5, ty + (0.1 if h > w else 0.16) * (1 + title.count('\n') * 0.8))
    if badge:
        # bottom: icon + name + Google Play
        foot = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        fd = ImageDraw.Draw(foot)
        for y in range(int(h * 0.8), h):
            fd.line([(0, y), (w, y)], fill=(4, 6, 22, int(200 * ((y - h * 0.8) / (h * 0.2)) ** 1.3)))
        im.alpha_composite(foot)
        ic = icon(int(u * 0.12))
        im.alpha_composite(ic, (int(w * 0.04) - ic.width // 4, int(h * 0.92 - ic.height / 2)))
        label = text_layer('RISING WAY', int(u * 0.045), glow=None)
        im.alpha_composite(label, (int(w * 0.04 + u * 0.12 * 0.85), int(h * 0.905 - label.height / 2)))
        gp = text_layer(foot_line or STORE_LINE, int(u * 0.026), CYAN, glow=None)
        im.alpha_composite(gp, (int(w * 0.04 + u * 0.12 * 0.85), int(h * 0.945 - gp.height / 2)))
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + '.jpg')
    im.convert('RGB').save(path, quality=quality, subsampling=0)
    print('wrote', path)
    return path


def frame_of(clip, s):
    fs = frames(clip)
    return fs[min(len(fs) - 1, int(s * FPS))]


# ---------------------------------------------------------------------------------------------
# The posts
# ---------------------------------------------------------------------------------------------
def T(text, size, color=WHITE, w=1080):
    return text_layer(text.split('\n'), size, color, max_width=w * 0.9)


def reel_tap_to_turn():
    make('reel_1_tap_to_turn', 1080, 1920,
         [('saturn', 0, 9.5)],
         [(T('TAP TO TURN', 110), 0.5, 0.36, 0.2, 3.0),
          (T('ONE TAP.\nEVERY TURN.', 70, CYAN), 0.5, 0.36, 3.2, 6.0),
          (T('HOW HIGH\nCAN YOU CLIMB?', 92), 0.5, 0.36, 6.2, 9.5)])


def reel_bolt():
    # (in the saturn clip the bolt runs from about 8.3 s to 12.8 s; the best is beaten at about 16.4)
    make('reel_2_bolt', 1080, 1920,
         [('saturn', 5.5, 18.5)],
         [(T('GRAB A BOLT...', 84), 0.5, 0.36, 0.3, 2.6),
          (T('SUPER SPEED', 120, GOLD), 0.5, 0.36, 2.8, 7.2),
          (T('NEW BEST\nIN SECONDS', 90, CYAN), 0.5, 0.36, 10.8, 13.0)])


# the parade clip's balls, 2 s each: -videoBalls 53,63,55,62,67,58,60,64,54
PARADE = [('EARTH', WHITE), ('SATURN', GOLD), ('THE SUN', GOLD), ('URANUS', CYAN), ('BRIGHT STAR', CYAN),
          ('JUPITER', GOLD), ('MARS', (255, 120, 100)), ('NEPTUNE', CYAN), ('THE MOON', WHITE)]


def reel_parade():
    caps = [(T('WHICH BALL\nARE YOU?', 100), 0.5, 0.17, 0.2, 18.0)]
    for i, (n, c) in enumerate(PARADE):
        caps.append((pill(n, 54, fg=c), 0.5, 0.74, i * 2 + 0.15, i * 2 + 1.95))
    make('reel_3_which_ball', 1080, 1920, [('parade', 0, 18.0, 1.35, 0.56)], caps, end_extra='COLLECT THEM ALL')


TRI_FROM = 2.0  # the triptych's first phone: the saturn clip from here (its sound too)


def triptych(t, w=1920, h=1080):
    """Three phone clips side by side on a dark, blurred backdrop (the trailer's 'balls' part)."""
    k = w / 1920
    gap = round(24 * k)
    picks = [('saturn', TRI_FROM + t, 1.0), ('parade', 4.0 + t, 1.0), ('parade', 12.0 + t, 1.0)]
    ph = h - round(120 * k)
    pw = round(ph * 9 / 16)
    bg = cover(Image.open(frame_of('parade', 10.0 + t)), w, h).filter(ImageFilter.GaussianBlur(30 * k))
    frame = bg.convert('RGBA')
    frame.alpha_composite(Image.new('RGBA', (w, h), (4, 6, 22, 150)))
    x0 = (w - 3 * pw - 2 * gap) // 2
    for i, (clip, s, z) in enumerate(picks):
        im = cover(Image.open(frame_of(clip, s)), pw, ph, 1.25 if clip == 'parade' else 1.0, 0.5, 0.56)
        mask = Image.new('L', (pw, ph), 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, pw - 1, ph - 1), radius=round(28 * k), fill=255)
        x = x0 + i * (pw + gap)
        e = round(4 * k)
        edge = Image.new('RGBA', (pw + 2 * e, ph + 2 * e), (0, 0, 0, 0))
        ImageDraw.Draw(edge).rounded_rectangle((0, 0, pw + 2 * e - 1, ph + 2 * e - 1), radius=round(32 * k),
                                               outline=CYAN + (200,), width=max(3, round(3 * k)))
        top = (h - ph) // 2
        frame.paste(im, (x, top), mask)
        frame.alpha_composite(edge, (x - e, top - e))
    return frame


def youtube_trailer():
    # 4K. The landscape run (see LAND_ZOOM). The bolt (-shotBoltAt 14) runs at about
    # BOLT_FROM..BOLT_TO seconds of the clip.
    w, h = 3840, 2160
    K = w / 1920
    BOLT_FROM, BOLT_TO = TRAILER_BOLT
    v = Video('youtube_trailer_16x9', w, h)
    fs = frames('landscape')
    # 0-9 s: the run, the name and the idea; 9-14: three balls side by side; then the run again
    # from just before the bolt, to the end of the clip.
    intro = BOLT_FROM - 1.8
    caps = [(T('RISING WAY', int(150 * K), w=w), 0.5, 0.17, 0.3, 3.4),
            (T('SPACE BALL RUN', int(60 * K), CYAN, w=w), 0.5, 0.29, 0.6, 3.4),
            (T('TAP TO TURN', int(110 * K), w=w), 0.5, 0.17, 3.7, 6.2),
            (T('CLIMB AN ENDLESS TRACK\nTHROUGH SPACE', int(76 * K), CYAN, w=w), 0.5, 0.2, 6.5, 9.0),
            (T('PLAY AS PLANETS,\nMOONS AND STARS', int(76 * K), w=w), 0.5, 0.5, 9.3, 13.8)]
    timeline = [('land', 0.0, 9.0), ('tri', 0.0, 5.0), ('land', intro, 30.0)]
    t_bolt = 14.0 + (BOLT_FROM - intro)
    t_after = 14.0 + (BOLT_TO - intro)
    caps += [(T('GRAB A BOLT...', int(96 * K), w=w), 0.5, 0.17, t_bolt - 1.6, t_bolt - 0.1),
             (T('SUPER SPEED', int(130 * K), GOLD, w=w), 0.5, 0.17, t_bolt + 0.1, t_after),
             (T('HOW HIGH CAN YOU CLIMB?', int(100 * K), w=w), 0.5, 0.17, t_after + 1.5, 14.0 + 30.0 - intro)]
    t = 0.0
    last = None
    XF = 8 / FPS
    for si, (kind, a, b) in enumerate(timeline):
        before = last
        final = si == len(timeline) - 1
        v.sound('saturn' if kind == 'tri' else 'landscape', t, TRI_FROM if kind == 'tri' else a,
                (b - a) + (3.5 if final else XF), fin=XF if si else 0.02, fout=3.2 if final else XF)
        n = int((b - a) * FPS)
        for i in range(n):
            if kind == 'tri':
                im = triptych(a + i / FPS, w, h)
            else:
                im = cover(Image.open(fs[min(len(fs) - 1, int(a * FPS) + i)]), w, h, LAND_ZOOM, 0.5, 0.6).convert('RGBA')
            if before is not None and i < 8:
                im = Image.blend(before, im, (i + 1) / 9)
            fr = im.copy()
            for layer, cx, cy_, t0, t1 in caps:
                place(fr, layer, cx, cy_, fade(t, t0, t1))
            v.add(fr)
            last = im
            t += 1 / FPS
    for k in range(int(3.5 * FPS)):
        v.add(end_card(last.convert('RGB'), k / FPS, w, h))
    v.close()


TRAILER_BOLT = (10.4, 15.2)  # when the bolt runs in the landscape clip (seen in its frames)


STILLS = os.path.join(RAW, 'stills')  # clean 1440x2560 pictures, no UI (StoreCapture stills, see the header)


def S(name):
    return os.path.join(STILLS, name + '.png')


# The carousel: the ball close-ups (StoreCapture -shotCloseup -shotBalls ...), in order.
CAROUSEL = [('close_6_ball53', 'EARTH', CYAN), ('close_6_ball63', 'SATURN', GOLD), ('close_6_ball62', 'URANUS', CYAN),
            ('close_6_ball54', 'THE MOON', WHITE), ('close_6_ball55', 'THE SUN', GOLD), ('close_6_ball58', 'JUPITER', GOLD),
            ('close_6_ball60', 'MARS', (255, 120, 100)), ('close_6_ball64', 'NEPTUNE', CYAN), ('close_6_ball67', 'BRIGHT STAR', CYAN)]


def carousel_cover(w=1080, h=1350):
    """Slide 1: the balls in a 3x3 grid under the question."""
    im = Image.new('RGBA', (w, h), (4, 6, 22, 255))
    cell, gap = 300, 24
    x0 = (w - 3 * cell - 2 * gap) // 2
    y0 = 330
    for k, (src, n, c) in enumerate(CAROUSEL):
        tile = cover(Image.open(S(src)), cell, cell, 1.9, 0.5, 0.47)
        mask = Image.new('L', (cell, cell), 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, cell - 1, cell - 1), radius=36, fill=255)
        x, y = x0 + (k % 3) * (cell + gap), y0 + (k // 3) * (cell + gap)
        im.paste(tile, (x, y), mask)
        ImageDraw.Draw(im).rounded_rectangle((x, y, x + cell - 1, y + cell - 1), radius=36, outline=c + (200,), width=3)
    place(im, text_layer(['WHICH BALL', 'ARE YOU?'], 96), 0.5, 0.12)
    place(im, text_layer('Swipe to see them all', 40, CYAN), 0.5, 0.95)
    path = os.path.join(OUT, 'ig_4_carousel_0_cover.jpg')
    im.convert('RGB').save(path, quality=100, subsampling=0)
    print('wrote', path)


def stills():
    # Instagram / Facebook feed: 1080x1350 (4:5)
    still('ig_1_we_are_back', S('saturn_9'), 1080, 1350, 'TAP. TURN.\nCLIMB.', 'Rising Way is back on Google Play and the App Store', cy=0.5, zoom=1.15)
    still('ig_2_super_speed', S('saturn_12'), 1080, 1350, 'SUPER SPEED', 'Grab a bolt and fly up the track', cy=0.52, zoom=1.25, sub_color=GOLD)
    # the challenge keeps the run's UI: the score and NEW BEST at the top, words lower down
    still('ig_3_challenge', frame_of('saturn', 19.5), 1080, 1350, 'CAN YOU\nBEAT 129?', 'Post your best in the comments', cy=0.0, ty=0.5, badge=False, band=False)
    carousel_cover()
    for k, (src, n, c) in enumerate(CAROUSEL):
        still('ig_4_carousel_%d_%s' % (k + 1, n.lower().replace(' ', '_')), S(src), 1080, 1350, n, 'Unlock it in the mystery box',
              cy=0.47, zoom=1.15, sub_color=c if c != WHITE else CYAN)
    # X / Facebook link: 1600x900 (16:9) - the landscape run, closer in as in the trailer
    still('x_1_tap_to_turn', frame_of('landscape', 7.0), 1600, 900, 'TAP TO TURN', 'One tap. Every turn. How high can you climb?', ty=0.14, zoom=LAND_ZOOM, cy=0.6)
    still('x_2_super_speed', frame_of('landscape', TRAILER_BOLT[0] + 2.0), 1600, 900, 'SUPER SPEED', 'Grab a bolt and fly', ty=0.14, zoom=LAND_ZOOM, cy=0.6, sub_color=GOLD)
    # YouTube thumbnail 1280x720, Facebook page cover 1640x624
    still('youtube_thumbnail', frame_of('landscape', TRAILER_BOLT[0] + 1.5), 1280, 720, 'HOW HIGH\nCAN YOU CLIMB?', None, ty=0.25, title_size=110, zoom=LAND_ZOOM, cy=0.62)
    still('facebook_cover', frame_of('landscape', 12.0), 1640, 624, 'RISING WAY', 'Tap. Turn. Climb forever.', ty=0.24, badge=False, zoom=1.3, cy=0.6)


# ---------------------------------------------------------------------------------------------
# The accounts' own pictures: profile picture, X header, YouTube banner (the Facebook cover is
# made with the stills).
# ---------------------------------------------------------------------------------------------
PROFILE_ICON = os.path.join(ROOT, 'Assets', 'Textures', 'RisingWay icon', 'ios_1024.png')


def banner(name, w, h, cx, title_y, title_size, sub_size, safe_w):
    """The run with its bolt, darkened, the name over it - inside the width every device shows."""
    im = cover(Image.open(frame_of('landscape', TRAILER_BOLT[0] + 2.0)), w, h, LAND_ZOOM, 0.5, 0.6).convert('RGBA')
    im.alpha_composite(Image.new('RGBA', (w, h), (4, 6, 22, 110)))
    place(im, text_layer('RISING WAY', title_size, max_width=safe_w), cx, title_y)
    place(im, text_layer('SPACE BALL RUN  ·  ONE TAP. ENDLESS CLIMB.', sub_size, CYAN, max_width=safe_w), cx, title_y + title_size * 1.05 / h)
    place(im, fitted_pill(STORE_LINE, int(sub_size * 0.9), safe_w), cx, title_y + title_size * 1.85 / h)
    path = os.path.join(OUT, name)
    im.convert('RGB').save(path, quality=100, subsampling=0)
    print('wrote', path)


def profile():
    # Profile picture: the icon's ball, centred for the round crop every site makes.
    ic = Image.open(PROFILE_ICON).convert('RGB')
    cxp, cyp, r = 500, 605, 335
    pic = ic.crop((cxp - r, cyp - r, cxp + r, cyp + r)).resize((1080, 1080), Image.LANCZOS)
    pic.save(os.path.join(OUT, 'profile_picture.png'))
    print('wrote profile_picture.png')
    # X header 1500x500: the profile picture covers the lower left, so the words sit right of it.
    banner('x_header.jpg', 1500, 500, 0.6, 0.3, 110, 32, 820)
    # YouTube banner 2560x1440: only the middle 1546x423 shows on every device.
    banner('youtube_banner.jpg', 2560, 1440, 0.5, 0.41, 150, 46, 1500)


# ---------------------------------------------------------------------------------------------
# Before the launch: a few posts so the pages are not empty. Nothing says where to get it yet
# (the version in the stores is the old one): COMING SOON instead.
# ---------------------------------------------------------------------------------------------
SOON = 'COMING SOON'


def prelaunch():
    still('pre_1_earth', S('close_6_ball53'), 1080, 1350, 'YOUR BALL CAN\nBE A PLANET', 'Rising Way', cy=0.47, zoom=1.15, foot_line=SOON)
    make('pre_2_tap_to_turn', 1080, 1920,
         [('saturn', 0, 9.5)],
         [(T('TAP TO TURN', 110), 0.5, 0.36, 0.2, 3.0),
          (T('ONE TAP.\nEVERY TURN.', 70, CYAN), 0.5, 0.36, 3.2, 6.0),
          (T('HOW HIGH\nCAN YOU CLIMB?', 92), 0.5, 0.36, 6.2, 9.5)],
         end_line=SOON)
    still('pre_3_saturn', S('close_6_ball63'), 1080, 1350, 'RINGS\nINCLUDED', 'Play as Saturn in Rising Way', cy=0.47, zoom=1.15, sub_color=GOLD, foot_line=SOON)
    still('pre_4_sky', frame_of('landscape', 26.0), 1080, 1350, 'WE REBUILT\nTHE WHOLE SKY', 'A galaxy and 14,000 stars around your track', cx=0.42, cy=0.55, foot_line=SOON)
    still('pre_5_sun', S('close_6_ball55'), 1080, 1350, 'SOME BALLS\nBURN BRIGHTER', 'The Sun, in Rising Way', cy=0.47, zoom=1.15, sub_color=GOLD, foot_line=SOON)
    # the teaser: the climb, a few words, the name, coming back soon
    make('pre_6_coming_back', 1080, 1920,
         [('parade', 0.5, 4.5, 1.35, 0.56), ('saturn', 8.0, 13.0)],
         [(T('ONE TAP.', 110), 0.5, 0.36, 0.3, 2.6),
          (T('ENDLESS\nCLIMB.', 110, CYAN), 0.5, 0.36, 2.9, 5.6),
          (T('SOMETHING IS\nRISING AGAIN', 92, GOLD), 0.5, 0.36, 6.0, 9.0)],
         end=3.2, end_line='COMING BACK SOON')


JOBS = {'prelaunch': prelaunch, 'profile': profile, 'tap': reel_tap_to_turn, 'bolt': reel_bolt, 'parade': reel_parade,
        'trailer': youtube_trailer, 'stills': stills}

if __name__ == '__main__':
    for k in (sys.argv[1:] or JOBS):
        JOBS[k]()
