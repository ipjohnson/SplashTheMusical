import { withBase } from 'vitepress'

export const repository = 'https://github.com/ipjohnson/SplashTheMusical'

const cache = new Map()

// Reads a file of the comparison data. A missing file reads as undefined.
export function load(file) {
	if (!cache.has(file)) {
		cache.set(file, fetch(withBase('/data/' + file)).then(r => (r.ok ? r.json() : undefined)).catch(() => undefined))
	}
	return cache.get(file)
}

export const imageUrl = hash => hash && withBase(`/data/images/${hash.slice(0, 2)}/${hash}.png`)
export const sourceUrl = hash => hash && withBase(`/data/sources/${hash.slice(0, 2)}/${hash}.txt`)
export const caseUrl = name => withBase('/compare/case?name=' + encodeURIComponent(name))
export const typeUrl = type => withBase('/compare/' + type)
export const absolute = url => new URL(url, window.location.href).href

export const formatScore = score => (score === null || score === undefined ? 'no score' : score.toFixed(4))
export const percent = (part, whole) => (whole ? ((100 * part) / whole).toFixed(1) + '%' : '-')

// The last entry of each case that is still in the corpus.
export const latest = history =>
	Object.entries(history?.cases ?? {})
		.map(([name, entries]) => ({ name, ...entries.at(-1) }))
		.filter(c => !c.removed)

// The GitHub link that opens the comparison issue form with the case filled in.
export function issueUrl({ name, entry, snapshot, threshold, source }) {
	const params = new URLSearchParams()
	params.set('template', 'comparison.yml')
	params.set('title', `[${name}] `)
	params.set('case', name)
	params.set('snapshot', snapshot ? `${snapshot.id} (${repository}/commit/${snapshot.commit}), mermaid.js ${snapshot.mermaid}` : entry.snapshot)
	params.set('score', `${formatScore(entry.score)} against a threshold of ${threshold}, outcome ${entry.outcome}`)
	const images = [
		entry.oracle ? `mermaid.js:\n![mermaid.js](${absolute(imageUrl(entry.oracle))})` : 'mermaid.js: no drawing',
		entry.stm ? `STM:\n![STM](${absolute(imageUrl(entry.stm))})` : 'STM: no drawing',
		`Comparison page: ${absolute(caseUrl(name))}`,
	]
	params.set('images', images.join('\n\n'))
	const limit = 3000
	const text = source ?? ''
	params.set('source', text.length > limit
		? text.slice(0, limit) + `\n%% The source is cut here. The full source is at ${absolute(sourceUrl(entry.source))}`
		: text)
	return `${repository}/issues/new?${params}`
}
