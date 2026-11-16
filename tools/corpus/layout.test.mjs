import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { test } from 'node:test'
import { docs, repositoryName, specs } from './layout.mjs'

function checkout(folders) {
	const root = fs.mkdtempSync(path.join(os.tmpdir(), 'stm-layout-'))
	for (const folder of folders) fs.mkdirSync(path.join(root, folder), { recursive: true })
	return root
}

test('a 12 checkout reads packages/mermaid', () => {
	const root = checkout(['packages/mermaid/src/docs/syntax', 'packages/mermaid/src/diagrams/flowchart', 'packages/parser/tests'])
	assert.equal(docs(root), path.join(root, 'packages/mermaid/src/docs/syntax'))
	assert.deepEqual(specs(root), [path.join(root, 'packages/mermaid/src/diagrams')])
})

test('a 13 checkout reads v13/docs and the src of each diagram package', () => {
	const root = checkout(['v13/docs/syntax', 'packages/diagram-pie/src', 'packages/diagram-flowchart/src', 'packages/core/src', 'packages/diagram-empty'])
	assert.equal(docs(root), path.join(root, 'v13/docs/syntax'))
	assert.deepEqual(specs(root), [path.join(root, 'packages/diagram-flowchart/src'), path.join(root, 'packages/diagram-pie/src')])
})

test('a checkout without syntax docs fails', () => {
	assert.throws(() => docs(checkout(['packages'])), /has neither/)
})

test('repositoryName reads https and ssh remotes', () => {
	assert.equal(repositoryName('https://github.com/mermaid-js/mermaid'), 'mermaid-js/mermaid')
	assert.equal(repositoryName('https://github.com/mermaid-js/mermaid-v13.git\n'), 'mermaid-js/mermaid-v13')
	assert.equal(repositoryName('git@github.com:mermaid-js/mermaid-v13.git'), 'mermaid-js/mermaid-v13')
})
