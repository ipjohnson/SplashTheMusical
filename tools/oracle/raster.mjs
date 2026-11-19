// L4 of the parity plan: rasterizes the mermaid.js SVG and STM's SVG of every corpus case in one headless Chromium
// page and scores their structural similarity. tools/oracle/README.md describes the outputs.
//
//   node raster.mjs <corpus-dir> <oracle-svg-dir> <stm-svg-dir> <outcomes-file> <images-dir> [oracle-raster-dir]
//
// A case's oracle acceptance comes from its facts file, and STM's from whether stm-svg-dir has its SVG. The threshold
// of a type comes from <corpus-dir>/tolerances/<type>.json. The run fails when an accepted case has no oracle SVG.
// With oracle-raster-dir, mermaid.js's raster of each accepted case is kept there and reused while its SVG, Chromium,
// this script and the bundled fonts are unchanged.
//
// With STM_RASTER_CASES, a comma-separated list of case names such as flowchart/basic, the run takes only those cases and
// draws each side by side whatever its outcome. It then deletes nothing from oracle-raster-dir.
import fs from 'node:fs'
import path from 'node:path'
import crypto from 'node:crypto'
import { fileURLToPath } from 'node:url'
import puppeteer from 'puppeteer'
import { checkPinned } from './pinned.mjs'

const [, , corpusDir, oracleSvgDir, stmSvgDir, outcomesFile, imagesDir, rasterDir] = process.argv
if (!corpusDir || !oracleSvgDir || !stmSvgDir || !outcomesFile || !imagesDir) {
	console.error('usage: node raster.mjs <corpus-dir> <oracle-svg-dir> <stm-svg-dir> <outcomes-file> <images-dir> [oracle-raster-dir]')
	process.exit(2)
}

const root = path.dirname(fileURLToPath(import.meta.url))
const problems = checkPinned(root)
if (problems.length) {
	for (const p of problems) console.error(p)
	console.error('run npm ci in tools/oracle with the Node mise.toml pins')
	process.exit(1)
}

// The starting threshold of section 7 of the parity plan, for a type whose tolerance file has no raster key.
const startThreshold = 0.9
// The longest side of a raster, in CSS pixels. A larger SVG is scaled down to it.
const maxSide = 800
// A run writes side-by-side images of at most this many failing cases per type.
const imagesPerType = 10

const sha256 = (...parts) => {
	const hash = crypto.createHash('sha256')
	for (const part of parts) hash.update(part)
	return hash.digest('hex')
}

const filesUnder = dir =>
	fs.existsSync(dir)
		? fs.readdirSync(dir, { recursive: true, withFileTypes: true })
			.filter(e => e.isFile())
			.map(e => path.relative(dir, path.join(e.parentPath, e.name)).split(path.sep).join('/'))
			.sort()
		: []

function tolerance(type) {
	const file = path.join(corpusDir, 'tolerances', type + '.json')
	const json = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {}
	return { threshold: json.raster ?? startThreshold, reportOnly: json.rasterReportOnly === true }
}

const oracleAccepts = name =>
	fs.readFileSync(path.join(corpusDir, name + '.facts.txt'), 'utf8').split('\n').includes('accept\tyes')
const readSvg = (dir, name) => {
	const file = path.join(dir, name + '.svg')
	return fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : undefined
}

const fontFaces = [
	{ file: 'LiberationSans-Regular.ttf', weight: '400', style: 'normal' },
	{ file: 'LiberationSans-Bold.ttf', weight: '700', style: 'normal' },
	{ file: 'LiberationSans-Italic.ttf', weight: '400', style: 'italic' },
	{ file: 'LiberationSans-BoldItalic.ttf', weight: '700', style: 'italic' },
]

// The page serves only the bundled font from a made-up origin, as facts.mjs's page does, so both SVGs are drawn in the
// font the oracle measured them in and a run never reaches the network.
const origin = 'http://oracle.invalid'
const browser = await puppeteer.launch({ headless: 'shell' })
const page = await browser.newPage()
await page.setViewport({ width: maxSide, height: maxSide, deviceScaleFactor: 1 })
await page.setRequestInterception(true)
page.on('request', request => {
	const url = new URL(request.url())
	if (url.protocol === 'data:') return request.continue()
	if (url.origin !== origin) return request.abort()
	if (url.pathname === '/') {
		return request.respond({
			contentType: 'text/html',
			body: '<!doctype html><html><body style="margin:0;background:#fff"><div id="stage"></div></body></html>',
		})
	}
	const file = path.join(root, 'fonts', path.basename(decodeURIComponent(url.pathname)))
	if (!url.pathname.startsWith('/fonts/') || !fs.existsSync(file)) return request.respond({ status: 404, body: '' })
	request.respond({ contentType: 'font/ttf', body: fs.readFileSync(file) })
})
await page.goto(origin + '/')
await page.evaluate(async faces => {
	for (const face of faces) {
		const f = new FontFace('Liberation Sans', await (await fetch('/fonts/' + face.file)).arrayBuffer(), { weight: face.weight, style: face.style })
		await f.load()
		document.fonts.add(f)
	}
	const style = document.createElement('style')
	style.textContent = `* { font-family: "Liberation Sans" !important; }
		#stage { position: absolute; left: 0; top: 0; overflow: hidden; background: #fff; }
		#stage > svg { display: block; width: 100% !important; height: 100% !important; max-width: none !important; }`
	document.head.append(style)
}, fontFaces)

// The size of an SVG: its viewBox, or the box of what it draws when it has none, as for mermaid's info diagram.
async function sizeOf(svg) {
	return page.evaluate(svg => {
		const stage = document.getElementById('stage')
		stage.style.width = stage.style.height = ''
		stage.innerHTML = svg
		const element = stage.querySelector('svg')
		if (!element) return undefined
		const box = element.viewBox?.baseVal
		if (box && box.width > 0 && box.height > 0) return { width: box.width, height: box.height }
		const drawn = element.getBoundingClientRect()
		return { width: drawn.width, height: drawn.height }
	}, svg)
}

// The stage of a case: the size of an SVG, scaled down so its longer side is at most maxSide.
async function stageOf(svg) {
	const size = (await sizeOf(svg)) ?? { width: 1, height: 1 }
	const scale = Math.min(1, maxSide / Math.max(size.width, size.height, 1))
	return { width: Math.max(1, Math.round(size.width * scale)), height: Math.max(1, Math.round(size.height * scale)) }
}

// What a cached raster depends on besides its SVG.
const rasterKey = sha256(
	await browser.version(),
	fs.readFileSync(fileURLToPath(import.meta.url)),
	...fontFaces.map(face => fs.readFileSync(path.join(root, 'fonts', face.file))),
)
const cached = { read: 0, drawn: 0 }

// mermaid.js's raster of a case and its stage, from oracle-raster-dir when <name>.key there holds the raster key and the
// SVG's hash. Otherwise it is drawn and, with oracle-raster-dir, written there.
async function oracleRaster(name, svg) {
	const svgHash = sha256(svg)
	const keyFile = rasterDir && path.join(rasterDir, name + '.key')
	const pngFile = rasterDir && path.join(rasterDir, name + '.png')
	if (rasterDir && fs.existsSync(keyFile) && fs.existsSync(pngFile)) {
		const [key, hash, width, height] = fs.readFileSync(keyFile, 'utf8').trim().split('\t')
		if (key === rasterKey && hash === svgHash) {
			cached.read++
			return { png: fs.readFileSync(pngFile).toString('base64'), width: Number(width), height: Number(height) }
		}
	}
	const { width, height } = await stageOf(svg)
	const png = await rasterize(svg, width, height)
	if (rasterDir) {
		cached.drawn++
		fs.mkdirSync(path.dirname(pngFile), { recursive: true })
		fs.writeFileSync(pngFile, Buffer.from(png, 'base64'))
		fs.writeFileSync(keyFile, [rasterKey, svgHash, width, height].join('\t') + '\n')
	}
	return { png, width, height }
}

// Draws an SVG to fill a width by height stage, keeping its aspect ratio, and screenshots the stage as a PNG.
async function rasterize(svg, width, height) {
	await page.evaluate((svg, width, height) => {
		const stage = document.getElementById('stage')
		stage.style.width = width + 'px'
		stage.style.height = height + 'px'
		stage.innerHTML = svg
		const element = stage.querySelector('svg')
		if (element && !element.getAttribute('viewBox')) {
			const drawn = element.getBBox()
			element.setAttribute('viewBox', `${drawn.x} ${drawn.y} ${Math.max(drawn.width, 1)} ${Math.max(drawn.height, 1)}`)
		}
	}, svg, width, height)
	const png = await page.screenshot({ clip: { x: 0, y: 0, width, height }, encoding: 'base64', captureBeyondViewport: false })
	await page.evaluate(() => { document.getElementById('stage').innerHTML = '' })
	return png
}

// The mean structural similarity of two PNGs of the same size, over the luma of 8 by 8 windows a stride of 4 apart, with
// the constants of Wang et al. 2004. An image smaller than a window is one window.
async function similarity(a, b) {
	return page.evaluate(async (a, b) => {
		const luma = async png => {
			const image = new Image()
			image.src = 'data:image/png;base64,' + png
			await image.decode()
			const canvas = new OffscreenCanvas(image.width, image.height)
			const context = canvas.getContext('2d')
			context.drawImage(image, 0, 0)
			const { data, width, height } = context.getImageData(0, 0, image.width, image.height)
			const y = new Float64Array(width * height)
			for (let i = 0; i < y.length; i++) y[i] = 0.299 * data[i * 4] + 0.587 * data[i * 4 + 1] + 0.114 * data[i * 4 + 2]
			return { y, width, height }
		}
		const x = await luma(a)
		const y = await luma(b)
		const c1 = (0.01 * 255) ** 2
		const c2 = (0.03 * 255) ** 2
		const w = Math.min(8, x.width)
		const h = Math.min(8, x.height)
		const starts = (size, window) => {
			const s = []
			for (let i = 0; i + window <= size; i += 4) s.push(i)
			if (s.at(-1) !== size - window) s.push(size - window)
			return s
		}
		let total = 0
		let count = 0
		for (const top of starts(x.height, h)) {
			for (const left of starts(x.width, w)) {
				let mx = 0, my = 0
				for (let r = top; r < top + h; r++) for (let c = left; c < left + w; c++) { mx += x.y[r * x.width + c]; my += y.y[r * x.width + c] }
				const n = w * h
				mx /= n
				my /= n
				let vx = 0, vy = 0, cov = 0
				for (let r = top; r < top + h; r++) {
					for (let c = left; c < left + w; c++) {
						const dx = x.y[r * x.width + c] - mx
						const dy = y.y[r * x.width + c] - my
						vx += dx * dx
						vy += dy * dy
						cov += dx * dy
					}
				}
				vx /= n - 1 || 1
				vy /= n - 1 || 1
				cov /= n - 1 || 1
				total += ((2 * mx * my + c1) * (2 * cov + c2)) / ((mx * mx + my * my + c1) * (vx + vy + c2))
				count++
			}
		}
		return total / count
	}, a, b)
}

// mermaid.js's raster on the left and STM's on the right, under a caption with the case, the score and the threshold. A
// side without an SVG is grey.
async function sideBySide(name, oracle, stm, width, height, caption) {
	const base64 = await page.evaluate(async (name, oracle, stm, width, height, caption) => {
		const gap = 16
		const top = 40
		const canvas = new OffscreenCanvas(width * 2 + gap * 3, height + top + gap)
		const context = canvas.getContext('2d')
		context.fillStyle = '#ffffff'
		context.fillRect(0, 0, canvas.width, canvas.height)
		context.fillStyle = '#000000'
		context.font = '12px "Liberation Sans"'
		context.fillText(`${name}: ${caption}`, gap, 16)
		const sides = [['mermaid.js', oracle], ['STM', stm]]
		for (let i = 0; i < 2; i++) {
			const left = gap + i * (width + gap)
			const [label, png] = sides[i]
			context.fillStyle = '#000000'
			context.fillText(png ? label : `${label}: no SVG`, left, 34)
			if (png) {
				const image = new Image()
				image.src = 'data:image/png;base64,' + png
				await image.decode()
				context.drawImage(image, left, top)
			} else {
				context.fillStyle = '#dddddd'
				context.fillRect(left, top, width, height)
			}
			context.strokeStyle = '#999999'
			context.strokeRect(left - 0.5, top - 0.5, width + 1, height + 1)
		}
		const blob = await canvas.convertToBlob({ type: 'image/png' })
		const bytes = new Uint8Array(await blob.arrayBuffer())
		let text = ''
		for (const b of bytes) text += String.fromCharCode(b)
		return btoa(text)
	}, name, oracle, stm, width, height, caption)
	const file = path.join(imagesDir, name + '.png')
	fs.mkdirSync(path.dirname(file), { recursive: true })
	fs.writeFileSync(file, Buffer.from(base64, 'base64'))
}

fs.rmSync(imagesDir, { recursive: true, force: true })
fs.mkdirSync(imagesDir, { recursive: true })
const corpusNames = filesUnder(corpusDir).filter(f => f.endsWith('.mmd')).map(f => f.slice(0, -4))
const wanted = new Set((process.env.STM_RASTER_CASES ?? '').split(',').map(n => n.trim()).filter(n => n))
const unknown = [...wanted].filter(n => !corpusNames.includes(n))
if (unknown.length) {
	console.error(`not corpus cases: ${unknown.join(', ')}`)
	await browser.close()
	process.exit(1)
}
const names = wanted.size ? corpusNames.filter(n => wanted.has(n)) : corpusNames
const outcomes = []
const scores = []
const images = new Map()
const kept = new Set()
const counts = { pass: 0, fail: 0, 'n/a': 0, report: 0 }
for (const name of names) {
	const type = name.split('/')[0]
	const { threshold, reportOnly } = tolerance(type)
	const accepts = oracleAccepts(name)
	const oracle = accepts ? readSvg(oracleSvgDir, name) : undefined
	if (accepts && oracle === undefined) {
		console.error(`${name}: mermaid.js accepts it and ${oracleSvgDir} has no SVG for it. Run facts.mjs with an svg folder first`)
		await browser.close()
		process.exit(1)
	}
	const stm = readSvg(stmSvgDir, name)
	if (oracle === undefined && stm === undefined) {
		counts['n/a']++
		outcomes.push(`${name}\tn/a`)
		scores.push(`${name}\tn/a`)
		continue
	}

	// A case is drawn only when it is scored or gets an image. With oracle-raster-dir, mermaid.js's side of every accepted
	// case is drawn, so the folder holds it when STM starts drawing the case.
	const both = oracle !== undefined && stm !== undefined
	const imageDue = wanted.size > 0 || (images.get(type) ?? 0) < imagesPerType
	let score
	let oraclePng
	let stmPng
	let width = 1
	let height = 1
	if (oracle !== undefined && (both || imageDue || rasterDir)) {
		kept.add(name)
		const drawn = await oracleRaster(name, oracle)
		oraclePng = drawn.png
		width = drawn.width
		height = drawn.height
	}
	if (both || imageDue) {
		// Both rasters take the size of mermaid.js's SVG, or of STM's when mermaid.js refuses the case.
		if (oracle === undefined) {
			const stage = await stageOf(stm)
			width = stage.width
			height = stage.height
		}
		stmPng = stm && await rasterize(stm, width, height)
		if (both) score = await similarity(oraclePng, stmPng)
	}
	const passed = score !== undefined && score >= threshold
	const outcome = reportOnly ? 'report' : passed ? 'pass' : 'fail'
	counts[outcome]++
	outcomes.push(`${name}\t${outcome}`)
	scores.push([name, outcome, score === undefined ? '' : score.toFixed(4)].join('\t').replace(/\t+$/, ''))
	if ((!passed || wanted.size) && imageDue) {
		images.set(type, (images.get(type) ?? 0) + 1)
		const caption = score === undefined
			? oracle === undefined ? 'mermaid.js refuses it' : 'STM drew nothing'
			: `similarity ${score.toFixed(4)}, threshold ${threshold}`
		await sideBySide(name, oraclePng, stmPng, width, height, caption)
	}
}
await browser.close()

// Rasters of cases that left the corpus or that mermaid.js now refuses.
let pruned = 0
for (const file of rasterDir && !wanted.size ? filesUnder(rasterDir) : []) {
	if (!kept.has(file.replace(/\.(png|key)$/, ''))) {
		fs.rmSync(path.join(rasterDir, file))
		pruned++
	}
}

fs.mkdirSync(path.dirname(path.resolve(outcomesFile)), { recursive: true })
fs.writeFileSync(outcomesFile, ['# L4 outcomes. Refresh with: mise run raster', ...outcomes].join('\n') + '\n')
fs.writeFileSync(path.join(imagesDir, 'scores.txt'), ['# L4 scores. Written by tools/oracle/raster.mjs', ...scores].join('\n') + '\n')
const imageCount = [...images.values()].reduce((a, b) => a + b, 0)
console.log(`${names.length} cases: ${counts.pass} pass, ${counts.fail} fail, ${counts.report} report-only, ${counts['n/a']} n/a. ${imageCount} side-by-side images`)
if (rasterDir) console.log(`mermaid.js rasters: ${cached.read} read from ${rasterDir}, ${cached.drawn} drawn, ${pruned} files deleted`)
