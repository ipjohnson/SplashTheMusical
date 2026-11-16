// Where the corpus sources are in a checkout of mermaid 12, mermaid-js/mermaid, or of mermaid 13, mermaid-js/mermaid-v13.
import fs from 'node:fs'
import path from 'node:path'

const v12Docs = ['packages', 'mermaid', 'src', 'docs', 'syntax']
const v13Docs = ['v13', 'docs', 'syntax']

/** The folder of the syntax docs: packages/mermaid/src/docs/syntax in 12, v13/docs/syntax in 13. */
export function docs(checkout) {
	for (const parts of [v12Docs, v13Docs]) {
		const dir = path.join(checkout, ...parts)
		if (fs.existsSync(dir)) return dir
	}
	throw new Error(`${checkout} has neither ${v12Docs.join('/')} nor ${v13Docs.join('/')}`)
}

/**
 * The folders whose *.spec.{js,ts} files hold parser sources: packages/mermaid/src/diagrams in 12, and the src folder of
 * each packages/diagram-* in 13, in sorted order.
 */
export function specs(checkout) {
	const v12 = path.join(checkout, 'packages', 'mermaid', 'src', 'diagrams')
	if (fs.existsSync(v12)) return [v12]
	const packages = path.join(checkout, 'packages')
	return fs.readdirSync(packages, { withFileTypes: true })
		.filter(e => e.isDirectory() && e.name.startsWith('diagram-'))
		.map(e => path.join(packages, e.name, 'src'))
		.filter(dir => fs.existsSync(dir))
		.sort()
}

/** owner/repo of a git remote URL, such as mermaid-js/mermaid-v13 of git@github.com:mermaid-js/mermaid-v13.git. */
export function repositoryName(url) {
	const parts = url.trim().replace(/\.git$/, '').replace(/\/+$/, '').split(/[/:]/)
	return parts.slice(-2).join('/')
}
