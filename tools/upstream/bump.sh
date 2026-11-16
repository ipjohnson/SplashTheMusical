#!/usr/bin/env bash
# Moves the oracle to another mermaid version and writes the upstream delta report.
#
#   tools/upstream/bump.sh <version>        mise run upstream-bump <version>
#
# It records the corpus and the conformance results, installs mermaid <version> in tools/oracle, re-extracts the
# corpus from the tag mermaid@<version>, rewrites the oracle files, colors.txt, MermaidConfig, MermaidCompatibility,
# docs/api/SplashTheMusical.txt and docs/parity-report.md, runs the conformance tests again, rewrites corpus/l4.txt, and
# writes .artifacts/upstream/after/upstream-delta.md. Failing conformance tests do not stop it, because the report lists
# them. The checkouts of both tags are kept in .artifacts/upstream/mermaid@<version>. Each is cloned from the repository
# npm names for that version, because the 13 pre-releases are tagged in mermaid-js/mermaid-v13.
set -euo pipefail

if [ $# -ne 1 ]; then
	echo "usage: tools/upstream/bump.sh <mermaid version>" >&2
	exit 2
fi

version="$1"
repo="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$repo"
work="$repo/.artifacts/upstream"
old="$(node -p "require('./tools/oracle/package.json').devDependencies.mermaid")"

checkout() {
	local dir="$work/mermaid@$1"
	if [ ! -d "$dir" ]; then
		local url
		url="$(npm view "mermaid@$1" repository.url)"
		url="${url#git+}"
		git -c advice.detachedHead=false clone --quiet --depth 1 --branch "mermaid@$1" "${url%.git}" "$dir"
	fi
}

# Runs the conformance tests and keeps their corpus results, TRX report and log in $1.
conformance() {
	echo "conformance tests, results in $1"
	if ! STM_RESULTS_FILE="$1/results.txt" dotnet run --project tests/SplashTheMusical.Conformance -- \
		--report-trx --report-trx-filename "$1/results.trx" >"$1/conformance.log" 2>&1; then
		echo "some conformance tests failed, see $1/conformance.log"
	fi
}

rm -rf "$work/before" "$work/after"
mkdir -p "$work/before" "$work/after"

echo "mermaid $old to $version"
checkout "$old"
checkout "$version"
cp -R corpus "$work/before/corpus"
conformance "$work/before"

npm install --prefix tools/oracle --save-exact --no-audit --no-fund "mermaid@$version"
node tools/corpus/corpus.mjs "$work/mermaid@$version"
node tools/oracle/facts.mjs corpus corpus .artifacts/corpus/svg
node tools/oracle/colors.mjs corpus/colors.txt
dotnet run --project tools/SplashTheMusical.Upstream -- config "$work/mermaid@$version"
dotnet run --project tools/SplashTheMusical.Upstream -- compatibility
STM_WRITE_API=1 dotnet run --project tests/SplashTheMusical.Tests -- --treenode-filter "/*/*/PublicApiTests/*" >/dev/null

STM_WRITE_REPORT=1 conformance "$work/after"
STM_SVG_DIR="$repo/.artifacts/stm/svg" dotnet run --project tests/SplashTheMusical.Conformance -- \
	--treenode-filter "/*/*/StmSvgsTests/*" >/dev/null
node tools/oracle/raster.mjs corpus .artifacts/corpus/svg .artifacts/stm/svg corpus/l4.txt .artifacts/raster .artifacts/corpus/raster
dotnet run --project tools/SplashTheMusical.Upstream -- delta "$work/mermaid@$old" "$work/mermaid@$version" "$work/before" "$work/after"
