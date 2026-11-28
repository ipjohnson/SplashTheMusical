import { test } from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { parityPage, parityTable, readReport } from './parity.mjs'

const header = '| Type | Cases | Refused by mermaid.js | L0 | L1 | L2 | L3 | L4 | bug | by-design | out-of-scope |'
const separator = '|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|'
const report = (row, mermaid = '12.3.0') =>
	`# Parity of a type\n\nSTM, as \`stm\`, against mermaid.js ${mermaid} on the cases of \`corpus/\`.\n\n${header}\n${separator}\n${row}\n`

const pie = '| pie | 4 | 2 | 50.0% (2 of 4) | 33.3% (1 of 3) | 33.3% (1 of 3) | 33.3% (1 of 3) | 33.3% (1 of 3) | 1 | 1 | 1 |'
const xychart = '| xychart | 1 | 1 | 100.0% (1 of 1) | n/a | n/a | n/a | n/a | 0 | 0 | 0 |'
const mindmap = '| mindmap | 1,000 | 0 | 100.0% (1,000 of 1,000) | 100.0% (1,000 of 1,000) | 100.0% (1,000 of 1,000) | 100.0% (1,000 of 1,000) | report-only | 2 | 0 | 0 |'

test('the totals row adds up the counts of each type and rates them', () => {
	const rows = parityTable([pie, xychart, mindmap].map((row, i) => readReport(report(row), `${i}.md`))).trimEnd().split('\n')

	assert.deepEqual(rows, [
		header,
		separator,
		mindmap,
		pie,
		xychart,
		'| **Total** | 1,005 | 3 | 99.8% (1,003 of 1,005) | 99.8% (1,001 of 1,003) | 99.8% (1,001 of 1,003) | 99.8% (1,001 of 1,003) | 33.3% (1 of 3) | 3 | 1 | 1 |',
	])
})

test('the totals of types that are all report-only at L4 are report-only', () => {
	const rows = parityTable([readReport(report(mindmap), 'mindmap.md')]).trimEnd().split('\n')
	assert.match(rows.at(-1), /^\| \*\*Total\*\* \| 1,000 \| .* \| report-only \| 2 \| 0 \| 0 \|$/)
})

test('a report without exactly one row of eleven cells is refused', () => {
	assert.throws(() => readReport(report(`${pie}\n${xychart}`), 'two.md'), /two.md has 2 rows, not one/)
	assert.throws(() => readReport(report('| pie | 4 |'), 'short.md'), /does not have 11 cells/)
	assert.throws(() => readReport('# Parity\n', 'none.md'), /none.md has no parity table/)
	assert.throws(() => readReport(report(pie.replace('50.0% (2 of 4)', 'half')), 'rate.md'), /half is not a rate/)
})

test('the page is the README with its links on GitHub, the version and case count, and the table', () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'stm-parity-'))
	fs.writeFileSync(path.join(dir, 'README.md'), '# Parity report\n\nSee [the plan](../mermaid-parity-plan.md) and [a type](#pie).\n')
	fs.writeFileSync(path.join(dir, 'pie.md'), report(pie))
	fs.writeFileSync(path.join(dir, 'xychart.md'), report(xychart))

	const page = parityPage(dir, 'https://example.com/blob/main')

	assert.ok(page.startsWith('# Parity report\n\nSee [the plan](https://example.com/blob/main/docs/mermaid-parity-plan.md) and [a type](#pie).\n\n'), page)
	assert.match(page, /\nSTM against mermaid\.js 12\.3\.0 on the 5 cases of `corpus\/`\.\n/)
	assert.match(page, /\n\| \*\*Total\*\* \| 5 \| 3 \| 60\.0% \(3 of 5\) \|/)
})

test('reports of different mermaid.js versions are refused', () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'stm-parity-'))
	fs.writeFileSync(path.join(dir, 'README.md'), '# Parity report\n')
	fs.writeFileSync(path.join(dir, 'pie.md'), report(pie))
	fs.writeFileSync(path.join(dir, 'xychart.md'), report(xychart, '12.4.0'))

	assert.throws(() => parityPage(dir, 'https://example.com'), /more than one mermaid\.js version: 12\.3\.0, 12\.4\.0/)
})
