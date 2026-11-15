# L3 and L4 tolerances

Each `<type>.json` holds the L3 and L4 tolerances of the corpus cases of one diagram type. Section 7 of
[the parity plan](../../docs/mermaid-parity-plan.md) describes both levels. `GeometryComparer` in
`tests/SplashTheMusical.Conformance` applies the L3 tolerances, and `ToleranceTests` checks the files. Neither needs
Node. `tools/oracle/raster.mjs` applies the L4 tolerances, and [the oracle README](../../tools/oracle/README.md#l4-rasters)
describes how it scores a case.

## What L3 checks

L3 pairs each box of STM's boxes with the box of mermaid.js's boxes that has the same kind and name. A box only one
side has is an L2 difference. Both sides' boxes are moved so their canvas starts at 0, 0. A case fails L3 when any of
these holds:

- The canvas width or height differs from mermaid.js's by more than `canvas`, as a fraction of mermaid.js's.
- Two boxes relate differently on an axis. On each axis a box's center is before, in the same rank as, or after the
  other's. Centers within 1 user unit share a rank.
- A box's intersection over union with its counterpart is under `overlap`. A side shorter than 10 user units, such as
  a straight edge's, is widened to 10 about its center first.
- `OverlapLint` finds an overlap in STM's boxes that it does not find in mermaid.js's.

`OverlapLint` gives each box kind a role per type: node, label, note or group. It reports a label on a label, a label
on a node, a node on a node, a note on a note, a label or a node, two groups that cross, and a box outside the canvas.
A node inside a node and a group inside a group nest, so they do not count. Kinds without a role, such as edges, are
not linted. The roles are the `Roles` table in `OverlapLint.cs`.

## Format

```json
{
  "canvas": 0.05,
  "overlap": 0.8,
  "raster": 0.9
}
```

| Key | Value |
|---|---|
| `canvas` | How far the canvas width and height may differ, from 0 to 1 |
| `overlap` | The least intersection over union of a box and its counterpart, from 0 to 1 |
| `raster` | The least structural similarity of STM's raster to mermaid.js's at L4, from 0 to 1 |
| `rasterReportOnly` | `true` when L4 reports the type's scores and does not judge them. Optional, `false` by default |
| `reason` | Why the type is looser than the starting tolerances. Optional |

Every corpus type has a file. The starting tolerances are a `canvas` of 0.05, an `overlap` of 0.8 and a `raster` of
0.9. A type may tighten them freely. A file with a larger `canvas`, a smaller `overlap` or `raster`, or
`rasterReportOnly` set fails `ToleranceTests` unless it has a `reason`.

`mindmap` and `architecture` set `rasterReportOnly`, because their default layouts are force-directed. The plan makes
L4 report-only for such types.
