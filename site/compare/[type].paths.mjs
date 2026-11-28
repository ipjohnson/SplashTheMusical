import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

// One page per diagram type of corpus/l4, which holds one outcome file per type.
const outcomes = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', 'corpus', 'l4')

export default {
	watch: [path.join(outcomes, '*.txt')],
	paths() {
		const types = fs.readdirSync(outcomes).filter(f => f.endsWith('.txt')).map(f => f.slice(0, -'.txt'.length))
		return types.sort().map(type => ({ params: { type } }))
	},
}
