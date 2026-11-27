import { test } from 'node:test'
import assert from 'node:assert/strict'
import { caseOf, titleFor } from './label-issue.mjs'

const body = name => `### Comparison case\n\n${name}\n\n### Snapshot\n\n2026-11-27-abcdef0\n`

test('reads the case of the form', () => {
	assert.equal(caseOf(body('flowchart/basic')), 'flowchart/basic')
	assert.equal(caseOf(body('quadrant-chart/docs-quadrant-001')), 'quadrant-chart/docs-quadrant-001')
})

test('refuses a value that is not a case name', () => {
	assert.equal(caseOf(body('flowchart/../../etc')), undefined)
	assert.equal(caseOf(body('flowchart')), undefined)
	assert.equal(caseOf(body('a/b; rm -rf /')), undefined)
	assert.equal(caseOf('no form here'), undefined)
	assert.equal(caseOf(undefined), undefined)
})

test('starts the title with the case once', () => {
	assert.equal(titleFor('pie/a', '[pie/a] labels overlap'), '[pie/a] labels overlap')
	assert.equal(titleFor('pie/a', 'labels overlap'), '[pie/a] labels overlap')
	assert.equal(titleFor('pie/a', '[pie/a] '), '[pie/a]')
})

test('groups open issues by the case their title starts with', async () => {
	const { issuesByCase } = await import('./issues.mjs')
	assert.deepEqual(
		issuesByCase([
			{ number: 9, title: '[pie/a] labels overlap' },
			{ number: 4, title: '[pie/a] wrong colour' },
			{ number: 5, title: 'no case' },
		]),
		{ 'pie/a': [4, 9] },
	)
})
