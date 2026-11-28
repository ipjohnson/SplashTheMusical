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

const run = (dir, { cache, cases, stmRasters } = {}) =>
	spawnSync(
		process.execPath,
		[path.join(root, 'raster.mjs'), ...['corpus', 'oracle', 'stm', 'l4', 'images', ...(cache ? ['cache'] : [])].map(f => path.join(dir, f))],
		{ encoding: 'utf8', env: { ...process.env, STM_RASTER_CASES: cases ?? '', STM_RASTER_DIR: stmRasters ? path.join(dir, 'stm-raster') : '' } },
	)

// The outcome files, by file name.
const readOutcomes = dir =>
	Object.fromEntries(fs.readdirSync(path.join(dir, 'l4')).sort().map(f => [f, fs.readFileSync(path.join(dir, 'l4', f), 'utf8')]))
const readScores = dir => fs.readFileSync(path.join(dir, 'images', 'scores.txt'), 'utf8')
const cacheFiles = dir => fs.readdirSync(path.join(dir, 'cache'), { recursive: true })
	.filter(f => f.endsWith('.png') || f.endsWith('.key'))
	.map(f => f.split(path.sep).join('/'))
	.sort()

const isPng = file => fs.readFileSync(file).subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]))

test('scores each case, writes the outcomes of each type and draws the failing cases side by side', () => {
	const dir = fixture()
	fs.mkdirSync(path.join(dir, 'l4'))
	fs.writeFileSync(path.join(dir, 'l4', 'gone.txt'), '# L4 outcomes of a type that left the corpus\n')
	const result = run(dir)
	assert.equal(result.status, 0, result.stderr)

	assert.deepEqual(readOutcomes(dir), {
		'mindmap.txt': ['# L4 outcomes of mindmap. Refresh with: mise run raster', 'mindmap/differ\treport', ''].join('\n'),
		'pie.txt': [
			'# L4 outcomes of pie. Refresh with: mise run raster',
			'pie/differ\tfail',
			'pie/no-stm\tfail',
			'pie/refused\tn/a',
			'pie/same\tpass',
			'pie/stm-only\tfail',
			'',
		].join('\n'),
	})

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

test('a run that reads mermaid.js rasters from the cache scores as a run without one', () => {
	const dir = fixture()
	const cold = run(dir)
	assert.equal(cold.status, 0, cold.stderr)
	const coldScores = readScores(dir)
	const coldOutcomes = readOutcomes(dir)

	const filling = run(dir, { cache: true })
	assert.equal(filling.status, 0, filling.stderr)
	assert.match(filling.stdout, /0 read from .*, 4 drawn, 0 files deleted/)
	assert.deepEqual(cacheFiles(dir), ['mindmap/differ', 'pie/differ', 'pie/no-stm', 'pie/same'].flatMap(n => [n + '.key', n + '.png']))

	const hit = run(dir, { cache: true })
	assert.equal(hit.status, 0, hit.stderr)
	assert.match(hit.stdout, /4 read from .*, 0 drawn, 0 files deleted/)
	assert.equal(readScores(dir), coldScores)
	assert.deepEqual(readOutcomes(dir), coldOutcomes)
})

test('the cache drops removed cases and redraws added and changed ones', () => {
	const dir = fixture()
	assert.equal(run(dir, { cache: true }).status, 0)

	for (const ext of ['.mmd', '.facts.txt']) fs.rmSync(path.join(dir, 'corpus', 'pie', 'differ' + ext))
	fs.writeFileSync(path.join(dir, 'corpus', 'pie', 'new.mmd'), 'pie\n')
	fs.writeFileSync(path.join(dir, 'corpus', 'pie', 'new.facts.txt'), facts(true))
	fs.writeFileSync(path.join(dir, 'oracle', 'pie', 'new.svg'), boxes)
	fs.writeFileSync(path.join(dir, 'stm', 'pie', 'new.svg'), boxes)
	fs.writeFileSync(path.join(dir, 'oracle', 'pie', 'same.svg'), text)

	const result = run(dir, { cache: true })
	assert.equal(result.status, 0, result.stderr)
	assert.match(result.stdout, /2 read from .*, 2 drawn, 2 files deleted/)
	assert.deepEqual(cacheFiles(dir), ['mindmap/differ', 'pie/new', 'pie/no-stm', 'pie/same'].flatMap(n => [n + '.key', n + '.png']))

	const scores = Object.fromEntries(readScores(dir).trim().split('\n').slice(1).map(l => [l.split('\t')[0], l.split('\t')[2]]))
	assert.equal(scores['pie/new'], '1.0000')
	assert.ok(Number(scores['pie/same']) < 0.9, scores['pie/same'])
})

test('fails when mermaid.js accepts a case and its SVG is missing, even with its raster in the cache', () => {
	const dir = fixture()
	assert.equal(run(dir, { cache: true }).status, 0)
	fs.rmSync(path.join(dir, 'oracle', 'pie', 'same.svg'))
	const result = run(dir, { cache: true })
	assert.equal(result.status, 1)
	assert.match(result.stderr, /pie\/same: mermaid.js accepts it/)
})

test('fails when mermaid.js accepts a case and its SVG is missing', () => {
	const dir = fixture({ dropOracleSvg: 'pie/same' })
	const result = run(dir)
	assert.equal(result.status, 1)
	assert.match(result.stderr, /pie\/same: mermaid.js accepts it/)
})

test('with named cases, draws only those side by side, passing ones too, and keeps the rest of the cache', () => {
	const dir = fixture()
	assert.equal(run(dir, { cache: true }).status, 0)

	const result = run(dir, { cache: true, cases: 'pie/same, pie/differ' })
	assert.equal(result.status, 0, result.stderr)
	assert.match(result.stdout, /2 read from .*, 0 drawn, 0 files deleted/)
	// Only the named cases' type is written, and the other type's file stays.
	assert.deepEqual(Object.keys(readOutcomes(dir)), ['mindmap.txt', 'pie.txt'])
	assert.equal(
		readOutcomes(dir)['pie.txt'],
		['# L4 outcomes of pie. Refresh with: mise run raster', 'pie/differ\tfail', 'pie/same\tpass', ''].join('\n'),
	)
	const images = fs.readdirSync(path.join(dir, 'images'), { recursive: true }).filter(f => f.endsWith('.png')).sort()
	assert.deepEqual(images, ['pie/differ.png', 'pie/same.png'].map(f => f.split('/').join(path.sep)))
	assert.deepEqual(cacheFiles(dir), ['mindmap/differ', 'pie/differ', 'pie/no-stm', 'pie/same'].flatMap(n => [n + '.key', n + '.png']))
})

test('with STM_RASTER_DIR, keeps STM\'s raster of every case STM draws and scores as a run without it', () => {
	const dir = fixture()
	assert.equal(run(dir).status, 0)
	const scores = readScores(dir)
	fs.mkdirSync(path.join(dir, 'stm-raster', 'stale'), { recursive: true })
	fs.writeFileSync(path.join(dir, 'stm-raster', 'stale', 'case.png'), '')

	const result = run(dir, { stmRasters: true })
	assert.equal(result.status, 0, result.stderr)
	assert.equal(readScores(dir), scores)
	const kept = fs.readdirSync(path.join(dir, 'stm-raster'), { recursive: true }).filter(f => f.endsWith('.png')).map(f => f.split(path.sep).join('/')).sort()
	assert.deepEqual(kept, ['mindmap/differ.png', 'pie/differ.png', 'pie/same.png', 'pie/stm-only.png'])
	for (const image of kept) assert.ok(isPng(path.join(dir, 'stm-raster', image)), image)
})

test('fails on a named case that is not in the corpus', () => {
	const dir = fixture()
	const result = run(dir, { cases: 'pie/same,pie/missing' })
	assert.equal(result.status, 1)
	assert.match(result.stderr, /not corpus cases: pie\/missing/)
})
