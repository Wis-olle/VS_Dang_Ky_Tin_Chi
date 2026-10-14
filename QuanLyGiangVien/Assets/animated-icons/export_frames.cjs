// Offline sprite export from the pinned lucide-animated Motion keyframes.
// Requires Node.js + sharp, resolved normally or through SHARP_MODULE_PATH/NODE_PATH.
// The retained Motion 13.1.1 generator supplies original easing and spring timing.
const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require(process.env.SHARP_MODULE_PATH || 'sharp');
const motion = require('./vendor/motion-13.1.1.cjs');

const ids = ['users', 'graduation-cap', 'book-text', 'folder-code',
  'chart-column-increasing', 'languages', 'cog'];
const frameSize = 128;
const ink = '#211A1C';
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const number = value => Number(value.toFixed(6));

function curve(values, duration, ease = 'easeInOut', times) {
  return motion.keyframes({ keyframes: values, duration, ease, times });
}

function spring(values, options) {
  return motion.spring({ keyframes: values, ...options });
}

const usersSpring = spring([-6, 0], { stiffness: 200, damping: 13 });
const cogSpring = spring([0, 180], { stiffness: 50, damping: 10 });
const durations = {
  users: 100 + motion.calcGeneratorDuration(usersSpring),
  'graduation-cap': 900, 'book-text': 600, 'folder-code': 500,
  'chart-column-increasing': 1000, languages: 800,
  cog: motion.calcGeneratorDuration(cogSpring)
};

const curves = {
  capY: curve([0, -2, 0], 600), capRotate: curve([0, -2, 2, 0], 600),
  tassel: curve([0, 15, -10, 5, 0], 800),
  bookScale: curve([1, 1.04, 1], 600, 'easeInOut', [0, 0.2, 0.5, 0.8, 1]),
  bookRotate: curve([0, -8, 8, -8, 0], 600, 'easeInOut', [0, 0.2, 0.5, 0.8, 1]),
  bookY: curve([0, -2, 0], 600, 'easeInOut', [0, 0.2, 0.5, 0.8, 1]),
  codeX: curve([0, 2, 0], 500), codeRotate: curve([0, -8, 0], 500),
  languageLength: spring([0, 1], { duration: 500, bounce: 0 }),
  languageOpacity: curve([0, 1], 10),
  languageOffset: curve([1, 0], 300,
    motion.getDefaultTransition('pathOffset', { keyframes: [1, 0] }).ease),
  chartHide: curve([1, 0], 300,
    motion.getDefaultTransition('pathLength', { keyframes: [1, 0] }).ease),
  chartShow: curve([0, 1], 300,
    motion.getDefaultTransition('pathLength', { keyframes: [0, 1] }).ease)
};

function sample(generator, time, before) {
  return number(time < 0 ? before : generator.next(time).value);
}

// Geometry is read from native TSX, rather than a newer static Lucide revision.
function shapesFromTsx(text) {
  const shapes = [...text.matchAll(/<(motion\.)?(path|circle|rect)\b([\s\S]*?)\/>/g)]
    .map(match => {
      const attributes = [...match[3].matchAll(/\b(d|cx|cy|r|x|y|rx|ry|width|height)="([^"]*)"/g)]
        .map(attribute => [attribute[1], attribute[2]]);
      return { tag: match[2], attributes, moving: Boolean(match[1]),
        custom: Number(match[3].match(/custom=\{(-?\d+)\}/)?.[1] || 0) };
    });
  if (!shapes.length) throw new Error('No retained SVG geometry found in TSX.');
  return shapes;
}

function shapeMarkup(shape, extra = '') {
  return `<${shape.tag} ${shape.attributes.map(([k, v]) => `${k}="${v}"`).join(' ')} ${extra}/>`;
}

function transform(translateX = 0, translateY = 0, rotate = 0, scale = 1, x = 12, y = 12) {
  return `translate(${translateX} ${translateY}) translate(${x} ${y}) rotate(${rotate}) scale(${scale}) translate(${-x} ${-y})`;
}

// These language/chart paths use only line commands. Length enables normalized
// Motion pathLength/pathOffset values in librsvg, which does not normalize dash units.
function lineLength(shape) {
  const d = Object.fromEntries(shape.attributes).d;
  const parts = d.match(/[MmLlHhVv]|[-+]?(?:\d*\.)?\d+/g);
  let command, x = 0, y = 0, length = 0, i = 0;
  while (i < parts.length) {
    if (/^[A-Za-z]$/.test(parts[i])) command = parts[i++];
    const relative = command === command.toLowerCase();
    const previous = [x, y];
    if (command.toLowerCase() === 'h') x = (relative ? x : 0) + Number(parts[i++]);
    else if (command.toLowerCase() === 'v') y = (relative ? y : 0) + Number(parts[i++]);
    else {
      x = (relative ? x : 0) + Number(parts[i++]);
      y = (relative ? y : 0) + Number(parts[i++]);
    }
    if (command.toLowerCase() !== 'm') length += Math.hypot(x - previous[0], y - previous[1]);
    if (command === 'm') command = 'l';
    if (command === 'M') command = 'L';
  }
  return length;
}

function drawPath(shape, progress, opacity = 1, offset = 0) {
  const length = lineLength(shape);
  // Motion uses a tiny dash in place of zero; opacity covers invisible start frames.
  return shapeMarkup(shape, `opacity="${opacity}" stroke-dasharray="${Math.max(progress, 0.000001) * length} ${length}" stroke-dashoffset="${-offset * length}"`);
}

function svgAt(id, shapes, time, idle = false) {
  let body = shapes.map(shape => shapeMarkup(shape)).join('');
  if (!idle) {
    if (id === 'users') body = shapes.map(shape => shapeMarkup(shape,
      shape.moving ? `transform="translate(${sample(usersSpring, time - 100, 0)} 0)"` : '')).join('');
    if (id === 'graduation-cap') {
      const cap = transform(0, sample(curves.capY, time, 0), sample(curves.capRotate, time, 0));
      body = `<g transform="${cap}">${shapes.map(shape => shapeMarkup(shape,
        shape.moving ? `transform="rotate(${sample(curves.tassel, time - 100, 0)} 22 10)"` : '')).join('')}</g>`;
    }
    if (id === 'book-text') body = `<g transform="${transform(0, sample(curves.bookY, time, 0),
      sample(curves.bookRotate, time, 0), sample(curves.bookScale, time, 1))}">${body}</g>`;
    if (id === 'folder-code') body = shapes.map(shape => {
      if (!shape.moving) return shapeMarkup(shape);
      const direction = shape.custom;
      return shapeMarkup(shape, `transform="${transform(direction * sample(curves.codeX, time, 0), 0,
        direction * sample(curves.codeRotate, time, 0), 1, direction < 0 ? 9 : 15, 13)}"`);
    }).join('');
    if (id === 'chart-column-increasing') body = shapes.map(shape => {
      if (!shape.moving) return shapeMarkup(shape);
      const stageTime = time < 500 ? time : time - 500;
      const progress = sample(time < 500 ? curves.chartHide : curves.chartShow,
        stageTime - shape.custom * 100, time < 500 ? 1 : 0);
      return drawPath(shape, progress, progress);
    }).join('');
    if (id === 'languages') body = shapes.map(shape => drawPath(shape,
      sample(curves.languageLength, time - shape.custom * 100, 1),
      sample(curves.languageOpacity, time - shape.custom * 100, 1),
      sample(curves.languageOffset, time, 0))).join('');
    if (id === 'cog') body = `<g transform="rotate(${sample(cogSpring, time, 0)} 12 12)">${body}</g>`;
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="-2 -2 28 28" fill="none" stroke="${ink}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${body}</svg>`;
}

async function exportIcon(id) {
  const source = await fs.readFile(path.join(__dirname, 'sources', `${id}.tsx`));
  const shapes = shapesFromTsx(source.toString('utf8'));
  const duration = durations[id];
  const animationFrames = Math.max(2, Math.ceil(duration * 30 / 1000));
  const frames = [];
  for (let frame = 0; frame < animationFrames; frame++) {
    const svg = svgAt(id, shapes, frame * duration / animationFrames);
    frames.push(await sharp(Buffer.from(svg)).png().toBuffer());
  }
  const idleSvg = svgAt(id, shapes, 0, true);
  await fs.writeFile(path.join(__dirname, 'sources', `${id}-idle.svg`), idleSvg + '\n');
  frames.push(await sharp(Buffer.from(idleSvg)).png().toBuffer());
  const sheet = await sharp({ create: { width: frames.length * frameSize, height: frameSize,
    channels: 4, background: '#00000000' } }).composite(frames.map((input, index) => ({
    input, left: index * frameSize, top: 0 }))).png().toBuffer();
  await fs.writeFile(path.join(__dirname, `${id}.png`), sheet);
  const metadata = { id, frameSize, frameCount: frames.length,
    durationMilliseconds: duration, idleFrame: animationFrames, sheet: `${id}.png` };
  await fs.writeFile(path.join(__dirname, `${id}.json`), JSON.stringify(metadata, null, 2) + '\n');
  process.stdout.write(`${id}: ${frames.length} cells, ${duration} ms, idle ${animationFrames}\n`);
  return { id, source_sha256: hash(source), sheet_sha256: hash(sheet), ...metadata };
}

async function main() {
  const exported = [];
  for (const id of ids) exported.push(await exportIcon(id));
  const manifestPath = path.join(__dirname, '..', 'licenses', 'Lucide-Animated-Sources.json');
  const provenance = JSON.parse(await fs.readFile(manifestPath, 'utf8'));
  await fs.writeFile(manifestPath, JSON.stringify({ ...provenance, derived_sprites: exported }, null, 2) + '\n');
}

main().catch(error => { process.stderr.write(error.message + '\n'); process.exitCode = 1; });
