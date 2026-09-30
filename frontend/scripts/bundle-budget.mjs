// Fails the build when the bundle grows past what we said it could.
//
// A budget nobody enforces is a note, not a budget, which is why this exits non-zero rather than
// printing a warning. Vite's own `chunkSizeWarningLimit` only warns, and a warning in a CI log is
// a warning nobody reads.
//
// Measured gzipped, because that is what the browser downloads. Raw byte counts are roughly three
// times larger and lead to budgets set against a number no reader ever waits for.
//
// Run as `npm run budget`, which builds first.

import { readdir, readFile } from 'node:fs/promises'
import { basename, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { gzipSync } from 'node:zlib'

const dist = join(dirname(fileURLToPath(import.meta.url)), '..', 'dist')
const assets = join(dist, 'assets')

/**
 * Budgets in gzipped kilobytes.
 *
 * `initial` is everything the browser fetches before it can paint: the entry chunk plus every
 * chunk `index.html` preloads. This is the number that decides how long a first-time visitor
 * stares at nothing, and the only one worth defending hard.
 *
 * `lazy` is any single on-demand chunk — the cost of one click, paid once, on a page the reader
 * has already decided they want. Loose by comparison, and deliberately so.
 *
 * `css` is the whole stylesheet, which Tailwind emits as one file and the page blocks on.
 *
 * Set from the current sizes plus headroom: tight enough to catch a stray dependency, loose enough
 * that ordinary feature work does not trip it. Raise them deliberately, in a commit that says why,
 * rather than reflexively when this fails.
 */
const BUDGETS_KB = {
  initial: 130,
  lazy: 45,
  css: 12,
}

let files
try {
  files = await readdir(assets)
} catch {
  console.error(`\nNo build output at ${assets}. Run \`npm run build\` first.\n`)
  process.exit(1)
}

const html = await readFile(join(dist, 'index.html'), 'utf8')

/**
 * The initial payload, taken from the markup rather than inferred from file names.
 *
 * Vite writes the entry as a `<script type="module">` and every chunk that entry statically
 * imports as a `<link rel="modulepreload">`. Together those are exactly what the browser requests
 * before rendering, so reading them is accurate in a way that guessing from the largest file is
 * not — a shared vendor chunk is preloaded and a lazy page chunk is not, and no naming convention
 * tells you which is which.
 */
const preloaded = new Set(
  [...html.matchAll(/(?:src|href)="([^"]+\.(?:js|css))"/g)].map((match) => basename(match[1])),
)

const sized = await Promise.all(
  files.map(async (name) => ({
    name,
    kb: gzipSync(await readFile(join(assets, name))).length / 1024,
    initial: preloaded.has(name),
  })),
)

const js = sized.filter((file) => file.name.endsWith('.js'))
const css = sized.filter((file) => file.name.endsWith('.css'))

if (js.length === 0) {
  console.error('\nNo JavaScript in the build output. Something is wrong with the build.\n')
  process.exit(1)
}

const failures = []

function check(label, actual, budget) {
  const over = actual > budget

  console.log(
    `  ${over ? 'FAIL' : 'ok  '}  ${label.padEnd(38)} ${actual.toFixed(1).padStart(6)} kB / ${budget} kB`,
  )

  if (over) {
    failures.push(
      `${label.trim()} is ${actual.toFixed(1)} kB gzipped, over its ${budget} kB budget`,
    )
  }
}

const initial = js.filter((file) => file.initial)
const lazy = js.filter((file) => !file.initial)

console.log('\nBundle budget (gzipped)\n')

console.log(`  initial payload — ${initial.length} chunk(s) fetched before first paint`)
check(
  'total',
  initial.reduce((sum, file) => sum + file.kb, 0),
  BUDGETS_KB.initial,
)
for (const chunk of [...initial].sort((a, b) => b.kb - a.kb)) {
  console.log(`          ${chunk.name.padEnd(38)} ${chunk.kb.toFixed(1).padStart(6)} kB`)
}

console.log(`\n  on demand — ${lazy.length} chunk(s), one click each`)
for (const chunk of [...lazy].sort((a, b) => b.kb - a.kb)) {
  check(chunk.name, chunk.kb, BUDGETS_KB.lazy)
}

console.log('\n  stylesheets')
for (const sheet of css) {
  check(sheet.name, sheet.kb, BUDGETS_KB.css)
}

const total = sized.reduce((sum, file) => sum + file.kb, 0)
console.log(`\n  ${total.toFixed(1)} kB gzipped across ${sized.length} files in total\n`)

if (failures.length > 0) {
  console.error('Bundle budget exceeded:')
  for (const failure of failures) {
    console.error(`  - ${failure}`)
  }
  console.error('\nEither make it smaller or raise the budget in scripts/bundle-budget.mjs.\n')
  process.exit(1)
}
