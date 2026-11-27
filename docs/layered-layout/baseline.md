# Layered layout baseline

How close the dagre port and Mermaider's `Sugiyama` engine come to the layouts ELK gives mermaid.js 12.3.0, on the
2,304 corpus cases with an `.elk.txt` recording. Each engine lays out the graph mermaid.js gave ELK, and
`ElkLayoutComparer` in `tests/SplashTheMusical.Conformance` scores its layout against the one ELK returned.
`mise run baseline` writes this file, and `ElkBaselineTests` fails when it is out of date.

This is the starting point of the layered layout engine of ipjohnson/SplashTheMusical#25, which is written under
[the clean-room rules](clean-room.md).

## Measures

ELK gives no layers, so they are read from its coordinates, and from an engine's in the same way. Along the layout
direction, a layer is a maximal run of leaf nodes whose extents overlap. A layer's order is its leaves' order across
the layout direction, by their centres. A case passes a measure when every ELK call it records does.

1,653 of the 2,304 cases have more than one layer in ELK's layout. The coordinates are
the canvas's, so the boxes measure also holds an engine to ELK's padding around the drawing.

- **Same layers**: every leaf node is in the layer ELK put it in.
- **Same order**: the leaves of each of ELK's layers are in ELK's order.
- **Boxes and canvas**: the box of every node, clusters included, overlaps ELK's by at least 80% of the larger
  box, and the canvas's width and height are each within 5% of ELK's.
- **Same bends**: every edge turns as many times as ELK's. Repeated points and points on a straight run are not bends.

## The dagre port

`src/SplashTheMusical.Layout/Dagre`, given the graph options of the case's `.dagre.txt` with ELK's direction, or
mermaid's flowchart options when the case has none. Clusters are compound nodes, and an edge that ends on a cluster
ends on its first leaf. The edge label's size is the edge's.

| Type | Cases | Same layers | Same order | Boxes and canvas | Same bends | All four |
|---|---:|---:|---:|---:|---:|---:|
| agentflow | 194 | 92.8% (180 of 194) | 98.5% (191 of 194) | 19.6% (38 of 194) | 80.4% (156 of 194) | 19.1% (37 of 194) |
| class | 382 | 91.6% (350 of 382) | 98.2% (375 of 382) | 23.0% (88 of 382) | 81.2% (310 of 382) | 20.9% (80 of 382) |
| er | 349 | 97.7% (341 of 349) | 96.0% (335 of 349) | 24.4% (85 of 349) | 71.1% (248 of 349) | 23.8% (83 of 349) |
| flowchart | 804 | 90.9% (731 of 804) | 94.2% (757 of 804) | 19.2% (154 of 804) | 66.5% (535 of 804) | 16.4% (132 of 804) |
| requirement | 179 | 100.0% (179 of 179) | 90.5% (162 of 179) | 12.3% (22 of 179) | 89.9% (161 of 179) | 12.3% (22 of 179) |
| state | 284 | 67.3% (191 of 284) | 93.7% (266 of 284) | 1.1% (3 of 284) | 45.1% (128 of 284) | 1.1% (3 of 284) |
| usecase | 112 | 83.0% (93 of 112) | 92.0% (103 of 112) | 13.4% (15 of 112) | 58.9% (66 of 112) | 7.1% (8 of 112) |
| **Total** | 2,304 | 89.6% (2,065 of 2,304) | 95.0% (2,189 of 2,304) | 17.6% (405 of 2,304) | 69.6% (1,604 of 2,304) | 15.8% (365 of 2,304) |

## Mermaider's Sugiyama

The `Sugiyama` package 0.17.0, built from `src/Sugiyama` of mermaider at commit
[`d35b8836`](https://github.com/ipjohnson/mermaider/commit/d35b8836d585efb13f8caa17b082510e17626502). It is a package
reference of `tests/SplashTheMusical.Conformance` only. Each type gets the entry point and options Mermaider's layout
engine for it passes: `SugiyamaLayout.Compute` with the ER options for ER, and `HierarchicalLayout.Compute` with the
class, requirement, state or flowchart options for the rest. Clusters are subgraphs, and an edge that ends on a cluster
ends on its first leaf with the cluster as its group.

| Type | Cases | Same layers | Same order | Boxes and canvas | Same bends | All four |
|---|---:|---:|---:|---:|---:|---:|
| agentflow | 194 | 92.3% (179 of 194) | 94.3% (183 of 194) | 30.4% (59 of 194) | 87.1% (169 of 194) | 29.9% (58 of 194) |
| class | 382 | 90.1% (344 of 382) | 91.6% (350 of 382) | 0.0% (0 of 382) | 92.7% (354 of 382) | 0.0% (0 of 382) |
| er | 349 | 98.0% (342 of 349) | 92.8% (324 of 349) | 15.2% (53 of 349) | 83.4% (291 of 349) | 14.6% (51 of 349) |
| flowchart | 804 | 92.8% (746 of 804) | 92.9% (747 of 804) | 19.7% (158 of 804) | 82.2% (661 of 804) | 19.2% (154 of 804) |
| requirement | 179 | 96.6% (173 of 179) | 87.2% (156 of 179) | 0.0% (0 of 179) | 95.0% (170 of 179) | 0.0% (0 of 179) |
| state | 284 | 74.3% (211 of 284) | 97.9% (278 of 284) | 1.4% (4 of 284) | 66.9% (190 of 284) | 1.4% (4 of 284) |
| usecase | 112 | 82.1% (92 of 112) | 80.4% (90 of 112) | 11.6% (13 of 112) | 77.7% (87 of 112) | 10.7% (12 of 112) |
| **Total** | 2,304 | 90.6% (2,087 of 2,304) | 92.4% (2,128 of 2,304) | 12.5% (287 of 2,304) | 83.4% (1,922 of 2,304) | 12.1% (279 of 2,304) |

## Sugiyama against the dagre port

Of all recorded cases, `Sugiyama` passes same layers (2,087 cases against 2,065) and same bends (1,922 cases against 1,604) more often than the dagre port, and same order (2,128 cases against 2,189), boxes and canvas (287 cases against 405) and all four (279 cases against 365) less often.

The cases `Sugiyama` passes minus the cases the dagre port passes:

| Type | Same layers | Same order | Boxes and canvas | Same bends | All four |
|---|---:|---:|---:|---:|---:|
| agentflow | -1 | -8 | +21 | +13 | +21 |
| class | -6 | -25 | -88 | +44 | -80 |
| er | +1 | -11 | -32 | +43 | -32 |
| flowchart | +15 | -10 | +4 | +126 | +22 |
| requirement | -6 | -6 | -22 | +9 | -22 |
| state | +20 | +12 | +1 | +62 | +1 |
| usecase | -1 | -13 | -2 | +21 | +4 |
| **Total** | +22 | -61 | -118 | +318 | -86 |

## Cases an engine could not lay out

A case whose layout throws fails every measure.

| Engine | Cases |
|---|---:|
| dagre port | 0 |
| Sugiyama | 0 |
