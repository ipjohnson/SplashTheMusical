// Writes the open comparison issues per case as JSON, from the JSON of
// gh issue list --label comparison --json number,title on standard input.
//
//   gh issue list ... | node issues.mjs <out-file>
import fs from 'node:fs'
import { pathToFileURL } from 'node:url'

export function issuesByCase(issues) {
	const byCase = {}
	for (const { number, title } of issues) {
		const name = /^\[([^\]]+)\]/.exec(title ?? '')?.[1]
		if (name) (byCase[name] ??= []).push(number)
	}
	for (const numbers of Object.values(byCase)) numbers.sort((a, b) => a - b)
	return byCase
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
	const issues = JSON.parse(fs.readFileSync(0, 'utf8'))
	fs.writeFileSync(process.argv[2], JSON.stringify(issuesByCase(issues)) + '\n')
}
