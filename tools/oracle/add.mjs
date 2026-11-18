// Adds minimized finds of the differential fuzzer to the corpus with their oracle files.
//
//   node add.mjs <find.mmd or folder>... [--corpus <dir>]
//
// A find is <type>/<name>.mmd, where <type> is the corpus folder of its diagram type, as the fuzzer writes it. Each
// becomes <corpus>/<type>/fuzz-<name>.mmd with the facts, boxes and dagre files facts.mjs would write for it, and the
// counts in the corpus README are rewritten. A find whose text is already a case of its type is left out. A folder adds
// every find in it and its subfolders.
import fs from 'node:fs'
import path from 'node:path'
import { execFileSync } from 'node:child_process'
import { exitUnlessPinned, filesUnder, openOracle, root } from './oracle.mjs'

const args = process.argv.slice(2)
const at = args.indexOf('--corpus')
const corpus = path.resolve(at >= 0 ? args[at + 1] : path.join(root, '..', '..', 'corpus'))
const inputs = at >= 0 ? [...args.slice(0, at), ...args.slice(at + 2)] : args
if (!inputs.length) {
	console.error('usage: node add.mjs <find.mmd or folder>... [--corpus <dir>]')
	process.exit(2)
}

const finds = inputs.flatMap(input => fs.statSync(input).isDirectory()
	? filesUnder(input).filter(f => f.endsWith('.mmd')).map(f => path.join(input, f))
	: [input])
const normalize = text => text.replace(/\r\n/g, '\n').trim()

exitUnlessPinned()
const oracle = await openOracle()
const added = []
for (const find of finds) {
	const type = path.basename(path.dirname(path.resolve(find)))
	const folder = path.join(corpus, type)
	if (!fs.existsSync(folder)) {
		console.error(`${find}: ${type} is not a folder of ${corpus}`)
		process.exitCode = 1
		continue
	}
	const source = fs.readFileSync(find, 'utf8')
	const name = 'fuzz-' + path.basename(find, '.mmd')
	const same = fs.readdirSync(folder).find(f => f.endsWith('.mmd') && normalize(fs.readFileSync(path.join(folder, f), 'utf8')) === normalize(source))
	if (same) {
		console.log(`skip ${find}: the same text as ${type}/${same}`)
		continue
	}
	const result = await oracle.run(source, name)
	if (result.unknownType) {
		console.error(`${find}: no extractor for mermaid diagram type '${result.unknownType}'`)
		process.exitCode = 1
		continue
	}
	const base = path.join(folder, name)
	fs.writeFileSync(base + '.mmd', source)
	fs.writeFileSync(base + '.facts.txt', result.facts)
	if (result.boxes) fs.writeFileSync(base + '.boxes.txt', result.boxes)
	if (result.dagre) fs.writeFileSync(base + '.dagre.txt', result.dagre)
	added.push(`${type}/${name}`)
	console.log(`add  ${type}/${name}${result.error === undefined ? '' : ': ' + result.error.split('\n')[0]}`)
}
await oracle.close()

execFileSync(process.execPath, [path.join(root, '..', 'corpus', 'corpus.mjs'), '--counts', corpus], { stdio: 'inherit' })
if (added.length) {
	console.log('Fix the difference or list it in the type\'s ledger under corpus/ledger, then run mise run raster for corpus/l4.txt.')
}
