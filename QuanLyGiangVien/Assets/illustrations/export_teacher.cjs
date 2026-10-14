// Convert the licensed, locally saved Storyset SVG for the native WinForms UI.
// Requires Node.js + sharp. Optional: SHARP_MODULE_PATH or NODE_PATH.
const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require(process.env.SHARP_MODULE_PATH || 'sharp');

async function main() {
  const svgPath = path.join(__dirname, 'teacher.svg');
  const svg = await fs.readFile(svgPath);
  const markup = svg.toString('utf8');
  if (!markup.includes('<svg') || /<script|<foreignObject|(?:href|src)\s*=\s*["'](?:https?:|data:)/i.test(markup)) {
    throw new Error('Expected a self-contained, passive SVG illustration.');
  }
  const raster = await sharp(svg, { density: 192 }).resize(1024, 1024).png().toBuffer();
  const trimmed = await sharp(raster).trim({ background: '#00000000', threshold: 1 }).png().toBuffer();
  const png = await sharp(trimmed).extend({ top: 20, bottom: 20, left: 20, right: 20, background: '#00000000' }).png().toBuffer();
  const metadata = await sharp(png).metadata();
  await fs.writeFile(path.join(__dirname, 'teacher.png'), png);
  const sha256 = data => crypto.createHash('sha256').update(data).digest('hex');
  const manifest = {
    title: 'Teacher', style: 'bro', provider: 'Storyset / Freepik Company',
    local_file: 'Assets/illustrations/teacher.svg',
    source_page: 'https://storyset.com/illustration/teacher/bro',
    download_url: 'https://stories.freepiklabs.com/storage/15839/Teacher-01.svg',
    license: 'https://storyset.com/terms', license_section: '6. Licence Agreement for Storyset Content',
    verified_on: '2026-10-10', sha256: sha256(svg), view_box: '0 0 500 500',
    attribution_required: true, suggested_credit: 'Education illustrations by Storyset',
    attribution_link: 'https://storyset.com/',
    notes: [
      'Original SVG retained without recoloring or copying Dribbble artwork.',
      'Professor, blackboard and globe replace the previous library scene.',
      'Original pale-green background-simple layer is retained.',
      'SVG has no scripts, foreignObject, embedded data images or external network references.'
    ],
    derived_formats: [{
      local_file: 'Assets/illustrations/teacher.png', format: 'PNG RGBA',
      dimensions: [metadata.width, metadata.height], transparent_background: true,
      source: 'Assets/illustrations/teacher.svg', sha256: sha256(png),
      conversion: 'Sharp / librsvg: rasterize to 1024 x 1024, trim transparent margins, add 20 px transparent padding on each side; source colors preserved.',
      reason: 'Native PNG preserves SVG CSS fills and makes the artwork legible in the dashboard banner.'
    }]
  };
  await fs.writeFile(path.join(__dirname, '..', 'licenses', 'Storyset-Teacher-Sources.json'), JSON.stringify(manifest, null, 2) + '\n');
  process.stdout.write(`Teacher PNG: ${metadata.width} x ${metadata.height}\n`);
}

main().catch(error => { process.stderr.write(error.message + '\n'); process.exitCode = 1; });
