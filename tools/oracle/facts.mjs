// Renders every .mmd in a folder and its subfolders with the mermaid.js that package-lock.json pins, in one headless
// page, and writes <name>.facts.txt per case, <name>.boxes.txt per accepted case, <name>.dagre.txt per accepted
// flowchart, state, class and ER case and <name>.elk.txt per accepted case mermaid.js lays out with ELK, in the case's
// subfolder. It also writes themes.txt, config.txt and shapes.txt to
// the top of the facts folder. tools/oracle/README.md describes the
// formats. extract.mjs says which facts come from mermaid's parsed model and which from its rendered DOM.
//
//   node facts.mjs <cases-dir> <facts-dir> [svg-dir]
//
// The run fails when node_modules does not match package-lock.json and when a case has a diagram type extract.mjs has
// no extractor for. Facts and boxes files whose case is no longer in the folder are deleted. With svg-dir, each
// accepted case's SVG is written there too.
import fs from 'node:fs'
import path from 'node:path'
import { configDumps, styleCases, styleDumps } from './dumps.mjs'
import { exitUnlessPinned, filesUnder, init, openOracle } from './oracle.mjs'

const [, , casesDir, factsDir, svgDir] = process.argv
if (!casesDir || !factsDir) {
	console.error('usage: node facts.mjs <cases-dir> <facts-dir> [svg-dir]')
	process.exit(2)
}

exitUnlessPinned()
const oracle = await openOracle()
const { version, page, shapeTable, themeNames } = oracle

fs.mkdirSync(factsDir, { recursive: true })
if (svgDir) fs.mkdirSync(svgDir, { recursive: true })

const dumpHeader = what => [`# mermaid.js ${what}. Refresh with: mise run oracle`, `# mermaid ${version}`]
const dumps = await page.evaluate(configDumps, themeNames, init)
fs.writeFileSync(path.join(factsDir, 'themes.txt'), [...dumpHeader('theme variables'), ...dumps.themes.sort()].join('\n') + '\n')
fs.writeFileSync(path.join(factsDir, 'config.txt'), [...dumpHeader('default config'), ...dumps.config.sort()].join('\n') + '\n')
const styles = await page.evaluate(styleDumps, styleCases(themeNames), init)
fs.writeFileSync(path.join(factsDir, 'styles.txt'), [...dumpHeader('stylesheets'), ...styles.sort()].join('\n') + '\n')
const shapeLines = [
	...shapeTable.defs.flatMap(d => [
		['shape', d.shortName, d.name, d.semanticName, d.description, d.handler],
		...d.aliases.map(a => ['alias', a, d.shortName]),
		...d.internalAliases.map(a => ['internal-alias', a, d.shortName]),
	]),
	...shapeTable.undocumented.map(s => ['undocumented', s.key, s.handler]),
	...[...shapeTable.keys].map(([k, h]) => ['key', k, h]),
].map(f => f.join('\t').replace(/\t+$/, '')).sort()
fs.writeFileSync(path.join(factsDir, 'shapes.txt'), [...dumpHeader('shape table'), ...shapeLines].join('\n') + '\n')

// A case in a subfolder, such as flowchart/e2e-basic, writes its files to the same subfolder of the facts folder. Its
// seed and SVG id come from its file name, so moving a case to another folder does not change its output.
const names = filesUnder(casesDir).filter(f => f.endsWith('.mmd')).map(f => f.slice(0, -4))
let accepted = 0
const dagreNames = new Set()
const elkNames = new Set()
for (const name of names) {
	const source = fs.readFileSync(path.join(casesDir, name + '.mmd'), 'utf8')
	fs.mkdirSync(path.dirname(path.join(factsDir, name)), { recursive: true })
	const result = await oracle.run(source, path.posix.basename(name))
	if (result.unknownType) {
		console.error(`${name}: no extractor for mermaid diagram type '${result.unknownType}'; add one to tools/oracle/extract.mjs`)
		await oracle.close()
		process.exit(1)
	}
	fs.writeFileSync(path.join(factsDir, name + '.facts.txt'), result.facts)
	const boxesFile = path.join(factsDir, name + '.boxes.txt')
	if (result.boxes) fs.writeFileSync(boxesFile, result.boxes)
	else if (fs.existsSync(boxesFile)) fs.unlinkSync(boxesFile)
	if (result.dagreError) console.log(`dagre-err  ${name}: ${result.dagreError}`)
	const dagreFile = path.join(factsDir, name + '.dagre.txt')
	if (result.dagre) {
		fs.writeFileSync(dagreFile, result.dagre)
		dagreNames.add(name + '.dagre.txt')
	} else if (fs.existsSync(dagreFile)) fs.unlinkSync(dagreFile)
	const elkFile = path.join(factsDir, name + '.elk.txt')
	if (result.elk) {
		fs.writeFileSync(elkFile, result.elk)
		elkNames.add(name + '.elk.txt')
	} else if (fs.existsSync(elkFile)) fs.unlinkSync(elkFile)
	if (svgDir && result.svg) {
		fs.mkdirSync(path.dirname(path.join(svgDir, name)), { recursive: true })
		fs.writeFileSync(path.join(svgDir, name + '.svg'), result.svg)
	}
	if (result.error === undefined) accepted++
	console.log(`${result.error === undefined ? 'ok ' : 'err'}  ${name}${result.error === undefined ? '' : ': ' + result.error.split('\n')[0]}`)
}

const keep = new Set([...names.flatMap(n => [n + '.facts.txt', n + '.boxes.txt']), ...dagreNames, ...elkNames])
const perCase = f => f.endsWith('.facts.txt') || f.endsWith('.boxes.txt') || f.endsWith('.dagre.txt') || f.endsWith('.elk.txt')
for (const f of filesUnder(factsDir).filter(f => perCase(f) && !keep.has(f))) {
	fs.unlinkSync(path.join(factsDir, f))
	console.log(`del  ${f}`)
}

await oracle.close()
console.log(`mermaid ${version}: ${names.length} cases, ${accepted} accepted, ${dagreNames.size} dagre dumps, ${elkNames.size} ELK dumps, ${themeNames.length} themes`)
