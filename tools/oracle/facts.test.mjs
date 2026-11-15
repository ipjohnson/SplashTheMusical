import { test, before, after } from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { checkPinned } from './pinned.mjs'

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
	for (const f of ['facts.mjs', 'pinned.mjs', 'package.json']) fs.copyFileSync(path.join(root, f), path.join(dir, f))
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

test('two runs write a facts file per case and a boxes file per accepted case, cover every diagram type and write identical files', () => {
	const names = fs.readdirSync(cases).filter(f => f.endsWith('.mmd')).map(f => f.slice(0, -4) + '.facts.txt').sort()
	const boxes = accepted(path.join(first, 'facts')).map(f => f.replace('.facts.txt', '.boxes.txt'))
	assert.equal(boxes.length, names.length - 1)
	assert.deepEqual(fs.readdirSync(path.join(first, 'facts')).sort(), [...names, ...boxes].sort())
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
