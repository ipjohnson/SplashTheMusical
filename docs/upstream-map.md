# Upstream map: mermaid 12.3.0

This file records what mermaid.js does at the version STM targets. It was read at tag `mermaid@12.3.0`, commit
`2086904e76c692331e34a45d7cc3bc30860d040e` of mermaid-js/mermaid, committed on 2026-11-03. Dependencies were read from
the npm tarballs at the versions `pnpm-lock.yaml` pins at that commit.

Paths in the tables are relative to these roots unless they say otherwise:

- Diagram files: `packages/mermaid/src/diagrams/<folder>/`.
- Langium grammars: `packages/parser/src/language/`.
- Layouts: `packages/mermaid/src/rendering-util/layout-algorithms/`.

## Registration

`packages/mermaid/src/diagram-api/diagram-orchestration.ts` registers the types. Detectors run in this order and the
first match wins:

1. `error` and `---`, registered directly.
2. `flowchart-elk`, `mindmap` and `architecture`. These are registered only when `injected.includeLargeFeatures` is
   true, which it is in the standard build and not in the `tiny` build.
3. `agentflow`, `c4`, `kanban`, `classDiagram`, `er`, `gantt`, `info`, `pie`, `requirement`, `sequence`, `swimlane`,
   `flowchart-v2`, `timeline`, `gitGraph`, `stateDiagram`, `journey`, `quadrantChart`, `sankey`, `packet`, `xychart`,
   `block`, `eventmodeling`, `treeView`, `radar`, `ishikawa`, `treemap`, `railroad`, `railroadEbnf`, `railroadAbnf`,
   `railroadPeg`, `venn`, `wardley`, `cynefin` and `usecase`.

That is 37 diagram ids over 33 diagram folders. `railroad` has four ids, one per input notation. `flowchart` has two,
and `swimlane` reuses the flowchart parser, db and renderer. ZenUML is not registered by the core. It ships as
`packages/mermaid-zenuml` through `registerExternalDiagrams`.

Every detector is a regular expression tested against the text after front matter and directives are removed. Most
are prefix matches with no word boundary, so `graphs` is detected as a flowchart. The `c4` expression is
`/^\s*C4Context|C4Container|C4Component|C4Dynamic|C4Deployment/`. Only `C4Context` is anchored, so the other four
keywords match anywhere in the text.

The parsers break down as follows:

- 19 jison grammars under `diagrams/*/parser/`.
- 18 langium files under `packages/parser/src/language`. 15 are diagram grammars. `common/common.langium`,
  `architecture/arch.langium` and `gitGraph/reference.langium` are shared fragments.
- `usecase` has a hand-written chevrotain lexer, parser and visitor.
- `swimlane` has no parser of its own.

## Diagram types

"Unified" in the layout columns means the renderer hands `LayoutData` to `rendering-util/render.ts`, which accepts any
registered layout name. At 12.3.0 those names are `dagre`, `swimlane`, `cose-bilkent`, `elk`, `elk.stress`,
`elk.force`, `elk.mrtree`, `elk.sporeOverlap`, `elk.box` and `elk.rectpacking`. `tidy-tree` is added when the host
registers `@mermaid-js/layout-tidy-tree`. An unknown name falls back to `dagre`, or to the fallback the renderer
names. "Own" means the renderer computes positions itself and ignores `layout`.

The top-level `layout` default in `schemas/config.schema.yaml` is `elk`. `config.ts` resolves `theme`, `look` and
`layout` per diagram: a value in the diagram's config section wins over the top-level value of the same layer. So
every unified renderer uses ELK by default, unless its section says otherwise or the build is `tiny`, where ELK falls
back to `dagre`.

| Id | Keywords | Grammar or parser | Db | Renderer | Default layout | Accepted layouts |
|---|---|---|---|---|---|---|
| `agentflow` | `agentflow-beta` with a word boundary | `agentflow/parser/agentflow.jison` through `parser/agentflowParser.ts` | `agentflowDb.ts` | `renderer.ts` | `elk` | Unified |
| `architecture` | `architecture` | `architecture/architecture.langium`, `architecture/arch.langium` | `architectureDb.ts` | `architectureRenderer.ts` | cytoscape-fcose | Own |
| `block` | `block`, `block-beta` | `block/parser/block.jison` | `blockDB.ts` | `blockRenderer.ts` | Own grid of columns in `layout.ts` | Own |
| `c4` | `C4Context`, `C4Container`, `C4Component`, `C4Dynamic`, `C4Deployment` | `c4/parser/c4Diagram.jison` | `c4Db.ts` | `c4Renderer.ts` | Own | Own |
| `classDiagram` | `classDiagram` | `class/parser/classDiagram.jison` | `classDb.ts` | `classRenderer-v3-unified.ts` | `elk` | Unified |
| `cynefin` | `cynefin-beta` followed by space, colon or end | `cynefin/cynefin.langium` | `cynefinDb.ts` | `cynefinRenderer.ts` | Own | Own |
| `er` | `erDiagram` | `er/parser/erDiagram.jison` | `erDb.ts` | `erRenderer-unified.ts` | `elk` | Unified |
| `eventmodeling` | `eventmodeling` | `eventmodeling/event-modeling.langium` | `db.ts` | `renderer.ts` | Own | Own |
| `flowchart-v2` | `graph`, `flowchart` | `flowchart/parser/flow.jison` through `parser/flowParser.ts` | `flowDb.ts` | `flowRenderer-v3-unified.ts` | `elk` | Unified |
| `flowchart-elk` | `flowchart-elk`. The detector sets `layout` to `elk` | Same as `flowchart-v2` | `flowDb.ts` | `flowRenderer-v3-unified.ts` | `elk` | Unified |
| `gantt` | `gantt` | `gantt/parser/gantt.jison` | `ganttDb.js` | `ganttRenderer.js` | Own, with dayjs for dates | Own |
| `gitGraph` | `gitGraph` | `gitGraph/gitGraph.langium`, `gitGraph/reference.langium`, read by `git/gitGraphParser.ts` | `gitGraphAst.ts` | `gitGraphRenderer.ts` | Own | Own |
| `info` | `info` | `info/info.langium` | `infoDb.ts` | `infoRenderer.ts` | Own | Own |
| `ishikawa` | `ishikawa`, `ishikawa-beta`, any case, word boundary | `ishikawa/parser/ishikawa.jison` | `ishikawaDb.ts` | `ishikawaRenderer.ts` | Own | Own |
| `journey` | `journey` | `user-journey/parser/journey.jison` | `journeyDb.js` | `journeyRenderer.ts` | Own | Own |
| `kanban` | `kanban` | `kanban/parser/kanban.jison` | `kanbanDb.ts` | `kanbanRenderer.ts` | Own columns | Own |
| `mindmap` | `mindmap` | `mindmap/parser/mindmap.jison` | `mindmapDb.ts` | `mindmapRenderer.ts` | `cose-bilkent`. `getData` sets it unless the user set `layout` | Unified |
| `packet` | `packet`, `packet-beta` | `packet/packet.langium` | `db.ts` | `renderer.ts` | Own | Own |
| `pie` | `pie` | `pie/pie.langium` | `pieDb.ts` | `pieRenderer.ts` | Own, with d3 `pie` and `arc` | Own |
| `quadrantChart` | `quadrantChart` | `quadrant-chart/parser/quadrant.jison` | `quadrantDb.ts` | `quadrantRenderer.ts` | Own | Own |
| `radar` | `radar-beta` | `radar/radar.langium` | `db.ts` | `renderer.ts` | Own | Own |
| `railroad` | `railroad-beta`, any case | `railroad/railroad.langium`, read by `railroad/parser/railroadParser.ts` | `railroadDb.ts` | `railroadRenderer.ts` | Own | Own |
| `railroadEbnf` | `railroad-ebnf-beta`, any case | `railroad-ebnf/railroad-ebnf.langium`, read by `railroad/parser/ebnfParser.ts` | `railroadDb.ts` | `railroadRenderer.ts` | Own | Own |
| `railroadAbnf` | `railroad-abnf-beta`, any case | `railroad-abnf/railroad-abnf.langium`, read by `railroad/parser/abnfParser.ts` | `railroadDb.ts` | `railroadRenderer.ts` | Own | Own |
| `railroadPeg` | `railroad-peg-beta`, any case | `railroad-peg/railroad-peg.langium`, read by `railroad/parser/pegParser.ts` | `railroadDb.ts` | `railroadRenderer.ts` | Own | Own |
| `requirement` | `requirement`, `requirementDiagram` | `requirement/parser/requirementDiagram.jison` | `requirementDb.ts` | `requirementRenderer.ts` | `elk` | Unified |
| `sankey` | `sankey`, `sankey-beta` | `sankey/parser/sankey.jison` | `sankeyDB.ts` | `sankeyRenderer.ts` | d3-sankey | Own |
| `sequence` | `sequenceDiagram` | `sequence/parser/sequenceDiagram.jison` | `sequenceDb.ts` | `sequenceRenderer.ts` | Own | Own |
| `stateDiagram` | `stateDiagram`, which also matches `stateDiagram-v2` | `state/parser/stateDiagram.jison` | `stateDb.ts` | `stateRenderer-v3-unified.ts` | `elk`, set in the `state` section too | Unified |
| `swimlane` | `swimlane-beta` with a word boundary | Flowchart's `flow.jison` | Flowchart's `flowDb.ts` | Flowchart's renderer, through `swimlanesDiagram.ts` | `swimlane`, set in the `swimlane` section | Unified |
| `timeline` | `timeline` | `timeline/parser/timeline.jison` | `timelineDb.js` | `timelineRenderer.ts` and `timelineRendererVertical.ts` | Own | Own |
| `treeView` | `treeView-beta` | `treeView/treeView.langium` | `db.ts` | `renderer.ts` | Own | Own |
| `treemap` | `treemap` | `treemap/treemap.langium` | `db.ts` | `renderer.ts` | d3-hierarchy `treemap` | Own |
| `usecase` | `usecase-beta` followed by space or end | Chevrotain: `usecase/parser/usecase.lexer.ts`, `usecase.parser.ts`, `usecase.visitor.ts` | `usecaseDb.ts` | `usecaseRenderer.ts` | `elk` | Unified |
| `venn` | `venn-beta` | `venn/parser/venn.jison` | `vennDB.ts` | `vennRenderer.ts` | @upsetjs/venn.js | Own |
| `wardley` | `wardley-beta`, any case | `wardley/wardley.langium` | `wardleyDb.ts` | `wardleyRenderer.ts` | Own | Own |
| `xychart` | `xychart`, `xychart-beta` | `xychart/parser/xychart.jison` | `xychartDb.ts` | `xychartRenderer.ts` | Own | Own |

## Appearance defaults

The top-level defaults are theme `default`, look `classic`, font family `"trebuchet ms", verdana, arial, sans-serif`
and font size 16. Ten types override theme and look in their config section with theme `redux-color` and look `neo`.
Those types get their font from `theme-redux-color.js`: `"Recursive Variable", arial, sans-serif` at 14px. Mermaid does
not bundle Recursive Variable. The host page loads it from `@fontsource-variable/recursive`, and without it the browser
uses Arial.

Effective `htmlLabels` is true when unset, because `evaluate(undefined)` in `config.ts` returns true. Shapes drawn
through `rendering-util` therefore put labels in `foreignObject`. The `flowchart.htmlLabels` and `class.htmlLabels`
keys are deprecated, and the root key wins over them.

| Id | Theme and look | Labels in `foreignObject` by default | Font defaults beyond the theme font |
|---|---|---|---|
| `agentflow` | `redux-color`, `neo` | Yes | None |
| `architecture` | `default`, `classic` | No. `createText` is called with `useHtmlLabels: false`. Only `iconText` uses a `foreignObject` | `fontSize` 16 |
| `block` | `default`, `classic` | Yes, through `insertNode` | None |
| `c4` | `default`, `classic` | No. `textPlacement` is unset, so text is `tspan` | Person, system, boundary, container and component 14, message 12, all `"Open Sans", sans-serif` |
| `classDiagram` | `redux-color`, `neo` | Yes | None |
| `cynefin` | `default`, `classic` | No | None |
| `er` | `redux-color`, `neo` | Yes | `fontSize` 12 |
| `eventmodeling` | `default`, `classic` | Yes. Box text is always a `foreignObject` | None |
| `flowchart-v2`, `flowchart-elk` | `redux-color`, `neo` | Yes | None |
| `gantt` | `default`, `classic` | No | `fontSize` 11, `sectionFontSize` 11 |
| `gitGraph` | `default`, `classic` | No | None |
| `info` | `default`, `classic` | No | None |
| `ishikawa` | `default`, `classic` | No | None |
| `journey` | `default`, `classic` | Yes. `textPlacement` defaults to `fo` | Task 14 `"Open Sans", sans-serif`. Title `4ex` in the top-level family |
| `kanban` | `default`, `classic` | Yes. The renderer sets `htmlLabels` false on its own copy of the config, which `labelHelper` does not read | None |
| `mindmap` | `default`, `classic` | Yes | None |
| `packet` | `default`, `classic` | No | None |
| `pie` | `default`, `classic` | No | None |
| `quadrantChart` | `default`, `classic` | No | Title 20, axis labels 16, quadrant labels 16, point labels 12 |
| `radar` | `default`, `classic` | No | None |
| `railroad`, `railroadEbnf`, `railroadAbnf`, `railroadPeg` | `default`, `classic` | No | `monospace` at 14 |
| `requirement` | `redux-color`, `neo` | Yes | `fontSize` 14 |
| `sankey` | `default`, `classic` | No | None |
| `sequence` | `redux-color`, `neo` | No. `textPlacement` is unset, so text is `tspan`. KaTeX math uses a `foreignObject` | Actor 14 `"Open Sans", sans-serif`. Note 14 and message 16 in the top-level family |
| `stateDiagram` | `redux-color`, `neo` | Yes | None. The `state.fontSize` of 24 sizes edge-label guesses in the old renderer |
| `swimlane` | `redux-color`, `neo` | Yes | None |
| `timeline` | `default`, `classic` | Yes. `textPlacement` defaults to `fo` | Task 14 `"Open Sans", sans-serif` |
| `treeView` | `default`, `classic` | No | None |
| `treemap` | `default`, `classic` | No | None |
| `usecase` | `redux-color`, `neo` | Yes | Actor 14 and use case 12, `"Open Sans", sans-serif` |
| `venn` | `redux-color`, `neo` | Yes, for text placed in an area | None |
| `wardley` | `default`, `classic` | No | Axis 12, labels 10 |
| `xychart` | `default`, `classic` | No | Title 20, axis labels 14, axis titles 16, legend 14 |

The `foreignObject` column comes from reading the source. The oracle in phase 0 confirms it per type.

## Layout folders

| Folder | What it does | Used by |
|---|---|---|
| `dagre` | Adapter from `LayoutData` to dagre-d3-es, with mermaid's own graphlib helpers for clusters | Any unified type that asks for `dagre`, and the `tiny` build's fallback for `elk` |
| `elk` | Adapter to elkjs, with mermaid's own edge post-processing: line hops, nested edge order and subgraph feedback edges | Every unified type by default, except `mindmap` and `swimlane` |
| `cose-bilkent` | Runs cytoscape-cose-bilkent through cytoscape | `mindmap` by default |
| `swimlanes` | A layered layout of its own for lanes: ranking, ordering, lane bands, anchored nodes and an orthogonal edge router | `swimlane` by default |
| `grid` | A grid placement with an orthogonal router that avoids nodes and groups. `edge-routing.md` documents it | Nothing. It is not registered in `render.ts`, and no file outside the folder imports it. `grid` appears only in the `LayoutAlgorithm` type union |
| `ddlt` | Data-driven layout tests: fixture loading, node size capture and size replay | Tests only. `createGraph.ts` imports `sizeCapture.ts` when `globalThis.mermaidCaptureSizes` is set |
| `layout-utils` | Geometry helpers, layout scoring and validation | The layouts above and their tests |
| `common` | Shared rendering of clusters, nodes and edges after layout | The layouts above |

## Port sizes

"Code" counts lines that are not blank and not comments. Tests and type declarations are excluded.

The ports the plan lists:

| What | Package and version | Files | Lines | Code |
|---|---|---|---|---|
| dagre | dagre-d3-es 7.0.14, `src/dagre` | 27 | 2,915 | 2,124 |
| graphlib | dagre-d3-es 7.0.14, `src/graphlib` | 17 | 2,438 | 922 |
| Polygon and rect intersection, used by flowchart | dagre-d3-es 7.0.14, `src/dagre-js/intersect` | 7 | 204 | 136 |
| d3 curve generators, all 19 that mermaid accepts by name | d3-shape 3.2.0, `src/curve` | 17 | 982 | 867 |
| d3 line, area and point helpers | d3-shape 3.2.0 | 6 | 208 | 171 |
| d3 path builder | d3-path 3.1.0 | 2 | 157 | 119 |
| Colour functions | khroma 2.1.0, `src` | 50 | 1,719 | 931 |
| cose-bilkent | cytoscape-cose-bilkent 4.1.0 | 1 | 390 | 279 |
| cose-bilkent base | cose-base 1.0.3 | 6 | 1,418 | 1,092 |
| cose-bilkent graph model | layout-base 1.0.2 | 27 | 4,458 | 3,351 |
| tidy-tree algorithm | non-layered-tidy-tree-layout 2.0.2 | 3 | 339 | 251 |
| tidy-tree adapter | `packages/mermaid-layout-tidy-tree/src` | 5 | 966 | 716 |
| rough.js | roughjs 4.6.6, `bin`, with mermaid's patch to its `.d.ts` | 17 | 1,311 | 1,309 |
| rough.js dependencies | hachure-fill 0.5.2, path-data-parser 0.1.0, points-on-curve 0.2.0, points-on-path 0.2.1 | 8 | 804 | 790 |

The themes and colour functions:

- The themes use these khroma functions: `adjust`, `channel`, `darken`, `hue`, `invert`, `isDark`, `lighten`, `rgba`,
  `toRgba` and `transparentize`. A port needs those, khroma's colour parser and its channel conversions. That is less
  than the whole of `src`.
- The 11 themes are 14 files under `packages/mermaid/src/themes`, with 5,220 lines and 4,101 lines of code.

The layout code the plan does not list:

| What | Package and version | Files | Lines | Code |
|---|---|---|---|---|
| fcose, used by `architecture` | cytoscape-fcose 2.2.0 | 6 | 1,392 | 1,069 |
| fcose base | cose-base 2.2.0 | 7 | 3,073 | 2,465 |
| fcose graph model | layout-base 2.0.1 | 29 | 5,345 | 4,085 |
| Sankey layout | d3-sankey 0.12.3 | 5 | 415 | 361 |
| Treemap tiling | d3-hierarchy 3.1.2, `src/treemap` | 8 | 278 | 234 |
| d3-hierarchy, whole package | d3-hierarchy 3.1.2 | 34 | 1,414 | 1,174 |
| Venn layout | @upsetjs/venn.js 2.0.0 | 4 | 1,892 | 1,299 |
| Venn optimizers, bundled into venn.js | fmin 0.0.4 | 6 | 472 | 364 |
| Swimlane layout, mermaid's own | `layout-algorithms/swimlanes` | 38 | 12,921 | 10,567 |
| ELK adapter, mermaid's own | `layout-algorithms/elk` | 10 | 3,962 | 2,578 |
| dagre adapter, mermaid's own | `layout-algorithms/dagre` | 2 | 1,291 | 1,089 |
| cose-bilkent adapter, mermaid's own | `layout-algorithms/cose-bilkent` | 5 | 549 | 355 |
| Grid layout, unregistered | `layout-algorithms/grid` | 14 | 7,707 | 6,998 |

elkjs 0.12.0 is Eclipse ELK compiled from Java by GWT. `lib/elk-worker.js` is 4.8 MB. A port would follow ELK's Java
source rather than this file, so its line count is not a measure of the port.

Mermaid's own source is 574 files with 117,287 lines and 92,769 lines of code in `packages/mermaid/src`, without the
jison grammars. `packages/parser/src` adds 2,123 lines and the langium grammars.

## Effort against the plan

These types need more or different work than the plan assumed:

- `flowchart-v2`, `classDiagram`, `stateDiagram`, `er`, `requirement`, `agentflow` and `usecase` default to `elk`, not
  `dagre`. The plan has ELK as an opt-in engine with L2 parity through `Sugiyama`. With mermaid's default config,
  geometry parity for these types needs ELK layered, not dagre.
- `swimlane` has its own layout engine of 10,567 lines of code. The plan does not size it.
- `architecture` uses cytoscape-fcose, not cose-bilkent. fcose with its own cose-base and layout-base is 7,619 lines of
  code.
- `mindmap` defaults to `cose-bilkent`. `tidy-tree` is opt-in through a separate package.
- `venn` needs venn.js and fmin, 1,663 lines of code together. `sankey` needs d3-sankey and `treemap` needs the
  d3-hierarchy treemap tiling.
- `flowchart-v2`, `swimlane`, `agentflow`, `classDiagram`, `stateDiagram`, `er`, `requirement`, `sequence`, `usecase`
  and `venn` default to theme `redux-color` and look `neo`. The plan builds `classic` first, so these ten types reach
  default-config parity only once `neo` and `redux-color` are done. Their text is measured in Recursive Variable,
  which the text tables then need.
- `railroad` is four ids with four langium grammars, one per input notation.
- `grid` needs no port at 12.3.0 because nothing uses it. `ddlt` is test tooling and needs no port.
