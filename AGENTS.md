# Agent configuration

## Project

SplashTheMusical (STM) parses, generates and renders Mermaid diagrams in .NET. The design is
[docs/mermaid-parity-plan.md](docs/mermaid-parity-plan.md). Work on a diagram type follows
[docs/adding-a-diagram-type.md](docs/adding-a-diagram-type.md), its checklist, shared files and definition of done.

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
mise run oracle-roundtrip  # fail when mermaid.js reads STM's written text of a corpus case differently, or refuses a random document
mise run colors            # rewrite corpus/colors.txt, khroma's results on mermaid's theme colours
mise run curves            # rewrite corpus/curves.txt and corpus/markers.txt, d3-shape's curves and mermaid's edge paths and markers
mise run api               # rewrite docs/api/SplashTheMusical.txt, the core's public API
mise run parity            # rewrite docs/parity-report.md from the corpus, the ledgers and corpus/l4.txt
mise run baseline          # rewrite docs/layered-layout/baseline.md, the dagre port and Mermaider's Sugiyama scored against ELK
mise run elk-survey        # rewrite docs/layered-layout/corpus-survey.md, the ELK options and graph features of the .elk.txt files
mise run screenshots <cases>  # mermaid.js and STM side by side as PNGs of the named corpus cases, written to .artifacts/screenshots
mise run raster            # L4: rasterize both SVGs of every corpus case and rewrite corpus/l4.txt
mise run conformance-aot   # run the conformance tests under JIT and native AOT, and fail when their corpus results differ
mise run fuzz-crash [sources] [seed]  # fail on a crash or missed deadline of 100,000 mutated sources per type, finds in .artifacts/fuzz/crash
mise run fuzz-diff [seconds] [seed]   # minimized sources STM and mermaid.js differ on, written to .artifacts/fuzz/differential
mise run fuzz-add [finds]             # add finds of fuzz-diff to the corpus with their oracle files
mise run bench             # STM's stages on corpus/benchmarks/cases.txt, JIT and AOT, written to .artifacts/bench
mise run bench-compare     # fail when a run is more than 10% worse than docs/benchmarks/stm.md
mise run bench-budget      # fail when STM's parse rows are slower or allocate more than docs/benchmarks/mermaider-baseline.md
mise run bench-mermaider   # rewrite docs/benchmarks/mermaider-baseline.md, the budget STM is held to
mise run bench-dagre       # rewrite docs/benchmarks/dagre.md, the dagre port on graphs of 10, 100 and 1,000 nodes
mise run size              # fail when SplashTheMusical.dll or the AOT stm grows more than 5% over docs/benchmarks/sizes.md
mise run pack              # pack the src projects to .artifacts/package, then publish and run the sample with AOT from them
mise run upstream-generate <checkout>          # rewrite src/SplashTheMusical/Config/Generated from mermaid's config schema
mise run upstream-generate <checkout> --check  # fail when the generated files differ from the committed ones
mise run upstream-fonts            # rewrite src/SplashTheMusical/Text/Generated from the fonts in tools/oracle/fonts
mise run upstream-compatibility    # rewrite src/SplashTheMusical/Generated from the mermaid version in tools/oracle/package-lock.json
mise run upstream-bump <version>   # move the oracle to another mermaid and write .artifacts/upstream/after/upstream-delta.md
```

`upstream-bump` runs `tools/upstream/bump.sh`. It clones the old and new mermaid tags into `.artifacts/upstream`, each
from the repository npm names for that version, and runs the conformance tests. It installs the new mermaid in
`tools/oracle`, re-extracts the corpus, rewrites the oracle files, `colors.txt`, `MermaidConfig`, `MermaidCompatibility`
and `docs/api/SplashTheMusical.txt`, runs the conformance tests again with `docs/parity-report.md`, `docs/layered-layout/corpus-survey.md` and `docs/layered-layout/baseline.md`, and rewrites
`corpus/l4.txt`. `DeltaReport` in `tools/SplashTheMusical.Upstream` then compares the two sides. A bump to the version already pinned
changes no file. Each parser source under `src/SplashTheMusical/Diagrams` starts with one
`// Upstream grammar: <path in mermaid-js/mermaid>` line per grammar file it implements, so the report names the parser
of a changed grammar. `GrammarHeaderTests` fails when a parser source has none.

`.github/workflows/upstream-watch.yml` runs `upstream-bump` daily when npm's `latest` mermaid is not the pin, and opens
a pull request with the delta report. `.github/workflows/upstream-canary.yml` runs it weekly against the newest
pre-release and posts the report to the issue "mermaid.js pre-release canary". `tools/upstream/versions.mjs` picks the
versions, and `node --test tools/upstream/versions.test.mjs` tests it. [docs/upstream-updates.md](docs/upstream-updates.md)
describes the process.

`SplashTheMusical`, `SplashTheMusical.Layout` and `SplashTheMusical.DependencyModule` ship as packages, at the
`VersionPrefix` and `VersionSuffix` of `Directory.Build.props`. `tools/packages/pack.sh`, which CI's `packages` job runs,
packs them and fails unless `samples/SplashTheMusical.Sample` publishes with native AOT from them alone and runs. Pushing a
tag `v<version>` of a commit on main runs `.github/workflows/release.yml`, which publishes them to nuget.org through
trusted publishing as the nuget.org user in the repository variable `NUGET_USER`. Raise the version before each release,
because nuget.org refuses a version it has.

`MermaidCompatibility.MermaidJsVersion` is generated by `upstream-compatibility`, and `MermaidCompatibilityTests` fails
when it differs from the oracle's lock file. Run it after a mermaid upgrade.

The `stm` command is `StmCommand` in `src/SplashTheMusical.Cli`, which parses its arguments by hand. CI publishes it,
the DependencyModule sample and the conformance runner with native AOT and `-warnaserror` on Linux x64, macOS arm64 and
Windows x64. The `Test` step and the native conformance runner each write the corpus results to the file
`STM_RESULTS_FILE` names, and CI fails when the two differ. CI also publishes `stm` with `-p:StmReflectionProbe=true`,
which compiles `Probe/ReflectionProbe.cs` with the analyzers off, and fails unless ILC refuses it.

Text is measured through `ITextMeasurer`. The default, `FontTableTextMeasurer`, reads the advance widths and kerning in
`src/SplashTheMusical/Text/Generated/FontTables.g.cs`, which only `upstream-fonts` writes. `FontTableWriter.Families`
lists the families and their aliases. `TextMeasurementTests` compares the measured widths with the oracle's label
boxes, and `FontTableTextMeasurerTests` fails when the generator's output differs from the committed tables.

`upstream-generate` takes a checkout of mermaid-js/mermaid at the tag of the mermaid `tools/oracle` pins. `ConfigSource`
finds the schema in packages/mermaid/src/schemas for mermaid 12 and in packages/core/schema for a mermaid-js/mermaid-v13
checkout of mermaid 13. The files in
`src/SplashTheMusical/Config/Generated` are only written by it. It also writes `ConfigKeys.g.cs` from the `name` lines of
`corpus/config.txt`, so run `mise run corpus-oracle` first after a mermaid upgrade. `DefaultConfigTests` compares the
default `MermaidConfig` with the oracle's `corpus/config.txt`. `AppliedConfigTests` compares the config
`MermaidConfig.Resolve` applies to each corpus case with the oracle's `config` facts. `ThemeTests` compares each
theme of `src/SplashTheMusical/Themes` with `corpus/themes.txt`, and the theme variables of each case that sets
`themeVariables` with the oracle's `theme-variable` facts. `StylesheetTests` compares the stylesheet
`MermaidStylesheet` builds and `SvgWriter` writes with `corpus/styles.txt`, for a quadrant chart and a flowchart in each
theme and for flowcharts with `classDef`, `style`, `class` and `linkStyle`. A layout gives the stylesheet its diagram's
rules and classes through `DiagramLayout`. mermaid.js writes `style`, `classDef` and `linkStyle` declarations as `style`
attributes, which the writer refuses, so `FlowchartStyles.ElementRules` writes them as rules keyed by an `stm-style-N`
class on the element. `FlowchartStyleTests` compares the style text of each flowchart node, cluster and edge with the
`element` lines of `corpus/styles.txt`.

Icons come from an `IIconProvider`, by default `IconRegistry.Default`, which holds mermaid's built-in
`mermaid-architecture` pack. A host registers other packs with `IconRegistry.With` and `IconPack.FromIconifyJson`, which
reads Iconify JSON with `Utf8JsonReader`. A `MermaidIcon` sanitizes its body when it is made: `IconContent` turns safe
`style` declarations into attributes and keeps only what `SvgWriter` writes. `FlowchartPictures` draws an icon node's icon
as a nested `svg`, or mermaid's unknown icon with a warning, and an image node's `img` through `SvgWriter.WriteImage`: a
`data:` PNG or SVG always, an `http` or `https` URL only with `AllowRemoteImages`, and otherwise a warning in
`RenderOptions.Diagnostics`. STM never fetches an image. `IconTests` and `ImageNodeTests` cover them.

Stylesheets are built only from the typed rules of `src/SplashTheMusical/Rendering`. `CssDeclaration` refuses a
property outside its allowlist and a value outside its grammar, so a value from the source or the config cannot hold
`;`, a brace or `url(`.

Renderers write SVG only through `SvgWriter`. It throws on an element or attribute outside `SvgValues.WriterElements`
and `SvgValues.WriterAttributes`, which are subsets of `SvgSanitizer`'s lists, and on a paint or marker value outside
the sanitizer's grammar. A test that writes a document goes through `tests/SplashTheMusical.Tests/Rendering/SvgDocuments.cs`,
which fails unless `SvgSanitizer.SanitizeRendererOutput` returns the output unchanged. The writer's lists are built from
`SvgValues.Elements` and `SvgValues.Attributes`, so a renderer that needs a new element or attribute adds it there.

The core reads XML only with `SvgMarkup` in `src/SplashTheMusical/Rendering`, which `SvgSanitizer` and `IconContent`
share. `System.Xml` doubled the native AOT `stm` and `Uri` added to it, so the core uses neither. `SvgMarkup`
accepts a subset of what `XmlReader` accepts, so the sanitizer's fuzz tests can still read its output with `XDocument`.

`tests/SplashTheMusical.Tests/Allocations.cs` asserts the exact bytes one call allocates.

`StmBenchmarks.Stages` measures parse, layout, render and write on the benchmark cases of the types in
`StmBenchmarks.Measured`, and `mise run bench` writes them. `docs/benchmarks/stm.md` holds the committed run.
`BudgetTests` fails when a committed `parse` row is slower or allocates more than Mermaider's row in
`docs/benchmarks/mermaider-baseline.md`. CI's `benchmarks` job runs both on one macOS runner, fails when STM's parse rows
are over that run's Mermaider rows, and uploads the two files as its `benchmarks` artifact. Its `budget --remeasure`
measures a row that is slower again, STM's and Mermaider's back to back for that case, stage and job through
`STM_BENCH_PARAMETERS`, and fails only when it is still slower, because a shared runner can slow one benchmark process
for its whole run. A row that allocates more fails at once. The committed files come
from that artifact, because a local machine runs other work while it measures.

`src/SplashTheMusical.Layout/Dagre` ports dagre-d3-es 7.0.14, the dagre mermaid 12.3.0 uses. Its `Graph` iterates
nodes and edges in graphlib's order, which is JavaScript's object key order: names that are array indexes, such as
`"2"`, come first in numeric order. `DagreRecords` in `tests/SplashTheMusical.Conformance` reads the oracle's
`.dagre.txt` files. `DagreRankReplayTests` replays every recorded layout and compares the ranks with the recorded
positions, and `DagreOrderReplayTests` compares the order of the nodes in each rank with them.
`DagrePositionReplayTests` runs every recorded layout, with its clusters and self edges, through all the phases and
compares the size, the node boxes, including the clusters', the edge points and the edge label positions with the recorded output, within 0.01. The expected lines
of `DagreRankTests.cs`, `DagreOrderTests.cs` and `DagrePositionTests.cs` in `tests/SplashTheMusical.Tests/Layout` are
dagre-d3-es's own output.
`JsSort` reproduces V8's `Array.prototype.sort`, which dagre's `sort` relies on when a barycenter is NaN.
Where dagre redoes work the port only updates, as in `NetworkSimplex.ExchangeEdges`, `FeasibleTree.TightTree` and
`LayerGraph.NodesByRank`, the port gives the same values, and the replay tests hold it to that. `DagreGraphs` in
`tests/SplashTheMusical.Benchmarks` makes seeded flowchart-shaped graphs of any size. `DagreLimitTests` lays out one of
5,000 nodes inside `ResourceLimits.Default.RenderDeadline`, `DagreBenchmarks` measures 10, 100 and 1,000 nodes, and
`dotnet run --project tests/SplashTheMusical.Benchmarks -c Release -- dagre-layout <nodes>` prints the time of each
phase group and a checksum of the positions, so two builds can be compared on graphs the corpus lacks.

`ElkLayoutComparer` in `tests/SplashTheMusical.Conformance` scores a `CandidateLayout` of a recorded ELK input graph
against ELK's result in the `.elk.txt` file: same layers and same order in each layer, read from the coordinates along
the layout direction, boxes overlapping by 80% with the canvas within 5%, and the same bends per edge. A layout that
has layers but no coordinates gives them in `CandidateLayout.Layers`. `ElkBaselineTests` scores the dagre port and
Mermaider's `Sugiyama`, a test-only package reference of that project, on every recording and fails when
`docs/layered-layout/baseline.md` differs. `SugiyamaOnElkInput.Commit` names the mermaider commit of the pinned package.

`src/SplashTheMusical.Layout/Paths` turns edge points into SVG paths. `CurveWriter` ports d3-shape 3.2.1's curves for
the 13 `curve` values of mermaid 12.3.0's schema, listed in `CurveNames`, and writes through `PathWriter`, which rounds
as d3-path does. `EdgePath` adds `insertEdge`'s corner fix and marker offsets. `src/SplashTheMusical.Layout/Markers`
holds mermaid's edge markers and marker ids. The rounded curve prints numbers in full, so `JsMath` reproduces V8's
`Math` on macOS arm64, fdlibm with fused multiply-adds. `CurveTests` and `EdgeMarkerTests` compare them with
`corpus/curves.txt` and `corpus/markers.txt`.

`src/SplashTheMusical/Rendering/Shapes` ports mermaid's node shapes. `NodeShapes` lists one `NodeShape` per drawing
function of the shape table, and `NodeShapes.Handler` maps every name mermaid's `shapes` map accepts. A shape takes a
`ShapeNode`, whose `IShapeLabelMeasurer` measures the label at the width the shape sets, and returns a `ShapeGeometry`:
the elements of its outline, the label's box and translate, the intersect, and the size `insertMeasuredNode` gives
dagre, which is the box of the elements and the label together. `ShapeTests` in `tests/SplashTheMusical.Conformance`
fails when a `shape` line of `corpus/shapes.txt` has no entry or a `key` line maps elsewhere, and compares the sizes
with the node sizes of the flowchart `.dagre.txt` files within 0.01.

The generation tests of the plan's section 7 need no code per type except a generator. `RoundTripTests` in
`tests/SplashTheMusical.Conformance` writes every accepted corpus case of each module of `Mermaid.CoreModules` and
compares the written text's facts, boxes and SVG with the case's. `DocumentGenerators` in
`tests/SplashTheMusical.Tests/Generation` lists one CsCheck generator per type with a writer, and
`RoundTripPropertyTests` round trips 10,000 random documents of each, comparing them with `DocumentDump`.
`RoundTripPropertyTests.EveryCoreModuleHasAGenerator` fails when a module has none. `mise run oracle-roundtrip`, which
CI's `l4` job runs, has the oracle read STM's text of every accepted case and 100 random documents per generator. A
written case whose facts differ from the committed ones is compared with the oracle's facts of the original case on the
same machine, so fonts that wrap differently off macOS do not fail it.

The fuzzing of the plan's section 7 is in `tests/SplashTheMusical.Conformance/Fuzzing`, and `FuzzRunTests` runs it when
its environment variables are set. `CrashFuzzer` feeds sources `Mutator` makes from the corpus to the parser and the
renderer under `ResourceLimits.Default`, and keeps a crash, an exception other than `MermaidParseException` and a missed
render deadline. CI's `crash-fuzz` job runs 100,000 sources per type with seed 1 on a pull request and the run id
otherwise, and a find is reproducible with `mise run fuzz-crash 100000 <seed>`. `DifferentialRunner` sends mutated
sources and sentences of the pinned tag's grammars to `tools/oracle/serve.mjs` and to STM, and `Minimizer` shrinks each
source they differ on. `tools/fuzz/differential.sh` runs it, and `.github/workflows/fuzz-differential.yml` runs that
daily on macOS and opens an issue with the finds, or says in the run's summary that it found none. `tools/oracle/add.mjs`
adds a find to the corpus. `BrokenRuleReplayTests` replays a recorded run against a flowchart parser with the thick link
rule broken. Its fixture is re-recorded with `STM_FUZZ_RECORD=1` when the mutator, the minimizer or the flowchart corpus
changes the run's sources.

`Mermaid` is the public entry point. It implements `IMermaid`, and `Mermaid.CoreModules` lists one `IDiagramModule` per
supported diagram type. `DiagramDetector` picks the type as mermaid.js's detectors do, and `DetectionTests` checks it
against the oracle's `type` fact of every accepted corpus case. `StmFactsProvider` runs STM for the conformance report,
so a new module changes the ledgers, `docs/parity-report.md` and `corpus/l4.txt`. `PublicApiTests` fails when the
public API differs from `docs/api/SplashTheMusical.txt`, which `mise run api` rewrites.

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
| `src/SplashTheMusical.DependencyModule` | Registers STM in DI: `SplashTheMusicalModule` and `AddSplashTheMusical`. Registration only. |
| `samples/SplashTheMusical.Sample` | A host that uses the module. CI publishes it with native AOT and runs it. |
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
- STM does not port ELK. The layered layout engine that reproduces mermaid.js's ELK layouts is written under
  [docs/layered-layout/clean-room.md](docs/layered-layout/clean-room.md), which lists what a session may and may not
  read. Never read ELK's Java source or any file under `node_modules/elkjs`, and never paste them into a prompt. A pull
  request that changes the engine names the papers and recordings each algorithm came from.

Tests may use reflection to check these rules.

## Conventions

- .NET 10 (`net10.0`), C# latest, nullable enabled, implicit usings, warnings as errors.
- Tabs, Allman braces, file-scoped namespaces and `var`. `.editorconfig` holds the full rules, and `mise run check`
  enforces them.
- `_camelCase` private instance fields, PascalCase static and constant fields.
- TUnit for tests, AwesomeAssertions for assertions and CsCheck for property tests, as in ipjohnson/mermaider. Both test
  projects reference VerificationGlue and VerificationGlue.Mocks, pinned in `Directory.Packages.props`.
- `tests/SplashTheMusical.Conformance` publishes with native AOT, so its checks are VerificationGlue's `Verify`, `Check`,
  `Throws` and `ThrowsAsync`, which `GlobalUsings.cs` imports from `VerificationGlue.VerifySyntax`. Their messages are
  generated at compile time and are the same under JIT and AOT.
- Write a check as `Verify(() => cases.Count == 9)`, whose failure lists the value of each part, also in a TUnit test
  body. Keep `Verify(condition)` when the condition awaits, or when the next lines rely on a null check in it, such as
  `Verify(accepted!.Accepted)`. A list prints as its type name, so a check on a list of problems joins them in an
  interpolated message: `Verify(!problems.Any(), $"{string.Join('\n', problems)}")`. Pass a message as `$"..."`, because
  a plain string binds to the condition's text. Compare lines with `LineAssert.Equal`, whose failure names the first
  differing line with both values. A test with no `await` is a `void` method.
- In test files, `Assembly` is ambiguous with TUnit's `HookType.Assembly`. Write `System.Reflection.Assembly`.
- Code derived from another project is listed in `THIRD-PARTY-NOTICES.md` with its license.
- A pull request that changes what STM draws shows it. `mise run screenshots <cases>` writes mermaid.js's raster and
  STM's side by side to `.artifacts/screenshots/<case>.png` for each named corpus case, passing or not. Add each PNG
  with the driver's `add_figure` tool and paste its `figure:v1` link under the template's Screenshots heading. Pick the
  cases the change affects, and for a change to existing output also run the task on the base branch for a before
  image. A pull request that draws nothing differently says so there.
