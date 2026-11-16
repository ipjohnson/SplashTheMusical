# Updating to a new mermaid.js

The oracle is always the latest mermaid.js release. This document describes how STM moves to a new release and how it
watches the next major version. Section 8 of [mermaid-parity-plan.md](mermaid-parity-plan.md) gives the reasons.

## Detect

`.github/workflows/upstream-watch.yml` runs daily at 06:41 UTC. Its `detect` job compares the mermaid version
`tools/oracle/package.json` pins with npm's `latest` tag. When they differ, and no branch or pull request named
`upstream/mermaid-<version>` exists, its `bump` job runs `mise run upstream-bump <version>` on macOS. It then pushes the
result to `upstream/mermaid-<version>` and opens a pull request with the delta report as its body. A closed pull request
also stops the next runs, so delete its branch to have the watch propose that version again.

A manual run takes a `version` input, and it opens the pull request into the branch it ran on. A run on a branch that
pins an older mermaid, such as 12.2.0, opens a pull request that moves that branch to `latest`.

Dependabot watches the other packages of `tools/oracle`. It ignores `mermaid`, which moves only through the watch,
because a bump has to regenerate the corpus and the code. A Dependabot pull request that moves `puppeteer` changes
Chromium, so it may need `mise run corpus-oracle` and `mise run raster` before CI passes.

Pull requests that `GITHUB_TOKEN` opens start no workflow, so CI does not run on the watch's pull request by default.
Set the repository secret `UPSTREAM_BUMP_TOKEN` to a fine-grained token with contents and pull requests write access, and
the watch pushes and opens the pull request with it. Without the secret, close and reopen the pull request to start CI.
The repository setting "Allow GitHub Actions to create and approve pull requests" must be on in either case.

## Bump

`mise run upstream-bump <version>` runs `tools/upstream/bump.sh`, which does the mechanical work in about 20 minutes. It:

1. clones the pinned and the new tag `mermaid@<version>` into `.artifacts/upstream`, from the repository npm names for
   each version
2. runs the conformance tests and keeps their results
3. installs the new mermaid in `tools/oracle`
4. re-extracts the corpus from the new tag and rewrites the oracle facts, boxes and dumps
5. rewrites `corpus/colors.txt`, `MermaidConfig`, `MermaidCompatibility` and `docs/api/SplashTheMusical.txt`
6. runs the conformance tests again and rewrites `docs/parity-report.md`
7. rewrites `corpus/l4.txt`
8. writes `.artifacts/upstream/after/upstream-delta.md`

The oracle's boxes depend on the fonts Chromium falls back to, so run a bump on macOS, as CI's `l4` job does. A bump to
the version already pinned changes no file.

## Report

The delta report compares the two sides in seven sections:

- diagram types added or removed
- grammar files changed, with each diff and the STM parser whose `// Upstream grammar:` header names it
- config keys added, removed or changed
- theme variables whose computed value changed
- shapes, edge types and layout algorithms added or removed
- corpus cases added, removed, or with changed oracle facts
- the conformance results that newly fail

## Triage

Fix small failures in the bump pull request. Each failure that remains becomes an issue and a `bug` entry in its
ledger. A new diagram type gets an issue that follows the checklist in section 8 of the plan.

Merge the bump within one week of the mermaid release. The pin then never falls behind `latest`, and the parity report
shows what STM still owes.

## Pre-release canary

`.github/workflows/upstream-canary.yml` runs every Tuesday at 07:23 UTC against the highest mermaid pre-release above
`latest`, which `node tools/upstream/versions.mjs prerelease` prints. The 13 pre-releases are tagged in
`mermaid-js/mermaid-v13`, which `upstream-bump` clones because npm names it as their repository. A manual run takes a
`version` input.

The canary runs `upstream-bump`, changes no branch, and posts the delta report as a comment on the open issue titled
"mermaid.js pre-release canary". It opens that issue when none is open. When the bump fails, the comment holds the end
of its log. The canary does not block anything, and its findings are notice of the next major version, not work to do
now.

## Release

Every STM release names the mermaid.js version it was verified against, in the release notes and in
`MermaidCompatibility.MermaidJsVersion`.
