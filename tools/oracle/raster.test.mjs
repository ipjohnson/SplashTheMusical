import { test } from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const root = path.dirname(fileURLToPath(import.meta.url))

const svg = body => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 100">${body}</svg>`
const boxes = svg('<rect x="10" y="10" width="80" height="80" fill="#336"/><rect x="110" y="10" width="80" height="80" fill="#633"/>')
const text = svg('<text x="20" y="60" font-size="30">Hello</text>')
const facts = accepted => `# mermaid.js facts\n# mermaid 12.3.0\n# source 0\naccept\t${accepted ? 'yes' : 'no'}\n`

// A corpus of one type with a threshold and one that is report-only, with the oracle's and STM's SVGs.
function fixture({ dropOracleSvg } = {}) {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'stm-raster-'))
	const cases = {
		'pie/same': { accepted: true, oracle: boxes, stm: boxes },
		'pie/differ': { accepted: true, oracle: boxes, stm: text },
		'pie/no-stm': { accepted: true, oracle: boxes },
		'pie/refused': { accepted: false },
		'pie/stm-only': { accepted: false, stm: boxes },
		'mindmap/differ': { accepted: true, oracle: boxes, stm: text },
	}
	for (const [name, c] of Object.entries(cases)) {
		for (const sub of ['corpus', 'oracle', 'stm']) fs.mkdirSync(path.join(dir, sub, path.dirname(name)), { recursive: true })
		fs.writeFileSync(path.join(dir, 'corpus', name + '.mmd'), 'pie\n')
		fs.writeFileSync(path.join(dir, 'corpus', name + '.facts.txt'), facts(c.accepted))
		if (c.oracle && name !== dropOracleSvg) fs.writeFileSync(path.join(dir, 'oracle', name + '.svg'), c.oracle)
		if (c.stm) fs.writeFileSync(path.join(dir, 'stm', name + '.svg'), c.stm)
	}
	fs.mkdirSync(path.join(dir, 'corpus', 'tolerances'))
	fs.writeFileSync(path.join(dir, 'corpus', 'tolerances', 'pie.json'), '{ "canvas": 0.05, "overlap": 0.8, "raster": 0.9 }')
	fs.writeFileSync(path.join(dir, 'corpus', 'tolerances', 'mindmap.json'), '{ "canvas": 0.05, "overlap": 0.8, "raster": 0.9, "rasterReportOnly": true }')
	return dir
}

const run = dir =>
	spawnSync(
		process.execPath,
		[path.join(root, 'raster.mjs'), ...['corpus', 'oracle', 'stm', 'l4.txt', 'images'].map(f => path.join(dir, f))],
		{ encoding: 'utf8' },
	)

const isPng = file => fs.readFileSync(file).subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]))

test('scores each case, writes the outcomes and draws the failing cases side by side', () => {
	const dir = fixture()
	const result = run(dir)
	assert.equal(result.status, 0, result.stderr)

	assert.equal(
		fs.readFileSync(path.join(dir, 'l4.txt'), 'utf8'),
		[
			'# L4 outcomes. Refresh with: mise run raster',
			'mindmap/differ\treport',
			'pie/differ\tfail',
			'pie/no-stm\tfail',
			'pie/refused\tn/a',
			'pie/same\tpass',
			'pie/stm-only\tfail',
			'',
		].join('\n'),
	)

	const scores = Object.fromEntries(
		fs.readFileSync(path.join(dir, 'images', 'scores.txt'), 'utf8').trim().split('\n').slice(1).map(l => [l.split('\t')[0], l.split('\t')[2]]),
	)
	assert.equal(scores['pie/same'], '1.0000')
	assert.ok(Number(scores['pie/differ']) < 0.9, scores['pie/differ'])
	assert.equal(scores['pie/no-stm'], undefined)

	const images = fs.readdirSync(path.join(dir, 'images'), { recursive: true }).filter(f => f.endsWith('.png')).sort()
	assert.deepEqual(images, ['mindmap/differ.png', 'pie/differ.png', 'pie/no-stm.png', 'pie/stm-only.png'].map(f => f.split('/').join(path.sep)))
	for (const image of images) assert.ok(isPng(path.join(dir, 'images', image)), image)
})

test('fails when mermaid.js accepts a case and its SVG is missing', () => {
	const dir = fixture({ dropOracleSvg: 'pie/same' })
	const result = run(dir)
	assert.equal(result.status, 1)
	assert.match(result.stderr, /pie\/same: mermaid.js accepts it/)
})
