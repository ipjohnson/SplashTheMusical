# Adding a diagram type

Every diagram type issue follows this checklist and this definition of done. They come from the flowchart work of
ipjohnson/SplashTheMusical#21, and the flowchart files are the worked example throughout. `info` is the smallest
complete module, in `src/SplashTheMusical/Diagrams/Info`.

[The parity plan](mermaid-parity-plan.md) is the design. Sections 3, 6 and 7 hold the rules this playbook applies.
`<Type>` below stands for the type's class prefix, such as `State`, and `<type>` for its corpus folder, such as
`state`. A path with either placeholder is one you create. Every other path exists in this repository, except paths
under `packages/` and upstream file names such as `flow.jison`, which are in mermaid-js/mermaid.

## Before you start

- Read the type's rows in [upstream-map.md](upstream-map.md): the detector id, the grammar files, the renderer, the
  layout, the label mode and the default look. Read the upstream code at the version
  `src/SplashTheMusical/Generated/MermaidCompatibility.g.cs` names.
- Read the type's extractor in `tools/oracle/extract.mjs` and its rows in the tables of `tools/oracle/README.md`. They
  say which facts and boxes STM has to produce.
- Read the type's row in `docs/parity-report.md`, its ledger in `corpus/ledger` and its cases in `corpus/<type>`.
  Before the type is supported, its ledger lists every case under `bug #13` at every level.
- Check that the type does not need a shared part that does not exist yet, such as a layout port (#23, #24, #25), the
  icons (#22) or a look (#61, #62). A missing shared part is its own step, done before the type's render step.

## Where each part lives

Each type gets a folder under `src/SplashTheMusical/Diagrams`, and its namespace is
`SplashTheMusical.Diagrams.<Type>`.

| Part | Flowchart | What it does |
|---|---|---|
| Document | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartDocument.cs` | Derives from `MermaidDocument`. Plain and mutable, with `List<T>` properties and `init`, so object initializers build it. It keeps what the source said: shape names as written, edge types and lengths, statement order where order changes the result |
| Lexer | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartLexer.cs` | A `ref struct` over `ReadOnlySpan<char>` that writes struct tokens into a `PooledList` |
| Parser | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartParser.cs` | Recursive descent over the tokens, with the actions of the upstream db file. Returns `null` with a diagnostic on bad input |
| Writer | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartWriter.cs` | The inverse of the parser. Writes one canonical form and quotes or escapes any model value so it survives a round trip |
| Builder | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartBuilder.cs` | A short fluent builder for the common case |
| Module | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartModule.cs` | Derives from `DiagramModule<TDocument, TLayout>` in `src/SplashTheMusical/Diagrams/DiagramModule.cs`: the detector, and calls to the parser, the writer, the layout step and the renderer |
| Layout result | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartLayout.cs` | Derives from `DiagramLayout`. Carries the canvas, the places of the parts, and the type's stylesheet rules, classes and element rules |
| Layout data | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartData.cs` | The port of the db's `getData`: the nodes and edges mermaid.js hands to its layout |
| Measuring | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartMeasure.cs`, `src/SplashTheMusical/Diagrams/Flowchart/FlowchartLabelText.cs` | Label sizes through `ITextMeasurer`, as mermaid.js measures HTML and SVG labels |
| Layout step | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartDagre.cs` | Mermaid's layout code around the engine: it builds the dagre input, calls `DagreLayout.Run` in `src/SplashTheMusical.Layout/Dagre/DagreLayout.cs`, and places clusters, edges and labels |
| Renderer | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartRenderer.cs` | Draws the layout through `SvgWriter`, with mermaid.js's element ids and class names |
| Styles | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartStyles.cs` | The type's `styles` function as typed `CssRule`s, and `classDef`, `style` and `linkStyle` as rules on `stm-style-N` classes |
| Labels | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartLabels.cs`, `src/SplashTheMusical/Diagrams/Flowchart/FlowchartText.cs` | Markdown strings, entity codes and line breaks in labels |
| Shapes | `src/SplashTheMusical/Diagrams/Flowchart/FlowchartShapes.cs` over `src/SplashTheMusical/Rendering/Shapes/NodeShapes.cs` | Which shape names the type accepts and which library shape each node draws |
| Registration | `src/SplashTheMusical/Mermaid.cs` | One instance in `Mermaid.CoreModules` |

Reuse what is shared rather than copy it:

- Lexing and parsing: `src/SplashTheMusical/Parsing/SourceCursor.cs`, `src/SplashTheMusical/Parsing/PooledList.cs`,
  `src/SplashTheMusical/Parsing/IdTable.cs`, `src/SplashTheMusical/Parsing/PooledStringTable.cs`,
  `src/SplashTheMusical/Parsing/EntityCodes.cs`, `src/SplashTheMusical/Parsing/AccessibilityReader.cs`,
  `src/SplashTheMusical/Parsing/ResourceGuard.cs` and `src/SplashTheMusical/Parsing/YamlReader.cs`. The caller has
  read the front matter and directives already, so a parser starts at `DiagramParseContext.BodyStart`.
- Writing: `src/SplashTheMusical/Writing/PreambleWriter.cs` writes the front matter and directives. A module writes
  only the body.
- Rendering: `src/SplashTheMusical/Rendering/SvgWriter.cs`, `src/SplashTheMusical/Rendering/MermaidStylesheet.cs`,
  `src/SplashTheMusical/Rendering/CssDeclaration.cs` and the shape library in `src/SplashTheMusical/Rendering/Shapes`.
- Layout: the dagre port in `src/SplashTheMusical.Layout/Dagre`, the curves in
  `src/SplashTheMusical.Layout/Paths/CurveWriter.cs` and the markers in
  `src/SplashTheMusical.Layout/Markers/EdgeMarkers.cs`. Types that compute positions in their renderer upstream, such
  as pie or gantt, port that arithmetic into their layout step and use no engine.

## Following the grammar

Each parser source starts with one line per upstream grammar file it implements, with the path in mermaid-js/mermaid:

```csharp
// Upstream grammar: packages/mermaid/src/diagrams/flowchart/parser/flow.jison
```

`GrammarHeaderTests` fails when a `*Parser.cs` under `src/SplashTheMusical/Diagrams` has none, and the delta report of
`mise run upstream-bump` names the C# file of each grammar that changed. Put the same line on a lexer that implements
the grammar's lexer rules, as `FlowchartLexer.cs` does. The class comment quotes the grammar rules it implements.

A jison grammar (`packages/mermaid/src/diagrams/*/parser/*.jison`), as `FlowchartLexer.cs` and `FlowchartParser.cs`
follow `flow.jison`:

- jison tries the rules of the current start condition in the order the grammar lists them and takes the first that
  matches. Each start condition is a method of the lexer that checks its rules in that order, and `begin` and
  `popState` push and pop a state.
- jison adds `\b` after a rule that ends in a letter or digit, so a keyword does not match the start of a longer word.
- The grammar's actions call the db, such as `flowDb.ts`. The parser does what the db does, including its quirks,
  because the oracle facts come from the db.

A langium grammar (`packages/parser/src/language/*/*.langium`), as `InfoParser.cs` follows `info.langium`:

- Name the shared fragments too, such as `common/common.langium`, with one header line each.
- langium lexes the whole body first. A character no token matches is a lexer error that skips to the next place a
  token matches. Keywords match only where the type's `TokenBuilder` lets them.
- Errors read as `MermaidParseError` writes them.

The default is to accept and refuse exactly what mermaid.js does. L0 compares acceptance on every corpus case,
including the ones mermaid.js refuses.

## Parser rules

These come from section 6 of the plan. The flowchart parser is the reference.

- **No regex.** `AssemblyReferenceTests.CoreDoesNotReferenceRegularExpressions` fails on any `Regex` in the core.
- **Spans, one pass.** The lexer walks the source as a span and never splits it into lines or trims into new strings.
  It may lex the whole input before parsing starts, as `FlowchartLexer` does, because the tokens do not depend on the
  parser. A token is a kind, a start and a length.
- **No backtracking.** The parser is recursive descent and never re-lexes. It decides on the next token, and on the
  one after it only where the grammar needs two, as flowchart's `((` and `{{` do. It may move back over tokens it has
  only looked at, as flowchart's `&` chain does, but never after it has built model state. A compound token such as a
  flowchart edge gets a dedicated scanner instead of tried rules.
- **The model is the only allocation.** Scratch space is pooled, through `PooledList` and `ArrayPool`. Ids are interned
  through `IdTable`, so each distinct id allocates one string. A label becomes a string once, when it is stored.
  `FlowchartAllocationTests` proves it with `tests/SplashTheMusical.Tests/Allocations.cs`: parsing allocates exactly
  the bytes building the same model does.
- **No exceptions on the parse path.** Errors are diagnostics with a line and a column. `ResourceGuard` checks the
  limits per statement.
- **Static data.** Lookup tables are `static readonly` `FrozenSet` or `FrozenDictionary` values, looked up from a span
  with `GetAlternateLookup<ReadOnlySpan<char>>`. Keywords are a `switch` over the span.
- **AOT.** No reflection, no `dynamic`, no `Activator`. Parser classes are sealed or static and the lexer is a `ref
  struct`.

## Oracle facts

`tools/oracle/extract.mjs` already has an extractor for every type, and the corpus holds its facts, boxes and, for the
dagre types, dagre files. Most types need no oracle change. STM's side is a branch in
`tests/SplashTheMusical.Conformance/StmFactsProvider.cs` that writes the same fact lines from the document and the
same box kinds from the layout. Put the type's code in its own file, such as
`tests/SplashTheMusical.Conformance/<Type>Facts.cs`, and add only the `else if` branch to `StmFactsProvider.Facts`.
`StmFactsProvider.Flowchart` and `StmFactsProvider.FlowchartBoxes` are the example.

When the extractor is wrong or a comparison needs a fact it does not write:

1. Change only the type's function in `tools/oracle/extract.mjs` and its rows in `tools/oracle/README.md`.
2. `mise run oracle` writes the facts of `tools/oracle/cases`, one case per type, to the build output for a quick
   look. `mise run oracle-test` runs the oracle's own tests.
3. `mise run corpus-oracle` rewrites the facts, boxes and dagre files of the whole corpus. Only the type's folder
   should change. A change elsewhere means a shared helper of the extractor changed, and belongs in its own pull
   request.

A new hand-written case goes in `corpus/<type>` as `corpus/README.md` describes, followed by `mise run corpus-oracle`.

`OverlapLint.Roles` in `tests/SplashTheMusical.Conformance/OverlapLint.cs` has a row for every type, which gives each
box kind a role for L3's overlap check. Correct the type's row if its box kinds differ.

## Ledger and tolerances

- `corpus/ledger/<type>.txt` lists the cases that fail L0 to L3. `corpus/ledger/README.md` has the format. A case that
  starts passing fails `LedgerTests` until its entry goes, so rewrite the ledger with
  `STM_WRITE_LEDGER='#N' dotnet run --project tests/SplashTheMusical.Conformance -- --treenode-filter "/*/*/LedgerTests/*"`
  and review the diff. Every entry that stays is `by-design`, `out-of-scope`, or `bug` with an open issue. The count
  in `corpus/ledger/bugs.txt` may only go down.
- `corpus/tolerances/<type>.json` holds the L3 and L4 thresholds. `corpus/tolerances/README.md` has the format. Start
  from 0.05, 0.8 and 0.9 and tighten when the type allows it. Looser values need a `reason`.
- L4 outcomes are in `corpus/l4.txt`, which `mise run raster` rewrites with Node and Chromium. A full run takes about
  25 minutes. CI's `l4` job fails when the committed file is stale. The ledger does not hold L4: a case that fails L4
  stays `fail` in `corpus/l4.txt`, and the pull request names the open issue that covers it.

## Tests and benchmarks

- **Unit tests** in `tests/SplashTheMusical.Tests/Diagrams`: parser, writer, builder, layout and render tests, as the
  flowchart tests there, such as `tests/SplashTheMusical.Tests/Diagrams/FlowchartParserTests.cs`. A test that writes SVG goes through
  `tests/SplashTheMusical.Tests/Rendering/SvgDocuments.cs`.
- **Round trip.** `RoundTripTests` in `tests/SplashTheMusical.Conformance/RoundTripTests.cs` checks every module of
  `Mermaid.CoreModules` with no code of the type's own: for every accepted case of the type it writes the parsed
  document, and the text must parse, write back to the same text, and give the same document preamble, the same
  `StmFactsProvider` facts and boxes and the same SVG. It fails when a module has no accepted case. A type may add a
  stricter test of its own model, as `tests/SplashTheMusical.Conformance/FlowchartRoundTripTests.cs` does.
- **Generator.** Add `tests/SplashTheMusical.Tests/Generation/<Type>Generator.cs`, a static class with a
  `Gen<<Type>Document> Document` that builds random documents the parser can give, and one line for it in
  `DocumentGenerators.All` in `tests/SplashTheMusical.Tests/Generation/DocumentGenerators.cs`. Then:
  - `RoundTripPropertyTests.RandomDocumentsRoundTrip` writes 10,000 of them in the default test run and parses each
    back. `DocumentDump` compares the documents by reflection, so the type needs no dump.
  - `RoundTripPropertyTests.EveryCoreModuleHasAGenerator` fails until the type's documents come from a generator.
  - `OracleSampleTests` writes 100 of them for the oracle round trip below, and mermaid.js must accept each.

  `FlowchartGenerator.cs` and `InfoGenerator.cs` are the examples. Use `GenText` for labels and titles: it has quotes,
  brackets, entity codes, line breaks and non-ASCII text. Generate only what a parse gives, such as text the grammar's
  tokens can hold and ids the db generates, and leave out what mermaid.js refuses. A coverage test, as
  `FlowchartGeneratorTests.cs` is, shows the generator reaches every shape, edge type, statement and label type the
  type has. When the property fails, CsCheck prints the seed and the written text. Fix the writer or the parser,
  and add the shrunk text to `corpus/<type>` as a hand-written case, followed by `mise run corpus-oracle`. Set
  `CsCheck_Iter` to run more documents, such as 300000, and `CsCheck_Seed` to repeat a failure.
- **Oracle round trip.** `mise run oracle-roundtrip` writes STM's text of every accepted case of a supported type and
  the random samples to `.artifacts/roundtrip`, runs the oracle on them, and fails when mermaid.js's facts of a written
  case differ from the case's or it refuses a random document. CI's `l4` job runs it, so it needs no step of the
  type's own. The committed facts come from macOS, and on Linux two flowchart cases with `htmlLabels: false` wrap
  their SVG text at other places. So when a written case's facts differ from the committed ones, the task copies the
  original case to `.artifacts/roundtrip/original`, runs the oracle on it on the same machine, and compares with those
  facts instead. It passes on Linux and macOS alike.
- **Detection.** `DiagramDetector` already returns every detector id mermaid.js registers. `DetectionTests` checks it
  against the oracle's `type` fact of every accepted case.
- **Public API.** `mise run api` rewrites `docs/api/SplashTheMusical.txt` after the type adds public types.
- **Benchmarks.** `corpus/benchmarks/cases.txt` already names a small, a medium and a large case of every type. Add
  the type's corpus folder to `StmBenchmarks.Measured` in `tests/SplashTheMusical.Benchmarks/StmBenchmarks.cs`, run
  `mise run bench`, and commit `docs/benchmarks/stm.md`. `BudgetTests` fails when a parse row is slower or allocates
  more than the type's row in `docs/benchmarks/mermaider-baseline.md`. Layout, render and write are measured but not
  gated. Flowchart's render is over Mermaider's (#158).
- **Size.** Code in the core grows `SplashTheMusical.dll`. `mise run size -- --accept` rewrites
  `docs/benchmarks/sizes.md`, and CI fails at more than 5% growth over it.
- **AOT.** `mise run conformance-aot` runs the conformance tests under JIT and native AOT and fails when the results
  differ.
- `mise run ci` before every commit: build, test and the formatting check.

## Order of work

A type is three steps, each its own pull request, as the state Task files them (#67, #68, #69):

1. **Model, parser and writer, to L0 and L1.** Document, lexer, parser, writer, builder, module registration with a
   layout and renderer that draw nothing yet, the generator and the allocation test. Done when L0 is 100%, every L1
   difference is in the ledger, and the round trip, the property tests and the oracle round trip pass.
2. **Layout and renderer, to L2.** Layout data, measuring, the layout step, the renderer and the styles, and the
   facts and boxes in `StmFactsProvider`. Done when every L2 difference is in the ledger.
3. **Parity closeout: L3 and L4.** Fix what the boxes and rasters show, set the tolerances, rewrite the ledger and
   `corpus/l4.txt`, commit the benchmarks and check native AOT. Done when the definition of done below holds.

Flowchart took six steps (#136 to #141), because it also built the shape library, the styling and the label measuring
every later type reuses. A type that needs a new shared part adds a step for it before step 2.

## Definition of done

Copy this into the type's issue. It is ipjohnson/SplashTheMusical#21's "Done when", with the type in place of
flowchart.

```markdown
## Done when

- L0: the parity report shows 100% acceptance parity for the <type> cases of the corpus.
- L1 and L2: every remaining <type> difference is in `corpus/ledger/<type>.txt` under `by-design` or `out-of-scope`, or under `bug` with an open issue.
- L3 and L4: the <type> cases meet the thresholds of `corpus/tolerances/<type>.json`, which are no looser than the starting values of the plan.
- For every accepted <type> case, parsing, writing and parsing again gives an equal model, and mermaid.js reads the written text as it reads the case.
- 10,000 random <type> documents round trip in the default test run, and mermaid.js accepts a sample of them.
- The shape library has one entry per shape the <type> diagram draws from the oracle's shape table, and a test fails when one is missing.
- The <type> cases pass on the native AOT conformance runner.
- Benchmarks for a small, a medium and a large <type> are committed, and parse time and allocation are no worse than the Mermaider baseline.
- The <type> parser uses no `Regex` and allocates only the model, which an allocation test shows.
```

How each line is checked, and how flowchart met it:

| Line | Check | Flowchart |
|---|---|---|
| L0 | The type's L0 column in `docs/parity-report.md`, which `mise run parity` writes | 100.0%, 891 of 891 |
| L1 and L2 | `LedgerTests` | Four L1 entries, the KaTeX cases under `bug #63`. No L2 entries |
| L3 and L4 | `LedgerTests` for L3, `corpus/l4.txt` for L4, `ToleranceTests` for the thresholds. A case under the threshold is a `bug` entry at L3 or a `fail` in `corpus/l4.txt`, each covered by an open issue | The 649 cases that mermaid.js draws with ELK and that fail L3 are `bug #25`, and most of them fail L4. One label precision case is `bug #152`, and long labels that fail L4 are #157 |
| Round trip | `RoundTripTests`, and the oracle round trip of CI's `l4` job | 874 cases, and `FlowchartRoundTripTests` compares the whole model |
| Random documents | `RoundTripPropertyTests`, and the oracle round trip's random sample | `FlowchartGenerator`, with every shape and edge type, which `FlowchartGeneratorTests` shows |
| Shape library | `ShapeTests` in `tests/SplashTheMusical.Conformance/ShapeTests.cs` | Every shape of `corpus/shapes.txt`. A type that draws no node shapes marks this line not applicable |
| Native AOT | CI's AOT runner and `mise run conformance-aot` | Same results under JIT and AOT |
| Benchmarks | `docs/benchmarks/stm.md` and `BudgetTests` | Small, medium and large parse rows under the Mermaider baseline |
| No regex, model only | `AssemblyReferenceTests` and the type's allocation test | `FlowchartAllocationTests` |

## Shared files

Parallel type issues touch these files. Keep each change to the lines of your type, and regenerate generated files
after rebasing instead of merging them by hand.

| File | Change | How to keep it small |
|---|---|---|
| `src/SplashTheMusical/Mermaid.cs` | One module in `CoreModules` | One module per line, yours appended last |
| `tests/SplashTheMusical.Conformance/StmFactsProvider.cs` | One `else if` branch in `Facts` | The type's fact and box code in its own file |
| `src/SplashTheMusical/Diagrams/DiagramDetector.cs` | Usually none. The `switch` detects every type already | Change it only to fix a detection `DetectionTests` shows wrong for your type |
| `tests/SplashTheMusical.Conformance/OverlapLint.cs` | The type's row of `Roles`, if wrong | Only your row |
| `tests/SplashTheMusical.Benchmarks/StmBenchmarks.cs` | The type's folder in `Measured` | One name |
| `tests/SplashTheMusical.Tests/Generation/DocumentGenerators.cs` | The type's generator in `All` | One generator per line, yours appended last |
| `tools/oracle/extract.mjs`, `tools/oracle/README.md` | The type's extractor and rows, if wrong | Only your function and rows. A shared helper is its own pull request |
| `corpus/ledger/bugs.txt` | The type's count | Rewrite it with the ledger after rebasing |
| `src/SplashTheMusical/Rendering/SvgValues.cs` | An element or attribute the renderer needs | Only what the renderer writes |
| `THIRD-PARTY-NOTICES.md` | A notice for ported code | One section |
| `docs/parity-report.md` | Generated | `mise run parity` after rebasing |
| `corpus/l4.txt` | Generated | `mise run raster` after rebasing |
| `docs/api/SplashTheMusical.txt` | Generated | `mise run api` after rebasing |
| `docs/benchmarks/stm.md` | Generated, the whole file per run | `mise run bench` after rebasing, on the machine the file names |
| `docs/benchmarks/sizes.md` | Generated | `mise run size -- --accept` after rebasing |
| `AGENTS.md` | Only a rule other work must follow | One paragraph |

Changing shared code, such as the shape library, the parsing helpers, `SvgWriter` or `SplashTheMusical.Layout`, is a
step of its own, merged before the type that needs it, so two types do not change it differently at once.

## What the flowchart work changed in the design

The plan now says each of these. They are listed here so a type issue does not follow the old text.

- **Layout engine.** Flowchart lays out every case with the dagre port and ignores `layout`. mermaid.js 12.3.0 draws
  most flowchart cases with ELK, so those are `bug #25` entries at L3 until the `elk` engine lands. The oracle renders
  the dagre types a second time with `layout: dagre` to record dagre's inputs and outputs.
- **Layout step.** The module's layout step calls the dagre port directly. `ILayoutEngine` is the extension point for
  a host's engine, and no core type uses it yet.
- **Ids.** The parser interns ids, so each distinct id allocates one string, but the model stores ids as strings so
  user code can build documents. Layout data is built from the model.
- **Comments.** The model does not keep comments. The writer's canonical form has none.
- **Lookahead.** The lexer lexes the whole input first. The parser looks two tokens ahead in a few rules and moves
  back over tokens it has only looked at, never re-lexing and never undoing model state.
- **Static tables.** Tables are `FrozenSet` and `FrozenDictionary` values built once per process. Only the generated
  font tables are `ReadOnlySpan<byte>` data.
- **Stylesheets.** The stylesheet is built per render, not cached. Source styles are rules on `stm-style-N` classes,
  because `SvgWriter` writes no `style` attribute.
- **L4.** The ledger holds L0 to L3. L4 outcomes are committed in `corpus/l4.txt`.
- **Budget.** `BudgetTests` gates parse time and allocation against Mermaider. Layout, render and write are measured
  only.
- **Generation tests.** One round trip test covers every module, comparing facts, boxes and SVG rather than a model
  dump of each type. The property tests need a generator per type, and the oracle round trip runs in the `l4` job.
