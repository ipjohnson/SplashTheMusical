# What the recorded cases ask of ELK

The graphs mermaid.js 12.3.0 gives ELK in the 2,304 corpus cases that have an `.elk.txt` file, with
2,304 ELK calls between them. `mise run elk-survey` writes this file from those files alone, and
`ElkSurveyTests` in `tests/SplashTheMusical.Conformance` fails when it is out of date. The section "ELK files" of
[the oracle README](../../tools/oracle/README.md) describes the recordings. The survey scopes the layered layout
engine of ipjohnson/SplashTheMusical#25 under [the clean-room rules](clean-room.md).

A count of cases counts a case once however many of its calls, nodes or edges qualify. A case with more than one
call adds the nodes and edges of its calls.

## Cases

| Type | Cases | ELK calls |
|---|---:|---:|
| agentflow | 194 | 194 |
| class | 382 | 382 |
| er | 349 | 349 |
| flowchart | 804 | 804 |
| requirement | 179 | 179 |
| state | 284 | 284 |
| usecase | 112 | 112 |
| **Total** | 2,304 | 2,304 |

## Algorithms

The cases whose root, or one of whose containers, sets `elk.algorithm` to each value. A root that does not set it
is counted under "not set".

| Algorithm | Cases, root | Cases, container |
|---|---:|---:|
| `elk.box` | 1 | 1 |
| `elk.layered` | 2,302 | 136 |
| `elk.rectpacking` | 1 | 1 |

## Layout options

Every `layoutOptions` key mermaid.js sets, by the kind of its owner, with the cases that set each value and the
owners that carry it. A root is the graph's top node, a container a node with children, and a node one without.
A key with more than 12 values on one kind of owner gets one row that counts its distinct values.
No case sets a key of `properties`.

| Owner | Key | Value | Cases | Owners | agentflow | class | er | flowchart | requirement | state | usecase |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| root | `elk.algorithm` | `elk.box` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `elk.algorithm` | `elk.layered` | 2,302 | 2,302 | 194 | 382 | 349 | 802 | 179 | 284 | 112 |
| root | `elk.algorithm` | `elk.rectpacking` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `elk.aspectRatio` | `1.6` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `elk.contentAlignment` | `H_CENTER V_TOP` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `elk.direction` | `DOWN` | 1,925 | 1,925 | 183 | 366 | 334 | 608 | 151 | 280 | 3 |
| root | `elk.direction` | `LEFT` | 25 | 25 | 0 | 5 | 3 | 7 | 9 | 0 | 1 |
| root | `elk.direction` | `RIGHT` | 331 | 331 | 11 | 8 | 9 | 182 | 10 | 4 | 107 |
| root | `elk.direction` | `UP` | 23 | 23 | 0 | 3 | 3 | 7 | 9 | 0 | 1 |
| root | `elk.expandNodes` | `true` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `elk.hierarchyHandling` | `INCLUDE_CHILDREN` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.considerModelOrder.strategy` | `NODES_AND_EDGES` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.crossingMinimization.forceNodeModelOrder` | `false` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.cycleBreaking.strategy` | `DEPTH_FIRST` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.edgeRouting.selfLoopDistribution` | `EQUALLY` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.layering.coffmanGraham.layerBound` | `4` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.layering.strategy` | `NETWORK_SIMPLEX` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.mergeEdges` | `false` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.mergeHierarchyEdges` | `true` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.nodePlacement.bk.fixedAlignment` | `BALANCED` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.nodePlacement.strategy` | `BRANDES_KOEPF` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.unnecessaryBendpoints` | `true` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.wrapping.multiEdge.improveCuts` | `true` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.layered.wrapping.multiEdge.improveWrappedEdges` | `true` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `elk.padding` | `[top=15,left=15,bottom=15,right=15]` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `elk.rectpacking.packing.compaction.iterations` | `10` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `elk.rectpacking.packing.compaction.rowHeightReevaluation` | `true` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `elk.rectpacking.trybox` | `true` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `elk.rectpacking.whiteSpaceElimination.strategy` | `EQUAL_BETWEEN_STRUCTURES` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `elk.rectpacking.widthApproximation.strategy` | `SCANLINE` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `elk.spacing.portsSurrounding` | `[top=12,left=12,bottom=12,right=12]` | 2,304 | 2,304 | 194 | 382 | 349 | 804 | 179 | 284 | 112 |
| root | `spacing.baseValue` | `15` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| root | `spacing.baseValue` | `40` | 2,303 | 2,303 | 194 | 382 | 349 | 803 | 179 | 284 | 112 |
| root | `spacing.nodeNode` | `15` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| container | `elk.algorithm` | `elk.box` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| container | `elk.algorithm` | `elk.layered` | 136 | 367 | 0 | 0 | 6 | 22 | 0 | 108 | 0 |
| container | `elk.algorithm` | `elk.rectpacking` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| container | `elk.aspectRatio` | `1.6` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| container | `elk.aspectRatio` | `2.0` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| container | `elk.contentAlignment` | `H_CENTER V_TOP` | 2 | 2 | 1 | 0 | 0 | 1 | 0 | 0 | 0 |
| container | `elk.direction` | `DOWN` | 124 | 336 | 0 | 0 | 5 | 13 | 0 | 106 | 0 |
| container | `elk.direction` | `LEFT` | 12 | 12 | 0 | 0 | 2 | 9 | 0 | 1 | 0 |
| container | `elk.direction` | `RIGHT` | 8 | 9 | 0 | 0 | 0 | 7 | 0 | 1 | 0 |
| container | `elk.direction` | `UP` | 10 | 10 | 0 | 0 | 1 | 8 | 0 | 1 | 0 |
| container | `elk.expandNodes` | `true` | 2 | 2 | 1 | 0 | 0 | 1 | 0 | 0 | 0 |
| container | `elk.hierarchyHandling` | `INCLUDE_CHILDREN` | 210 | 391 | 33 | 19 | 9 | 86 | 0 | 40 | 23 |
| container | `elk.hierarchyHandling` | `SEPARATE_CHILDREN` | 124 | 345 | 1 | 0 | 3 | 18 | 0 | 102 | 0 |
| container | `elk.layered.cycleBreaking.strategy` | `DEPTH_FIRST` | 487 | 1,004 | 77 | 43 | 37 | 161 | 0 | 134 | 35 |
| container | `elk.layered.mergeEdges` | `false` | 487 | 1,004 | 77 | 43 | 37 | 161 | 0 | 134 | 35 |
| container | `elk.layered.nodePlacement.bk.fixedAlignment` | `BALANCED` | 487 | 1,004 | 77 | 43 | 37 | 161 | 0 | 134 | 35 |
| container | `elk.layered.nodePlacement.networkSimplex.nodeFlexibility` | `PORT_POSITION` | 487 | 1,004 | 77 | 43 | 37 | 161 | 0 | 134 | 35 |
| container | `elk.layered.nodePlacement.strategy` | `BRANDES_KOEPF` | 487 | 1,004 | 77 | 43 | 37 | 161 | 0 | 134 | 35 |
| container | `elk.layered.spacing.edgeNodeBetweenLayers` | `30` | 487 | 1,004 | 77 | 43 | 37 | 161 | 0 | 134 | 35 |
| container | `elk.padding` | `[top=24,left=24,bottom=24,right=24]` | 485 | 1,002 | 76 | 43 | 37 | 160 | 0 | 134 | 35 |
| container | `elk.padding` | `[top=29,left=10,bottom=10,right=10]` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| container | `elk.padding` | `[top=34,left=15,bottom=15,right=15]` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| container | `elk.rectpacking.packing.compaction.iterations` | `10` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| container | `elk.rectpacking.packing.compaction.rowHeightReevaluation` | `true` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| container | `elk.rectpacking.trybox` | `true` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| container | `elk.rectpacking.whiteSpaceElimination.strategy` | `EQUAL_BETWEEN_STRUCTURES` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| container | `elk.rectpacking.widthApproximation.strategy` | `SCANLINE` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| container | `elk.spacing.edgeEdge` | `20` | 487 | 1,004 | 77 | 43 | 37 | 161 | 0 | 134 | 35 |
| container | `elk.spacing.portsSurrounding` | `[top=12,left=12,bottom=12,right=12]` | 487 | 1,004 | 77 | 43 | 37 | 161 | 0 | 134 | 35 |
| container | `nodeLabels.placement` | `[H_CENTER V_TOP, INSIDE]` | 487 | 1,004 | 77 | 43 | 37 | 161 | 0 | 134 | 35 |
| container | `nodeSize.constraints` | `[MINIMUM_SIZE, NODE_LABELS]` | 461 | 859 | 77 | 43 | 37 | 161 | 0 | 108 | 35 |
| container | `nodeSize.minimum` | 224 distinct values | 461 | 859 | 77 | 43 | 37 | 161 | 0 | 108 | 35 |
| container | `spacing.baseValue` | `15` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| container | `spacing.baseValue` | `24` | 486 | 1,003 | 76 | 43 | 37 | 161 | 0 | 134 | 35 |
| container | `spacing.nodeNode` | `15` | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| container | `spacing.nodeNode` | `50` | 486 | 1,003 | 76 | 43 | 37 | 161 | 0 | 134 | 35 |
| node | `elk.hierarchyHandling` | `INCLUDE_CHILDREN` | 210 | 645 | 33 | 19 | 9 | 86 | 0 | 40 | 23 |
| node | `elk.layered.layering.layerConstraint` | `FIRST` | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |
| node | `elk.portAlignment.default` | `CENTER` | 94 | 173 | 0 | 0 | 0 | 0 | 0 | 0 | 94 |
| edge-label | `edgeLabels.inline` | `true` | 1,665 | 5,709 | 133 | 187 | 283 | 601 | 141 | 240 | 80 |
| edge-label | `edgeLabels.placement` | `CENTER` | 1,665 | 5,709 | 133 | 187 | 283 | 601 | 141 | 240 | 80 |
| edge-label | `edgeLabels.placement` | `HEAD` | 36 | 77 | 0 | 36 | 0 | 0 | 0 | 0 | 0 |
| edge-label | `edgeLabels.placement` | `TAIL` | 34 | 71 | 0 | 34 | 0 | 0 | 0 | 0 | 0 |

## Graph features

The cases that have each feature, and the edges or nodes that have it across all cases. The ends of an edge are
its nodes, with a port taken as its node.

- A nested graph is a node with children.
- An edge label is a label with a width and a height. mermaid.js gives most edges without a label an empty one.
- A self loop is an edge with the same node at both ends.
- A parallel edge is an edge whose source and target an earlier edge already joins in the same direction.
- A reverse pair is two nodes joined by edges in both directions.
- A cycle is a directed cycle of two or more nodes. Self loops do not make one.
- The components are the weakly connected components of the nodes other than the root, where an edge joins its
  ends and a child joins its container. A graph with more than one is disconnected.
- A hierarchy-crossing edge has ends with different parents.

| Feature | Cases | Share | Count | agentflow | class | er | flowchart | requirement | state | usecase |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Nested graphs | 487 | 21.1% | 1,004 containers | 77 | 43 | 37 | 161 | 0 | 134 | 35 |
| Nesting depth 3 or more | 139 | 6.0% |  | 15 | 20 | 10 | 33 | 0 | 61 | 0 |
| Ports | 0 | 0.0% | 0 ports | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Self loops | 38 | 1.6% | 51 edges | 2 | 4 | 19 | 4 | 0 | 8 | 1 |
| Edge labels | 826 | 35.9% | 2,240 edges | 13 | 109 | 279 | 172 | 141 | 85 | 27 |
| Node labels | 487 | 21.1% | 1,004 labels | 77 | 43 | 37 | 161 | 0 | 134 | 35 |
| Parallel edges | 38 | 1.6% | 75 edges | 0 | 4 | 11 | 3 | 3 | 11 | 6 |
| Reverse pairs | 70 | 3.0% | 123 pairs | 2 | 1 | 0 | 10 | 0 | 57 | 0 |
| Cycles | 90 | 3.9% |  | 2 | 1 | 7 | 18 | 0 | 62 | 0 |
| Disconnected components | 378 | 16.4% |  | 28 | 88 | 60 | 119 | 42 | 18 | 23 |
| Hyperedges, more than one source or target | 0 | 0.0% | 0 edges | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Hierarchy-crossing edges | 210 | 9.1% | 445 edges | 33 | 19 | 9 | 86 | 0 | 40 | 23 |
| Edges that end on a container | 153 | 6.6% | 349 edges | 17 | 0 | 11 | 43 | 0 | 82 | 0 |
| Edges held by a container | 0 | 0.0% | 0 edges | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

The cases by nesting depth, where 1 is a graph without containers and 0 a graph without nodes.

| Depth | Cases | agentflow | class | er | flowchart | requirement | state | usecase |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 25 | 1 | 2 | 0 | 8 | 1 | 9 | 4 |
| 1 | 1,792 | 116 | 337 | 312 | 635 | 178 | 141 | 73 |
| 2 | 348 | 62 | 23 | 27 | 128 | 0 | 73 | 35 |
| 3 | 106 | 13 | 7 | 10 | 29 | 0 | 47 | 0 |
| 4 | 30 | 2 | 11 | 0 | 3 | 0 | 14 | 0 |
| 5 | 3 | 0 | 2 | 0 | 1 | 0 | 0 | 0 |

The cases by number of components.

| Components | Cases | agentflow | class | er | flowchart | requirement | state | usecase |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 25 | 1 | 2 | 0 | 8 | 1 | 9 | 4 |
| 1 to 5 | 2,250 | 193 | 370 | 348 | 791 | 166 | 275 | 107 |
| 6 to 10 | 27 | 0 | 8 | 1 | 5 | 12 | 0 | 1 |
| 11 to 20 | 2 | 0 | 2 | 0 | 0 | 0 | 0 | 0 |

## Graph sizes

The nodes of a case are all its nodes other than the root, containers included. The median and the 90th
percentile are nearest-rank.

### Nodes per case

| Type | Cases | Min | Median | 90th percentile | Max | Total |
|---|---:|---:|---:|---:|---:|---:|
| agentflow | 194 | 0 | 3 | 6 | 15 | 645 |
| class | 382 | 0 | 2 | 6 | 21 | 1,157 |
| er | 349 | 1 | 3 | 7 | 29 | 1,252 |
| flowchart | 804 | 0 | 3 | 8 | 53 | 3,240 |
| requirement | 179 | 0 | 2 | 6 | 14 | 528 |
| state | 284 | 0 | 5 | 14 | 21 | 1,760 |
| usecase | 112 | 0 | 4 | 8 | 9 | 439 |
| **All** | 2,304 | 0 | 3 | 8 | 53 | 9,021 |

| Nodes | Cases | agentflow | class | er | flowchart | requirement | state | usecase |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 25 | 1 | 2 | 0 | 8 | 1 | 9 | 4 |
| 1 to 5 | 1,794 | 169 | 324 | 300 | 612 | 159 | 143 | 87 |
| 6 to 10 | 363 | 18 | 40 | 37 | 144 | 13 | 90 | 21 |
| 11 to 20 | 113 | 6 | 15 | 10 | 35 | 6 | 41 | 0 |
| 21 to 50 | 8 | 0 | 1 | 2 | 4 | 0 | 1 | 0 |
| 51 to 100 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 |

### Edges per case

| Type | Cases | Min | Median | 90th percentile | Max | Total |
|---|---:|---:|---:|---:|---:|---:|
| agentflow | 194 | 0 | 1 | 4 | 10 | 298 |
| class | 382 | 0 | 0 | 3 | 23 | 488 |
| er | 349 | 0 | 1 | 6 | 25 | 829 |
| flowchart | 804 | 0 | 1 | 8 | 46 | 2,322 |
| requirement | 179 | 0 | 1 | 5 | 8 | 266 |
| state | 284 | 0 | 4 | 10 | 22 | 1,260 |
| usecase | 112 | 0 | 2 | 6 | 7 | 246 |
| **All** | 2,304 | 0 | 1 | 7 | 46 | 5,709 |

| Edges | Cases | agentflow | class | er | flowchart | requirement | state | usecase |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 639 | 61 | 195 | 66 | 203 | 38 | 44 | 32 |
| 1 to 5 | 1,350 | 119 | 173 | 247 | 481 | 128 | 135 | 67 |
| 6 to 10 | 249 | 14 | 9 | 25 | 90 | 13 | 85 | 13 |
| 11 to 20 | 55 | 0 | 4 | 7 | 25 | 0 | 19 | 0 |
| 21 to 50 | 11 | 0 | 1 | 4 | 5 | 0 | 1 | 0 |

## The 20 largest cases

By nodes and edges together, then by nodes, then by name.

| # | Case | Nodes | Edges | Containers | Depth | Components | Edge labels |
|---:|---|---:|---:|---:|---:|---:|---:|
| 1 | `flowchart/mermaider-flowchart-expanded-shapes` | 53 | 46 | 0 | 1 | 7 | 0 |
| 2 | `flowchart/e2e-flowchart-nightly-check-handdrawn-should-render-a-flowchart-full-of-icons` | 22 | 40 | 0 | 1 | 1 | 0 |
| 3 | `flowchart/e2e-flowchart-visual-check-7-should-render-a-flowchart-full-of-icons` | 22 | 40 | 0 | 1 | 1 | 0 |
| 4 | `er/mermaider-db-erd-20-content` | 29 | 25 | 0 | 1 | 6 | 25 |
| 5 | `flowchart/mermaider-agentdriver-work-1` | 22 | 25 | 0 | 1 | 1 | 4 |
| 6 | `er/mermaider-db-erd-19-docset` | 22 | 23 | 0 | 1 | 4 | 23 |
| 7 | `class/mermaider-agentdriver-ledger-2` | 21 | 23 | 0 | 1 | 1 | 16 |
| 8 | `er/mermaider-db-erd-25-release-notes` | 20 | 24 | 0 | 1 | 1 | 24 |
| 9 | `class/e2e-class-diagram-visual-check-dark-theme` | 20 | 19 | 0 | 1 | 9 | 10 |
| 10 | `class/e2e-class-diagram-visual-check-default-theme` | 20 | 19 | 0 | 1 | 9 | 10 |
| 11 | `class/e2e-class-diagram-visual-check-forest-theme` | 20 | 19 | 0 | 1 | 9 | 10 |
| 12 | `class/e2e-class-diagram-visual-check-neutral-theme` | 20 | 19 | 0 | 1 | 9 | 10 |
| 13 | `flowchart/mermaider-db-flow-01-system` | 18 | 21 | 5 | 2 | 1 | 3 |
| 14 | `er/mermaider-db-erd-05-building-blocks` | 16 | 22 | 0 | 1 | 1 | 22 |
| 15 | `state/e2e-state-diagram-v2-pr-check-should-render-edge-labels-correctly-with-multiple-transitions` | 19 | 17 | 9 | 2 | 1 | 15 |
| 16 | `state/e2e-state-diagram-v2-nightly-check-dark-theme` | 20 | 14 | 6 | 3 | 2 | 6 |
| 17 | `state/e2e-state-diagram-v2-nightly-check-default-theme` | 20 | 14 | 6 | 3 | 2 | 6 |
| 18 | `state/e2e-state-diagram-v2-nightly-check-forest-theme` | 20 | 14 | 6 | 3 | 2 | 6 |
| 19 | `state/e2e-state-diagram-v2-nightly-check-neutral-theme` | 20 | 14 | 6 | 3 | 2 | 6 |
| 20 | `flowchart/e2e-flowchart-nightly-check-handdrawn-should-render-a-flowchart-with-long-names-and-class-definitions` | 15 | 19 | 0 | 1 | 1 | 0 |

## Where mermaid.js sets the options

From `packages/mermaid/src/rendering-util/layout-algorithms/elk/render.ts` of mermaid-js/mermaid at `mermaid@12.3.0`.

- The root gets one block of options. Its `elk.direction` comes from the diagram's direction, and `config.elk`
  can override others, such as `elk.layered.nodePlacement.strategy` and `elk.layered.cycleBreaking.strategy`,
  whose defaults come from `config.elk.preset`. A root laid out with `elk.rectpacking` also gets the rectpacking
  options and spacings of 15.
- A subgraph gets its own block of layered options, `elk.padding` of 24, `spacing.baseValue` of 24 and
  `spacing.nodeNode` of 50. A subgraph whose frame paints a title adds `nodeSize.constraints` and
  `nodeSize.minimum` of the title's width, which is why that key has so many values.
- A container that asks for an algorithm with `@{ algorithm: ... }` gets it with `SEPARATE_CHILDREN`. When an edge
  crosses its boundary, mermaid.js drops the algorithm and sets `INCLUDE_CHILDREN` on it and its ancestors.
- `elk.layered.layering.layerConstraint` `FIRST` pins the entry node of a cyclic component when
  `config.elk.keepEntryNodeOnTop` is set.
- `elk.portAlignment.default` `CENTER` is set on a node whose edges are spread along its sides.
- An edge's own label is `CENTER` and `inline`. A label at an edge's start is `TAIL` and one at its end is `HEAD`.
