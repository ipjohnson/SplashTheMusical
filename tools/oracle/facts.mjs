// Renders every .mmd in a folder and its subfolders with the mermaid.js that package-lock.json pins, in one headless
// page, and writes <name>.facts.txt per case, <name>.boxes.txt per accepted case and <name>.dagre.txt per accepted
// flowchart, state, class and ER case, in the case's subfolder. It also writes themes.txt, config.txt and shapes.txt to
// the top of the facts folder. tools/oracle/README.md describes the
// formats. extract.mjs says which facts come from mermaid's parsed model and which from its rendered DOM.
//
//   node facts.mjs <cases-dir> <facts-dir> [svg-dir]
//
// The run fails when node_modules does not match package-lock.json and when a case has a diagram type extract.mjs has
// no extractor for. Facts and boxes files whose case is no longer in the folder are deleted. With svg-dir, each
// accepted case's SVG is written there too.
import fs from 'node:fs'
import path from 'node:path'
import crypto from 'node:crypto'
import { fileURLToPath } from 'node:url'
import puppeteer from 'puppeteer'
import { checkPinned } from './pinned.mjs'
import { extractFacts } from './extract.mjs'
import { configDumps, dagreLayouts, hookDagre, readShapes, readThemeNames } from './dumps.mjs'

const [, , casesDir, factsDir, svgDir] = process.argv
if (!casesDir || !factsDir) {
	console.error('usage: node facts.mjs <cases-dir> <facts-dir> [svg-dir]')
	process.exit(2)
}

const root = path.dirname(fileURLToPath(import.meta.url))
const problems = checkPinned(root)
if (problems.length) {
	for (const p of problems) console.error(p)
	console.error('run npm ci in tools/oracle')
	process.exit(1)
}
const mermaidDir = path.join(root, 'node_modules', 'mermaid')
const version = JSON.parse(fs.readFileSync(path.join(mermaidDir, 'package.json'), 'utf8')).version

// Hash of the source with CRLF folded to LF, so a consumer can tell a facts file from an edited case.
const sourceHash = text => crypto.createHash('sha256').update(text.replace(/\r\n/g, '\n'), 'utf8').digest('hex').slice(0, 16)
// The seed of the page's Math.random for one case. It depends on the case's name only, so a case's output does not
// depend on which cases ran before it.
const seedOf = name => crypto.createHash('sha256').update(name, 'utf8').digest().readUInt32LE(0)

// Node shapes are written in mermaid's documented short names (rect, rounded, diam, cyl, fr-rect, sm-circ, ...). mermaid
// keeps a node's shape as written, an alias such as `database` or an internal name such as `squareRect`, so its own
// table of shapes (shapesDefs in its dist) maps each name to the short one.
const shapeTable = readShapes(mermaidDir)
const shapes = {}
for (const d of shapeTable.defs) {
	shapes[d.shortName] = d.shortName
	for (const alias of [...d.aliases, ...d.internalAliases]) shapes[alias] ??= d.shortName
}
const themeNames = readThemeNames(mermaidDir)
// The types whose cases are rendered a second time with `layout: dagre`, to record dagre's inputs and outputs.
const dagreTypes = new Set(['flowchart-v2', 'stateDiagram', 'classDiagram', 'er'])
const init = { startOnLoad: false, securityLevel: 'strict' }

// The bundled font, Liberation Sans 2.1.5 under the SIL Open Font License (fonts/LICENSE). All four faces are bundled so
// Chromium never synthesizes bold or italic.
const fontFamily = 'Liberation Sans'
const fontFaces = [
	{ file: 'LiberationSans-Regular.ttf', weight: '400', style: 'normal' },
	{ file: 'LiberationSans-Bold.ttf', weight: '700', style: 'normal' },
	{ file: 'LiberationSans-Italic.ttf', weight: '400', style: 'italic' },
	{ file: 'LiberationSans-BoldItalic.ttf', weight: '700', style: 'italic' },
]

// The page is served from a made-up origin so that ES modules, such as the tidy-tree layout and its chunks, load from
// node_modules, and the bundled font from fonts. Every other request is refused, so a run never reaches the network.
const origin = 'http://oracle.invalid'
const mime = { '.js': 'text/javascript', '.mjs': 'text/javascript', '.html': 'text/html', '.ttf': 'font/ttf' }
const served = ['node_modules', 'fonts'].map(d => path.join(root, d) + path.sep)
const browser = await puppeteer.launch({ headless: 'shell' })
const page = await browser.newPage()
await page.setRequestInterception(true)
page.on('request', request => {
	const url = new URL(request.url())
	if (url.origin !== origin) return request.abort()
	if (url.pathname === '/') return request.respond({ contentType: 'text/html', body: '<!doctype html><html><body></body></html>' })
	const file = path.join(root, decodeURIComponent(url.pathname))
	if (!served.some(d => file.startsWith(d)) || !fs.existsSync(file)) return request.respond({ status: 404, body: '' })
	request.respond({ contentType: mime[path.extname(file)] ?? 'application/octet-stream', body: fs.readFileSync(file) })
})
// mermaid reads a gantt date without a dateFormat as UTC and a time-only date against today, so the page runs in UTC on
// a fixed day.
await page.emulateTimezone('UTC')
await page.evaluateOnNewDocument(() => {
	const now = Date.UTC(2000, 0, 1)
	const D = Date
	globalThis.Date = class extends D {
		constructor(...a) { super(...(a.length ? a : [now])) }
		static now() { return now }
	}
	// mulberry32, seeded here for what mermaid draws while it loads, such as iconify's id prefix, and again before each case.
	globalThis.__oracleSeed = seed => {
		let a = seed >>> 0
		Math.random = () => {
			a = (a + 0x6d2b79f5) | 0
			let t = Math.imul(a ^ (a >>> 15), 1 | a)
			t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
			return ((t ^ (t >>> 14)) >>> 0) / 4294967296
		}
	}
	globalThis.__oracleSeed(0)
	// @zenuml/core reads localStorage, which this page may not touch; an in-memory store stands in.
	const m = new Map()
	Object.defineProperty(window, 'localStorage', {
		value: {
			getItem: k => m.get(k) ?? null, setItem: (k, v) => m.set(k, String(v)), removeItem: k => m.delete(k), clear: () => m.clear(),
			key: i => [...m.keys()][i] ?? null, get length() { return m.size },
		},
	})
})
await page.goto(origin + '/')
// Every text is drawn and measured in the bundled Liberation Sans, so text width is the same for the oracle and for STM's
// width table of the same files. The faces load before mermaid does, so the first case is measured in them too. The
// !important style reaches mermaid's per-diagram font settings and generic families such as sans-serif. A canvas
// ignores CSS, so the faces are also registered under the families that canvas measurement asks for: ZenUML measures
// in "Helvetica, Verdana, serif".
await page.evaluate(async (family, aliases, faces) => {
	for (const face of faces) {
		const data = await (await fetch('/fonts/' + face.file)).arrayBuffer()
		for (const name of [family, ...aliases]) {
			const f = new FontFace(name, data, { weight: face.weight, style: face.style })
			await f.load()
			document.fonts.add(f)
		}
	}
	const style = document.createElement('style')
	style.textContent = `* { font-family: "${family}" !important; }`
	document.head.append(style)
}, fontFamily, ['Helvetica', 'Verdana'], fontFaces)
await page.addScriptTag({ content: hookDagre(fs.readFileSync(path.join(mermaidDir, 'dist', 'mermaid.min.js'), 'utf8')) })
// ZenUML is an external diagram, as it is for mermaid-cli and the mermaid live editor.
await page.addScriptTag({ path: path.join(root, 'node_modules', '@mermaid-js', 'mermaid-zenuml', 'dist', 'mermaid-zenuml.min.js') })
await page.addScriptTag({
	type: 'module',
	content: `import tidyTree from '/node_modules/@mermaid-js/layout-tidy-tree/dist/mermaid-layout-tidy-tree.esm.min.mjs'
		mermaid.registerLayoutLoaders(tidyTree)
		globalThis.__oracleLayouts = true`,
})
await page.waitForFunction(() => globalThis.__oracleLayouts === true)
await page.evaluate(async init => {
	await mermaid.registerExternalDiagrams([globalThis.__esbuild_esm_mermaid_nm['mermaid-zenuml'].default])
	mermaid.initialize(init)
}, init)

// The fonts Chromium drew the case's text in, with their glyph counts, from the DevTools protocol. A glyph the bundled
// font lacks, such as a CJK letter or an emoji, falls back to a system font and shows up here.
const cdp = await page.createCDPSession()
await cdp.send('DOM.enable')
await cdp.send('CSS.enable')
async function fontsDrawn() {
	const { root: doc } = await cdp.send('DOM.getDocument', { depth: -1 })
	const { nodeIds } = await cdp.send('DOM.querySelectorAll', { nodeId: doc.nodeId, selector: 'body svg, body svg *' })
	const glyphs = new Map()
	for (const nodeId of nodeIds) {
		const { fonts } = await cdp.send('CSS.getPlatformFontsForNode', { nodeId })
		for (const f of fonts) glyphs.set(f.familyName, (glyphs.get(f.familyName) ?? 0) + f.glyphCount)
	}
	return [...glyphs].map(([family, count]) => `font\t${family}\t${count}`)
}

fs.mkdirSync(factsDir, { recursive: true })
if (svgDir) fs.mkdirSync(svgDir, { recursive: true })

const dumpHeader = what => [`# mermaid.js ${what}. Refresh with: mise run oracle`, `# mermaid ${version}`]
const dumps = await page.evaluate(configDumps, themeNames, init)
fs.writeFileSync(path.join(factsDir, 'themes.txt'), [...dumpHeader('theme variables'), ...dumps.themes.sort()].join('\n') + '\n')
fs.writeFileSync(path.join(factsDir, 'config.txt'), [...dumpHeader('default config'), ...dumps.config.sort()].join('\n') + '\n')
const shapeLines = [
	...shapeTable.defs.flatMap(d => [
		['shape', d.shortName, d.name, d.semanticName, d.description, d.handler],
		...d.aliases.map(a => ['alias', a, d.shortName]),
		...d.internalAliases.map(a => ['internal-alias', a, d.shortName]),
	]),
	...shapeTable.undocumented.map(s => ['undocumented', s.key, s.handler]),
	...[...shapeTable.keys].map(([k, h]) => ['key', k, h]),
].map(f => f.join('\t').replace(/\t+$/, '')).sort()
fs.writeFileSync(path.join(factsDir, 'shapes.txt'), [...dumpHeader('shape table'), ...shapeLines].join('\n') + '\n')

// The files of a folder and its subfolders, as paths relative to it with / between folders.
const filesUnder = dir =>
	fs.readdirSync(dir, { recursive: true, withFileTypes: true })
		.filter(e => e.isFile())
		.map(e => path.relative(dir, path.join(e.parentPath, e.name)).split(path.sep).join('/'))
		.sort()
// A case in a subfolder, such as flowchart/e2e-basic, writes its files to the same subfolder of the facts folder. Its
// seed and SVG id come from its file name, so moving a case to another folder does not change its output.
const names = filesUnder(casesDir).filter(f => f.endsWith('.mmd')).map(f => f.slice(0, -4))
let accepted = 0
const dagreNames = new Set()
for (const name of names) {
	const source = fs.readFileSync(path.join(casesDir, name + '.mmd'), 'utf8')
	const baseName = path.posix.basename(name)
	fs.mkdirSync(path.dirname(path.join(factsDir, name)), { recursive: true })
	await page.evaluate(seed => globalThis.__oracleSeed(seed), seedOf(baseName))
	const result = await page.evaluate(extractFacts, source, baseName, shapes)
	if (result.unknownType) {
		console.error(`${name}: no extractor for mermaid diagram type '${result.unknownType}'; add one to tools/oracle/extract.mjs`)
		await browser.close()
		process.exit(1)
	}
	const header = [
		'# mermaid.js facts. Refresh with: mise run oracle',
		`# mermaid ${version}`,
		`# source ${sourceHash(source)}`,
	]
	const body = result.error === undefined
		? ['accept\tyes', ...result.facts.sort()]
		: ['accept\tno', `# error ${result.error.split('\n')[0].trim()}`]
	fs.writeFileSync(path.join(factsDir, name + '.facts.txt'), [...header, ...body].join('\n') + '\n')
	const boxesFile = path.join(factsDir, name + '.boxes.txt')
	if (result.error === undefined) {
		const boxes = [...result.boxes, ...await fontsDrawn()].sort()
		fs.writeFileSync(boxesFile, ['# mermaid.js boxes. Refresh with: mise run oracle', ...header.slice(1), ...boxes].join('\n') + '\n')
	} else if (fs.existsSync(boxesFile)) fs.unlinkSync(boxesFile)
	const dagreFile = path.join(factsDir, name + '.dagre.txt')
	const type = result.facts?.find(f => f.startsWith('type\t'))?.slice(5)
	if (result.error === undefined && dagreTypes.has(type)) {
		await page.evaluate(seed => globalThis.__oracleSeed(seed), seedOf(baseName))
		const layouts = await page.evaluate(dagreLayouts, source, baseName, init)
		fs.writeFileSync(dagreFile, ['# mermaid.js dagre layouts. Refresh with: mise run oracle', ...header.slice(1), ...layouts].join('\n') + '\n')
		dagreNames.add(name + '.dagre.txt')
	} else if (fs.existsSync(dagreFile)) fs.unlinkSync(dagreFile)
	if (svgDir && result.svg) {
		fs.mkdirSync(path.dirname(path.join(svgDir, name)), { recursive: true })
		fs.writeFileSync(path.join(svgDir, name + '.svg'), result.svg)
	}
	if (result.error === undefined) accepted++
	console.log(`${result.error === undefined ? 'ok ' : 'err'}  ${name}${result.error === undefined ? '' : ': ' + result.error.split('\n')[0]}`)
}

const keep = new Set([...names.flatMap(n => [n + '.facts.txt', n + '.boxes.txt']), ...dagreNames])
const perCase = f => f.endsWith('.facts.txt') || f.endsWith('.boxes.txt') || f.endsWith('.dagre.txt')
for (const f of filesUnder(factsDir).filter(f => perCase(f) && !keep.has(f))) {
	fs.unlinkSync(path.join(factsDir, f))
	console.log(`del  ${f}`)
}

await browser.close()
console.log(`mermaid ${version}: ${names.length} cases, ${accepted} accepted, ${dagreNames.size} dagre dumps, ${themeNames.length} themes`)
