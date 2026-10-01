// Turns the two SVG sources in this folder into every raster icon the app serves, plus the
// favicon.ico that browsers and crawlers request by path whether we link to it or not.
//
// sharp is deliberately not a dependency of the frontend. It carries platform-specific binaries and
// it is needed only when the brand changes, which is close to never, so it is installed on demand:
//
//   mkdir ..\..\..\.icons-tmp && cd .icons-tmp && npm install sharp
//   cd ..\cricket-live\frontend
//   node --env-file=/dev/null brand/generate-icons.mjs        # with NODE_PATH=..\..\.icons-tmp\node_modules
//
// The generated PNGs are committed, so a fresh clone needs none of this to build.

import { createRequire } from 'node:module'
import { mkdir, readFile, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const sharp = createRequire(import.meta.url)('sharp')

const brand = dirname(fileURLToPath(import.meta.url))
const publicDir = join(brand, '..', 'public')

// The SVGs declare a 64px box, and librsvg rasterises at the box size scaled by density/72. Asking
// for a 64px render and then enlarging it would give us a blurred 64px image, so every icon is
// rendered large from the vector and then reduced, which is the direction that stays sharp.
const density = (target, box = 64) => (72 * target) / box

async function render(source, size, { flatten = false } = {}) {
  const svg = await readFile(join(brand, source))
  let pipeline = sharp(svg, { density: density(1024) }).resize(size, size)

  // iOS composites an apple-touch-icon onto nothing in particular, so transparent corners come out
  // black. Flattening onto the brand green means the mask has solid pixels to cut from.
  if (flatten) {
    pipeline = pipeline.flatten({ background: '#059669' })
  }

  return pipeline.png({ compressionLevel: 9 }).toBuffer()
}

// ICO is a directory of images rather than one image. Vista onwards accepts PNG payloads, which
// saves writing a BMP encoder, and every browser that still asks for favicon.ico is newer than that.
function ico(images) {
  const header = Buffer.alloc(6)
  header.writeUInt16LE(0, 0) // reserved
  header.writeUInt16LE(1, 2) // 1 = icon
  header.writeUInt16LE(images.length, 4)

  let offset = 6 + images.length * 16
  const entries = images.map(({ size, data }) => {
    const entry = Buffer.alloc(16)
    entry.writeUInt8(size === 256 ? 0 : size, 0) // 0 means 256; the field is one byte
    entry.writeUInt8(size === 256 ? 0 : size, 1)
    entry.writeUInt8(0, 2) // palette size, 0 for truecolour
    entry.writeUInt8(0, 3) // reserved
    entry.writeUInt16LE(1, 4) // colour planes
    entry.writeUInt16LE(32, 6) // bits per pixel
    entry.writeUInt32LE(data.length, 8)
    entry.writeUInt32LE(offset, 12)
    offset += data.length
    return entry
  })

  return Buffer.concat([header, ...entries, ...images.map((image) => image.data)])
}

await mkdir(publicDir, { recursive: true })

// The rounded mark is for tabs, where nothing masks it. The square one is for the platforms that
// apply their own shape.
const [icoSizes, pngs] = await Promise.all([
  Promise.all(
    [16, 32, 48].map(async (size) => ({
      size,
      data: await render('../public/favicon.svg', size),
    })),
  ),
  Promise.all([
    render('../public/favicon.svg', 32).then((data) => ['favicon-32.png', data]),
    render('icon-square.svg', 180, { flatten: true }).then((data) => [
      'apple-touch-icon.png',
      data,
    ]),
    render('icon-square.svg', 192).then((data) => ['icon-192.png', data]),
    render('icon-square.svg', 512).then((data) => ['icon-512.png', data]),
  ]),
])

await writeFile(join(publicDir, 'favicon.ico'), ico(icoSizes))

for (const [name, data] of pngs) {
  await writeFile(join(publicDir, name), data)
}

// The card is already 1200x630 in its own box, so it needs no scaling, only rasterising.
const card = await readFile(join(brand, 'social-card.svg'))
await writeFile(
  join(publicDir, 'social-card.png'),
  await sharp(card, { density: 72 }).png({ compressionLevel: 9 }).toBuffer(),
)

console.log(['favicon.ico', ...pngs.map(([name]) => name), 'social-card.png'].join('\n'))
