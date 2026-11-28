// Builds the site's parity page from docs/parity: the README, then the row of each type's report and a totals row.
// `mise run parity` writes one report per type and no totals, so branches that change different types do not change
// the same file. The totals are added up here, where they are read.
import fs from 'node:fs'
import path from 'node:path'

const header = '| Type | Cases | Refused by mermaid.js | L0 | L1 | L2 | L3 | L4 | bug | by-design | out-of-scope |'
const separator = '|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|'
const levels = ['L0', 'L1', 'L2', 'L3', 'L4']

const number = text => {
	const value = Number(text.replaceAll(',', ''))
	if (!Number.isInteger(value)) throw new Error(`${text} is not a count`)
	return value
}

const count = value => value.toLocaleString('en-US')

// A level's cell: "64.8% (3,072 of 4,744)", "n/a" when no case applies, or "report-only" for L4.
function readRate(cell) {
	if (cell === 'n/a') return { passed: 0, applies: 0 }
	if (cell === 'report-only') return { passed: 0, applies: 0, reportOnly: true }
	const match = /^\d+\.\d% \(([\d,]+) of ([\d,]+)\)$/.exec(cell)
	if (!match) throw new Error(`${cell} is not a rate`)
	return { passed: number(match[1]), applies: number(match[2]) }
}

const rate = ({ passed, applies, reportOnly }) =>
	applies === 0 ? (reportOnly ? 'report-only' : 'n/a') : `${(100 * passed / applies).toFixed(1)}% (${count(passed)} of ${count(applies)})`

/** The row of one type's report, and the mermaid.js version it was measured against. */
export function readReport(text, name) {
	const lines = text.split('\n')
	const at = lines.indexOf(header)
	if (at < 0 || lines[at + 1] !== separator) throw new Error(`${name} has no parity table`)
	const rows = lines.slice(at + 2).filter(l => l.startsWith('|'))
	if (rows.length !== 1) throw new Error(`${name} has ${rows.length} rows, not one`)
	const cells = rows[0].slice(1, -1).split('|').map(c => c.trim())
	if (cells.length !== 11) throw new Error(`${name}: ${rows[0]} does not have 11 cells`)
	const mermaid = /against mermaid\.js (\S+) on/.exec(text)?.[1]
	if (!mermaid) throw new Error(`${name} names no mermaid.js version`)
	return {
		type: cells[0],
		mermaid,
		cases: number(cells[1]),
		refused: number(cells[2]),
		levels: cells.slice(3, 8).map(readRate),
		ledger: cells.slice(8).map(number),
	}
}

/** The table of every report, sorted by type, and its totals row. */
export function parityTable(reports) {
	const sorted = [...reports].sort((a, b) => (a.type < b.type ? -1 : a.type > b.type ? 1 : 0))
	const total = {
		type: '**Total**',
		cases: sorted.reduce((sum, r) => sum + r.cases, 0),
		refused: sorted.reduce((sum, r) => sum + r.refused, 0),
		levels: levels.map((_, i) => ({
			passed: sorted.reduce((sum, r) => sum + r.levels[i].passed, 0),
			applies: sorted.reduce((sum, r) => sum + r.levels[i].applies, 0),
			reportOnly: sorted.some(r => r.levels[i].reportOnly),
		})),
		ledger: [0, 1, 2].map(i => sorted.reduce((sum, r) => sum + r.ledger[i], 0)),
	}
	const row = r => `| ${[r.type, count(r.cases), count(r.refused), ...r.levels.map(rate), ...r.ledger.map(count)].join(' | ')} |`
	return [header, separator, ...sorted.map(row), row(total)].join('\n') + '\n'
}

/**
 * The parity page: the README of the folder, a line with the mermaid.js version and the case count, and the table.
 * Each relative link of the README is pointed at `<linkBase>/docs/parity/<target>`.
 */
export function parityPage(folder, linkBase) {
	const files = fs.readdirSync(folder).filter(f => f.endsWith('.md') && f !== 'README.md').sort()
	if (files.length === 0) throw new Error(`${folder} has no reports`)
	const reports = files.map(f => readReport(fs.readFileSync(path.join(folder, f), 'utf8'), f))
	const versions = [...new Set(reports.map(r => r.mermaid))]
	if (versions.length !== 1) throw new Error(`the reports name more than one mermaid.js version: ${versions.join(', ')}`)
	const readme = fs.readFileSync(path.join(folder, 'README.md'), 'utf8').replace(/\]\((?!https?:|#)([^)]+)\)/g, (_, target) =>
		`](${linkBase}/${path.posix.normalize(path.posix.join('docs', 'parity', target))})`)
	const cases = reports.reduce((sum, r) => sum + r.cases, 0)
	return `${readme.trimEnd()}\n\nSTM against mermaid.js ${versions[0]} on the ${count(cases)} cases of \`corpus/\`.\n\n${parityTable(reports)}`
}
