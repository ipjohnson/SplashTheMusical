import fs from 'node:fs'
import path from 'node:path'

// The parts of a version such as 22.12.0, compared part by part.
const older = (a, b) => {
	for (let i = 0; i < 3; i++) if (a[i] !== b[i]) return a[i] < b[i]
	return false
}

// Compares each package that package.json names with the version package-lock.json pins and the version node_modules
// holds. Returns one message per package that differs or is missing, or that asks for a newer Node in its engines field,
// so an empty list means the install matches the lock. puppeteer 25 asks for Node 22.12.0, and under 22.7.0 a corpus run
// stops answering after a few hundred cases.
export function checkPinned(root) {
	const node = process.versions.node.split('.').map(Number)
	const manifest = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))
	const lock = JSON.parse(fs.readFileSync(path.join(root, 'package-lock.json'), 'utf8'))
	const problems = []
	for (const name of Object.keys({ ...manifest.dependencies, ...manifest.devDependencies }).sort()) {
		const pinned = lock.packages?.[`node_modules/${name}`]?.version
		const file = path.join(root, 'node_modules', name, 'package.json')
		const pkg = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : undefined
		const installed = pkg?.version
		if (!pinned) problems.push(`package-lock.json does not pin ${name}`)
		else if (!installed) problems.push(`node_modules has no ${name}, package-lock.json pins ${pinned}`)
		else if (installed !== pinned) problems.push(`node_modules has ${name} ${installed}, package-lock.json pins ${pinned}`)
		const wanted = /^>=(\d+)\.(\d+)\.(\d+)$/.exec(pkg?.engines?.node ?? '')
		if (wanted && older(node, wanted.slice(1).map(Number)))
			problems.push(`${name} ${installed} needs Node ${pkg.engines.node} and this is Node ${process.versions.node}. Use the Node mise.toml pins`)
	}
	return problems
}
