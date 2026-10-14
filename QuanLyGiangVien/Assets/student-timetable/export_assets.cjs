// Offline SVG-to-PNG preparation. Requires sharp via NODE_PATH or SHARP_MODULE_PATH.
const fs = require('fs');
const path = require('path');
const sharp = require(process.env.SHARP_MODULE_PATH || 'sharp');
async function main() {
  await sharp(path.join(__dirname, 'programming.svg')).resize(320, 220, { fit: 'inside' })
    .png().toFile(path.join(__dirname, 'programming.png'));
  for (const name of ['graduation-cap', 'calendar-days', 'book-open', 'clock', 'map-pin', 'user-round']) {
    const svg = fs.readFileSync(path.join(__dirname, name + '.svg'), 'utf8').replaceAll('currentColor', '#8E7BEA');
    await sharp(Buffer.from(svg)).resize(64, 64).png().toFile(path.join(__dirname, name + '.png'));
  }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
