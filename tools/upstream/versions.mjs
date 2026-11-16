// Prints the mermaid versions the upstream workflows act on.
//
//   node tools/upstream/versions.mjs pinned       the version tools/oracle/package.json pins
//   node tools/upstream/versions.mjs latest       npm's latest tag
//   node tools/upstream/versions.mjs prerelease   the highest pre-release above latest, or nothing
//
// The pre-release comes from the list of published versions because the alpha dist-tag can point at an older alpha.
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

function parse(version) {
	const [core, prerelease] = version.split("+")[0].split(/-(.*)/s);
	const [major, minor, patch] = core.split(".").map(Number);
	return { major, minor, patch, prerelease: prerelease ? prerelease.split(".") : [] };
}

function compareIdentifiers(a, b) {
	const numericA = /^\d+$/.test(a);
	const numericB = /^\d+$/.test(b);
	if (numericA && numericB) {
		return Number(a) - Number(b);
	}
	if (numericA !== numericB) {
		return numericA ? -1 : 1;
	}
	return a < b ? -1 : a > b ? 1 : 0;
}

/** Orders two versions by semver precedence. */
export function compareVersions(a, b) {
	const x = parse(a);
	const y = parse(b);
	const core = x.major - y.major || x.minor - y.minor || x.patch - y.patch;
	if (core !== 0) {
		return core;
	}
	if (x.prerelease.length === 0 || y.prerelease.length === 0) {
		return y.prerelease.length - x.prerelease.length;
	}
	for (let i = 0; i < Math.min(x.prerelease.length, y.prerelease.length); i++) {
		const order = compareIdentifiers(x.prerelease[i], y.prerelease[i]);
		if (order !== 0) {
			return order;
		}
	}
	return x.prerelease.length - y.prerelease.length;
}

/** The highest pre-release in versions that is above latest, or undefined. */
export function newestPrerelease(versions, latest) {
	return versions
		.filter((version) => version.includes("-") && compareVersions(version, latest) > 0)
		.sort(compareVersions)
		.at(-1);
}

function npmView(field) {
	return JSON.parse(execFileSync("npm", ["view", "mermaid", field, "--json"], { encoding: "utf8" }));
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
	const command = process.argv[2];
	if (command === "pinned") {
		const packageJson = new URL("../oracle/package.json", import.meta.url);
		console.log(JSON.parse(readFileSync(packageJson, "utf8")).devDependencies.mermaid);
	} else if (command === "latest") {
		console.log(npmView("dist-tags").latest);
	} else if (command === "prerelease") {
		console.log(newestPrerelease(npmView("versions"), npmView("dist-tags").latest) ?? "");
	} else {
		console.error("usage: node tools/upstream/versions.mjs pinned|latest|prerelease");
		process.exit(2);
	}
}
