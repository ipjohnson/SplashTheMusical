// The dumps that do not belong to one case: the theme variables, the default config and the shape table, and the dagre
// recorder that runs per flowchart, state, class and ER case. tools/oracle/README.md describes the formats.
//
// readShapes, readThemeNames and hookDagre run in Node and read mermaid's dist. configDumps and dagreLayouts are handed
// to page.evaluate, so each must be self-contained: puppeteer sends the function's source to the page, not its module
// scope.
import fs from 'node:fs'
import path from 'node:path'

// The unminified chunk of mermaid's ESM build that holds a marker, and the marker's offset in it.
function findInDist(mermaidDir, marker, what) {
	const chunks = path.join(mermaidDir, 'dist', 'chunks', 'mermaid.esm')
	for (const file of fs.readdirSync(chunks).filter(f => f.endsWith('.mjs')).sort()) {
		const code = fs.readFileSync(path.join(chunks, file), 'utf8')
		const start = code.indexOf(marker)
		if (start >= 0) return { code, start }
	}
	throw new Error(`${marker} not found in mermaid's dist; ${what} moved`)
}

// shapesDefs, the documented shapes in table order, and undocumentedShapes, the shapes mermaid's diagrams use
// internally. Each handler is the drawing function's name in the unminified dist. `keys` is the lookup table mermaid
// builds from both, generateShapeMap: the undocumented shapes first, then each shape's short name, aliases and internal
// aliases, with a later entry replacing an earlier one.
export function readShapes(mermaidDir) {
	const { code, start } = findInDist(mermaidDir, 'shapesDefs = [', 'the shape table')
	const table = code.slice(start, code.indexOf('\n];', start))
	const defs = []
	for (const entry of table.split(/\n {2}\{/).slice(1)) {
		const field = name => {
			const m = new RegExp(`\\n\\s+${name}: "((?:[^"\\\\]|\\\\.)*)"`).exec(entry)
			return m ? JSON.parse(`"${m[1]}"`) : ''
		}
		const list = name => [...(new RegExp(`\\n\\s+${name}: \\[([^\\]]*)\\]`).exec(entry)?.[1] ?? '').matchAll(/"([^"]+)"/g)].map(m => m[1])
		const shortName = field('shortName')
		if (!shortName) continue
		defs.push({
			shortName, name: field('name'), semanticName: field('semanticName'), description: field('description'),
			aliases: list('aliases'), internalAliases: list('internalAliases'), handler: /\n\s+handler: ([\w$]+)/.exec(entry)?.[1] ?? '',
		})
	}
	const u = code.indexOf('const undocumentedShapes = {')
	if (u < 0) throw new Error('undocumentedShapes not found in mermaid\'s dist; the shape table moved')
	const undocumented = []
	for (const line of code.slice(u, code.indexOf('\n  };', u)).split('\n').slice(1)) {
		const m = /^\s*(?:"([^"]+)"|([\w$]+))(?::\s*([\w$]+))?,?$/.exec(line)
		if (m) undocumented.push({ key: m[1] ?? m[2], handler: m[3] ?? m[2] })
	}
	const keys = new Map(undocumented.map(s => [s.key, s.handler]))
	for (const d of defs)
		for (const k of [d.shortName, ...d.aliases, ...d.internalAliases]) keys.set(k, d.handler)
	return { defs, undocumented, keys }
}

// The names in mermaid's themes table, themes_default, in source order.
export function readThemeNames(mermaidDir) {
	const { code, start } = findInDist(mermaidDir, 'var themes_default = {', 'the themes table')
	const block = code.slice(start, code.indexOf('\n};', start))
	return [...block.matchAll(/^ {2}"?([\w-]+)"?: \{/gm)].map(m => m[1])
}

// mermaid.min.js with a hook around dagre's layout(g, opts), the one function every dagre layout goes through. The hook
// calls globalThis.__oracleDagre.before(g) and .after(g) when a recorder is set. The function is found by its timing
// labels, which minification keeps; the run fails when it is not found exactly once.
export function hookDagre(code) {
	const re = /function ([\w$]+)\(([\w$]+),([\w$]+)\)\{var [\w$]+=\3&&\3\.debugTiming\?[\w$]+:[\w$]+;[\w$]+\("layout",\(\)=>\{var [\w$]+=[\w$]+\("  buildLayoutGraph"/g
	const found = [...code.matchAll(re)]
	if (found.length !== 1) throw new Error(`dagre's layout function found ${found.length} times in mermaid.min.js, expected once; the dagre hook in tools/oracle/dumps.mjs needs updating`)
	const [, fn, g, opts] = found[0]
	const head = `function ${fn}(${g},${opts}){`
	const hook = `function ${fn}(${g},${opts}){globalThis.__oracleDagre?.before(${g});__oracleDagreLayout(${g},${opts});globalThis.__oracleDagre?.after(${g})}function __oracleDagreLayout(${g},${opts}){`
	return code.slice(0, found[0].index) + hook + code.slice(found[0].index + head.length)
}

// mermaid.min.js with its preprocessDiagram set on globalThis.__oraclePreprocess. preprocessDiagram returns the front
// matter title and the config of the front matter and the directives, which no public function returns together. The
// function is found by the name label minification keeps; the run fails when it is not found exactly once.
export function hookPreprocess(code) {
	const found = [...code.matchAll(/[\w$]+\(([\w$]+),"preprocessDiagram"\);/g)]
	if (found.length !== 1) throw new Error(`preprocessDiagram found ${found.length} times in mermaid.min.js, expected once; the preprocess hook in tools/oracle/dumps.mjs needs updating`)
	const end = found[0].index + found[0][0].length
	return code.slice(0, end) + `globalThis.__oraclePreprocess=${found[0][1]};` + code.slice(end)
}

// In the page: the computed variables of each theme and the default config, as lines of `<kind>\t<fields>`. A value is
// JSON, `undefined` for a key that is present without a value, or NaN or Infinity as JavaScript writes them. Nested
// objects and arrays are flattened into dotted paths; an empty one is written as {} or [].
export function configDumps(themeNames, init) {
	const value = v => v === undefined ? 'undefined' : typeof v === 'number' && !Number.isFinite(v) ? String(v) : JSON.stringify(v)
	const flatten = (prefix, v, out) => {
		if (typeof v === 'function') return
		if (v !== null && typeof v === 'object') {
			const keys = Object.keys(v)
			if (!keys.length) out.push([prefix, Array.isArray(v) ? '[]' : '{}'])
			for (const k of keys) flatten(prefix ? `${prefix}.${k}` : k, v[k], out)
		} else out.push([prefix, value(v)])
		return out
	}
	const themes = []
	try {
		for (const theme of themeNames) {
			mermaid.initialize({ ...init, theme })
			const site = mermaid.mermaidAPI.getSiteConfig()
			if (site.theme !== theme) throw new Error(`mermaid did not accept the theme '${theme}'`)
			for (const [k, v] of flatten('', site.themeVariables, [])) themes.push(`theme\t${theme}\t${k}\t${v}`)
		}
	} finally {
		mermaid.initialize(init)
	}
	const config = flatten('', mermaid.mermaidAPI.defaultConfig, []).map(([k, v]) => `key\t${k}\t${v}`)
	return { themes, config }
}

// In the page: renders the source with `layout: dagre` and returns each dagre layout as lines, in call order. The input
// is what dagre's buildLayoutGraph reads, with its defaults applied. The output is what dagre writes back to the graph.
// Numbers are written in full, as JavaScript's String writes them.
export async function dagreLayouts(source, name, init) {
	const esc = v => v === undefined || v === null ? '' : String(v).replace(/\\/g, '\\\\').replace(/\t/g, '\\t').replace(/\n/g, '\\n').replace(/\r/g, '\\r')
	const line = (...fields) => fields.map(esc).join('\t').replace(/\t+$/, '')
	// dagre's canonicalize: keys lower-cased, a later key replacing an earlier one.
	const canonical = label => {
		const out = {}
		for (const [k, v] of Object.entries(label ?? {})) out[k.toLowerCase()] = v
		return out
	}
	// dagre's selectNumberAttrs and merge with defaults: a present key is passed through Number, and a missing one keeps
	// the default. pick sees inherited keys too, hence `in`.
	const number = (label, key, fallback) => key in label ? Number(label[key]) : fallback
	const text = (label, key, fallback) => key in label && label[key] !== undefined ? label[key] : fallback
	const lines = []
	let calls = 0
	globalThis.__oracleDagre = {
		before(g) {
			const gl = canonical(g.graph())
			lines.push(line('layout', ++calls))
			lines.push(line('graph', text(gl, 'rankdir', 'tb'), number(gl, 'nodesep', 50), number(gl, 'edgesep', 20), number(gl, 'ranksep', 50),
				number(gl, 'marginx'), number(gl, 'marginy'), text(gl, 'align'), text(gl, 'acyclicer'), text(gl, 'ranker')))
			for (const v of g.nodes()) {
				const n = canonical(g.node(v))
				lines.push(line('node', v, g.parent(v), number(n, 'width', 0), number(n, 'height', 0)))
			}
			for (const e of g.edges()) {
				const l = canonical(g.edge(e))
				lines.push(line('edge', e.v, e.w, e.name, number(l, 'minlen', 1), number(l, 'weight', 1), number(l, 'width', 0), number(l, 'height', 0),
					number(l, 'labeloffset', 10), text(l, 'labelpos', 'r')))
			}
		},
		after(g) {
			lines.push(line('size', g.graph().width, g.graph().height))
			for (const v of g.nodes()) {
				const n = g.node(v)
				lines.push(line('place', v, n.x, n.y, n.width, n.height))
			}
			for (const e of g.edges()) {
				const l = g.edge(e)
				lines.push(line('route', e.v, e.w, e.name, l.x, l.y, (l.points ?? []).map(p => `${p.x},${p.y}`).join(' ')))
			}
		},
	}
	try {
		mermaid.initialize({ ...init, layout: 'dagre' })
		await mermaid.render('dagre' + name.replace(/[^A-Za-z0-9]/g, ''), source)
	} finally {
		globalThis.__oracleDagre = undefined
		mermaid.initialize(init)
	}
	return lines
}
