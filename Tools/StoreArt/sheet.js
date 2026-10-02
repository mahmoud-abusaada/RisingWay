// Contact sheet of PNGs, no dependencies:  node sheet.js <out.png> <thumbWidth> <cols> <in1.png> [in2.png ...]
// Decodes 8-bit non-interlaced PNGs (RGB or RGBA), box-downscales each to thumbWidth, tiles them.
const fs = require('fs');
const zlib = require('zlib');

function decode(file) {
  const b = fs.readFileSync(file);
  let p = 8, w, h, ct, idat = [];
  while (p < b.length) {
    const len = b.readUInt32BE(p), type = b.toString('ascii', p + 4, p + 8), d = b.subarray(p + 8, p + 8 + len);
    if (type === 'IHDR') { w = d.readUInt32BE(0); h = d.readUInt32BE(4); ct = d[9]; if (d[8] !== 8 || d[12] !== 0) throw new Error(file + ': only 8-bit non-interlaced'); }
    else if (type === 'IDAT') idat.push(d);
    else if (type === 'IEND') break;
    p += 12 + len;
  }
  const bpp = ct === 6 ? 4 : ct === 2 ? 3 : (() => { throw new Error(file + ': colour type ' + ct); })();
  const raw = zlib.inflateSync(Buffer.concat(idat)), stride = w * bpp, out = Buffer.alloc(w * h * 3);
  let prev = Buffer.alloc(stride), cur = Buffer.alloc(stride);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)], line = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    for (let i = 0; i < stride; i++) {
      const a = i >= bpp ? cur[i - bpp] : 0, up = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
      let v = line[i];
      if (f === 1) v += a; else if (f === 2) v += up; else if (f === 3) v += (a + up) >> 1;
      else if (f === 4) { const pp = a + up - c, pa = Math.abs(pp - a), pb = Math.abs(pp - up), pc = Math.abs(pp - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? up : c; }
      cur[i] = v & 255;
    }
    for (let x = 0; x < w; x++) { out[(y * w + x) * 3] = cur[x * bpp]; out[(y * w + x) * 3 + 1] = cur[x * bpp + 1]; out[(y * w + x) * 3 + 2] = cur[x * bpp + 2]; }
    [prev, cur] = [cur, prev];
  }
  return { w, h, px: out };
}

function encode(w, h, px) {
  const raw = Buffer.alloc((w * 3 + 1) * h);
  for (let y = 0; y < h; y++) { raw[y * (w * 3 + 1)] = 0; px.copy(raw, y * (w * 3 + 1) + 1, y * w * 3, (y + 1) * w * 3); }
  const crcTable = Array.from({ length: 256 }, (_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
  const crc = buf => { let c = 0xffffffff; for (const x of buf) c = crcTable[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
  const chunk = (type, data) => { const l = Buffer.alloc(4); l.writeUInt32BE(data.length); const td = Buffer.concat([Buffer.from(type, 'ascii'), data]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 2;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]);
}

const [, , out, tw, cols, ...files] = process.argv;
const T = +tw, C = +cols, imgs = files.map(decode);
const th = Math.max(...imgs.map(i => Math.round(i.h * T / i.w))), gap = 8;
const rows = Math.ceil(imgs.length / C), W = C * T + (C + 1) * gap, H = rows * th + (rows + 1) * gap;
const sheet = Buffer.alloc(W * H * 3, 40);
imgs.forEach((img, n) => {
  const ox = gap + (n % C) * (T + gap), oy = gap + Math.floor(n / C) * (th + gap), s = img.w / T, h = Math.round(img.h / s);
  for (let y = 0; y < h; y++) for (let x = 0; x < T; x++) {
    let r = 0, g = 0, bl = 0, k = 0;
    for (let sy = Math.floor(y * s); sy < Math.min(img.h, Math.floor((y + 1) * s)); sy++)
      for (let sx = Math.floor(x * s); sx < Math.min(img.w, Math.floor((x + 1) * s)); sx++) { const i = (sy * img.w + sx) * 3; r += img.px[i]; g += img.px[i + 1]; bl += img.px[i + 2]; k++; }
    const o = ((oy + y) * W + ox + x) * 3; sheet[o] = r / k; sheet[o + 1] = g / k; sheet[o + 2] = bl / k;
  }
});
fs.writeFileSync(out, encode(W, H, sheet));
console.log('wrote ' + out + ' ' + W + 'x' + H + ': ' + files.map((f, i) => (i + 1) + '=' + f.split(/[\\/]/).pop()).join(' '));
