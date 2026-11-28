import { test } from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { snapshot } from './snapshot.mjs'

// A corpus of two pie cases with the rasters and scores of one raster.mjs run.
function fixture() {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'stm-snapshot-'))
	const write = (file, text) => {
		fs.mkdirSync(path.dirname(path.join(dir, file)), { recursive: true })
		fs.writeFileSync(path.join(dir, file), text)
	}
	write('corpus/pie/a.mmd', 'pie\n"A": 1\n')
	write('corpus/pie/b.mmd', 'pie\n"B": 1\n')
	write('corpus/tolerances/pie.json', '{ "raster": 0.8 }')
	write('oracle/pie/a.png', 'oracle a')
	write('oracle/pie/b.png', 'oracle b')
	write('stm/pie/a.png', 'stm a')
	write('scores.txt', '# L4 scores\npie/a\tpass\t0.9500\npie/b\tfail\n')
	return { dir, write }
}

const take = (dir, commit, date = '2026-11-27T10:00:00Z') => snapshot({
	corpusDir: path.join(dir, 'corpus'),
	oracleRasterDir: path.join(dir, 'oracle'),
	stmRasterDir: path.join(dir, 'stm'),
	scoresFile: path.join(dir, 'scores.txt'),
	dataDir: path.join(dir, 'data'),
	commit,
	date,
	mermaid: '12.3.0',
})

const json = (dir, file) => JSON.parse(fs.readFileSync(path.join(dir, 'data', file), 'utf8'))
const files = (dir, folder) => fs.readdirSync(path.join(dir, 'data', folder), { recursive: true }).filter(f => f.includes('.')).length

test('stores each image and source once and records each case and the type totals', () => {
	const { dir } = fixture()
	assert.deepEqual(take(dir, 'aaaaaaaaaa'), { added: true, id: '2026-11-27-aaaaaaa' })

	const history = json(dir, 'history/pie.json')
	assert.equal(history.threshold, 0.8)
	assert.equal(history.cases['pie/a'].length, 1)
	assert.equal(history.cases['pie/a'][0].score, 0.95)
	assert.equal(history.cases['pie/a'][0].outcome, 'pass')
	assert.equal(history.cases['pie/b'][0].stm, null)
	assert.equal(history.cases['pie/b'][0].score, null)
	const a = history.cases['pie/a'][0]
	assert.equal(fs.readFileSync(path.join(dir, 'data', 'images', a.stm.slice(0, 2), a.stm + '.png'), 'utf8'), 'stm a')
	assert.equal(fs.readFileSync(path.join(dir, 'data', 'sources', a.source.slice(0, 2), a.source + '.txt'), 'utf8'), 'pie\n"A": 1\n')

	const [only] = json(dir, 'snapshots.json').snapshots
	assert.equal(only.commit, 'aaaaaaaaaa')
	assert.equal(only.mermaid, '12.3.0')
	assert.deepEqual(only.types.pie, { pass: 1, fail: 1, report: 0, 'n/a': 0, cases: 2, scored: 1, mean: 0.95 })
})

test('a second snapshot adds entries and files only for what changed', () => {
	const { dir, write } = fixture()
	take(dir, 'aaaaaaaaaa')
	const images = files(dir, 'images')
	write('stm/pie/b.png', 'stm b')
	write('scores.txt', '# L4 scores\npie/a\tpass\t0.9500\npie/b\tfail\t0.5000\n')
	take(dir, 'bbbbbbbbbb', '2026-11-28T10:00:00Z')

	const history = json(dir, 'history/pie.json')
	assert.equal(history.cases['pie/a'].length, 1)
	assert.equal(history.cases['pie/b'].length, 2)
	assert.equal(history.cases['pie/b'][1].snapshot, '2026-11-28-bbbbbbb')
	assert.equal(history.cases['pie/b'][1].score, 0.5)
	assert.equal(files(dir, 'images'), images + 1)
	assert.equal(json(dir, 'snapshots.json').snapshots.length, 2)
})

test('a case that leaves the corpus is marked removed once', () => {
	const { dir, write } = fixture()
	take(dir, 'aaaaaaaaaa')
	write('scores.txt', '# L4 scores\npie/a\tpass\t0.9500\n')
	take(dir, 'bbbbbbbbbb')
	take(dir, 'cccccccccc')
	const entries = json(dir, 'history/pie.json').cases['pie/b']
	assert.equal(entries.length, 2)
	assert.equal(entries[1].removed, true)
})

test('a snapshot of a commit the data has changes no file', () => {
	const { dir } = fixture()
	take(dir, 'aaaaaaaaaa')
	const before = fs.readFileSync(path.join(dir, 'data', 'snapshots.json'), 'utf8')
	assert.deepEqual(take(dir, 'aaaaaaaaaa'), { added: false })
	assert.equal(fs.readFileSync(path.join(dir, 'data', 'snapshots.json'), 'utf8'), before)
})
