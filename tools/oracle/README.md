# Oracle

The oracle runs the mermaid.js that `package-lock.json` pins in headless Chromium and writes what STM is compared
against. Section 7 of [the parity plan](../../docs/mermaid-parity-plan.md) describes its place in the harness.

## Running it

```bash
mise run oracle                              # cases in tools/oracle/cases, output in .artifacts/oracle
mise run oracle <cases> <facts-dir> <svg-dir>
mise run oracle-test                         # node --test tools/oracle/facts.test.mjs
```

Both tasks run `npm ci` in `tools/oracle` when `node_modules` is missing. Without mise, run
`node tools/oracle/facts.mjs <cases> <facts-dir> [svg-dir]` after `npm ci`.

The run fails in these cases:

- `node_modules` holds a version of a package in `package.json` other than the one `package-lock.json` pins. Run
  `npm ci` in `tools/oracle`.
- A case has a diagram type that `extract.mjs` has no extractor for. Add one.

Facts and boxes files whose case is no longer in the cases folder are deleted. A case that mermaid.js refuses has no
boxes file.

## What it pins

| Package | Version | Why |
|---|---|---|
| `mermaid` | 12.3.0 | The version STM targets. Its build includes the ELK layouts |
| `@mermaid-js/mermaid-zenuml` | 1.0.1 | ZenUML is an external diagram, registered with `registerExternalDiagrams` |
| `@mermaid-js/layout-tidy-tree` | 1.0.1 | The `tidy-tree` layout, registered with `registerLayoutLoaders` |
| `puppeteer` | 25.17.0 | Headless Chromium |

To move to another mermaid version, change it in `package.json`, run `npm install` in `tools/oracle`, then run the
oracle.

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
test the runner. The corpus STM is measured on is a separate folder, `corpus/` in the plan.

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
| `block` | `block` by the id of the facts. `edge` and `edge-label` by ends |
| `usecase` | `actor`, `usecase`, `boundary` and `json` by id. `note` by target. `relation` and `relation-label` by ends |
| `treeView` | `node` by path |
| `agentflow` | `title`. `node` and `flow` by id. `edge` and `edge-label` by ends |
| `cynefin` | `title`. `domain-label` by domain. `item` by domain and index. `transition` and `transition-label` by ends |
| `eventmodeling` | `title`. `lane` and `frame` by index. `relation` by frame names |
| `info` | `version` |
| `ishikawa` | `head` by text. `cause` by path |

## SVGs

With an SVG folder, the runner writes `<name>.svg` for each accepted case. The SVGs are build artifacts and are not
committed.
