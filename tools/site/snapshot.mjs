// Adds one snapshot of the L4 rasters to the site's comparison data. site/README.md describes the data folder.
//
//   node snapshot.mjs <corpus-dir> <oracle-raster-dir> <stm-raster-dir> <scores-file> <data-dir> [commit] [date] [mermaid-version]
//
// The commit defaults to HEAD, the date to now and the mermaid.js version to the one tools/oracle/package-lock.json pins.
//
// The rasters and scores are what tools/oracle/raster.mjs writes with an oracle-raster-dir and STM_RASTER_DIR. Images
// and sources are stored once under their SHA-256, so a snapshot adds only what changed. A case's history gets an entry
// only when its images, source, score or outcome differ from its last one. A snapshot of a commit the data already has,
// or of a commit dated before the newest snapshot, changes nothing.
import fs from 'node:fs'
import path from 'node:path'
import crypto from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { fileURLToPath, pathToFileURL } from 'node:url'

const sha256 = bytes => crypto.createHash('sha256').update(bytes).digest('hex')

const readJson = (file, fallback) => fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : fallback

// Writes JSON with sorted keys, so an unchanged file is byte for byte the same.
const writeJson = (file, value) => {
	const sorted = v => Array.isArray(v)
		? v.map(sorted)
		: v && typeof v === 'object'
			? Object.fromEntries(Object.keys(v).sort().map(k => [k, sorted(v[k])]))
			: v
	fs.mkdirSync(path.dirname(file), { recursive: true })
	fs.writeFileSync(file, JSON.stringify(sorted(value)) + '\n')
}

// Stores bytes under <data-dir>/<folder>/<first two hex digits>/<hash><extension> and returns the hash.
function store(dataDir, folder, extension, bytes) {
	const hash = sha256(bytes)
	const file = path.join(dataDir, folder, hash.slice(0, 2), hash + extension)
	if (!fs.existsSync(file)) {
		fs.mkdirSync(path.dirname(file), { recursive: true })
		fs.writeFileSync(file, bytes)
	}
	return hash
}

export function readScores(file) {
	return fs.readFileSync(file, 'utf8').split('\n')
		.filter(line => line && !line.startsWith('#'))
		.map(line => {
			const [name, outcome, score] = line.split('\t')
			return { name, outcome, score: score ? Number(score) : null }
		})
}

const threshold = (corpusDir, type) => {
	const json = readJson(path.join(corpusDir, 'tolerances', type + '.json'), {})
	return { threshold: json.raster ?? 0.9, reportOnly: json.rasterReportOnly === true }
}

const sameEntry = (a, b) => ['oracle', 'stm', 'source', 'score', 'outcome', 'removed'].every(k => (a[k] ?? null) === (b[k] ?? null))

export function snapshot({ corpusDir, oracleRasterDir, stmRasterDir, scoresFile, dataDir, commit, date, mermaid }) {
	const index = readJson(path.join(dataDir, 'snapshots.json'), { snapshots: [] })
	if (index.snapshots.some(s => s.commit === commit)) return { added: false }
	// CI runs on main can finish out of order. A commit older than the newest snapshot is left out, so a case's history
	// never steps back to an earlier commit.
	const newest = index.snapshots.at(-1)
	if (newest && Date.parse(date) < Date.parse(newest.date)) return { added: false, older: newest.id }
	const id = `${date.slice(0, 10)}-${commit.slice(0, 7)}`
	fs.mkdirSync(path.join(dataDir, 'history'), { recursive: true })

	const byType = new Map()
	for (const row of readScores(scoresFile)) {
		const type = row.name.split('/')[0]
		if (!byType.has(type)) byType.set(type, [])
		byType.get(type).push(row)
	}

	const types = {}
	for (const type of [...new Set([...byType.keys(), ...fs.readdirSync(path.join(dataDir, 'history')).map(f => f.replace(/\.json$/, ''))].filter(Boolean))].sort()) {
		const rows = byType.get(type) ?? []
		const historyFile = path.join(dataDir, 'history', type + '.json')
		const history = readJson(historyFile, { cases: {} })
		const { threshold: limit, reportOnly } = threshold(corpusDir, type)
		history.threshold = limit
		history.reportOnly = reportOnly
		const counts = { pass: 0, fail: 0, report: 0, 'n/a': 0 }
		let total = 0
		let scored = 0
		const seen = new Set()
		for (const row of rows) {
			seen.add(row.name)
			counts[row.outcome] = (counts[row.outcome] ?? 0) + 1
			if (row.score !== null) {
				total += row.score
				scored++
			}
			const image = dir => {
				const file = path.join(dir, row.name + '.png')
				return fs.existsSync(file) ? store(dataDir, 'images', '.png', fs.readFileSync(file)) : null
			}
			const sourceFile = path.join(corpusDir, row.name + '.mmd')
			const entry = {
				snapshot: id,
				outcome: row.outcome,
				score: row.score,
				oracle: image(oracleRasterDir),
				stm: image(stmRasterDir),
				source: fs.existsSync(sourceFile) ? store(dataDir, 'sources', '.txt', fs.readFileSync(sourceFile)) : null,
			}
			const entries = history.cases[row.name] ?? (history.cases[row.name] = [])
			if (!entries.length || !sameEntry(entries.at(-1), entry)) entries.push(entry)
		}
		for (const [name, entries] of Object.entries(history.cases)) {
			if (!seen.has(name) && !entries.at(-1).removed) entries.push({ snapshot: id, removed: true })
		}
		writeJson(historyFile, history)
		if (rows.length) {
			types[type] = { ...counts, cases: rows.length, scored, mean: scored ? Number((total / scored).toFixed(4)) : null }
		}
	}

	index.snapshots.push({ id, commit, date, mermaid, types })
	writeJson(path.join(dataDir, 'snapshots.json'), index)
	return { added: true, id }
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
	const [, , corpusDir, oracleRasterDir, stmRasterDir, scoresFile, dataDir, ...rest] = process.argv
	if (!dataDir) {
		console.error('usage: node snapshot.mjs <corpus-dir> <oracle-raster-dir> <stm-raster-dir> <scores-file> <data-dir> [commit] [date] [mermaid-version]')
		process.exit(2)
	}
	const lock = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'oracle', 'package-lock.json')
	const commit = rest[0] || execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8' }).trim()
	const date = rest[1] || new Date().toISOString()
	const mermaid = rest[2] || JSON.parse(fs.readFileSync(lock, 'utf8')).packages['node_modules/mermaid'].version
	const result = snapshot({ corpusDir, oracleRasterDir, stmRasterDir, scoresFile, dataDir, commit, date, mermaid })
	console.log(result.added
		? `added snapshot ${result.id}`
		: result.older
			? `${commit} is older than the newest snapshot, ${result.older}, so it is left out`
			: `the data already has a snapshot of ${commit}`)
}
