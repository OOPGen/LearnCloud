// Cuts the brand assets from the official logo. Crop boxes were measured from the ink
// profile (see analyse.mjs): the mark sits above y=571, where the ERP badge begins.
import sharp from 'sharp';
import fs from 'node:fs';
import path from 'node:path';

const SRC = process.argv[2];
const WEB = process.argv[3];
const MKT = process.argv[4];

const MARK = { left: 176, top: 80, width: 925 - 176 + 1, height: 570 - 80 + 1 };
const FULL = { left: 44, top: 80, width: 1088 - 44 + 1, height: 751 - 80 + 1 };
const WHITE = { r: 255, g: 255, b: 255, alpha: 1 };
const png = { compressionLevel: 9, palette: true, quality: 92, effort: 10 };

const out = [];
async function write(dirs, name, buffer) {
  for (const dir of dirs) {
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(path.join(dir, name), buffer);
  }
  out.push(`${name}  ${(buffer.length / 1024).toFixed(1)} KB  -> ${dirs.length} place(s)`);
}

// Square mark, padded so the cloud is not cropped tight
const markSquare = async size => {
  const pad = 24;
  const inner = size - pad * 2;
  const mark = await sharp(SRC).extract(MARK).resize(inner, inner, { fit: 'contain', background: WHITE }).toBuffer();
  return sharp({ create: { width: size, height: size, channels: 3, background: WHITE } })
    .composite([{ input: mark, gravity: 'center' }]).png(png).toBuffer();
};

// Full logo (mark + wordmark), trimmed to the ink
const full = async width =>
  sharp(SRC).extract(FULL).resize({ width }).png(png).toBuffer();

const both = [WEB, MKT];
await write(both, 'logo.png', await full(720));
await write(both, 'logo-mark.png', await markSquare(512));
await write(both, 'favicon.png', await markSquare(64));
await write(both, 'apple-touch-icon.png', await markSquare(180));

// Social preview: the full logo centred on white, 1200x630
const logo = await sharp(SRC).extract(FULL).resize({ width: 880 }).toBuffer();
await write([MKT], 'og-image.png', await sharp({ create: { width: 1200, height: 630, channels: 3, background: WHITE } })
  .composite([{ input: logo, gravity: 'center' }]).png(png).toBuffer());

console.log(out.join('\n'));
