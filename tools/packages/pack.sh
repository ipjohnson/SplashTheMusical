#!/usr/bin/env bash
# Packs the src projects to .artifacts/package, then publishes samples/SplashTheMusical.Sample with native AOT from
# those packages alone and runs it. A package that misses a file or a dependency fails here, where a project
# reference would hide it.
set -euo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
cd "$root"
packages=.artifacts/package
check=.artifacts/package-check

rm -rf "$packages" "$check"
dotnet pack SplashTheMusical.slnx -c Release -warnaserror -o "$packages"

mkdir -p "$check"
cat > "$check/nuget.config" <<CONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$root/$packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="SplashTheMusical*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
CONFIG

# Its own packages folder, so a package of the same version restored earlier is not reused.
dotnet publish samples/SplashTheMusical.Sample -warnaserror \
	-p:StmFromPackages=true \
	-p:RestoreConfigFile="$check/nuget.config" \
	-p:RestorePackagesPath="$check/packages" \
	--artifacts-path "$check/build" \
	-o "$check/sample"
"$check/sample/SplashTheMusical.Sample" > /dev/null

ls "$packages"
