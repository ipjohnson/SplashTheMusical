import { test, before, after } from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { checkPinned } from './pinned.mjs'
import { hookDagre, hookPreprocess } from './dumps.mjs'

const root = path.dirname(fileURLToPath(import.meta.url))
const cases = path.join(root, 'cases')

// The 37 diagram ids mermaid 12.3.0 registers (docs/upstream-map.md) and ZenUML's.
const types = [
	'agentflow', 'architecture', 'block', 'c4', 'classDiagram', 'cynefin', 'er', 'eventmodeling', 'flowchart-elk',
	'flowchart-v2', 'gantt', 'gitGraph', 'info', 'ishikawa', 'journey', 'kanban', 'mindmap', 'packet', 'pie',
	'quadrantChart', 'radar', 'railroad', 'railroadAbnf', 'railroadEbnf', 'railroadPeg', 'requirement', 'sankey',
	'sequence', 'stateDiagram', 'swimlane', 'timeline', 'treeView', 'treemap', 'usecase', 'venn', 'wardley', 'xychart',
	'zenuml',
]

const temp = () => fs.mkdtempSync(path.join(os.tmpdir(), 'stm-oracle-'))
const run = (dir, casesDir, out) =>
	spawnSync(process.execPath, [path.join(dir, 'facts.mjs'), casesDir, path.join(out, 'facts'), path.join(out, 'svg')], { encoding: 'utf8' })

// A copy of the runner whose lock file or extractor can be changed, sharing the real node_modules and fonts.
function copyOfRunner({ pinMermaid, extract } = {}) {
	const dir = temp()
	for (const f of ['facts.mjs', 'pinned.mjs', 'dumps.mjs', 'package.json']) fs.copyFileSync(path.join(root, f), path.join(dir, f))
	fs.writeFileSync(path.join(dir, 'extract.mjs'), extract ?? fs.readFileSync(path.join(root, 'extract.mjs'), 'utf8'))
	const lock = JSON.parse(fs.readFileSync(path.join(root, 'package-lock.json'), 'utf8'))
	if (pinMermaid) lock.packages['node_modules/mermaid'].version = pinMermaid
	fs.writeFileSync(path.join(dir, 'package-lock.json'), JSON.stringify(lock))
	fs.symlinkSync(path.join(root, 'node_modules'), path.join(dir, 'node_modules'), 'dir')
	fs.symlinkSync(path.join(root, 'fonts'), path.join(dir, 'fonts'), 'dir')
	return dir
}

test('node_modules matches package-lock.json', () => {
	assert.deepEqual(checkPinned(root), [])
})

// For each case, fact kinds whose elements must each have a box: the fact's kind, the field that names the element, and
// the box's kind.
const boxed = {
	'flowchart-v2': [['node', 1, 'node'], ['group', 1, 'group']],
	'flowchart-elk': [['node', 1, 'node']],
	swimlane: [['node', 1, 'node'], ['group', 1, 'group']],
	stateDiagram: [['node', 1, 'node'], ['group', 1, 'group'], ['note', 1, 'note']],
	classDiagram: [['class', 1, 'class'], ['namespace', 1, 'namespace']],
	sequence: [['actor', 1, 'actor'], ['actor', 1, 'actor-bottom'], ['box', 1, 'box']],
	er: [['entity', 1, 'entity']],
	pie: [['slice', 1, 'slice'], ['title', 1, 'title']],
	quadrantChart: [['quadrant', 1, 'quadrant'], ['point', 1, 'point']],
	timeline: [['section', 1, 'section'], ['period', 2, 'period']],
	gitGraph: [['branch', 1, 'branch'], ['commit', 1, 'commit']],
	radar: [['axis', 1, 'axis'], ['curve', 1, 'curve']],
	railroadAbnf: [['rule', 1, 'rule']],
	wardley: [['node', 1, 'node']],
	zenuml: [['participant', 2, 'participant']],
	treemap: [['node', 1, 'node']],
	venn: [['set', 1, 'set'], ['union', 1, 'union']],
	mindmap: [['node', 1, 'node']],
	'mindmap-tidy-tree': [['node', 1, 'node']],
	gantt: [['section', 1, 'section']],
	journey: [['section', 1, 'section']],
	c4: [['element', 1, 'element'], ['boundary', 1, 'boundary']],
	sankey: [],
	xychart: [],
	requirement: [['requirement', 1, 'requirement'], ['element', 1, 'element']],
	packet: [],
	kanban: [['column', 1, 'column'], ['card', 2, 'card']],
	architecture: [['group', 1, 'group'], ['service', 1, 'service'], ['junction', 1, 'junction']],
	block: [['block', 1, 'block']],
	usecase: [['actor', 1, 'actor'], ['usecase', 1, 'usecase']],
	treeView: [['node', 1, 'node']],
	agentflow: [['node', 1, 'node'], ['flow', 1, 'flow']],
	cynefin: [],
	eventmodeling: [['lane', 1, 'lane'], ['frame', 1, 'frame']],
	ishikawa: [['head', 1, 'head'], ['cause', 1, 'cause']],
}

const accepted = dir => fs.readdirSync(dir).filter(f => f.endsWith('.facts.txt') && /^accept\tyes$/m.test(fs.readFileSync(path.join(dir, f), 'utf8')))

// Two runs over the cases, shared by the tests that read their output.
const first = temp(), second = temp()
before(() => {
	for (const out of [first, second]) {
		const result = run(root, cases, out)
		assert.equal(result.status, 0, result.stderr)
	}
})
after(() => {
	fs.rmSync(first, { recursive: true, force: true })
	fs.rmSync(second, { recursive: true, force: true })
})

// The cases whose types are rendered again with `layout: dagre`.
const dagreCases = ['classDiagram', 'er', 'flowchart-v2', 'stateDiagram']
const dumpFiles = ['config.txt', 'shapes.txt', 'themes.txt']

test('two runs write a facts file per case, a boxes file per accepted case, the dagre and global dumps, cover every diagram type and write identical files', () => {
	const names = fs.readdirSync(cases).filter(f => f.endsWith('.mmd')).map(f => f.slice(0, -4) + '.facts.txt').sort()
	const boxes = accepted(path.join(first, 'facts')).map(f => f.replace('.facts.txt', '.boxes.txt'))
	assert.equal(boxes.length, names.length - 1)
	const dagre = dagreCases.map(n => n + '.dagre.txt')
	assert.deepEqual(fs.readdirSync(path.join(first, 'facts')).sort(), [...names, ...boxes, ...dagre, ...dumpFiles].sort())
	for (const dir of ['facts', 'svg']) {
		const files = fs.readdirSync(path.join(first, dir)).sort()
		assert.deepEqual(fs.readdirSync(path.join(second, dir)).sort(), files)
		for (const f of files)
			assert.ok(fs.readFileSync(path.join(first, dir, f)).equals(fs.readFileSync(path.join(second, dir, f))), `${dir}/${f} differs between runs`)
	}
	const seen = new Set()
	for (const f of names)
		for (const line of fs.readFileSync(path.join(first, 'facts', f), 'utf8').split('\n'))
			if (line.startsWith('type\t')) seen.add(line.slice(5))
	assert.deepEqual([...seen].sort(), [...types].sort())
	assert.match(fs.readFileSync(path.join(first, 'facts', 'refused.facts.txt'), 'utf8'), /^accept\tno$/m)
})

test('every accepted case has a canvas, text drawn only in the bundled font, and a box for each element its facts name', () => {
	const dir = path.join(first, 'facts')
	const numbers = /^-?\d+(\.\d+)?$/
	for (const f of accepted(dir)) {
		const name = f.slice(0, -'.facts.txt'.length)
		const facts = fs.readFileSync(path.join(dir, f), 'utf8').split('\n').map(l => l.split('\t'))
		const lines = fs.readFileSync(path.join(dir, name + '.boxes.txt'), 'utf8').split('\n').filter(Boolean)
		assert.deepEqual(lines.slice(0, 3).map(l => l.split(' ')[1]), ['mermaid.js', 'mermaid', 'source'], name)
		const rows = lines.slice(3).map(l => l.split('\t'))
		const canvas = rows.filter(r => r[0] === 'canvas')
		assert.equal(canvas.length, 1, `${name}: one canvas`)
		assert.ok(canvas[0].slice(1).every(v => numbers.test(v)) && Number(canvas[0][3]) > 0 && Number(canvas[0][4]) > 0, `${name}: ${canvas[0].join(' ')}`)
		assert.deepEqual(rows.filter(r => r[0] === 'font').map(r => r[1]), ['Liberation Sans'], `${name}: fonts`)
		const boxes = rows.filter(r => r[0] === 'box')
		assert.ok(boxes.length > 0, `${name}: no boxes`)
		for (const b of boxes) assert.ok(b.length === 7 && b.slice(3).every(v => numbers.test(v)), `${name}: ${b.join(' ')}`)
		const names = new Set(boxes.map(b => b[1] + '\t' + b[2]))
		for (const [kind, field, boxKind] of boxed[name] ?? []) {
			const named = facts.filter(r => r[0] === kind)
			assert.ok(named.length > 0, `${name}: no ${kind} facts`)
			for (const r of named) assert.ok(names.has(boxKind + '\t' + r[field]), `${name}: no ${boxKind} box for ${r[field]}`)
		}
	}
	assert.deepEqual(Object.keys(boxed).filter(n => !fs.existsSync(path.join(dir, n + '.boxes.txt'))), [])
})

const lines = file => fs.readFileSync(file, 'utf8').split('\n').filter(Boolean)
const finite = /^-?\d+(\.\d+)?(e[+-]?\d+)?$/

test('the theme dump has the computed variables of each of the 11 themes', () => {
	const rows = lines(path.join(first, 'facts', 'themes.txt'))
	assert.deepEqual(rows.slice(0, 2), ['# mermaid.js theme variables. Refresh with: mise run oracle', '# mermaid 12.3.0'])
	const vars = rows.slice(2).map(l => l.split('\t'))
	assert.ok(vars.every(r => r.length === 4 && r[0] === 'theme'))
	const themes = ['base', 'dark', 'default', 'forest', 'neo', 'neo-dark', 'neutral', 'redux', 'redux-color', 'redux-dark', 'redux-dark-color']
	assert.deepEqual([...new Set(vars.map(r => r[1]))].sort(), themes)
	const value = (theme, name) => vars.find(r => r[1] === theme && r[2] === name)?.[3]
	assert.equal(value('default', 'primaryColor'), '"#ECECFF"')
	for (const theme of themes) {
		for (const name of ['primaryColor', 'primaryTextColor', 'lineColor', 'fontFamily', 'mainBkg'])
			assert.match(value(theme, name) ?? '', /^".+"$/, `${theme} ${name}`)
		assert.ok(vars.filter(r => r[1] === theme).length > 200, `${theme}: too few variables`)
	}
})

test('the config dump has mermaid\'s default config and the shape dump has its shape table', () => {
	const config = lines(path.join(first, 'facts', 'config.txt'))
	assert.equal(config[0], '# mermaid.js default config. Refresh with: mise run oracle')
	for (const row of ['key\tlayout\t"elk"', 'key\ttheme\t"default"', 'key\tflowchart.theme\t"redux-color"', 'key\tflowchart.nodeSpacing\t50', 'key\tdeterministicIDSeed\tundefined', 'key\tthemeVariables.primaryColor\t"#ECECFF"'])
		assert.ok(config.includes(row), row)
	const shapes = lines(path.join(first, 'facts', 'shapes.txt'))
	assert.equal(shapes[0], '# mermaid.js shape table. Refresh with: mise run oracle')
	for (const row of ['shape\trect\tRectangle\tProcess\tStandard process shape\tsquareRect', 'alias\tdatabase\tcyl', 'internal-alias\tsquareRect\trect', 'undocumented\tstate\tstate', 'key\tdb\tcylinder', 'key\tclassBox\tclassBox'])
		assert.ok(shapes.includes(row), row)
	assert.equal(shapes.filter(l => l.startsWith('shape\t')).length, 53)
})

test('each dagre dump has a layout whose every node is placed and every edge routed, and covers the case\'s nodes', () => {
	for (const name of dagreCases) {
		const rows = lines(path.join(first, 'facts', name + '.dagre.txt'))
		assert.deepEqual(rows.slice(0, 2), ['# mermaid.js dagre layouts. Refresh with: mise run oracle', '# mermaid 12.3.0'])
		const all = rows.slice(3).map(l => l.split('\t'))
		const starts = all.flatMap((r, i) => r[0] === 'layout' ? [i] : [])
		assert.ok(starts.length > 0, `${name}: no layouts`)
		assert.deepEqual(all.filter(r => r[0] === 'layout').map(r => Number(r[1])), starts.map((_, i) => i + 1), `${name}: layout numbers`)
		for (const [i, start] of starts.entries()) {
			const block = all.slice(start + 1, starts[i + 1])
			const kinds = block.map(r => r[0])
			assert.equal(kinds[0], 'graph', `${name} ${i + 1}`)
			assert.ok(block[0].slice(2, 5).every(v => finite.test(v)), `${name}: ${block[0].join(' ')}`)
			const nodes = block.filter(r => r[0] === 'node'), edges = block.filter(r => r[0] === 'edge')
			assert.ok(nodes.length > 0, `${name} ${i + 1}: no nodes`)
			for (const n of nodes) assert.ok(n.slice(3).every(v => finite.test(v)), `${name}: ${n.join(' ')}`)
			for (const e of edges) assert.ok(e.slice(4, 9).every(v => finite.test(v)), `${name}: ${e.join(' ')}`)
			assert.ok(block.filter(r => r[0] === 'size').length === 1 && block.find(r => r[0] === 'size').slice(1).every(v => finite.test(v)), `${name}: size`)
			assert.deepEqual(block.filter(r => r[0] === 'place').map(r => r[1]), nodes.map(r => r[1]), `${name}: places`)
			for (const p of block.filter(r => r[0] === 'place')) assert.ok(p.slice(2).every(v => finite.test(v)), `${name}: ${p.join(' ')}`)
			const routes = block.filter(r => r[0] === 'route')
			assert.deepEqual(routes.map(r => r.slice(1, 4).join(' ')), edges.map(r => r.slice(1, 4).join(' ')), `${name}: routes`)
			for (const r of routes) {
				const points = r[6].split(' ').map(p => p.split(','))
				assert.ok(points.length >= 2 && points.flat().every(v => finite.test(v)), `${name}: ${r.join(' ')}`)
			}
			assert.match(kinds.join(' '), /^graph( node)+( edge)* size( place)+( route)*$/, `${name} ${i + 1}: line order`)
		}
		const placed = new Set(all.filter(r => r[0] === 'place').map(r => r[1]))
		const facts = lines(path.join(first, 'facts', name + '.facts.txt')).map(l => l.split('\t'))
		const kind = { classDiagram: 'class', er: 'entity' }[name] ?? 'node'
		const ids = facts.filter(r => r[0] === kind).map(r => r[1]).filter(id => !id.startsWith('[*]'))
		assert.ok(ids.length > 0, `${name}: no ${kind} facts`)
		// mermaid's ER renderer names an entity's node entity-<id>-<n>.
		const placedAs = id => name === 'er' ? [...placed].some(p => p.startsWith(`entity-${id}-`) && finite.test(p.slice(id.length + 8))) : placed.has(id)
		for (const id of ids) assert.ok(placedAs(id), `${name}: ${id} not placed`)
	}
})

test('the dagre hook wraps dagre\'s layout function once and refuses a bundle without it', () => {
	const code = fs.readFileSync(path.join(root, 'node_modules', 'mermaid', 'dist', 'mermaid.min.js'), 'utf8')
	const hooked = hookDagre(code)
	assert.equal(hooked.split('function __oracleDagreLayout(').length, 2)
	assert.equal(hooked.split('globalThis.__oracleDagre?.before(').length, 2)
	assert.throws(() => hookDagre(code.replace('"  buildLayoutGraph"', '"buildLayoutGraph"')), /found 0 times in mermaid\.min\.js/)
})

test('the preprocess hook exposes preprocessDiagram once and refuses a bundle without it', () => {
	const code = fs.readFileSync(path.join(root, 'node_modules', 'mermaid', 'dist', 'mermaid.min.js'), 'utf8')
	assert.equal(hookPreprocess(code).split('globalThis.__oraclePreprocess=').length, 2)
	assert.throws(() => hookPreprocess(code.replace('"preprocessDiagram"', '"preprocess"')), /found 0 times in mermaid\.min\.js/)
})

test('a case with front matter gets the preamble title and config mermaid.js read', () => {
	const dir = temp()
	try {
		fs.mkdirSync(path.join(dir, 'cases'))
		fs.writeFileSync(path.join(dir, 'cases', 'titled.mmd'), '---\ntitle: Sales\nconfig:\n  theme: dark\n---\n%%{init: {"pie": {"textPosition": 0.5}}}%%\npie\n  "a": 1\n')
		const result = run(root, path.join(dir, 'cases'), path.join(dir, 'out'))
		assert.equal(result.status, 0, result.stderr)
		const facts = fs.readFileSync(path.join(dir, 'out', 'facts', 'titled.facts.txt'), 'utf8').split('\n').filter(l => l.startsWith('preamble-'))
		assert.deepEqual(facts, ['preamble-config\tpie.textPosition\t0.5', 'preamble-config\ttheme\t"dark"', 'preamble-title\tSales'])
	} finally {
		fs.rmSync(dir, { recursive: true, force: true })
	}
})

test('a case in a subfolder gets its files beside it, with the output of the same case in a flat folder', () => {
	const dir = temp()
	try {
		fs.mkdirSync(path.join(dir, 'pie'))
		fs.mkdirSync(path.join(dir, 'flowchart'))
		fs.copyFileSync(path.join(cases, 'pie.mmd'), path.join(dir, 'pie', 'pie.mmd'))
		fs.copyFileSync(path.join(cases, 'flowchart-v2.mmd'), path.join(dir, 'flowchart', 'flowchart-v2.mmd'))
		fs.writeFileSync(path.join(dir, 'pie', 'gone.facts.txt'), '')
		const result = spawnSync(process.execPath, [path.join(root, 'facts.mjs'), dir, dir], { encoding: 'utf8' })
		assert.equal(result.status, 0, result.stderr)
		assert.deepEqual(fs.readdirSync(path.join(dir, 'pie')).sort(), ['pie.boxes.txt', 'pie.facts.txt', 'pie.mmd'])
		assert.deepEqual(fs.readdirSync(path.join(dir, 'flowchart')).sort(),
			['flowchart-v2.boxes.txt', 'flowchart-v2.dagre.txt', 'flowchart-v2.facts.txt', 'flowchart-v2.mmd'])
		for (const f of [...dumpFiles, 'pie/pie.facts.txt', 'pie/pie.boxes.txt', 'flowchart/flowchart-v2.dagre.txt'])
			assert.ok(fs.readFileSync(path.join(dir, f)).equals(fs.readFileSync(path.join(first, 'facts', path.basename(f)))), f)
	} finally {
		fs.rmSync(dir, { recursive: true, force: true })
	}
})

test('the run fails when node_modules holds another mermaid than the lock file pins', () => {
	const dir = copyOfRunner({ pinMermaid: '12.2.0' })
	try {
		const result = run(dir, cases, path.join(dir, 'out'))
		assert.equal(result.status, 1)
		assert.match(result.stderr, /node_modules has mermaid 12\.3\.0, package-lock\.json pins 12\.2\.0/)
		assert.equal(fs.existsSync(path.join(dir, 'out')), false)
	} finally {
		fs.rmSync(dir, { recursive: true, force: true })
	}
})

test('the run fails on a diagram type with no extractor', () => {
	const source = fs.readFileSync(path.join(root, 'extract.mjs'), 'utf8')
	const extract = source.replace('\t\tinfo, ishikawa,\n', '\t\tishikawa,\n')
	assert.notEqual(extract, source, 'the extractor table no longer lists info where the test expects it')
	const dir = copyOfRunner({ extract })
	try {
		const only = path.join(dir, 'cases')
		fs.mkdirSync(only)
		fs.copyFileSync(path.join(cases, 'info.mmd'), path.join(only, 'info.mmd'))
		const result = run(dir, only, path.join(dir, 'out'))
		assert.equal(result.status, 1)
		assert.match(result.stderr, /info: no extractor for mermaid diagram type 'info'/)
		assert.equal(fs.existsSync(path.join(dir, 'out', 'facts', 'info.facts.txt')), false)
	} finally {
		fs.rmSync(dir, { recursive: true, force: true })
	}
})
