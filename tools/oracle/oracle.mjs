// The oracle's headless page with the pinned mermaid.js loaded, and the files it writes for one case. facts.mjs runs it
// over a folder, serve.mjs over sources it reads from stdin, and add.mjs over the finds it adds to the corpus.
// tools/oracle/README.md describes the formats.
import fs from 'node:fs'
import path from 'node:path'
import crypto from 'node:crypto'
import { fileURLToPath } from 'node:url'
import puppeteer from 'puppeteer'
import { checkPinned } from './pinned.mjs'
import { extractFacts } from './extract.mjs'
import { dagreLayouts, hookAddDirective, hookDagre, hookPreprocess, readShapes, readThemeNames } from './dumps.mjs'

export const root = path.dirname(fileURLToPath(import.meta.url))

// Hash of the source with CRLF folded to LF, so a consumer can tell a facts file from an edited case.
export const sourceHash = text => crypto.createHash('sha256').update(text.replace(/\r\n/g, '\n'), 'utf8').digest('hex').slice(0, 16)
// The seed of the page's Math.random for one case. It depends on the case's name only, so a case's output does not
// depend on which cases ran before it.
const seedOf = name => crypto.createHash('sha256').update(name, 'utf8').digest().readUInt32LE(0)

// The types whose cases are rendered a second time with `layout: dagre`, to record dagre's inputs and outputs.
const dagreTypes = new Set(['flowchart-v2', 'stateDiagram', 'classDiagram', 'er'])
export const init = { startOnLoad: false, securityLevel: 'strict' }

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
// node_modules, and the bundled font from fonts. An image from another origin, such as an image node's `img`, is
// answered with stubImage, so mermaid can decode it without the network. Every other request is refused, so a run never
// reaches the network.
const origin = 'http://oracle.invalid'
const mime = { '.js': 'text/javascript', '.mjs': 'text/javascript', '.html': 'text/html', '.ttf': 'font/ttf' }
const served = ['node_modules', 'fonts'].map(d => path.join(root, d) + path.sep)
const stubImage = '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><rect width="100" height="100" fill="#ccc"/></svg>'

// Exits when node_modules does not match package-lock.json.
export function exitUnlessPinned() {
	const problems = checkPinned(root)
	if (problems.length) {
		for (const p of problems) console.error(p)
		console.error('run npm ci in tools/oracle with the Node mise.toml pins')
		process.exit(1)
	}
}

// Starts Chromium with mermaid.js loaded. run(source, name, { dagre }) renders one case and returns its files.
export async function openOracle() {
	const mermaidDir = path.join(root, 'node_modules', 'mermaid')
	const version = JSON.parse(fs.readFileSync(path.join(mermaidDir, 'package.json'), 'utf8')).version

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

	const browser = await puppeteer.launch({ headless: 'shell' })
	const page = await browser.newPage()
	await page.setRequestInterception(true)
	page.on('request', request => {
		const url = new URL(request.url())
		if (url.origin !== origin && request.resourceType() === 'image') return request.respond({ contentType: 'image/svg+xml', body: stubImage })
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
	await page.addScriptTag({ content: hookAddDirective(hookPreprocess(hookDagre(fs.readFileSync(path.join(mermaidDir, 'dist', 'mermaid.min.js'), 'utf8')))) })
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

	// The files of one case: facts, and boxes when mermaid.js accepts it, and with `dagre` the dagre layouts of an accepted
	// case of a type in dagreTypes, each as its text, or null when the case has none. unknownType names a type extract.mjs
	// has no extractor for, and then nothing else is set. `name` is the case's file name without .mmd, which seeds the
	// page's Math.random and names the SVG.
	async function run(source, name, { dagre = true } = {}) {
		await page.evaluate(seed => globalThis.__oracleSeed(seed), seedOf(name))
		const result = await page.evaluate(extractFacts, source, name, shapes)
		if (result.unknownType) return { unknownType: result.unknownType }
		const header = [`# mermaid ${version}`, `# source ${sourceHash(source)}`]
		const body = result.error === undefined
			? ['accept\tyes', ...result.facts.sort()]
			: ['accept\tno', `# error ${result.error.split('\n')[0].trim()}`]
		const facts = ['# mermaid.js facts. Refresh with: mise run oracle', ...header, ...body].join('\n') + '\n'
		const boxes = result.error === undefined
			? ['# mermaid.js boxes. Refresh with: mise run oracle', ...header, ...[...result.boxes, ...await fontsDrawn()].sort()].join('\n') + '\n'
			: null
		const type = result.facts?.find(f => f.startsWith('type\t'))?.slice(5)
		// A case mermaid.js draws can still throw when it is drawn again with dagre, as a random flowchart with an INVALID
		// edge does in insertEdge. It then has no dagre file.
		let layouts = null
		let dagreError
		if (dagre && result.error === undefined && dagreTypes.has(type)) {
			await page.evaluate(seed => globalThis.__oracleSeed(seed), seedOf(name))
			try {
				layouts = await page.evaluate(dagreLayouts, source, name, init)
			} catch (e) {
				dagreError = String(e.message).split('\n')[0]
			}
		}
		const dagreText = layouts ? ['# mermaid.js dagre layouts. Refresh with: mise run oracle', ...header, ...layouts].join('\n') + '\n' : null
		return { error: result.error, facts, boxes, dagre: dagreText, dagreError, svg: result.svg }
	}

	return { version, page, shapeTable, themeNames, run, close: () => browser.close() }
}

// The files of a folder and its subfolders, as paths relative to it with / between folders.
export const filesUnder = dir =>
	fs.readdirSync(dir, { recursive: true, withFileTypes: true })
		.filter(e => e.isFile())
		.map(e => path.relative(dir, path.join(e.parentPath, e.name)).split(path.sep).join('/'))
		.sort()
