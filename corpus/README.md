# Corpus

The `.mmd` cases STM is tested on, with the mermaid.js oracle's output for each. Section 7 of
[the parity plan](../docs/mermaid-parity-plan.md) describes the corpus and the harness that reads it.

## Layout

A case is `<type>/<source>-<name>.mmd`. `<type>` is the folder of the diagram type under `packages/mermaid/src/diagrams`
in mermaid, such as `flowchart`, `state` or `user-journey`. ZenUML, which mermaid keeps outside that folder, is
`zenuml`.

| Source | What it is | Written by |
|---|---|---|
| `docs` | The examples of mermaid's syntax docs | `mise run corpus` |
| `e2e` | The fixtures of mermaid's rendering tests | `mise run corpus` |
| `parser` | The diagram sources of mermaid's parser tests, including sources mermaid.js refuses | `mise run corpus` |
| `mermaider` | The hand-written cases, repros, gallery examples and spec test sources of ipjohnson/mermaider | Copied once |

The `mermaider` cases are the sources of mermaider's `MjsCorpus` at commit `1d04c901808aa369c3ddfc0a057e8a0b30d6619c`,
written out by its `Export_corpus` test and copied with `node tools/corpus/corpus.mjs --add <folder> mermaider`. Of
its 4,365 sources, 3,945 are upstream cases that `mise run corpus` writes too, and 16 of the 420 others are the same
text as an upstream case. Both kinds were left out, so the corpus holds every source of mermaider once.

`SOURCE.txt` names the mermaid tag and commit the upstream cases come from, and holds mermaid's MIT license, which the
`docs`, `e2e` and `parser` cases are under.

Beside each case are the files the oracle in `tools/oracle` writes for it. Its README describes their formats.

| File | Per |
|---|---|
| `<name>.facts.txt` | case |
| `<name>.boxes.txt` | case mermaid.js accepts |
| `<name>.dagre.txt` | accepted flowchart, state, class and ER case |

`themes.txt`, `config.txt`, `shapes.txt` and `styles.txt` in this folder are the oracle's theme variables, default
config, shape table and stylesheets. `colors.txt` holds the results of the colour functions mermaid's themes call, which `mise run colors` writes. `ledger` holds the parity ledgers, which [its README](ledger/README.md) describes. `tolerances` holds the L3
and L4 tolerances of each type, which [its README](tolerances/README.md) describes. `l4.txt` holds the L4 outcome of
each case, which `mise run raster` writes and [the oracle README](../tools/oracle/README.md#l4-rasters) describes.

## Refreshing

```bash
mise run corpus <mermaid checkout>   # rewrite the docs, e2e and parser cases, SOURCE.txt and the counts below
mise run corpus-oracle               # rewrite every facts, boxes and dagre file with the pinned mermaid.js
```

`mise run upstream-bump <version>` runs both after it moves the oracle to another mermaid. The checkout must be at the
tag of the mermaid that `tools/oracle/package.json` pins. `mise run corpus` deletes every
`docs-*`, `e2e-*` and `parser-*` case before it writes them, and leaves the other cases alone. An e2e or parser source
that is already in the corpus is left out, and so is a source whose first line is not a diagram header.

A new hand-written case goes in its type's folder with a source prefix other than `docs`, `e2e` and `parser`. Run
`mise run corpus-oracle` to write its facts, then `node tools/corpus/corpus.mjs --counts` to update the counts.

Each facts, boxes and dagre file holds a hash of its case's text. `CorpusTests` in `tests/SplashTheMusical.Conformance`
fails when a case has no facts file, a file has no case, a boxes file is missing or left over, a hash does not match, or
a file names another mermaid than `tools/oracle/package.json` pins. It needs no Node.

## Counts

<!-- counts: written by tools/corpus/corpus.mjs -->

| Type | docs | e2e | parser | mermaider | Total |
|---|--:|--:|--:|--:|--:|
| `agentflow` | 12 | 39 | 157 | 0 | 208 |
| `architecture` | 6 | 20 | 32 | 12 | 70 |
| `block` | 30 | 58 | 28 | 9 | 125 |
| `c4` | 6 | 32 | 27 | 2 | 67 |
| `class` | 38 | 276 | 136 | 48 | 498 |
| `cynefin` | 3 | 12 | 16 | 0 | 31 |
| `er` | 25 | 206 | 81 | 46 | 358 |
| `eventmodeling` | 13 | 6 | 16 | 0 | 35 |
| `flowchart` | 116 | 408 | 255 | 112 | 891 |
| `gantt` | 11 | 52 | 19 | 3 | 85 |
| `git` | 35 | 126 | 94 | 3 | 258 |
| `info` | 0 | 3 | 6 | 0 | 9 |
| `ishikawa` | 1 | 13 | 3 | 0 | 17 |
| `kanban` | 3 | 11 | 27 | 6 | 47 |
| `mindmap` | 15 | 33 | 28 | 7 | 83 |
| `packet` | 4 | 6 | 18 | 2 | 30 |
| `pie` | 2 | 24 | 36 | 5 | 67 |
| `quadrant-chart` | 3 | 14 | 32 | 3 | 52 |
| `radar` | 3 | 6 | 27 | 2 | 38 |
| `railroad` | 14 | 9 | 85 | 12 | 120 |
| `requirement` | 9 | 172 | 2 | 5 | 188 |
| `sankey` | 8 | 18 | 4 | 3 | 33 |
| `sequence` | 38 | 193 | 141 | 50 | 422 |
| `state` | 22 | 144 | 95 | 27 | 288 |
| `swimlanes` | 13 | 50 | 6 | 0 | 69 |
| `timeline` | 14 | 29 | 11 | 5 | 59 |
| `treeView` | 13 | 15 | 36 | 11 | 75 |
| `treemap` | 8 | 26 | 17 | 2 | 53 |
| `usecase` | 24 | 32 | 127 | 3 | 186 |
| `user-journey` | 1 | 18 | 7 | 4 | 30 |
| `venn` | 8 | 21 | 17 | 2 | 48 |
| `wardley` | 19 | 10 | 20 | 5 | 54 |
| `xychart` | 8 | 48 | 62 | 2 | 120 |
| `zenuml` | 16 | 1 | 0 | 13 | 30 |
| Total | 541 | 2131 | 1668 | 404 | 4744 |

<!-- end of counts -->
