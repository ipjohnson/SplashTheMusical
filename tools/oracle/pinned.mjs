import fs from 'node:fs'
import path from 'node:path'

// Compares each package that package.json names with the version package-lock.json pins and the version node_modules
// holds. Returns one message per package that differs or is missing, so an empty list means the install matches the lock.
export function checkPinned(root) {
	const manifest = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))
	const lock = JSON.parse(fs.readFileSync(path.join(root, 'package-lock.json'), 'utf8'))
	const problems = []
	for (const name of Object.keys({ ...manifest.dependencies, ...manifest.devDependencies }).sort()) {
		const pinned = lock.packages?.[`node_modules/${name}`]?.version
		const file = path.join(root, 'node_modules', name, 'package.json')
		const installed = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')).version : undefined
		if (!pinned) problems.push(`package-lock.json does not pin ${name}`)
		else if (!installed) problems.push(`node_modules has no ${name}, package-lock.json pins ${pinned}`)
		else if (installed !== pinned) problems.push(`node_modules has ${name} ${installed}, package-lock.json pins ${pinned}`)
	}
	return problems
}
