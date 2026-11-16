// Writes the upstream cases of corpus/ from a checkout of mermaid-js/mermaid, or of mermaid-js/mermaid-v13 for mermaid 13,
// then corpus/SOURCE.txt and the counts in corpus/README.md. It is a port of ipjohnson/mermaider's
// scripts/mermaid-docs-corpus.py and scripts/mermaid-test-corpus.py.
//
//   node tools/corpus/corpus.mjs <mermaid checkout> [corpus-dir]
//   node tools/corpus/corpus.mjs --add <folder> <source> [corpus-dir]
//   node tools/corpus/corpus.mjs --counts [corpus-dir]
//
// The checkout must be at the tag mermaid@<version>, where <version> is the mermaid that tools/oracle pins.
//
// A case is written to <corpus>/<type>/<source>-<name>.mmd. <type> is the folder of the diagram type under mermaid 12's
// packages/mermaid/src/diagrams. layout.mjs finds the docs and spec folders of each layout. The sources are:
//
// - docs: every ```mermaid, ```mermaid-example and ```mermaid-nocode fence of the syntax docs, 12's
//   packages/mermaid/src/docs/syntax/*.md or 13's v13/docs/syntax/*.md, except all-examples.md, as docs-<doc>-<n>. <n>
//   counts the fences of that doc from 1.
// - e2e: every e2e/diagrams/**/*.mmd fixture, as e2e-<path> with each run of other characters than letters and digits
//   in the path written as one dash.
// - parser: every string literal, or chain of literals joined with +, of the **/*.spec.{js,ts} files of 12's
//   packages/mermaid/src/diagrams or 13's packages/diagram-*/src,
//   as parser-<spec>-<n>, and of 12's packages/parser/tests/*.test.{js,ts}, as parser-langium-<file>-<n>. <n> counts the kept
//   sources of the spec files with that name from 1. A parser source is dedented and trimmed. A template literal with
//   ${...} is left out, because its text is only known when the test runs.
//
// Only sources whose first line, after front matter and %% comments, starts with a diagram header are kept. An e2e or
// parser source that is already in the corpus is left out. Sources are compared dedented, with trailing spaces and blank
// edge lines removed. Every other .mmd in the corpus, such as the mermaider-*.mmd cases, is left alone.
//
// --add copies the hand-written cases of a folder in as <type>/<source>-<name>.mmd, and leaves out the ones that are
// already in the corpus or have no diagram header. --counts only rewrites the counts in README.md.
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import * as layout from './layout.mjs'

const here = path.dirname(fileURLToPath(import.meta.url))
const repo = path.join(here, '..', '..')
const upstreamSources = ['docs', 'e2e', 'parser']

// The keyword each detector of mermaid 12.3.0 looks for, and the folder of its type. A keyword must be followed by the
// end of the line or a character that is not a letter, digit or underscore, so `stateDiagram` covers `stateDiagram-v2`.
const headers = [
	['graph', 'flowchart'],
	['flowchart', 'flowchart'],
	['swimlane-beta', 'swimlanes'],
	['stateDiagram', 'state'],
	['classDiagram', 'class'],
	['sequenceDiagram', 'sequence'],
	['erDiagram', 'er'],
	['pie', 'pie'],
	['quadrantChart', 'quadrant-chart'],
	['timeline', 'timeline'],
	['gitGraph', 'git'],
	['radar-beta', 'radar'],
	['treemap', 'treemap'],
	['venn-beta', 'venn'],
	['mindmap', 'mindmap'],
	['gantt', 'gantt'],
	['journey', 'user-journey'],
	['C4Context', 'c4'],
	['C4Container', 'c4'],
	['C4Component', 'c4'],
	['C4Dynamic', 'c4'],
	['C4Deployment', 'c4'],
	['sankey', 'sankey'],
	['xychart', 'xychart'],
	['requirement', 'requirement'],
	['requirementDiagram', 'requirement'],
	['packet', 'packet'],
	['kanban', 'kanban'],
	['architecture', 'architecture'],
	['block', 'block'],
	['treeView-beta', 'treeView'],
	['usecase-beta', 'usecase'],
	['wardley-beta', 'wardley', 'i'],
	['zenuml', 'zenuml'],
	['railroad-beta', 'railroad', 'i'],
	['railroad-ebnf-beta', 'railroad', 'i'],
	['railroad-abnf-beta', 'railroad', 'i'],
	['railroad-peg-beta', 'railroad', 'i'],
	['ishikawa', 'ishikawa', 'i'],
	['cynefin-beta', 'cynefin'],
	['eventmodeling', 'eventmodeling'],
	['agentflow-beta', 'agentflow'],
	['info', 'info'],
]

const isWordChar = c => c !== undefined && /[\p{L}\p{N}_]/u.test(c)
// Python's str.isspace, which the original scripts used.
const isSpace = c => (/\s/.test(c) && c !== '﻿') || '\x1c\x1d\x1e\x1f\x85'.includes(c)
const strip = s => {
	let a = 0
	let b = s.length
	while (a < b && isSpace(s[a])) a++
	while (b > a && isSpace(s[b - 1])) b--
	return s.slice(a, b)
}
const rstrip = s => {
	let b = s.length
	while (b > 0 && isSpace(s[b - 1])) b--
	return s.slice(0, b)
}
// Reads a text file with Python's universal newlines, as the original scripts did.
const readText = file => fs.readFileSync(file, 'utf8').replace(/\r\n?/g, '\n')

/** The folder of the diagram type a header line starts, or undefined. */
function typeOf(line) {
	for (const [keyword, folder, flags] of headers) {
		const head = line.slice(0, keyword.length)
		const same = flags === 'i' ? head.toLowerCase() === keyword.toLowerCase() : head === keyword
		if (same && !isWordChar(line[keyword.length])) return folder
	}
	return undefined
}

/** The first line that is not front matter, blank or a %% comment. */
function firstLine(body) {
	let inFrontMatter = false
	for (const raw of body.split('\n')) {
		const line = strip(raw)
		if (line === '---') {
			inFrontMatter = !inFrontMatter
			continue
		}
		if (inFrontMatter || !line || line.startsWith('%%')) continue
		return line
	}
	return ''
}

function normalize(body) {
	const lines = body.replace(/\r\n/g, '\n').split('\n').map(rstrip)
	while (lines.length && !lines[0]) lines.shift()
	while (lines.length && !lines[lines.length - 1]) lines.pop()
	const indents = lines.filter(l => l).map(l => l.length - l.replace(/^[ \t]+/, '').length)
	const cut = indents.length ? Math.min(...indents) : 0
	return lines.map(l => l.slice(cut)).join('\n') + '\n'
}

const escapes = { n: '\n', t: '\t', r: '\r', '\\': '\\', "'": "'", '"': '"', '`': '`', 0: '\0', b: '\b', f: '\f', v: '\v', $: '$', '\n': '' }
const regexBefore = new Set([...'(,=:[!&|?{};+-*%<>~^', ''])
const testCalls = new Set(['describe', 'it', 'test', 'bench'])

// Whether the string literal at i is the title of a describe(), it(), test() or bench() call, such as
// describe('mindmap color selection', ...), which is not a diagram. The call may have a chain such as it.each.
function isTestTitle(code, i) {
	let j = i - 1
	while (j >= 0 && isSpace(code[j])) j--
	if (j < 0 || code[j] !== '(') return false
	let start = j
	while (start > 0 && (isWordChar(code[start - 1]) || code[start - 1] === '.')) start--
	const parts = code.slice(start, j).split('.')
	return parts.some((p, k) => testCalls.has(p) && parts.slice(k + 1).every(q => q.length > 0))
}

function readString(code, i) {
	const quote = code[i]
	i++
	let out = ''
	let dynamic = false
	while (i < code.length) {
		const c = code[i]
		if (c === '\\' && i + 1 < code.length) {
			const e = code[i + 1]
			if (e === 'u' && code[i + 2] === '{') {
				const end = code.indexOf('}', i)
				out += String.fromCodePoint(parseInt(code.slice(i + 3, end), 16))
				i = end + 1
			} else if (e === 'u') {
				out += String.fromCharCode(parseInt(code.slice(i + 2, i + 6), 16))
				i += 6
			} else if (e === 'x') {
				out += String.fromCharCode(parseInt(code.slice(i + 2, i + 4), 16))
				i += 4
			} else {
				out += escapes[e] ?? e
				i += 2
			}
			continue
		}
		if (c === quote) return [out, dynamic, i + 1]
		if (quote === '`' && code.startsWith('${', i)) {
			dynamic = true
			i = skipBraces(code, i + 1)
			continue
		}
		if (c === '\n' && quote !== '`') return [out, true, i]
		out += c
		i++
	}
	return [out, true, i]
}

function skipBraces(code, i) {
	let depth = 0
	while (i < code.length) {
		const c = code[i]
		if (c === "'" || c === '"' || c === '`') {
			i = readString(code, i)[2]
			continue
		}
		if (c === '{') depth++
		else if (c === '}') {
			depth--
			if (depth === 0) return i + 1
		}
		i++
	}
	return i
}

function skipRegex(code, i) {
	i++
	let inClass = false
	while (i < code.length && code[i] !== '\n') {
		const c = code[i]
		if (c === '\\') {
			i += 2
			continue
		}
		if (c === '[') inClass = true
		else if (c === ']') inClass = false
		else if (c === '/' && !inClass) {
			i++
			while (i < code.length && /\p{L}/u.test(code[i])) i++
			return i
		}
		i++
	}
	return i
}

/** Each string literal chain of JS or TS code: its decoded text, or null when it has an interpolation. */
function literals(code) {
	const n = code.length
	const out = []
	let i = 0
	let chain = null
	let joined = false
	let prev = ''
	const flush = () => {
		if (chain !== null) out.push(chain.text)
		chain = null
	}
	while (i < n) {
		const c = code[i]
		if (code.startsWith('//', i)) {
			const end = code.indexOf('\n', i)
			i = end < 0 ? n : end
			continue
		}
		if (code.startsWith('/*', i)) {
			const end = code.indexOf('*/', i + 2)
			i = end < 0 ? n : end + 2
			continue
		}
		if (isSpace(c)) {
			i++
			continue
		}
		if (c === "'" || c === '"' || c === '`') {
			const title = !joined && isTestTitle(code, i)
			const [text, dynamic, next] = readString(code, i)
			i = next
			if (title) {
				flush()
				joined = false
				prev = 'str'
				continue
			}
			if (chain !== null && joined) chain = { text: chain.text === null || dynamic ? null : chain.text + text }
			else {
				flush()
				chain = { text: dynamic ? null : text }
			}
			joined = false
			prev = 'str'
			continue
		}
		if (c === '+' && chain !== null && !code.startsWith('++', i)) {
			joined = true
			i++
			continue
		}
		if (c === '/' && regexBefore.has(prev)) {
			i = skipRegex(code, i)
			flush()
			prev = 'regex'
			continue
		}
		flush()
		joined = false
		prev = isWordChar(c) || c === '$' ? 'word' : c
		i++
	}
	flush()
	return out
}

// os.walk's order: a folder's files in sorted order, then its subfolders in sorted order.
function walk(dir, visit) {
	const entries = fs.readdirSync(dir, { withFileTypes: true })
	const dirs = entries.filter(e => e.isDirectory()).map(e => e.name).sort()
	for (const file of entries.filter(e => !e.isDirectory()).map(e => e.name).sort()) visit(dir, file)
	for (const d of dirs) walk(path.join(dir, d), visit)
}

const caseFiles = corpus =>
	fs.readdirSync(corpus, { withFileTypes: true })
		.filter(e => e.isDirectory())
		.flatMap(e => fs.readdirSync(path.join(corpus, e.name)).filter(f => f.endsWith('.mmd')).map(f => [e.name, f]))

function extract(checkout, corpus) {
	const pinned = JSON.parse(fs.readFileSync(path.join(repo, 'tools', 'oracle', 'package.json'), 'utf8')).devDependencies.mermaid
	const git = (...args) => execFileSync('git', ['-C', checkout, ...args], { encoding: 'utf8' }).trim()
	const commit = git('rev-parse', 'HEAD')
	const tags = git('tag', '--points-at', 'HEAD').split('\n').filter(t => t)
	const tag = `mermaid@${pinned}`
	if (!tags.includes(tag)) {
		console.error(`${checkout} is at ${tags.join(', ') || commit}, not ${tag}, the mermaid that tools/oracle pins`)
		process.exit(1)
	}

	for (const [type, file] of caseFiles(corpus))
		if (upstreamSources.some(s => file.startsWith(s + '-'))) fs.rmSync(path.join(corpus, type, file))

	const counts = { docs: 0, e2e: 0, parser: 0, duplicate: 0, 'other type': 0 }
	const write = (type, name, body) => {
		fs.mkdirSync(path.join(corpus, type), { recursive: true })
		fs.writeFileSync(path.join(corpus, type, name + '.mmd'), body)
	}

	const seen = new Set()
	const docs = layout.docs(checkout)
	for (const name of fs.readdirSync(docs).sort()) {
		if (!name.endsWith('.md') || name === 'all-examples.md') continue
		const text = readText(path.join(docs, name))
		let n = 0
		for (const m of text.matchAll(/^```(mermaid(?:-example|-nocode)?)\s*\n([\s\S]*?)^```\s*$/gm)) {
			n++
			const body = m[2]
			if (!strip(body)) continue
			const type = typeOf(firstLine(body))
			if (!type) {
				counts['other type']++
				continue
			}
			seen.add(normalize(body))
			write(type, `docs-${name.slice(0, -3)}-${String(n).padStart(3, '0')}`, body)
			counts.docs++
		}
	}

	const written = new Set()
	const keep = (source, name, body) => {
		const type = typeOf(firstLine(body))
		if (!type) {
			counts['other type']++
			return false
		}
		const key = normalize(body)
		if (seen.has(key)) {
			counts.duplicate++
			return false
		}
		seen.add(key)
		written.add(name)
		write(type, name, body)
		counts[source]++
		return true
	}

	const e2e = path.join(checkout, 'e2e', 'diagrams')
	walk(e2e, (dir, file) => {
		if (!file.endsWith('.mmd')) return
		const rel = path.relative(e2e, path.join(dir, file)).slice(0, -4)
		const name = 'e2e-' + rel.replace(/[^A-Za-z0-9]+/g, '-').replace(/^-+|-+$/g, '').toLowerCase()
		// Two paths can make the same name, such as a-b.mmd and a_b.mmd.
		let suffix = 1
		while (written.has(suffix > 1 ? `${name}-${suffix}` : name)) suffix++
		keep('e2e', suffix > 1 ? `${name}-${suffix}` : name, readText(path.join(dir, file)))
	})

	// Spec files of different diagrams can share a name, such as parser.spec.ts, so the count goes on across them.
	const numbers = new Map()
	const keepLiterals = (spec, code) => {
		for (const text of literals(code)) {
			if (text === null || !strip(text)) continue
			const next = (numbers.get(spec) ?? 0) + 1
			if (keep('parser', `parser-${spec}-${String(next).padStart(3, '0')}`, normalize(text))) numbers.set(spec, next)
		}
	}
	const specName = (file, suffix) => file.replace(suffix, '').replace(/[^A-Za-z0-9]+/g, '-').replace(/^-+|-+$/g, '').toLowerCase()
	for (const specs of layout.specs(checkout))
		walk(specs, (dir, file) => {
			if (!/\.spec\.[jt]s$/.test(file)) return
			keepLiterals(specName(file, /\.spec\.[jt]s$/), readText(path.join(dir, file)))
		})
	// The langium grammars of 12, such as architecture, gitGraph, pie, radar and treeView, keep their parser tests in
	// packages/parser. 13 has no packages/parser.
	const langium = path.join(checkout, 'packages', 'parser', 'tests')
	for (const file of fs.existsSync(langium) ? fs.readdirSync(langium).sort() : []) {
		if (!/\.test\.[jt]s$/.test(file)) continue
		keepLiterals('langium-' + specName(file, /\.test\.[jt]s$/), readText(path.join(langium, file)))
	}

	const license = fs.readFileSync(path.join(checkout, 'LICENSE'), 'utf8').replace(/\r\n/g, '\n').trimEnd()
	fs.writeFileSync(path.join(corpus, 'SOURCE.txt'), [
		`The docs-*, e2e-* and parser-* cases come from ${layout.repositoryName(git('remote', 'get-url', 'origin'))} at the tag ${tag}, commit ${commit}.`,
		'mise run corpus writes them with tools/corpus/corpus.mjs. They are under mermaid\'s MIT license:',
		'',
		license,
		'',
	].join('\n'))
	console.log(`${counts.docs} docs, ${counts.e2e} e2e and ${counts.parser} parser cases written. `
		+ `${counts.duplicate} already in the corpus and ${counts['other type']} without a diagram header left out`)
}

// Copies each .mmd of a folder to <corpus>/<type>/<source>-<name>.mmd, except a source that is already in the corpus
// and a source without a diagram header.
function add(folder, source, corpus) {
	if (!/^[a-z0-9]+$/.test(source) || upstreamSources.includes(source)) {
		console.error(`the source must be lower-case letters and digits, and not one of ${upstreamSources.join(', ')}`)
		process.exit(2)
	}
	const seen = new Set(caseFiles(corpus).map(([type, file]) => normalize(readText(path.join(corpus, type, file)))))
	let added = 0
	let duplicate = 0
	for (const file of fs.readdirSync(folder).filter(f => f.endsWith('.mmd')).sort()) {
		const body = fs.readFileSync(path.join(folder, file), 'utf8')
		const type = typeOf(firstLine(body.replace(/\r\n?/g, '\n')))
		if (!type) {
			console.log(`skip ${file}: no diagram header`)
			continue
		}
		const key = normalize(body)
		if (seen.has(key)) {
			duplicate++
			continue
		}
		seen.add(key)
		fs.mkdirSync(path.join(corpus, type), { recursive: true })
		fs.writeFileSync(path.join(corpus, type, `${source}-${file}`), body)
		added++
	}
	console.log(`${added} cases added, ${duplicate} already in the corpus left out`)
}

// Rewrites the table between the count markers of README.md.
function writeCounts(corpus) {
	const readme = path.join(corpus, 'README.md')
	const text = fs.readFileSync(readme, 'utf8')
	const begin = '<!-- counts: written by tools/corpus/corpus.mjs -->'
	const end = '<!-- end of counts -->'
	const a = text.indexOf(begin)
	const b = text.indexOf(end)
	if (a < 0 || b < a) {
		console.error(`${readme} has no count markers`)
		process.exit(1)
	}
	const table = new Map()
	const sources = new Set()
	for (const [type, file] of caseFiles(corpus)) {
		const source = file.slice(0, file.indexOf('-'))
		sources.add(source)
		const row = table.get(type) ?? new Map()
		row.set(source, (row.get(source) ?? 0) + 1)
		table.set(type, row)
	}
	const columns = [...upstreamSources.filter(s => sources.has(s)), ...[...sources].filter(s => !upstreamSources.includes(s)).sort()]
	const types = [...table.keys()].sort()
	const total = (row, s) => row.get(s) ?? 0
	const sum = row => [...row.values()].reduce((x, y) => x + y, 0)
	const all = new Map(columns.map(s => [s, types.reduce((x, t) => x + total(table.get(t), s), 0)]))
	const lines = [
		`| Type | ${columns.join(' | ')} | Total |`,
		`|---|${columns.map(() => '--:|').join('')}--:|`,
		...types.map(t => `| \`${t}\` | ${columns.map(s => total(table.get(t), s)).join(' | ')} | ${sum(table.get(t))} |`),
		`| Total | ${columns.map(s => all.get(s)).join(' | ')} | ${sum(all)} |`,
	]
	fs.writeFileSync(readme, text.slice(0, a + begin.length) + '\n\n' + lines.join('\n') + '\n\n' + text.slice(b))
	console.log(`${sum(all)} cases in ${types.length} types`)
}

const args = process.argv.slice(2)
const usage = [
	'usage: node tools/corpus/corpus.mjs <mermaid checkout> [corpus-dir]',
	'       node tools/corpus/corpus.mjs --add <folder> <source> [corpus-dir]',
	'       node tools/corpus/corpus.mjs --counts [corpus-dir]',
].join('\n')
const corpusArg = dir => path.resolve(dir ?? path.join(repo, 'corpus'))
if (args[0] === '--counts') writeCounts(corpusArg(args[1]))
else if (args[0] === '--add' && args.length >= 3) {
	add(path.resolve(args[1]), args[2], corpusArg(args[3]))
	writeCounts(corpusArg(args[3]))
} else if (args.length && !args[0].startsWith('--')) {
	extract(path.resolve(args[0]), corpusArg(args[1]))
	writeCounts(corpusArg(args[1]))
} else {
	console.error(usage)
	process.exit(2)
}
