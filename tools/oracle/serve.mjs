// The oracle as a long-lived process, for the differential fuzzer in tests/SplashTheMusical.Conformance/Fuzzing, which
// sends it many sources and cannot start Chromium for each.
//
//   node serve.mjs
//
// It reads one request per line from stdin: the case's name, a tab, and the source as base64 of its UTF-8. For each it
// writes the lines of the facts file facts.mjs would write for that source, then a line holding only a period. It does
// not record dagre layouts. A source with a type extract.mjs has no extractor for answers `# unknown-type <type>`, and
// one the page fails on answers `# oracle-error <message>`. It prints `ready` once mermaid.js has loaded.
import readline from 'node:readline'
import { exitUnlessPinned, openOracle } from './oracle.mjs'

exitUnlessPinned()
const oracle = await openOracle()
const write = text => new Promise(resolve => process.stdout.write(text, resolve))
await write('ready\n')

for await (const line of readline.createInterface({ input: process.stdin, crlfDelay: Infinity })) {
	if (!line) continue
	const tab = line.indexOf('\t')
	const name = line.slice(0, tab)
	const source = Buffer.from(line.slice(tab + 1), 'base64').toString('utf8')
	let answer
	try {
		const result = await oracle.run(source, name, { dagre: false })
		answer = result.unknownType ? `# unknown-type ${result.unknownType}\n` : result.facts
	} catch (e) {
		answer = `# oracle-error ${String(e.message).split('\n')[0]}\n`
	}
	await write(answer + '.\n')
}

await oracle.close()
