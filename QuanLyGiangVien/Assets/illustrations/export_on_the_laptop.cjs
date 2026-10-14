// Rasterize the licensed, locally retained Overflow Design SVG for WinForms.
// Requires Node.js + sharp. Configure NODE_PATH or SHARP_MODULE_PATH as needed.
// Source acquisition used the official Overflow Design MCP; no expiring URL is stored.
const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require(process.env.SHARP_MODULE_PATH || 'sharp');

function assertPassiveSvg(markup) {
  const active = /<\s*(?:script|foreignObject|animate|animateTransform|set)\b|\bon\w+\s*=|@import|<!DOCTYPE|<!ENTITY/i;
  const externalReference = /(?:href|src)\s*=\s*["'](?!#)|url\(\s*["']?\s*(?:https?:|data:|file:|\/\/)/i;
  if (!markup.includes('<svg') || active.test(markup) || externalReference.test(markup)) {
    throw new Error('Expected a self-contained, passive SVG illustration.');
  }
}

async function main() {
  const svgPath = path.join(__dirname, 'on-the-laptop.svg');
  const svg = await fs.readFile(svgPath);
  assertPassiveSvg(svg.toString('utf8'));
  const raster = await sharp(svg, { density: 192 })
    .resize(1024, 1024, { fit: 'contain', background: '#00000000' }).png().toBuffer();
  const trimmed = await sharp(raster)
    .trim({ background: '#00000000', threshold: 1 }).png().toBuffer();
  const png = await sharp(trimmed).extend({
    top: 20, bottom: 20, left: 20, right: 20, background: '#00000000'
  }).png().toBuffer();
  const metadata = await sharp(png).metadata();
  await fs.writeFile(path.join(__dirname, 'on-the-laptop.png'), png);
  const sha256 = data => crypto.createHash('sha256').update(data).digest('hex');
  const manifest = {
    title: 'On The Laptop', pack: 'Office Club', style: 'regular',
    provider: 'Overflow Design', free_item: true,
    item_id: 'illustration/office-club/on-the-laptop',
    local_file: 'Assets/illustrations/on-the-laptop.svg',
    source_page: 'https://www.overflow.design/notion-illustrations/office-club/',
    acquisition: {
      method: 'Official Overflow Design MCP, signed download of the free item',
      format: 'svg', look: 'colour', theme: 'light', animated: false,
      note: 'Temporary signed download URL intentionally omitted.'
    },
    license: 'https://www.overflow.design/license/',
    license_type: 'Overflow Design Free license', license_updated_on: '2026-09-29',
    verified_on: '2026-10-10', source_sha256: sha256(svg),
    original_svg_bytes: svg.length, attribution_required: false,
    notes: [
      'Official Office Club page identifies On The Laptop as Free.',
      'Original SVG bytes retained; no recoloring or redrawing.',
      'SVG has no scripts, event handlers, animation, foreignObject or external resources.',
      'This licensed asset is integrated into the application banner; it is not an asset pack.'
    ],
    derived_formats: [{
      local_file: 'Assets/illustrations/on-the-laptop.png', format: 'PNG RGBA',
      dimensions: [metadata.width, metadata.height], transparent_background: true,
      source: 'Assets/illustrations/on-the-laptop.svg', sha256: sha256(png),
      conversion: 'Sharp / librsvg: rasterize in a transparent 1024 x 1024 canvas, trim alpha margins, add 20 px transparent padding to every side; source colors preserved.',
      reason: 'Native PNG preserves source colors and fills in the WinForms banner.'
    }]
  };
  await fs.writeFile(path.join(__dirname, '..', 'licenses', 'Overflow-OfficeClub-Sources.json'),
    JSON.stringify(manifest, null, 2) + '\n');
  process.stdout.write(`On The Laptop PNG: ${metadata.width} x ${metadata.height}\n`);
}

main().catch(error => { process.stderr.write(error.message + '\n'); process.exitCode = 1; });
