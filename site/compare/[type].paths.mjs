import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

// One page per diagram type of corpus/l4.txt.
const outcomes = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', 'corpus', 'l4.txt')

export default {
	watch: [outcomes],
	paths() {
		const types = new Set(
			fs.readFileSync(outcomes, 'utf8').split('\n').filter(l => l && !l.startsWith('#')).map(l => l.split('/')[0]),
		)
		return [...types].sort().map(type => ({ params: { type } }))
	},
}
