# Oracle

The oracle runs the mermaid.js that `package-lock.json` pins in headless Chromium and writes what STM is compared
against. Section 7 of [the parity plan](../../docs/mermaid-parity-plan.md) describes its place in the harness.

## Running it

```bash
mise run oracle                              # cases in tools/oracle/cases, output in .artifacts/oracle
mise run oracle <cases> <facts-dir> <svg-dir>
mise run oracle-test                         # node --test on the oracle's test files
mise run elk-probe <graph file>              # ELK's layout of a graph, as the ELK probe section describes
mise run raster                              # L4: corpus/l4.txt and .artifacts/raster
mise run screenshots <cases>                 # the named cases side by side in .artifacts/screenshots
```

Both tasks run `npm ci` in `tools/oracle` when `node_modules` is missing. Without mise, run
`node tools/oracle/facts.mjs <cases> <facts-dir> [svg-dir]` after `npm ci`.

The run fails in these cases:

- `node_modules` holds a version of a package in `package.json` other than the one `package-lock.json` pins. Run
  `npm ci` in `tools/oracle`.
- A package in `package.json` asks for a newer Node in its `engines` field. puppeteer 25.17.0 asks for 22.12.0. Under
  Node 22.7.0 a run over `corpus/` stops answering after a few hundred cases, so mise.toml pins Node 22.23.
- A case has a diagram type that `extract.mjs` has no extractor for. Add one.

- `mermaid.min.js` does not hold dagre's `layout` function exactly once. The [dagre dumps](#dagre-files) section says
  why the runner looks for it.
- `mermaid.min.js` does not hold mermaid's `elk.layout` call exactly once. The [ELK files](#elk-files) section says why
  the runner looks for it.

The runner reads the cases of the folder and its subfolders. A case in a subfolder, such as `pie/docs-pie-001.mmd`,
gets its facts, boxes, dagre, ELK and SVG files in the same subfolder of the output folders. A case's seed and SVG id come
from its file name alone. `mise run corpus-oracle` runs it on `corpus/` with the facts folder set to `corpus/`, so each
case's files sit beside it.

Facts, boxes, dagre and ELK files whose case is no longer in the cases folder are deleted, as is the dagre or ELK file
of a case that no longer has one. A case that mermaid.js refuses has no boxes file, no dagre file and no ELK file.

Each run writes these files to the facts folder:

| File | Per | Section |
|---|---|---|
| `<name>.facts.txt` | case | [Facts files](#facts-files) |
| `<name>.boxes.txt` | accepted case | [Boxes files](#boxes-files) |
| `<name>.dagre.txt` | accepted flowchart, state, class and ER case | [Dagre files](#dagre-files) |
| `<name>.elk.txt` | accepted case that mermaid.js lays out with ELK | [ELK files](#elk-files) |
| `themes.txt` | run | [Theme variables](#theme-variables) |
| `config.txt` | run | [Default config](#default-config) |
| `shapes.txt` | run | [Shape table](#shape-table) |
| `styles.txt` | run | [Stylesheets](#stylesheets) |

## Fuzzing

```bash
mise run fuzz-diff [seconds] [seed]   # tools/fuzz/differential.sh, output in .artifacts/fuzz/differential
mise run fuzz-add [finds]             # node tools/oracle/add.mjs <find.mmd or folder>
```

The differential fuzzer in `tests/SplashTheMusical.Conformance/Fuzzing` asks `serve.mjs` for the facts of each source
it makes. `serve.mjs` loads mermaid.js once, as `facts.mjs` does, and answers one source per line on stdin with the
lines of its facts file. It records no dagre layout. `differential.sh` clones mermaid at the pinned tag into
`.artifacts/fuzz/mermaid@<version>` for the grammars, unless `STM_FUZZ_MERMAID` names a checkout.

A run writes `report.md`, each minimized find as `finds/<type>/<name>.mmd` and the source it came from as
`original/<type>/<name>.mmd`. `add.mjs` writes a find as `corpus/<type>/fuzz-<name>.mmd` with its facts, boxes and
dagre and ELK files, skips one whose text is already a case of its type, and rewrites the corpus counts. Fix the difference or
list it in the type's ledger, then run `mise run raster`.

`.github/workflows/fuzz-differential.yml` runs the fuzzer daily on macOS for 600 seconds per type. A run with finds
opens an issue with the report and the commands that download its `fuzz-differential` artifact and add the finds. A run
without finds says so in its summary.

## What it pins

| Package | Version | Why |
|---|---|---|
| `mermaid` | 12.3.0 | The version STM targets. Its build includes the ELK layouts |
| `@mermaid-js/mermaid-zenuml` | 1.0.1 | ZenUML is an external diagram, registered with `registerExternalDiagrams` |
| `@mermaid-js/layout-tidy-tree` | 1.0.1 | The `tidy-tree` layout, registered with `registerLayoutLoaders` |
| `puppeteer` | 25.17.0 | Headless Chromium |

khroma, the colour library of mermaid's themes, is not named in `package.json`. `colors.mjs` uses the version
`package-lock.json` resolves for mermaid, 2.1.0 at 12.3.0.

To move to another mermaid version, run `mise run upstream-bump <version>`. It installs the version, rewrites every
file that depends on it, and writes the delta report to `.artifacts/upstream/after/upstream-delta.md`.

## Reproducibility

Two runs on the same machine write byte-identical files. `mise run oracle-test` checks this. The page is set up so:

- The time zone is UTC, and `Date` with no arguments and `Date.now()` return 2000-01-01T00:00:00Z.
- `Math.random` is a seeded generator. It is seeded with 0 before mermaid loads and with a hash of the case's name
  before each case, so a case's output does not depend on the cases before it.
- The page loads from a made-up origin, `http://oracle.invalid`, which serves only `tools/oracle/node_modules` and
  `tools/oracle/fonts`. Every other request is refused, so a run never reaches the network.
- Every text is drawn and measured in the bundled font. The font section below says how.
- Cases run in file-name order in one page.

## The font

`fonts/` holds Liberation Sans 2.1.5 in its four faces: regular, bold, italic and bold italic. It is under the SIL Open
Font License 1.1, in `fonts/LICENSE`. Its widths match Arial's, and Arial is in mermaid's default font list. STM's width
table for the harness is generated from the same files, so text width is not a source of difference in box comparisons.

The page loads the four faces before mermaid loads, so the first case is measured in them too. A page style sets
`font-family: "Liberation Sans" !important` on every element. That covers mermaid's per-diagram font settings and
generic families such as `sans-serif`. A canvas ignores CSS, so the faces are also registered as `Helvetica` and
`Verdana`, the families ZenUML's canvas measurement asks for. Chromium never synthesizes bold or italic, because every
face is bundled.

A glyph the font lacks, such as a CJK letter or an emoji, is drawn in a system font. The `font` lines of the boxes
file show when that happens.

## The cases

`cases/` holds one case per diagram id of mermaid 12.3.0 and ZenUML, named after the id. It also holds
`mindmap-tidy-tree.mmd`, which uses the `tidy-tree` layout, and `refused.mmd`, which mermaid.js refuses. These cases
test the runner. The corpus STM is measured on is a separate folder, [`corpus/`](../../corpus/README.md).

## Facts files

The runner writes `<name>.facts.txt` per case. A facts file is UTF-8 with LF line endings. It starts with three
comment lines:

```
# mermaid.js facts. Refresh with: mise run oracle
# mermaid 12.3.0
# source 0f3a9c1d2b4e5f60
```

`source` is the first 16 hex digits of the SHA-256 of the case's text with CRLF folded to LF. A consumer compares it
with its own hash to spot a facts file that is older than its case.

The next line is `accept\tyes` or `accept\tno`.

When mermaid.js refuses the source, one more line follows: `# error ` and the first line of the error message.

When it accepts the source, every following line is a fact. A fact is a kind and its fields, separated by tabs.
Trailing empty fields are dropped. Whitespace inside a field is folded to one space. Numbers are rounded to 3 decimals
and written in their shortest form. The lines are sorted by UTF-16 code unit, so a fact's position carries no meaning.
Where order matters, the fact holds an index.

Every accepted case has these facts:

| Fact | Fields | Source |
|---|---|---|
| `type` | the diagram id mermaid's detector chose | Model |
| `drawn` | `yes` when the SVG shows a shape or a non-empty text with a size, outside `defs` and markers, else `no` | DOM |

A case with front matter or directives also has these facts:

| Fact | Fields | Source |
|---|---|---|
| `preamble-title` | the front matter title | `preprocessDiagram` |
| `preamble-config` | path, value | `preprocessDiagram` |

`preamble-config` holds the config of the front matter and the `init` and `wrap` directives, merged as mermaid.js merges
them before it applies the config. Its paths and values are written as in [`config.txt`](#theme-variables). The runner
reads both from mermaid's `preprocessDiagram`, which no public function returns. `hookPreprocess` in `dumps.mjs` sets it
on `globalThis` when it loads `mermaid.min.js`, and fails when it does not find the function exactly once.

Every accepted case also has `config` facts, with fields path and value, for the config mermaid.js drew it with. That is
the default config, then the runner's `initialize` options, then the front matter and directives, with mermaid's
sanitizing, locked keys and per-diagram `theme`, `look` and `layout`. A fact is written for each path whose value
differs from `getSiteConfig()`, and with the value `missing` for a site config path the drawn config lacks.
`themeVariables` are left out, because the theme computes them. render() clears the diagram's config scope before it
returns, so the runner reads `getConfig()` inside it: `hookAddDirective` in `dumps.mjs` adds a call to the page at the
end of mermaid's `addDirective`, and fails when it does not find the function exactly once.

A case whose `preamble-config` sets `themeVariables` also has `theme-variable` facts, with fields path and value, for
every path of the drawn config's `themeVariables`. Its paths and values are written as in
[`themes.txt`](#theme-variables). They are the theme's computed variables when the case sets or resolves a theme other
than the site config's, and otherwise the site config's variables with the case's merged over them.

Most types also have `label` facts. A `label` fact names a label from the model that the DOM shows. A label counts as
shown when its letters and digits, case-folded, occur in the drawn text joined the same way. Longer labels are matched
first, and each match is used once, so two equal labels need two drawn copies.

Node shapes are written in mermaid's short shape names, such as `rect`, `rounded`, `diam`, `cyl` and `fr-rect`. The
runner reads the table from `shapesDefs` in mermaid's dist and maps each alias and internal name to its short name.

Arrow ends are written as `none`, `arrow`, `circle`, `cross`, `triangle`, `diamond`, `diamond-filled` or `lollipop`.

The facts of each type follow. "Model" means the field comes from the diagram's db. "DOM" means it comes from the
rendered SVG. The comment above each extractor in `extract.mjs` says the same.

| Type | Facts | Source |
|---|---|---|
| `flowchart-v2`, `flowchart-elk`, `swimlane` | `node` id, label, shape. `group` id, label, member ids. `edge` start, end, start end, end end, line (`solid`, `dotted`, `thick`, `invisible`), label. `icon` id, name. `image` id, url | Model for ids, shapes, members, ends and lines. DOM for labels and the icons a label draws |
| `stateDiagram` | `node` id, label, shape. `group` id, label, members. `note` state, text. `edge` start, end, start end, end end, `solid`, label. Start and end states are `[*]start@<parent>` and `[*]end@<parent>` | Model for structure. DOM for labels |
| `classDiagram` | `class` id, label. `annotation`, `attribute`, `method` class, text. `namespace` id, members. `note` class, text. `relation` start, end, start end, end end, line, label, start cardinality, end cardinality | Model for structure, notes and cardinalities. DOM for class texts and relation labels |
| `sequence` | `actor` id, label, type. `box` name, members. `message` from, to, line, head, text. `note` placement, actors, text. `block` kind, text. `divider` text | Model |
| `er` | `entity` id, display name. `attribute` entity, type, name, keys, comment. `relation` a, b, cardinality at a, cardinality at b, `identifying` or `non-identifying`, label | Model |
| `pie` | `title`. `showdata` `yes`. `slice` label, value | Model |
| `quadrantChart` | `title`. `quadrant` number, text. `axis` side, text. `point` label, x, y, as fractions of the chart | Model |
| `timeline` | `title`. `section` name. `period` section, text. `event` period, text | Model |
| `gitGraph` | `branch` name. `commit` index, branch, kind, tags, merged branch, given id | Model |
| `radar` | `title`. `axis` label. `curve` label, values | Model |
| `railroad`, `railroadEbnf`, `railroadAbnf`, `railroadPeg` | `title`. `rule` name, expression: `"t"`, `<n>`, `?s?`, `(a b)`, `(a \| b)`, `[a]`, `{a}min..max` | Model |
| `wardley` | `title`, `size`, `stage`, `boundaries`, `node`, `link`, `trend`, `pipeline`, `note`, `annotation`, `annotations`, `accelerator`, `deaccelerator` | Model |
| `zenuml` | `title`. `participant` index, id, label. `icon` id. `group` name, members. `message` kind, from, to, text. `fragment` kind, condition. `section` kind, label. `comment`, `divider` text | DOM. The plugin's db is empty |
| `treemap` | `node` path, leaf value | Model |
| `venn` | `set` and `union` sets, label, size | Model |
| `mindmap` | `node` path, shape. `icon` path, name | Model |
| `gantt` | `title`. `section` name. `task` section, name, given id, start, end, tags. Times are UTC wall-clock times | Model |
| `journey` | `title`. `section` name. `task` section, name, score, people | Model |
| `c4` | `title`. `element` alias, type, `external` or `internal`, label, technology, description, boundary. `boundary` alias, type, label, parent. `rel` from, to, `one` or `both`, label, technology | Model |
| `sankey` | `flow` source, target, value | Model |
| `xychart` | `title`. `orientation` `horizontal`. `x-axis` title, categories. `y-axis` title. `series` kind, values | Model |
| `requirement` | `requirement` name, kind, id, text, risk, verification. `element` name, type, document. `relation` source, target, type | Model |
| `packet` | `title`. `field` first bit, last bit, label | Model |
| `kanban` | `column` id, label. `card` column, id, label, assignee, ticket, priority | Model |
| `architecture` | `group` id, title, parent. `service` id, title, parent. `junction` id, parent. `icon` id, name. `icon-text` id, text. `edge` a, a side, b, b side, a end, b end, `group`, title | Model |
| `block` | `block` id, label, shape, columns, parent. `space` parent, columns. `edge` start, end, start end, end end, label. A block without an id is named `anonymous-<n>` | Model |
| `usecase` | `direction`, `actor`, `icon`, `usecase`, `boundary`, `relation`, `note`, `json`, `row` | Model |
| `treeView` | `node` path, `dir` or `file`, description. `icon` path, name | Model |
| `agentflow` | `title`. `direction`. `node` id, label, shape, kind. `flow` id, title, members. `connector` id, title. `edge` start, end, meaning (`sequence`, `reference`, `failure`), label | Model, from the db's semantic model |
| `cynefin` | `title`. `item` domain, index, label. `transition` from, to, label | Model |
| `eventmodeling` | `title`. `lane` index, label, namespace. `frame` index, name, entity type, entity name, lane index, inline data. `relation` source frame, target frame | Model, from the db's state |
| `info` | Only `label`, for the version | Model |
| `ishikawa` | `head` text. `cause` path below the head | Model |

A path joins the names from the root with ` / `.

## Boxes files

The runner writes `<name>.boxes.txt` for each accepted case. It has the same header, encoding, field and sorting rules
as a facts file, without the `accept` line:

```
# mermaid.js boxes. Refresh with: mise run oracle
# mermaid 12.3.0
# source 0f3a9c1d2b4e5f60
```

The lines that follow are:

| Line | Fields |
|---|---|
| `canvas` | x, y, width, height of the SVG's viewBox. An SVG without a viewBox, such as `info`'s, gets 0, 0 and its drawn size |
| `font` | a font family Chromium drew the case's text in, and its glyph count, from the DevTools protocol's platform fonts |
| `box` | kind, name, x, y, width, height |

Boxes are in the SVG's user units, the space of the viewBox. A box is the union of the client rects of the elements
that draw it, mapped back through the SVG's screen transform. Transforms count and strokes do not. A straight edge has
a width or a height of 0. An element with no size, such as an empty edge label, has no box.

A box's name is the identifier the facts use for the element: a node id, a path, or a label. Edges and other links
are named `<start> --> <end>`. When a kind uses a name twice, the second box gets ` #2`, the third ` #3`, in the
order the extractor meets them.

| Type | Box kinds and names |
|---|---|
| `flowchart-v2`, `flowchart-elk`, `swimlane` | `node` and `group` by id. `edge` and `edge-label` by ends |
| `stateDiagram` | `node` and `group` by the names of the facts. `note` by its state. `edge` and `edge-label` by ends |
| `classDiagram` | `class` and `namespace` by id. `note` by its text. `relation` and `relation-label` by ends |
| `sequence` | `actor` and `actor-bottom` by id. `box` by name. `message` and `message-text` by ends. `note` by text. `block` by kind and text |
| `er` | `entity` by key. `relation` and `relation-label` by ends |
| `pie` | `title`. `slice` and `legend` by label |
| `quadrantChart` | `title`. `quadrant` by number. `axis` by side. `point` by label |
| `timeline` | `title`. `section` and `period` by text. `event` by period and text |
| `gitGraph` | `branch` and `branch-label` by name. `commit` and `commit-label` by index. `tag` by commit index and tag |
| `radar` | `title`. `axis` by label. `curve` and `legend` by label |
| `railroad`, `railroadEbnf`, `railroadAbnf`, `railroadPeg` | `title`. `rule` by name. `terminal`, `nonterminal` and `special` by rule and text |
| `wardley` | `title`. `stage` by index. `node` by id. `link` by ends |
| `zenuml` | `title`. `participant` by id. `group` by name. `message` by ends. `fragment` by kind and condition |
| `treemap` | `node` by path |
| `venn` | `set` and `union` by sets |
| `mindmap` | `node` by path. `edge` by the child's path |
| `gantt` | `title`. `section` by name. `task` and `task-text` by section and task |
| `journey` | `title`. `section` by name. `task` and `face` by section and task. `person` by name |
| `c4` | `title`. `element` and `boundary` by alias. `rel-label` by ends |
| `sankey` | `node` by name. `flow` by ends |
| `xychart` | `title`. `plot`. `series` by index. `axis` and `axis-title` by `x` or `y`. `category` by text |
| `requirement` | `requirement` and `element` by name. `relation` and `relation-label` by ends |
| `packet` | `title`. `field` by the first and last bit of each piece on a row |
| `kanban` | `column` and `card` by id |
| `architecture` | `group`, `service` and `junction` by id. `edge` by ends |
| `block` | `block` by the id of the facts, and a block whose id the db generated by its place among the drawn blocks with one, because `render` parses the source into a db with other generated ids. `edge` and `edge-label` by ends |
| `usecase` | `actor`, `usecase`, `boundary` and `json` by id. `note` by target. `relation` and `relation-label` by ends |
| `treeView` | `node` by path |
| `agentflow` | `title`. `node` and `flow` by id. `edge` and `edge-label` by ends |
| `cynefin` | `title`. `domain-label` by domain. `item` by domain and index. `transition` and `transition-label` by ends |
| `eventmodeling` | `title`. `lane` and `frame` by index. `relation` by frame names |
| `info` | `version` |
| `ishikawa` | `head` by text. `cause` by path |

## Dagre files

The runner writes `<name>.dagre.txt` for each accepted case of type `flowchart-v2`, `stateDiagram`, `classDiagram` or
`er`. At 12.3.0 these types default to the ELK layout, so the runner renders each such case a second time with
`layout: dagre` set through `mermaid.initialize`, then initializes mermaid back. Every dagre layout of that render is
recorded, in call order. mermaid's dagre renderer lays out a cluster that it extracts from the graph on its own, before
the graph that holds it, so a case can have several layouts. When the second render throws, as it does for a flowchart
with an `INVALID` edge, the case has no dagre file and the runner prints a `dagre-err` line.

The runner records them through a hook. It finds dagre's `layout(g, opts)` in `mermaid.min.js` by its timing labels,
which minification keeps, and wraps it before the page loads the bundle. The hook reads the graph before and after the
call. The run fails when the function is not found exactly once.

The file has the same header as a facts file, with `# mermaid.js dagre layouts. Refresh with: mise run oracle` as its
first line. Unlike the other files, its lines are not sorted. dagre iterates nodes and edges in the graph's order, and
that order changes its results, so the file keeps it. Fields are separated by tabs and trailing empty fields are
dropped. In ids and names, a backslash, tab, newline and carriage return are written `\\`, `\t`, `\n` and `\r`. Numbers
are written in full, as JavaScript's `String` writes them, so a replay starts from dagre's exact input.

Each layout is a `layout` line followed by its input lines and then its output lines:

| Line | Fields |
|---|---|
| `layout` | the layout's number, from 1 |
| `graph` | rankdir, nodesep, edgesep, ranksep, marginx, marginy, align, acyclicer, ranker |
| `node` | id, parent id or empty for the root, width, height. One per node in the graph's order |
| `edge` | start, end, name, minlen, weight, width, height, labeloffset, labelpos. One per edge in the graph's order |
| `size` | width and height of the laid-out graph |
| `place` | id, x, y, width, height. One per node, in the same order as `node` |
| `route` | start, end, name, label x, label y, points as `x,y` pairs separated by spaces. One per edge, in the same order as `edge` |

The input is what dagre's `buildLayoutGraph` reads from the graph. Keys are lower-cased, a present numeric attribute
goes through `Number`, and a missing one takes dagre's default: nodesep 50, edgesep 20, ranksep 50, rankdir `tb`, node
width and height 0, edge minlen 1, weight 1, width and height 0, labeloffset 10 and labelpos `r`. marginx, marginy,
align, acyclicer and ranker have no default and are empty when missing. A cluster's input width and height are 0,
because dagre sizes it.

The output is what dagre writes back to the graph. Label x and y are empty for an edge dagre gives no label position.

## ELK files

The runner writes `<name>.elk.txt` for each accepted case whose own render calls ELK, which at 12.3.0 is every case of
a type mermaid lays out with its default `elk` layout, such as flowchart, state, class and ER, and any case whose
config asks for an `elk` or `elk.*` layout. A case drawn with `layout: dagre` makes no ELK call and gets no file. The
file records every ELK call of the case's render, in call order. The second render with `layout: dagre` that writes the
dagre file is not recorded.

mermaid's code calls ELK in one place, `graph = await elk.layout(elkGraph)` in `runElkLayout` of
`src/rendering-util/layout-algorithms/elk/render.ts`. The runner finds that call in `mermaid.min.js` by the
`layoutCore` profiler label around it, which minification keeps, and wraps it before the page loads the bundle: the
hook hands the graph to the recorder before the call and the graph ELK returns after it. elkjs itself is run as it is;
no file under `node_modules/elkjs` is read or changed. The run fails when the call is not found exactly once.

The file has the same header as a facts file, with `# mermaid.js ELK layouts. Refresh with: mise run oracle` as its
first line. Its lines are not sorted, and its escapes and numbers are written as in a [dagre file](#dagre-files). A
field that lists ids separates them with spaces and writes a space in an id as `\s`. Points are `x,y` pairs separated
by spaces.

Each call is a `layout` line, the lines of the graph mermaid gives ELK, a `result` line and the lines of the graph ELK
returns. Both graphs are walked in the same order: a node, its options, labels and ports, its children, then the edges
it holds. Only the fields of ELK's JSON graph format are written; the fields mermaid adds to its nodes and edges that
ELK does not read are left out.

| Line | Fields |
|---|---|
| `layout` | the call's number, from 1 |
| `node` | id, parent id or empty for the root, width, height, x, y. A field mermaid does not set is empty |
| `port` | id, node id, width, height, x, y |
| `edge` | id, id of the node whose `edges` holds it, source ids, target ids |
| `label` | owner kind (`node`, `port` or `edge`), owner id, index, text, width, height, x, y |
| `option` | owner kind (`node`, `port`, `edge`, `node-label`, `port-label` or `edge-label`), owner id, label index or empty, key, value. One per key of the owner's `layoutOptions`, in key order. A key whose value is `undefined` is left out, as ELK never sees it |
| `property` | the same fields, for a key of the owner's `properties` |
| `result` | none. The lines after it, up to the next `layout`, are ELK's output |
| `place` | id, parent id or empty for the root, x, y, width, height. x and y are relative to the parent, as ELK gives them |
| `place-port` | id, node id, x, y, width, height |
| `place-label` | owner kind, owner id, index, x, y, width, height |
| `route` | edge id, id of the node whose `edges` holds it, the edge's `container`, junction points |
| `section` | edge id, section id, start point, end point, bend points, incoming shape, outgoing shape, incoming section ids, outgoing section ids. One per section of the `route` before it |

`ElkRecords` in `tests/SplashTheMusical.Conformance` reads the files, and `ElkRecordsTests` reads every committed one.

Like the boxes and dagre files, the committed ELK files come from macOS, because label sizes depend on the fonts
Chromium falls back to. CI's `l4` job fails when a committed file differs from what it writes, and then uploads the
corpus files it rewrote as its `oracle-corpus` artifact, whose files can be committed as they are.

## ELK probe

```bash
mise run elk-probe [options] <graph file>   # node tools/oracle/elk-probe.mjs [options] <graph file>
mise run elk-diff [graphs] [seed]           # the differential run, output in .artifacts/elk-diff
node tools/oracle/elk-probe.mjs --help
```

`elk-probe.mjs` runs the pinned elkjs on any graph and prints its layout, so a session can learn how ELK behaves on a
graph the corpus lacks without reading ELK's source. It imports `elkjs/lib/elk.bundled.js`, the entry mermaid's
`render.ts` imports, calls `layout` once per layout and reads the graph it returns, as the [ELK files](#elk-files)
recorder does in the page. It opens no file under `node_modules/elkjs`. Node prints the line of code that throws an
uncaught exception, so the probe catches every exception of elkjs and prints only the first line of its message.

A graph file holds the input lines of an ELK file: `node`, `port`, `edge`, `label`, `option` and `property`. A
recorded `.elk.txt` file is a graph file as it is. A file without a `layout` line is one layout, and a file without a
root node gets the node `root`. A root that sets no `elk.algorithm` gets the root options mermaid.js gives a flowchart
at default config, which every flowchart recording has, and a root option the file sets replaces mermaid's. Each edge of
such a graph gets the empty label mermaid gives an edge without text, and each edge label gets mermaid's two label
options. Every recording sets `elk.algorithm`, so a recorded graph runs with the options mermaid gave it.

The output is an ELK file with `# ELK probe layouts` as its first line, the elkjs version `package-lock.json` pins, the
graph file and the overrides. For each layout it prints the graph ELK was given and the graph ELK returned, then one
`# layer` line per leaf node with its id, its layer and its position in the layer, read from ELK's coordinates as
`ElkLayoutComparer` reads them. The layer lines are comments, so `ElkRecords` reads the output as it reads a recording.

| Option | What it does |
|---|---|
| `--keep-layers` | Sets `elk.layered.layering.strategy` to `INTERACTIVE`: ELK keeps the layers the input's node coordinates give |
| `--keep-order` | Sets `elk.layered.crossingMinimization.strategy` to `INTERACTIVE`: ELK keeps the order within each layer the coordinates give |
| `--keep-cycles` | Sets `elk.layered.cycleBreaking.strategy` to `INTERACTIVE`: ELK reverses the edges that point against the coordinates |
| `--place-from-result` | Gives each node of a recorded layout the `x` and `y` of its recorded `place` line, so the pins keep ELK's own layers and order |
| `--option <key>=<value>` | Sets a root layout option, after the file's and the pins' |
| `--layout <n>` | Runs only layout `n` of the file |
| `--check` | Prints `# check` after each layout and exits with 1 unless the result lines equal the file's. With no override it compares the input lines too |
| `--serve` | Reads one graph file per line on stdin as base64 of its UTF-8 and answers each with the output lines and a line holding a period, or `# error <message>` and the period. It prints `ready` first |

The pins are set on the root and on every node with children, because ELK refuses a child graph whose stage differs
from the root's. They hold the stages before the one a session probes still: `--keep-layers --keep-order` with
`--place-from-result` on a recording leaves node placement and edge routing to vary with `--option`.

Run with `--check` on each of the 2,304 committed recordings of mermaid 12.3.0, the probe's input and result lines
equal the recorded ones, so elkjs under Node lays out a graph as it does in the page. `elk-probe.test.mjs` checks three
recordings, the pins, the defaults, the help, the errors and the serve mode.

### The differential run

`ElkDifferentialTests.RunsWhenAsked` in `tests/SplashTheMusical.Conformance`, which `mise run elk-diff` runs, feeds
seeded random graphs to the probe and to the dagre port. `ElkDifferential.Generate` makes graph `i` of seed `s` from
`FuzzRandom.For(s, i)`: 2 to 8 nodes of flowchart sizes, up to two more edges than nodes with an occasional self loop
or label, laid out down or right. The probe gives each mermaid's flowchart options. `DagreOnElkInput` lays out the
graph ELK was given with mermaid's dagre options for a flowchart, as `docs/layered-layout/baseline.md` does, and
`ElkLayoutComparer` scores it against ELK's result.

A graph the dagre port fails a measure on is a find, named by the first measure it fails: `layers`, `order`, `boxes`
or `bends`, or `error` when the port throws. The run shrinks it with `Minimizer.DeltaDebug`: edges, then nodes with their
edges, then labels, then a right layout to down, while the same measure still fails. These variables set the run:

| Variable | Default | What it sets |
|---|---|---|
| `STM_ELK_DIFF_GRAPHS` | none, the test does nothing | Random graphs |
| `STM_ELK_DIFF_SEED` | 1 | The seed |
| `STM_ELK_DIFF_MEASURES` | all five | A comma-separated list of the measures a find may fail |
| `STM_ELK_DIFF_TESTS` | 400 | The most graphs the shrinking of one find lays out |
| `STM_ELK_DIFF_DIR` | `.artifacts/elk-diff` | The folder of the report and the finds |

The run prints each distinct smallest graph and writes `report.md` with the graphs by measure, and each find's smallest
graph as `finds/<measure>/<index>.txt`, a graph file of the probe.

## Theme variables

`themes.txt` holds the computed variables of each theme in mermaid's themes table: `default`, `base`, `dark`,
`forest`, `neutral`, `neo`, `neo-dark`, `redux`, `redux-dark`, `redux-color` and `redux-dark-color` at 12.3.0. The
runner reads the theme names from `themes_default` in mermaid's dist, or in mermaid 13 from `THEME_NAMES` in
`@mermaid-js/core/dist/theme/registry.js`, initializes mermaid with each theme and reads
`themeVariables` from its site config. That is the theme's `getThemeVariables()` with no overrides.

`themes.txt`, `config.txt`, `shapes.txt` and `styles.txt` each start with two comment lines, such as:

```
# mermaid.js theme variables. Refresh with: mise run oracle
# mermaid 12.3.0
```

The lines that follow are sorted by UTF-16 code unit and their fields are separated by tabs. In `themes.txt` and
`config.txt` a value is JSON, so `"#ECECFF"` is a string and `16` is a number. A key that is present with no value
is written `undefined`, and a number that JSON cannot hold is written `NaN` or `Infinity`. Nested objects and arrays
are flattened into dotted paths, such as `xyChart.plotColorPalette`. An empty one is written `{}` or `[]`. Functions are
left out.

| Line | Fields |
|---|---|
| `theme` | theme, variable path, value |

## Default config

`config.txt` holds `mermaidAPI.defaultConfig`, the config before any `initialize`, directive or front matter. It
includes `themeVariables`, which are the `default` theme's.

| Line | Fields |
|---|---|
| `key` | path, value |
| `name` | a key name of `configKeys` in mermaid's `defaultConfig.ts` |

`configKeys` holds every key name at any depth of the default config, apart from keys whose value is an array. It
includes the theme variables and the function defaults, which `key` lines leave out. `sanitizeDirective` drops a
directive key whose name it does not hold.

## Shape table

`shapes.txt` holds mermaid's table of node shapes. The runner reads it from `shapesDefs` and `undocumentedShapes` in
the unminified ESM build in mermaid's dist. A handler is the name of the shape's drawing function there. mermaid 13 has
no such build. For it, `readShapes` reads `SHAPE_GROUPS` and `UNDOCUMENTED` from
`@mermaid-js/diagram-flowchart/dist/parser/shapes.js`, where each shape's handler is its short name and the name,
semantic name, description and internal aliases are empty.

| Line | Fields |
|---|---|
| `shape` | short name, name, semantic name, description, handler. One per documented shape |
| `alias` | alias, short name |
| `internal-alias` | internal alias, short name |
| `undocumented` | name, handler. The shapes mermaid's diagrams use internally, such as `state`, `classBox` and `erBox` |
| `key` | name, handler. Every name mermaid's shape lookup accepts, built as `generateShapeMap` builds it |

## Stylesheets

`styles.txt` holds the text of the `style` element mermaid writes for a few fixed sources, which `styleCases` in
`dumps.mjs` lists. Each is rendered with the SVG id `styles`. The `theme-<name>` cases are a quadrant chart in each
theme. A quadrant chart has no styles of its own, so its stylesheet is only the rules every diagram shares. The
`classdef-html` and `classdef-svg` cases are a flowchart with `classDef` statements, with HTML labels on and off. Their
values show how Chromium serializes a declaration before stylis compiles it, such as `#f9f` as `rgb(255, 153, 255)`.
The `flowchart-theme-<name>` cases are a flowchart with a subgraph in each theme. The `flowchart-styled` cases set
`style`, `classDef`, `class` and `linkStyle` on nodes, subgraphs and edges, once in the default theme and classic look
and once in the flowchart's own default, the `redux-color` theme and the `neo` look.

| Line | Fields |
|---|---|
| `source` | case, source as a JSON string |
| `class` | case, index, id, styles as JSON, text styles as JSON. One per class the diagram's `getClasses` returns |
| `style` | case, the text of the `style` element |
| `element` | flowchart case, `node`, DOM id without the SVG id, the `style` of its `.label-container`, the `style` of its label group |
| `element` | flowchart case, `cluster`, DOM id without the SVG id, the `style` of its rect, its `data-color-id` |
| `element` | flowchart case, `edge`, id, the `style` of its path, its `marker-start`, its `marker-end` |

## Curves and markers

`corpus/curves.txt` and `corpus/markers.txt` hold the edge paths and edge markers STM's ports in
`src/SplashTheMusical.Layout` must reproduce. `mise run curves` writes both with `curves.mjs`, which runs in Node. It
imports the d3-shape that `package-lock.json` resolves, and the chunk of mermaid's `mermaid.core` dist that defines
`insertEdge` and `insertMarkers`. It calls those two with a selection that records the elements, attributes and styles
they write, so the rounded curve, the marker offsets, the marker ids and the marker definitions come from mermaid's own
code. `CurveTests` and `EdgeMarkerTests` in `tests/SplashTheMusical.Conformance` check every line.

`curves.txt` starts with four comment lines, naming mermaid, d3-shape and d3-path. Each line that follows has
tab-separated fields. Points are written as `x,y x,y`, with each number as JavaScript's `String()` writes it.

| Line | Fields |
|---|---|
| `curve-name` | a `curve` value of the config schema, read from the `curve?:` type of mermaid's `config.type.d.ts`, in its order |
| `curve` | curve, points, the path. d3-shape's `line()` with that curve, or `insertEdge` with no markers for `rounded`. Empty for no path |
| `edge` | curve, start arrow type, end arrow type, points, the path `insertEdge` draws through the points with no node or cluster to cut them |
| `math` | function, arguments, result. V8's `Math` on seeded inputs, with `-0` for negative zero |
| `marker-offset` | arrow type, `markerOffsets` value |
| `dash-offset` | arrow type, `markerOffsets2` value |

The `math` lines cover the functions the rounded curve and the marker offsets print in full: `acos`, `atan`, `atan2`,
`sin`, `cos` and `hypot`. V8 computes the trigonometric ones with fdlibm, which clang compiles with fused multiply-adds
on arm64, so these lines are those of macOS arm64, where the oracle runs.

`markers.txt` starts with two comment lines. The markers are drawn for the diagram id `ID` and the diagram type `TYPE`,
with the theme variables the neo markers read set to their names in braces, such as `{strokeWidth}`.

| Line | Fields |
|---|---|
| `element` | marker set, depth, tag, then one `name=value` field per attribute and one `style:name=value` field per style, attributes first. Each set's elements are written in the order `insertMarkers` appends them. Tabs, newlines and backslashes in a value are escaped as `\t`, `\n` and `\\` |
| `arrow` | arrow type, look (`classic` or `neo`), the `marker-start` and the `marker-end` `insertEdge` sets for that type at both ends, empty when it sets none |

## Colour functions

`corpus/colors.txt` holds calls of the khroma functions mermaid's themes call, `adjust`, `darken`, `lighten`, `invert`,
`isDark` and `rgba`, and of mermaid's `mkBorder`, with their results. `mise run colors` writes it with
`colors.mjs`, which runs in Node and imports the khroma that `package-lock.json` resolves for mermaid. `mkBorder` is
copied from mermaid's `src/themes/theme-helpers.js`, because mermaid does not export it. `KhromaTests` in
`tests/SplashTheMusical.Conformance` checks every line against `Khroma` in the core.

The colours are the string literals of mermaid's theme code that khroma parses, edge cases of each format, and seeded
random hex, `rgba()` and `hsl()` colours. The `adjust` channels and the `darken` and `lighten` amounts are the
literal ones of the theme code, read from the unminified ESM build in mermaid's dist, with edge cases. Channels the
theme code computes are left out. The edge colours get every channel and amount, and the other colours a seeded few. A
sample of the `hsl()` and `rgb()` results is fed back in, because the themes pass one call's result to the next.

The file starts with three comment lines, the third naming the khroma version:

```
# khroma colour functions on mermaid's theme colours. Refresh with: mise run colors
# mermaid 12.3.0
# khroma 2.1.0
```

Each line that follows is one call, with tab-separated fields: function, colour, arguments, result. The result is the
string khroma returns, `true` or `false` for `isDark`, or `error: ` and the message of the error khroma throws.

| Function | Arguments |
|---|---|
| `adjust` | The channels, such as `h=-10,l=5`. Empty for `{}` |
| `darken`, `lighten` | The amount, or `undefined` when the call passes none |
| `invert`, `isDark` | Empty |
| `mkBorder` | `true` or `false`, the `darkMode` argument |
| `rgba` | The alpha of `rgba(color, alpha)`. With an empty colour, the numbers of `rgba(r, g, b, a)`, such as `1,2,3` |

## SVGs

With an SVG folder, the runner writes `<name>.svg` for each accepted case. The SVGs are build artifacts and are not
committed.

## L4 rasters

`raster.mjs` is L4 of [the parity plan](../../docs/mermaid-parity-plan.md). It draws mermaid.js's SVG and STM's SVG of
each corpus case in one headless Chromium page, so a difference comes from the SVGs and not from the rasterizer.

```bash
mise run raster   # runs stm-svg and corpus-oracle first
node tools/oracle/raster.mjs <corpus> <oracle-svg-dir> <stm-svg-dir> <outcomes-file> <images-dir> [oracle-raster-dir]
```

`mise run stm-svg` writes STM's SVGs to `.artifacts/stm/svg`. `mise run corpus-oracle` writes mermaid.js's to
`.artifacts/corpus/svg`. A case's acceptance by mermaid.js comes from its facts file, and STM's from whether STM wrote
an SVG for it. The run fails when mermaid.js accepts a case and the oracle SVG folder has no SVG for it.

The page serves only the bundled font and draws every text in it, as the oracle's page does. Each SVG is put inline on
a white stage of the size of mermaid.js's SVG, scaled down so its longer side is at most 800 pixels, and fills the
stage with its aspect ratio kept. When mermaid.js refuses a case, STM's SVG sets the size. The stage is screenshotted
at a device scale of 1.

The score is the mean structural similarity (SSIM) of the two screenshots' luma, over 8 by 8 windows 4 pixels apart,
with the constants of Wang et al. 2004. Identical rasters score 1. The threshold of a type is `raster` in its
[tolerance file](../../corpus/tolerances/README.md).

| Outcome | When |
|---|---|
| `n/a` | Neither side has an SVG |
| `report` | The type's tolerance file sets `rasterReportOnly` |
| `pass` | Both sides have an SVG and the score is at least the threshold |
| `fail` | Otherwise, including when only one side has an SVG |

The run writes these files:

| File | Lines |
|---|---|
| The outcomes file, `corpus/l4.txt` for `mise run raster` | A comment line, then case and outcome per case, sorted by UTF-16 code unit |
| `<images-dir>/scores.txt` | A comment line, then case, outcome and score to 4 decimals per case. The score is empty when one side has no SVG |
| `<images-dir>/<name>.png` | mermaid.js's raster on the left and STM's on the right, for the first 10 failing or report-only cases below the threshold of each type. A side without an SVG is grey |

`corpus/l4.txt` is committed. `docs/parity-report.md` takes its L4 column from it, and `RasterOutcomesTests` in
`tests/SplashTheMusical.Conformance` fails when a corpus case has no line in it. The scores and images are build
artifacts. Scores are not committed, because small differences between machines would change them.

### Named cases

With `STM_RASTER_CASES` set to a comma-separated list of case names, such as `flowchart/basic,state/notes`, the run
takes only those cases and writes the side-by-side image of each, whatever its outcome and however many its type has.
It fails when a name is not a corpus case, and it deletes nothing from the oracle raster folder. `mise run screenshots`
runs it this way and writes the outcomes and images to `.artifacts/screenshots`, so `corpus/l4.txt` is unchanged.

### The oracle raster folder

With an oracle raster folder, the run keeps mermaid.js's raster of every case that mermaid.js accepts there, so a later
run draws only STM's side. A cached raster is read back byte for byte, so the scores equal those of a run without the
folder. The folder holds two files per case, in the case's subfolder:

| File | Contents |
|---|---|
| `<name>.png` | mermaid.js's raster of the case |
| `<name>.key` | One line: the raster key, the SHA-256 of the case's oracle SVG, and the raster's width and height, separated by tabs |

The raster key is the SHA-256 of the Chromium version, `raster.mjs` and the bundled fonts. A run reuses a raster only
when both hashes in its key file match. Otherwise it draws the raster again and overwrites the files. After the run it
deletes the files of every case that is no longer in the corpus or that mermaid.js now refuses. The run still fails
when mermaid.js accepts a case and the oracle SVG folder has no SVG for it, whatever the raster folder holds. The last
line of the output counts the rasters read, the rasters drawn and the files deleted.

The fonts Chromium falls back to for glyphs the bundled font lacks are not in the key.

### The CI job

The `l4` job of `.github/workflows/ci.yml` runs on macOS. It runs on macOS because the committed boxes files name the
macOS fonts Chromium falls back to for emoji and CJK text. It caches `.artifacts/corpus/svg` and the oracle raster
folder, `.artifacts/corpus/raster`, with `actions/cache`. The key is the runner's OS, architecture and image OS, such as
`macos15`, and a hash of `package-lock.json`, the oracle scripts, `fonts/`, the corpus's `.mmd` files and
`corpus/config.txt`.

On an exact hit the job skips `facts.mjs`. On a miss it restores the newest cache with the same OS, architecture and
image OS, if any, and rewrites the corpus's oracle output and mermaid.js's SVGs with `facts.mjs`. `raster.mjs` then
redraws only the rasters whose SVG changed. A miss saves the new cache when the job passes.

The job then runs `raster.mjs` and uploads the scores and images as the `l4-raster` artifact. It fails when any file
under `corpus/` then differs from the commit. On a miss that catches oracle output that the pinned mermaid.js no
longer writes. On every run it catches an `l4.txt` that STM's SVGs no longer give.

A weekly scheduled run of the workflow skips the cache and runs the whole job, so a runner image update that changes
the fallback fonts shows up within a week. It saves no cache, so it runs `raster.mjs` without the oracle raster folder.

Filling the folder draws mermaid.js's raster of every accepted case, including the cases STM does not draw yet, so a
hit draws none. That makes a miss that redraws every raster slower than a run without the folder. It is the one-time
cost of a new oracle version. While STM drew no corpus case, the raster step of such a miss took 21m29s on CI, against
8m12s without the folder.
