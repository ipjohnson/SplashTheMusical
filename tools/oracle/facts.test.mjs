import { test } from 'node:test'
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

// A copy of the runner whose lock file or extractor can be changed, sharing the real node_modules.
function copyOfRunner({ pinMermaid, extract } = {}) {
	const dir = temp()
	for (const f of ['facts.mjs', 'pinned.mjs', 'package.json']) fs.copyFileSync(path.join(root, f), path.join(dir, f))
	fs.writeFileSync(path.join(dir, 'extract.mjs'), extract ?? fs.readFileSync(path.join(root, 'extract.mjs'), 'utf8'))
	const lock = JSON.parse(fs.readFileSync(path.join(root, 'package-lock.json'), 'utf8'))
	if (pinMermaid) lock.packages['node_modules/mermaid'].version = pinMermaid
	fs.writeFileSync(path.join(dir, 'package-lock.json'), JSON.stringify(lock))
	fs.symlinkSync(path.join(root, 'node_modules'), path.join(dir, 'node_modules'), 'dir')
	return dir
}

test('node_modules matches package-lock.json', () => {
	assert.deepEqual(checkPinned(root), [])
})

test('two runs write a facts file per case, cover every diagram type and write identical files', () => {
	const first = temp(), second = temp()
	try {
		for (const out of [first, second]) {
			const result = run(root, cases, out)
			assert.equal(result.status, 0, result.stderr)
		}
		const names = fs.readdirSync(cases).filter(f => f.endsWith('.mmd')).map(f => f.slice(0, -4) + '.facts.txt').sort()
		assert.deepEqual(fs.readdirSync(path.join(first, 'facts')).sort(), names)
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
	} finally {
		fs.rmSync(first, { recursive: true, force: true })
		fs.rmSync(second, { recursive: true, force: true })
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
