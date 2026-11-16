// Runs khroma, the colour library mermaid's themes call, and writes one line per call. The inputs are the colour
// literals, adjust() channels and darken()/lighten() amounts of mermaid's theme code, read from its dist, with edge cases
// and seeded random colours. tools/oracle/README.md describes the format.
//
//   node colors.mjs <out-file>
//
// The run fails when node_modules does not match package-lock.json, and when the khroma it would import is not the one
// package-lock.json resolves for mermaid.
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { adjust, darken, invert, isDark, lighten, rgba } from 'khroma'
import { checkPinned } from './pinned.mjs'
import { findInDist } from './dumps.mjs'

const [, , outFile] = process.argv
if (!outFile) {
	console.error('usage: node colors.mjs <out-file>')
	process.exit(2)
}

const root = path.dirname(fileURLToPath(import.meta.url))
const problems = checkPinned(root)
const lock = JSON.parse(fs.readFileSync(path.join(root, 'package-lock.json'), 'utf8'))
const khromaPinned = lock.packages['node_modules/khroma']?.version
const khromaInstalled = JSON.parse(fs.readFileSync(path.join(root, 'node_modules', 'khroma', 'package.json'), 'utf8')).version
if (khromaInstalled !== khromaPinned) problems.push(`node_modules has khroma ${khromaInstalled}, package-lock.json pins ${khromaPinned}`)
if (problems.length) {
	for (const p of problems) console.error(p)
	console.error('run npm ci in tools/oracle with the Node mise.toml pins')
	process.exit(1)
}
const mermaidDir = path.join(root, 'node_modules', 'mermaid')
const version = JSON.parse(fs.readFileSync(path.join(mermaidDir, 'package.json'), 'utf8')).version

// mkBorder from mermaid's src/themes/theme-helpers.js, which mermaid does not export.
const mkBorder = (col, darkMode) => darkMode ? adjust(col, { s: -40, l: 10 }) : adjust(col, { s: -40, l: -10 })

// The theme classes, theme-base.js to theme-redux-dark-color.js, which the bundle places before the themes table.
const { code, start } = findInDist(mermaidDir, '// src/themes/theme-base.js', 'the theme code')
const end = code.indexOf('var themes_default = {', start)
if (end < 0) throw new Error('themes_default not found after the theme code')
const themes = code.slice(start, end)

// The argument texts of each call of khroma's `name` in the theme code, split at top-level commas.
function calls(name) {
	const found = []
	for (const m of themes.matchAll(new RegExp(`\\b${name}_default\\d*\\(`, 'g'))) {
		const args = []
		let depth = 0
		let arg = ''
		for (let i = m.index + m[0].length; i < themes.length; i++) {
			const c = themes[i]
			if (depth === 0 && (c === ')' || c === ',')) {
				args.push(arg.trim())
				arg = ''
				if (c === ')') break
				continue
			}
			if ('({['.includes(c)) depth++
			if (')}]'.includes(c)) depth--
			arg += c
		}
		found.push(args.filter(a => a !== ''))
	}
	if (!found.length) throw new Error(`no ${name}_default call in the theme code; the bundle's names changed`)
	return found
}

const isNumber = text => /^-?(\d+(\.\d*)?|\.\d+)$/.test(text)
const unique = (items, key = JSON.stringify) => [...new Map(items.map(i => [key(i), i])).values()]
const parses = text => {
	try {
		isDark(text)
		return true
	} catch {
		return false
	}
}

// Channels computed in a loop, such as { h: 180, l: multiplier * (5 + i * 3) }, are left out.
const themeAdjusts = unique(calls('adjust')
	.map(([, channels]) => channels ?? '')
	.filter(channels => /^\{(\s*[hslrgba]: -?[\d.]+,?)*\s*\}$/.test(channels))
	.map(channels => JSON.parse(channels.replace(/(\w+):/g, '"$1":'))))
const themeAmounts = unique(['darken', 'lighten'].flatMap(name => calls(name))
	.filter(args => args.length === 1 || isNumber(args[1]))
	.map(args => args.length === 1 ? undefined : Number(args[1])), String)
const themeRgbas = unique(calls('rgba').filter(args => args.every(isNumber)).map(args => args.map(Number)))
const themeColors = unique([...themes.matchAll(/"([^"\n]*)"/g)].map(m => m[1]).filter(parses), String)

// mulberry32, so the random colours are the same on every run.
let seed = 0x5eed
const random = () => {
	seed = (seed + 0x6d2b79f5) | 0
	let t = Math.imul(seed ^ (seed >>> 15), 1 | seed)
	t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
	return ((t ^ (t >>> 14)) >>> 0) / 4294967296
}
const byte = () => Math.floor(random() * 256)
const hex = n => n.toString(16).padStart(2, '0')
const randomColors = [
	...Array.from({ length: 48 }, () => `#${hex(byte())}${hex(byte())}${hex(byte())}`),
	...Array.from({ length: 12 }, () => `rgba(${byte()}, ${byte()}, ${byte()}, ${Math.round(random() * 100) / 100})`),
	...Array.from({ length: 12 }, () => `hsl(${(random() * 720 - 360).toFixed(4)}, ${(random() * 100).toFixed(4)}%, ${(random() * 100).toFixed(4)}%)`),
]

const edgeColors = [
	'#fff', '#FFF', '#000', '#abc', '#ABCD', '#11223344', '#00000080', '#ffffff', '#808080', '#7f7f7f',
	'transparent', 'red', 'Red', 'WHITE', 'black', 'lightgrey', 'cyan', 'cyanaqua', 'rebeccapurple',
	'rgb(255, 0, 0)', 'RGB(1,2,3)', 'rgb( 1 , 2 , 3 )', 'rgb(1 2 3)', 'rgb(1.5, 2, 3)', 'rgb(-5,300,12.5)',
	'rgb(1e2 50 .5)', 'rgba(10%, 20%, 30%, 50%)', 'rgba(1,2,3 / 0.5)', 'rgba(1,2,3,+.5)', 'rgba(1, 2, 3, 2)',
	'hsl(120, 50%, 50%)', 'HSL(120, 50%, 50%)', 'hsla(120deg 50% 50% / 0.3)', 'hsl(1.5turn, 10%, 20%)',
	'hsl(100grad, 10%, 20%)', 'hsl(2rad, 10%, 20%)', 'hsl(90DEG, 10%, 20%)', 'hsl(1TURN, 10%, 20%)',
	'hsl(-30, 150%, -5%)', 'hsl(400, 0%, 40%)', 'hsl(1e1, 1e1%, 5e-1%)', 'hsla(0, 0%, 0%, 50%)', 'hsl(0, 0%, 100%)',
	'hsl(-79.4117647059, 100%, 93.3333333333%)', 'hsl(240, 100%, 46.2745098039%)',
	'nope', '#12', '#12345', '#ggg', ' #fff', 'rgb(1,2)', 'rgb(1,2,3,)', 'rgb(1.,2,3)', 'hsl(1, 2, 3)', 'hsl(1, 2%, 3)',
]

const edgeAdjusts = [{}, { h: 0 }, { h: 360 }, { h: -400 }, { s: 200 }, { l: -200 }, { a: -0.5 }, { r: 300, g: -300 }, { r: 10, h: 5 }]
const edgeAmounts = [0, -10, 100, 0.5, 1e-7]
const alphas = [0, 0.25, 0.5, 1, 1.5, -1]
const edgeRgbas = [[255, 255, 255, 50], [300, -1, 12.5, 0.5], [1, 2], [1, 2, 3], [0, 0, 0, 0], [12.5, 0, 0, 1]]

const adjustArgs = channels => Object.entries(channels).map(([k, v]) => `${k}=${v}`).join(',')
const lines = new Set()
const call = (fn, color, args, run) => {
	let result
	try {
		result = String(run())
	} catch (e) {
		result = `error: ${e.message}`
	}
	lines.add(`${fn}\t${color}\t${args}\t${result}`)
}
const every = (color, adjusts, amounts, alphas) => {
	call('isDark', color, '', () => isDark(color))
	call('invert', color, '', () => invert(color))
	for (const dark of [false, true]) call('mkBorder', color, String(dark), () => mkBorder(color, dark))
	for (const a of alphas) call('rgba', color, String(a), () => rgba(color, a))
	for (const amount of amounts) {
		call('darken', color, String(amount), () => darken(color, amount))
		call('lighten', color, String(amount), () => lighten(color, amount))
	}
	for (const channels of adjusts) call('adjust', color, adjustArgs(channels), () => adjust(color, channels))
}

const allAdjusts = [...themeAdjusts, ...edgeAdjusts]
const allAmounts = [...themeAmounts, ...edgeAmounts]
// The edge colours get every channel and amount. The other colours get every function, with a few of them.
const pick = items => items[Math.floor(random() * items.length)]
const some = (color, adjusts, amounts) =>
	every(color, Array.from({ length: adjusts }, () => pick(allAdjusts)), Array.from({ length: amounts }, () => pick(allAmounts)),
		[pick(alphas), pick(alphas)])
for (const color of edgeColors) every(color, allAdjusts, allAmounts, alphas)
for (const color of themeColors) some(color, 6, 3)
for (const color of randomColors) some(color, 6, 3)
// The themes pass the result of one call to the next, so the hsl() and rgb() results are inputs too.
const results = unique([...lines].map(l => l.split('\t')[3]).filter(r => /^(hsla?|rgba?)\(/.test(r) && !r.includes('NaN')), String)
for (const color of results.filter((_, i) => i % 10 === 0)) some(color, 2, 1)
for (const args of [...themeRgbas, ...edgeRgbas]) call('rgba', '', args.join(','), () => rgba(...args))

const header = [
	'# khroma colour functions on mermaid\'s theme colours. Refresh with: mise run colors',
	`# mermaid ${version}`,
	`# khroma ${khromaInstalled}`,
]
fs.writeFileSync(outFile, [...header, ...lines].join('\n') + '\n')
console.log(`${lines.size} calls: ${themeColors.length} theme colours, ${themeAdjusts.length} adjust channels, ${themeAmounts.length} amounts`)
