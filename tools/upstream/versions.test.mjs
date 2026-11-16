import assert from "node:assert/strict";
import { test } from "node:test";
import { compareVersions, newestPrerelease } from "./versions.mjs";

test("orders versions by semver precedence", () => {
	const ordered = [
		"10.9.0-rc.2",
		"12.2.0",
		"12.3.0",
		"13.0.0-alpha.0",
		"13.0.0-alpha.2",
		"13.0.0-alpha.10",
		"13.0.0-alpha.beta",
		"13.0.0-beta.1",
		"13.0.0-rc.1",
		"13.0.0",
	];
	const shuffled = [...ordered].reverse();
	assert.deepEqual(shuffled.sort(compareVersions), ordered);
	assert.equal(compareVersions("12.3.0", "12.3.0"), 0);
});

test("picks the highest pre-release above latest", () => {
	const versions = ["10.9.0-rc.2", "11.0.0-alpha.7", "12.3.0", "13.0.0-alpha.0", "13.0.0-alpha.3", "13.0.0-alpha.1"];
	assert.equal(newestPrerelease(versions, "12.3.0"), "13.0.0-alpha.3");
});

test("picks nothing when every pre-release is below latest", () => {
	assert.equal(newestPrerelease(["10.9.0-rc.2", "12.3.0", "13.0.0-rc.1", "13.0.0"], "13.0.0"), undefined);
});
