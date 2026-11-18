#!/usr/bin/env bash
# Runs the differential fuzzer of the parity plan's section 7: sources mutated from the corpus and sentences of the
# pinned tag's grammars go through mermaid.js and STM, and each source on which they differ is minimized.
#
#   tools/fuzz/differential.sh [seconds per type] [seed]        mise run fuzz-diff [seconds] [seed]
#
# It installs the oracle, clones mermaid at the tag tools/oracle pins into .artifacts/fuzz/mermaid@<version> unless
# STM_FUZZ_MERMAID names a checkout, and runs FuzzRunTests.DifferentialFuzzesWhenAsked. The run writes report.md, finds/
# and original/ to STM_FUZZ_DIR, .artifacts/fuzz/differential when it is not set. STM_FUZZ_TYPES limits the run to a
# comma-separated list of corpus folders. mise run fuzz-add adds a file of finds/ to the corpus.
set -Eeuo pipefail
trap 'echo "fuzz-diff failed at tools/fuzz/differential.sh:$LINENO: $BASH_COMMAND" >&2' ERR

repo="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$repo"
seconds="${1:-60}"
seed="${2:-1}"

test -d tools/oracle/node_modules || npm ci --prefix tools/oracle --no-audit --no-fund

if [ -z "${STM_FUZZ_MERMAID:-}" ]; then
	version="$(node tools/upstream/versions.mjs pinned)"
	STM_FUZZ_MERMAID="$repo/.artifacts/fuzz/mermaid@$version"
	if [ ! -d "$STM_FUZZ_MERMAID" ]; then
		# The repository npm names for the version, because the 13 pre-releases are tagged in mermaid-js/mermaid-v13.
		url="$(npm view "mermaid@$version" repository.url)"
		url="${url#git+}"
		git -c advice.detachedHead=false clone --quiet --depth 1 --branch "mermaid@$version" "${url%.git}" "$STM_FUZZ_MERMAID"
	fi
fi

export STM_FUZZ_MERMAID
export STM_FUZZ_DIFF_SECONDS="$seconds"
export STM_FUZZ_SEED="$seed"
export STM_FUZZ_DIR="${STM_FUZZ_DIR:-$repo/.artifacts/fuzz/differential}"
echo "differential fuzzing for ${seconds}s per type with seed $seed, grammars from $STM_FUZZ_MERMAID, finds to $STM_FUZZ_DIR"
dotnet run --project tests/SplashTheMusical.Conformance -c Release -- \
	--treenode-filter "/*/*/FuzzRunTests/DifferentialFuzzesWhenAsked" --output Detailed
