// Writes the pages the site takes from the repository: parity.md from docs/parity-report.md, with its relative links
// pointed at the files on GitHub.
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

export const repository = 'https://github.com/ipjohnson/SplashTheMusical'
const site = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const root = path.dirname(site)

const report = fs.readFileSync(path.join(root, 'docs', 'parity-report.md'), 'utf8')
const linked = report.replace(/\]\((?!https?:|#)([^)]+)\)/g, (_, target) =>
	`](${repository}/blob/main/${path.posix.normalize(path.posix.join('docs', target))})`)
fs.writeFileSync(
	path.join(site, 'parity.md'),
	'<!-- Written by site/scripts/prepare.mjs from docs/parity-report.md. Do not edit. -->\n\n' + linked,
)
