import { test } from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { readShapes, readThemeNames } from './dumps.mjs'

const shapesJs = `export const SHAPE_GROUPS = [
    ['rect', 'proc', 'squareRect'],
    ['cyl', 'db'],
];
const UNDOCUMENTED = 'bucket rect ' +
    'cloud';
`
const registryJs = `export const THEME_NAMES = [
    'redux',
    'default',
    'neo-dark'
];
`

// A node_modules with mermaid 13: a mermaid package without dist/chunks/mermaid.esm, @mermaid-js/core nested under it
// and @mermaid-js/diagram-flowchart hoisted next to it, as npm can install them.
function install() {
	const modules = fs.mkdtempSync(path.join(os.tmpdir(), 'stm-dumps-'))
	const write = (file, text) => {
		fs.mkdirSync(path.dirname(path.join(modules, file)), { recursive: true })
		fs.writeFileSync(path.join(modules, file), text)
	}
	write('mermaid/dist/mermaid.min.js', '')
	write('mermaid/node_modules/@mermaid-js/core/dist/theme/registry.js', registryJs)
	write('@mermaid-js/diagram-flowchart/dist/parser/shapes.js', shapesJs)
	return path.join(modules, 'mermaid')
}

test('readShapes reads the shape groups and the undocumented shapes of mermaid 13', () => {
	const shapes = readShapes(install())
	assert.deepEqual(shapes.defs.map(d => [d.shortName, d.aliases, d.handler]), [['rect', ['proc', 'squareRect'], 'rect'], ['cyl', ['db'], 'cyl']])
	assert.deepEqual(shapes.undocumented, [{ key: 'bucket', handler: 'bucket' }, { key: 'rect', handler: 'rect' }, { key: 'cloud', handler: 'cloud' }])
	assert.deepEqual([...shapes.keys], [['bucket', 'bucket'], ['rect', 'rect'], ['cloud', 'cloud'], ['proc', 'rect'], ['squareRect', 'rect'], ['cyl', 'cyl'], ['db', 'cyl']])
})

test('readThemeNames reads THEME_NAMES of mermaid 13 in source order', () => {
	assert.deepEqual(readThemeNames(install()), ['redux', 'default', 'neo-dark'])
})

test('a mermaid 13 install without the flowchart package fails with the missing file', () => {
	const mermaid = install()
	fs.rmSync(path.join(mermaid, '..', '@mermaid-js', 'diagram-flowchart'), { recursive: true })
	assert.throws(() => readShapes(mermaid), /@mermaid-js\/diagram-flowchart\/dist\/parser\/shapes\.js not found/)
})
