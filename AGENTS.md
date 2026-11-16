# Agent configuration

## Project

SplashTheMusical (STM) parses, generates and renders Mermaid diagrams in .NET. The design is
[docs/mermaid-parity-plan.md](docs/mermaid-parity-plan.md).

## Build and test

`global.json` pins the .NET 10 SDK and selects Microsoft.Testing.Platform for `dotnet test`. `mise install` provides
the SDK. The tasks are:

```bash
mise run build    # dotnet build SplashTheMusical.slnx
mise run test     # dotnet test --solution SplashTheMusical.slnx
mise run format   # dotnet format SplashTheMusical.slnx
mise run check    # fail on formatting drift
mise run ci       # what CI runs: build, test and the formatting check
mise run oracle   # mermaid.js facts for tools/oracle/cases, written to .artifacts/oracle
mise run oracle-test
mise run colors            # rewrite corpus/colors.txt, khroma's results on mermaid's theme colours
mise run parity            # rewrite docs/parity-report.md from the corpus, the ledgers and corpus/l4.txt
mise run raster            # L4: rasterize both SVGs of every corpus case and rewrite corpus/l4.txt
mise run conformance-aot   # publish the conformance tests with native AOT and run them
mise run bench             # STM's stages on corpus/benchmarks/cases.txt, JIT and AOT, written to .artifacts/bench
mise run bench-compare     # fail when a run is more than 10% worse than docs/benchmarks/stm.md
mise run bench-mermaider   # rewrite docs/benchmarks/mermaider-baseline.md, the budget STM is held to
mise run size              # fail when SplashTheMusical.dll or the AOT stm grows more than 5% over docs/benchmarks/sizes.md
mise run upstream-generate <checkout>          # rewrite src/SplashTheMusical/Config/Generated from mermaid's config schema
mise run upstream-generate <checkout> --check  # fail when the generated files differ from the committed ones
mise run upstream-fonts            # rewrite src/SplashTheMusical/Text/Generated from the fonts in tools/oracle/fonts
```

Text is measured through `ITextMeasurer`. The default, `FontTableTextMeasurer`, reads the advance widths and kerning in
`src/SplashTheMusical/Text/Generated/FontTables.g.cs`, which only `upstream-fonts` writes. `FontTableWriter.Families`
lists the families and their aliases. `TextMeasurementTests` compares the measured widths with the oracle's label
boxes, and `FontTableTextMeasurerTests` fails when the generator's output differs from the committed tables.

`upstream-generate` takes a checkout of mermaid-js/mermaid at the tag of the mermaid `tools/oracle` pins. The files in
`src/SplashTheMusical/Config/Generated` are only written by it. It also writes `ConfigKeys.g.cs` from the `name` lines of
`corpus/config.txt`, so run `mise run corpus-oracle` first after a mermaid upgrade. `DefaultConfigTests` compares the
default `MermaidConfig` with the oracle's `corpus/config.txt`. `AppliedConfigTests` compares the config
`MermaidConfig.Resolve` applies to each corpus case with the oracle's `config` facts. `ThemeTests` compares each
theme of `src/SplashTheMusical/Themes` with `corpus/themes.txt`, and the theme variables of each case that sets
`themeVariables` with the oracle's `theme-variable` facts. `StylesheetTests` compares the stylesheet
`MermaidStylesheet` builds and `SvgWriter` writes with `corpus/styles.txt`.

Stylesheets are built only from the typed rules of `src/SplashTheMusical/Rendering`. `CssDeclaration` refuses a
property outside its allowlist and a value outside its grammar, so a value from the source or the config cannot hold
`;`, a brace or `url(`.

`tests/SplashTheMusical.Tests/Allocations.cs` asserts the exact bytes one call allocates.

`mise install` also provides Node 22.23 for the oracle. `tools/oracle/README.md` describes its outputs.

Without mise:

```bash
dotnet build SplashTheMusical.slnx
dotnet test
dotnet run --project tests/SplashTheMusical.Tests   # one test project
dotnet publish src/SplashTheMusical.Cli             # native AOT build of the stm command
```

Build output goes to `.artifacts/`.

## Layout

| Project | Purpose |
|---|---|
| `src/SplashTheMusical` | Core: model, parsers, writers, config, themes, renderers. No package dependencies. |
| `src/SplashTheMusical.Layout` | Graph layout engines. No reference to the core and no knowledge of Mermaid. |
| `src/SplashTheMusical.Cli` | The `stm` command, published with native AOT. |
| `tests/SplashTheMusical.Tests` | Unit tests. |
| `tests/SplashTheMusical.Conformance` | The harness that compares STM with the mermaid.js oracle. |
| `tests/SplashTheMusical.Benchmarks` | BenchmarkDotNet. |
| `tools/SplashTheMusical.Upstream` | Generates code: the `MermaidConfig` classes from a mermaid checkout's `config.schema.yaml`, and the font width tables from font files. |
| `tools/oracle` | Node: runs the pinned mermaid.js in headless Chromium and writes the facts STM is compared against. |

## Rules

These come from sections 3 and 6 of the plan. The build and the tests enforce what they can.

- The core has no package dependencies. `src/SplashTheMusical/SplashTheMusical.csproj` has no `PackageReference`.
- The layout project does not reference the core.
- Every project under `src` sets `IsAotCompatible` to `true` through `Directory.Build.props`. Trim, AOT and
  single-file warnings are errors.
- No regex. Parsers lex spans in one pass and do not split the source into line strings.
- No reflection, no `dynamic`, no `Activator` and no runtime code generation.
- No global mutable state. Lookup tables are static read-only data, and diagram modules are listed explicitly.
- Package versions live only in `Directory.Packages.props`. A `PackageReference` never carries a `Version`.

Tests may use reflection to check these rules.

## Conventions

- .NET 10 (`net10.0`), C# latest, nullable enabled, implicit usings, warnings as errors.
- Tabs, Allman braces, file-scoped namespaces and `var`. `.editorconfig` holds the full rules, and `mise run check`
  enforces them.
- `_camelCase` private instance fields, PascalCase static and constant fields.
- TUnit for tests, AwesomeAssertions for assertions and CsCheck for property tests, as in ipjohnson/mermaider.
- `tests/SplashTheMusical.Conformance` publishes with native AOT, so it uses TUnit's assertions instead of
  AwesomeAssertions, and not TUnit's collection `IsEquivalentTo`, which needs reflection.
- In test files, `Assembly` is ambiguous with TUnit's `HookType.Assembly`. Write `System.Reflection.Assembly`.
- Code derived from another project is listed in `THIRD-PARTY-NOTICES.md` with its license.
