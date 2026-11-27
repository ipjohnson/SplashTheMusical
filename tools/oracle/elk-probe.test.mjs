import { test } from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { flowchartOptions, help, probe, readLayouts, stageFlags } from './elk-probe.mjs'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..')
const script = path.join(root, 'tools', 'oracle', 'elk-probe.mjs')
const recorded = ['flowchart/docs-flowchart-007', 'state/docs-stateDiagram-003', 'class/docs-classDiagram-020']
const plain = { overrides: [], pins: [] }

// A graph we write: a points to b and c, and c to d. Its coordinates put b alone in the last layer and b before c.
const written = [
	'node\ta\troot\t50\t40\t0\t0',
	'node\tb\troot\t50\t40\t0\t300',
	'node\tc\troot\t50\t40\t100\t100',
	'node\td\troot\t50\t40\t200\t200',
	'edge\te1\troot\ta\tb',
	'edge\te2\troot\ta\tc',
	'edge\te3\troot\tc\td',
].join('\n')

const layers = lines => lines.filter(l => l.startsWith('# layer\t')).map(l => l.split('\t').slice(1).join(' '))
const resultOf = lines => lines.slice(lines.indexOf('result') + 1).filter(l => !l.startsWith('#'))

for (const name of recorded) {
	test(`the probe's input and result of ${name} are the recorded ones`, async () => {
		const file = fs.readFileSync(path.join(root, 'corpus', name + '.elk.txt'), 'utf8')
		const { lines, differs } = await probe(file, { ...plain, check: true }, name)
		assert.equal(differs, 0, lines.filter(l => l.startsWith('# check')).join('\n'))
		const [layout] = readLayouts(file)
		assert.deepEqual(resultOf(lines), layout.recorded)
		assert.deepEqual(lines.slice(lines.indexOf('layout\t1') + 1, lines.indexOf('result')), layout.input)
	})
}

test('the probe prints the layer and position of every leaf of a recorded case', async () => {
	const file = fs.readFileSync(path.join(root, 'corpus', 'state/docs-stateDiagram-003.elk.txt'), 'utf8')
	const { lines } = await probe(file, plain)
	const [layout] = readLayouts(file)
	const nodes = layout.input.filter(l => l.startsWith('node\t')).map(l => l.split('\t'))
	const leaves = nodes.filter(n => n[2] && !nodes.some(o => o[2] === n[1])).map(n => n[1])
	assert.deepEqual(layers(lines).map(l => l.split(' ')[0]).sort(), [...leaves].sort())
	for (const line of layers(lines)) assert.match(line, /^\S+ \d+ \d+$/)
})

test('a graph without elk.algorithm gets the options mermaid gives a flowchart', async () => {
	const { lines } = await probe(written, plain)
	for (const [key, value] of flowchartOptions) assert.ok(lines.includes(`option\tnode\troot\t\t${key}\t${value}`), key)
	assert.ok(lines.includes('option\tedge-label\te1\t0\tedgeLabels.placement\tCENTER'))
	assert.deepEqual(layers(lines), ['a 0 0', 'b 1 0', 'c 1 1', 'd 2 0'])
})

test('a root option the graph sets replaces the flowchart default', async () => {
	const { lines } = await probe('option\tnode\troot\t\telk.direction\tRIGHT\n' + written, plain)
	assert.ok(lines.includes('option\tnode\troot\t\telk.direction\tRIGHT'))
	assert.ok(!lines.includes('option\tnode\troot\t\telk.direction\tDOWN'))
})

test('--keep-layers keeps the layers the coordinates give', async () => {
	const { lines } = await probe(written, { ...plain, pins: [stageFlags['--keep-layers']] })
	assert.deepEqual(layers(lines), ['a 0 0', 'c 1 0', 'd 2 0', 'b 3 0'])
})

test('--keep-order keeps the order within a layer the coordinates give', async () => {
	const reversed = written.replace('node\tb\troot\t50\t40\t0\t300', 'node\tb\troot\t50\t40\t200\t100').replace('node\tc\troot\t50\t40\t100\t100', 'node\tc\troot\t50\t40\t0\t100')
	const { lines } = await probe(reversed, { ...plain, pins: [stageFlags['--keep-order']] })
	assert.deepEqual(layers(lines), ['a 0 0', 'c 1 0', 'b 1 1', 'd 2 0'])
})

test('the pins with --place-from-result keep ELK\'s own layers and order of a recorded case', async () => {
	const file = fs.readFileSync(path.join(root, 'corpus', 'flowchart/docs-flowchart-007.elk.txt'), 'utf8')
	const free = await probe(file, plain)
	const pinned = await probe(file, { ...plain, pins: [stageFlags['--keep-layers'], stageFlags['--keep-order']], placeFromResult: true })
	assert.deepEqual(layers(pinned.lines), layers(free.lines))
	assert.ok(pinned.lines.includes('option\tnode\troot\t\telk.layered.layering.strategy\tINTERACTIVE'))
})

test('--option sets a root option', async () => {
	const { lines } = await probe(written, { ...plain, overrides: [['elk.direction', 'LEFT']] })
	assert.ok(lines.includes('option\tnode\troot\t\telk.direction\tLEFT'))
	assert.ok(lines.includes('# overrides elk.direction=LEFT'))
})

test('the help names the overrides that pin a stage', () => {
	const text = execFileSync(process.execPath, [script, '--help'], { encoding: 'utf8' })
	assert.equal(text.trim(), help)
	for (const flag of ['--keep-layers', '--keep-order', '--place-from-result', '--option']) assert.ok(text.includes(flag), flag)
	assert.match(text, /layers[\s\S]*are kept/)
	assert.match(text, /order within each layer[\s\S]*is kept/)
})

test('the command prints the graph file and fails --check on a changed result', () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'stm-elk-probe-'))
	const file = path.join(dir, 'case.elk.txt')
	const source = fs.readFileSync(path.join(root, 'corpus', 'flowchart/docs-flowchart-005.elk.txt'), 'utf8')
	fs.writeFileSync(file, source)
	const same = spawnSync(process.execPath, [script, '--check', file], { encoding: 'utf8' })
	assert.equal(same.status, 0, same.stderr)
	assert.ok(same.stdout.includes('# check\tsame'))
	fs.writeFileSync(file, source.replace('place\tStop\troot\t12\t97', 'place\tStop\troot\t12\t98'))
	const changed = spawnSync(process.execPath, [script, '--check', file], { encoding: 'utf8' })
	assert.equal(changed.status, 1)
	assert.match(changed.stdout, /# check\tdiffers\tline 3 of the result/)
})

test('an error of elkjs is one line of its message', () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'stm-elk-probe-'))
	const file = path.join(dir, 'bad.txt')
	fs.writeFileSync(file, 'node\ta\troot\t50\t40\nedge\te\troot\ta\tmissing\n')
	const run = spawnSync(process.execPath, [script, file], { encoding: 'utf8' })
	assert.equal(run.status, 2)
	assert.equal(run.stderr.trim().split('\n').length, 1, run.stderr.slice(0, 300))
	assert.match(run.stderr, /^elk-probe: /)
})

test('--serve answers each graph with the output lines and a period', () => {
	const input = Buffer.from(written).toString('base64') + '\n' + Buffer.from('nonsense').toString('base64') + '\n'
	const run = spawnSync(process.execPath, [script, '--serve'], { input, encoding: 'utf8' })
	const lines = run.stdout.split('\n')
	assert.equal(lines[0], 'ready')
	const answers = run.stdout.slice('ready\n'.length).split('\n.\n')
	assert.ok(answers[0].includes('# layer\td\t2\t0'))
	assert.match(answers[1], /^# error /)
})
