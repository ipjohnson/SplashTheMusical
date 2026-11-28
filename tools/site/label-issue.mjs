// Labels an issue filed with the comparison form: adds comparison and comparison:<type>, creating either when the
// repository lacks it, and starts the title with [<case>]. .github/workflows/comparison-issue.yml runs it.
//
//   node label-issue.mjs <corpus-dir> <event-file>
//
// The case comes from the issue body, which anyone can write, so it is used only when it names a corpus case.
import fs from 'node:fs'
import path from 'node:path'
import { execFileSync } from 'node:child_process'
import { pathToFileURL } from 'node:url'

// The value of the form's "Comparison case" field, or undefined.
export function caseOf(body) {
	const match = /^### Comparison case\s*\n+([^\n]*)/m.exec(body ?? '')
	const value = match?.[1].trim()
	return value && /^[A-Za-z0-9-]+\/[A-Za-z0-9._-]+$/.test(value) ? value : undefined
}

export function titleFor(name, title) {
	const prefix = `[${name}]`
	const rest = (title ?? '').trim()
	return rest.startsWith(prefix) ? rest : `${prefix} ${rest}`.trim()
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
	const [, , corpusDir, eventFile] = process.argv
	const { issue } = JSON.parse(fs.readFileSync(eventFile, 'utf8'))
	const name = caseOf(issue.body)
	if (!name || !fs.existsSync(path.join(corpusDir, name + '.mmd'))) {
		console.log(`issue ${issue.number} names no corpus case`)
		process.exit(0)
	}
	const type = name.split('/')[0]
	const gh = args => execFileSync('gh', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] })
	const existing = new Set(JSON.parse(gh(['label', 'list', '--limit', '500', '--json', 'name'])).map(l => l.name))
	for (const [label, description] of [['comparison', 'STM draws a corpus case differently from mermaid.js'], [`comparison:${type}`, `A comparison difference of a ${type} case`]]) {
		if (!existing.has(label)) gh(['label', 'create', label, '--description', description, '--color', 'c5def5'])
	}
	gh(['issue', 'edit', String(issue.number), '--add-label', `comparison,comparison:${type}`, '--title', titleFor(name, issue.title)])
	console.log(`labelled issue ${issue.number} as ${name}`)
}
