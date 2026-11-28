#!/usr/bin/env bash
# Prints the size of the core assembly, release build, and of the native AOT stm as a markdown table.
#
#   tools/benchmarks/sizes.sh        mise run size
#
# It reads what `dotnet build src/SplashTheMusical -c Release` and `dotnet publish src/SplashTheMusical.Cli` write.
# Nothing compares the sizes with a committed value, because the binaries grow with each feature. CI writes the table
# to the job summary of each runner of its build job.
set -euo pipefail
cd "$(dirname "$0")/../.."

dll=.artifacts/bin/SplashTheMusical/release/SplashTheMusical.dll
cli=.artifacts/publish/SplashTheMusical.Cli/release/stm
if [ -f "$cli.exe" ]; then
	cli="$cli.exe"
fi

for file in "$dll" "$cli"; do
	if [ ! -f "$file" ]; then
		echo "$file does not exist. Build the core in release and publish stm first, as mise run size does" >&2
		exit 1
	fi
done

rid="$(dotnet --info | awk '$1 == "RID:" { print $2; exit }')"
bytes() { wc -c <"$1" | tr -d ' \r'; }

echo "| File | RID | Bytes |"
echo "|---|---|---:|"
echo "| SplashTheMusical.dll | any | $(bytes "$dll") |"
echo "| $(basename "$cli") | $rid | $(bytes "$cli") |"
