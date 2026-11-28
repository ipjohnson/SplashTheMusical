// Writes the pages the site takes from the repository: parity.md from the per-type reports in docs/parity, with their
// totals and the README's relative links pointed at the files on GitHub.
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { parityPage } from '../../tools/site/parity.mjs'

export const repository = 'https://github.com/ipjohnson/SplashTheMusical'
const site = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const root = path.dirname(site)

fs.writeFileSync(
	path.join(site, 'parity.md'),
	'<!-- Written by site/scripts/prepare.mjs from docs/parity. Do not edit. -->\n\n' +
		parityPage(path.join(root, 'docs', 'parity'), `${repository}/blob/main`),
)
