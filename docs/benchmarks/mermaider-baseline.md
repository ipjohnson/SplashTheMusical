# Mermaider baseline

The time and allocation of the Mermaider package on the benchmark set in `corpus/benchmarks/cases.txt`. STM's budget is to be
no slower and to allocate no more than these numbers. Section 6 of [the parity plan](../mermaid-parity-plan.md) sets it.
Written by `mise run bench-mermaider`.

Mermaider's public API has two entry points. `parse` is `MermaidRenderer.Parse`. `render` is `MermaidRenderer.RenderSvg`,
which parses, lays out and renders, so it is the budget for STM's parse, layout and render together. `Parse` handles only the
graph types, so the other types have no `parse` row. `ns` is the mean time per call and `Bytes` the bytes allocated per call.
The per-character columns divide them by `Chars`, the length of the source.

| Property | Value |
|---|---|
| Package | Mermaider 0.17.0 |
| Date | 2026-11-15 |
| Machine | Apple M3 Pro, 11 logical cores |
| OS | macOS 27.0.1 (osx-arm64) |
| Runtime | .NET 10.0.14 |
| BenchmarkDotNet | 0.15.8 |
| Jobs | JIT, AOT: 3 warmup and 10 measured iterations of about 100 ms each |

## Results

| Job | Case | Stage | Chars | ns | Bytes | ns/char | Bytes/char |
|---|---|---|---:|---:|---:|---:|---:|
| AOT | architecture-large | render | 1305 | 1268137.6 | 456190 | 971.75 | 349.57 |
| AOT | architecture-medium | render | 280 | 703771.9 | 203230 | 2513.47 | 725.82 |
| AOT | architecture-small | render | 50 | 27253.0 | 48070 | 545.06 | 961.40 |
| AOT | block-large | render | 564 | 747728.9 | 214698 | 1325.76 | 380.67 |
| AOT | block-medium | render | 220 | 179895.5 | 159101 | 817.71 | 723.19 |
| AOT | block-small | render | 36 | 187329.5 | 80371 | 5203.60 | 2232.53 |
| AOT | c4-large | render | 2178 | 473762.8 | 407770 | 217.52 | 187.22 |
| AOT | c4-medium | render | 351 | 199003.0 | 109847 | 566.96 | 312.95 |
| AOT | c4-small | render | 49 | 55508.6 | 67385 | 1132.83 | 1375.20 |
| AOT | class-large | render | 663 | 2248390.9 | 356532 | 3391.24 | 537.76 |
| AOT | class-medium | render | 272 | 449805.1 | 118717 | 1653.70 | 436.46 |
| AOT | class-small | render | 53 | 84653.9 | 112839 | 1597.24 | 2129.04 |
| AOT | er-large | render | 585 | 1614166.5 | 687633 | 2759.26 | 1175.44 |
| AOT | er-medium | render | 330 | 178556.1 | 385875 | 541.08 | 1169.32 |
| AOT | er-small | render | 36 | 33883.3 | 42315 | 941.20 | 1175.42 |
| AOT | flowchart-large | parse | 951 | 53668.8 | 56277 | 56.43 | 59.18 |
| AOT | flowchart-large | render | 951 | 860783.7 | 927204 | 905.14 | 974.98 |
| AOT | flowchart-medium | parse | 336 | 18554.5 | 11971 | 55.22 | 35.63 |
| AOT | flowchart-medium | render | 336 | 106981.8 | 180357 | 318.40 | 536.78 |
| AOT | flowchart-small | parse | 33 | 6754.2 | 5788 | 204.67 | 175.39 |
| AOT | flowchart-small | render | 33 | 136026.4 | 129241 | 4122.01 | 3916.39 |
| AOT | gantt-large | render | 1547 | 262223.8 | 253802 | 169.50 | 164.06 |
| AOT | gantt-medium | render | 422 | 318106.3 | 124786 | 753.81 | 295.70 |
| AOT | gantt-small | render | 109 | 28889.9 | 50536 | 265.05 | 463.63 |
| AOT | git-large | render | 987 | 234594.2 | 257704 | 237.68 | 261.10 |
| AOT | git-medium | render | 467 | 61603.2 | 101744 | 131.91 | 217.87 |
| AOT | git-small | render | 90 | 40775.4 | 75473 | 453.06 | 838.59 |
| AOT | kanban-large | render | 809 | 248877.8 | 345012 | 307.64 | 426.47 |
| AOT | kanban-medium | render | 77 | 44083.2 | 77223 | 572.51 | 1002.90 |
| AOT | kanban-small | render | 32 | 40488.8 | 73749 | 1265.28 | 2304.66 |
| AOT | mindmap-large | render | 950 | 328816.2 | 410587 | 346.12 | 432.20 |
| AOT | mindmap-medium | render | 130 | 78757.8 | 91273 | 605.83 | 702.10 |
| AOT | mindmap-small | render | 26 | 26312.9 | 58975 | 1012.03 | 2268.27 |
| AOT | packet-large | render | 690 | 335329.5 | 257040 | 485.98 | 372.52 |
| AOT | packet-medium | render | 115 | 39905.7 | 70385 | 347.01 | 612.04 |
| AOT | packet-small | render | 18 | 27542.7 | 61379 | 1530.15 | 3409.94 |
| AOT | pie-large | render | 449 | 195430.9 | 260405 | 435.26 | 579.97 |
| AOT | pie-medium | render | 90 | 50161.8 | 88089 | 557.35 | 978.77 |
| AOT | pie-small | render | 23 | 26122.1 | 47246 | 1135.74 | 2054.17 |
| AOT | quadrant-chart-large | render | 1158 | 149514.8 | 103029 | 129.11 | 88.97 |
| AOT | quadrant-chart-medium | render | 88 | 31912.8 | 55629 | 362.65 | 632.15 |
| AOT | quadrant-chart-small | render | 31 | 29307.4 | 61778 | 945.40 | 1992.84 |
| AOT | radar-large | render | 389 | 43400.8 | 77073 | 111.57 | 198.13 |
| AOT | radar-medium | render | 183 | 46625.1 | 85288 | 254.78 | 466.05 |
| AOT | radar-small | render | 59 | 45420.7 | 75289 | 769.84 | 1276.08 |
| AOT | requirement-large | render | 1566 | 479926.4 | 629043 | 306.47 | 401.69 |
| AOT | requirement-medium | render | 494 | 117459.6 | 192279 | 237.77 | 389.23 |
| AOT | requirement-small | render | 323 | 77094.8 | 140266 | 238.68 | 434.26 |
| AOT | sankey-large | render | 1262 | 192669.5 | 267123 | 152.67 | 211.67 |
| AOT | sankey-medium | render | 227 | 36597.4 | 77117 | 161.22 | 339.72 |
| AOT | sankey-small | render | 110 | 48103.3 | 93380 | 437.30 | 848.91 |
| AOT | sequence-large | render | 1094 | 72615.2 | 119621 | 66.38 | 109.34 |
| AOT | sequence-medium | render | 284 | 95135.4 | 139599 | 334.98 | 491.55 |
| AOT | sequence-small | render | 84 | 62575.5 | 105480 | 744.95 | 1255.71 |
| AOT | state-large | parse | 689 | 6746.4 | 20120 | 9.79 | 29.20 |
| AOT | state-large | render | 689 | 119235.7 | 236720 | 173.06 | 343.57 |
| AOT | state-medium | parse | 323 | 1887.1 | 5634 | 5.84 | 17.44 |
| AOT | state-medium | render | 323 | 65712.2 | 108875 | 203.44 | 337.07 |
| AOT | state-small | parse | 63 | 3012.2 | 2881 | 47.81 | 45.73 |
| AOT | state-small | render | 63 | 89047.6 | 80622 | 1413.45 | 1279.71 |
| AOT | timeline-large | render | 942 | 338409.7 | 234083 | 359.25 | 248.50 |
| AOT | timeline-medium | render | 463 | 195855.2 | 206831 | 423.01 | 446.72 |
| AOT | timeline-small | render | 77 | 65498.2 | 109033 | 850.63 | 1416.01 |
| AOT | treeView-large | render | 491 | 78385.9 | 115059 | 159.65 | 234.34 |
| AOT | treeView-medium | render | 119 | 61120.3 | 81106 | 513.62 | 681.56 |
| AOT | treeView-small | render | 32 | 25825.1 | 51036 | 807.04 | 1594.88 |
| AOT | treemap-large | render | 762 | 265847.3 | 202431 | 348.88 | 265.66 |
| AOT | treemap-medium | render | 405 | 171655.9 | 112710 | 423.84 | 278.30 |
| AOT | treemap-small | render | 147 | 128379.8 | 138146 | 873.33 | 939.77 |
| AOT | user-journey-large | render | 885 | 229385.7 | 296131 | 259.19 | 334.61 |
| AOT | user-journey-medium | render | 493 | 503455.5 | 205801 | 1021.21 | 417.45 |
| AOT | user-journey-small | render | 116 | 17049.8 | 50391 | 146.98 | 434.41 |
| AOT | venn-large | render | 534 | 32770.8 | 60367 | 61.37 | 113.05 |
| AOT | venn-medium | render | 251 | 79372.7 | 77909 | 316.23 | 310.39 |
| AOT | venn-small | render | 76 | 60579.3 | 56382 | 797.10 | 741.87 |
| AOT | xychart-large | render | 707 | 146920.6 | 181902 | 207.81 | 257.29 |
| AOT | xychart-medium | render | 395 | 157657.9 | 106562 | 399.13 | 269.78 |
| AOT | xychart-small | render | 76 | 68888.3 | 92336 | 906.42 | 1214.95 |
| JIT | architecture-large | render | 1305 | 2827648.0 | 457310 | 2166.78 | 350.43 |
| JIT | architecture-medium | render | 280 | 839396.7 | 204548 | 2997.85 | 730.53 |
| JIT | architecture-small | render | 50 | 90279.4 | 47940 | 1805.59 | 958.80 |
| JIT | block-large | render | 564 | 2202649.7 | 213062 | 3905.41 | 377.77 |
| JIT | block-medium | render | 220 | 1219051.5 | 158862 | 5541.14 | 722.10 |
| JIT | block-small | render | 36 | 280156.4 | 81495 | 7782.12 | 2263.75 |
| JIT | c4-large | render | 2178 | 1550340.6 | 410300 | 711.82 | 188.38 |
| JIT | c4-medium | render | 351 | 266979.9 | 109846 | 760.63 | 312.95 |
| JIT | c4-small | render | 49 | 481758.6 | 67908 | 9831.81 | 1385.88 |
| JIT | class-large | render | 663 | 3202670.8 | 356715 | 4830.57 | 538.03 |
| JIT | class-medium | render | 272 | 625481.4 | 120129 | 2299.56 | 441.65 |
| JIT | class-small | render | 53 | 882067.4 | 110762 | 16642.78 | 2089.85 |
| JIT | er-large | render | 585 | 1192462.4 | 691813 | 2038.40 | 1182.59 |
| JIT | er-medium | render | 330 | 904604.8 | 389653 | 2741.23 | 1180.77 |
| JIT | er-small | render | 36 | 70341.4 | 42659 | 1953.93 | 1184.97 |
| JIT | flowchart-large | parse | 951 | 49010.7 | 56259 | 51.54 | 59.16 |
| JIT | flowchart-large | render | 951 | 5680256.5 | 930884 | 5972.93 | 978.85 |
| JIT | flowchart-medium | parse | 336 | 11228.3 | 11981 | 33.42 | 35.66 |
| JIT | flowchart-medium | render | 336 | 365185.3 | 180476 | 1086.86 | 537.13 |
| JIT | flowchart-small | parse | 33 | 8919.5 | 5784 | 270.29 | 175.27 |
| JIT | flowchart-small | render | 33 | 294574.5 | 128621 | 8926.50 | 3897.61 |
| JIT | gantt-large | render | 1547 | 897087.6 | 254912 | 579.89 | 164.78 |
| JIT | gantt-medium | render | 422 | 495827.3 | 125758 | 1174.95 | 298.00 |
| JIT | gantt-small | render | 109 | 80614.9 | 50928 | 739.59 | 467.23 |
| JIT | git-large | render | 987 | 801776.1 | 259507 | 812.34 | 262.93 |
| JIT | git-medium | render | 467 | 191437.2 | 102349 | 409.93 | 219.16 |
| JIT | git-small | render | 90 | 129420.7 | 75952 | 1438.01 | 843.91 |
| JIT | kanban-large | render | 809 | 818599.3 | 347345 | 1011.87 | 429.35 |
| JIT | kanban-medium | render | 77 | 137641.9 | 77828 | 1787.56 | 1010.75 |
| JIT | kanban-small | render | 32 | 142560.0 | 74346 | 4455.00 | 2323.31 |
| JIT | mindmap-large | render | 950 | 2312985.9 | 412462 | 2434.72 | 434.17 |
| JIT | mindmap-medium | render | 130 | 307216.0 | 92090 | 2363.20 | 708.38 |
| JIT | mindmap-small | render | 26 | 113609.4 | 59438 | 4369.59 | 2286.08 |
| JIT | packet-large | render | 690 | 926689.2 | 259216 | 1343.03 | 375.68 |
| JIT | packet-medium | render | 115 | 139695.3 | 70232 | 1214.74 | 610.71 |
| JIT | packet-small | render | 18 | 79245.5 | 61878 | 4402.53 | 3437.67 |
| JIT | pie-large | render | 449 | 637113.7 | 262450 | 1418.96 | 584.52 |
| JIT | pie-medium | render | 90 | 165706.3 | 88437 | 1841.18 | 982.63 |
| JIT | pie-small | render | 23 | 85394.6 | 46934 | 3712.81 | 2040.61 |
| JIT | quadrant-chart-large | render | 1158 | 215891.6 | 103694 | 186.43 | 89.55 |
| JIT | quadrant-chart-medium | render | 88 | 85199.9 | 56092 | 968.18 | 637.41 |
| JIT | quadrant-chart-small | render | 31 | 90196.0 | 62289 | 2909.55 | 2009.32 |
| JIT | radar-large | render | 389 | 134056.2 | 77705 | 344.62 | 199.76 |
| JIT | radar-medium | render | 183 | 163787.0 | 85981 | 895.01 | 469.84 |
| JIT | radar-small | render | 59 | 155569.6 | 75930 | 2636.77 | 1286.95 |
| JIT | requirement-large | render | 1566 | 1589834.1 | 626033 | 1015.22 | 399.77 |
| JIT | requirement-medium | render | 494 | 428056.4 | 192760 | 866.51 | 390.20 |
| JIT | requirement-small | render | 323 | 291841.8 | 141632 | 903.53 | 438.49 |
| JIT | sankey-large | render | 1262 | 636778.1 | 268623 | 504.58 | 212.85 |
| JIT | sankey-medium | render | 227 | 108347.8 | 77040 | 477.30 | 339.38 |
| JIT | sankey-small | render | 110 | 161624.7 | 93716 | 1469.32 | 851.96 |
| JIT | sequence-large | render | 1094 | 257957.5 | 121172 | 235.79 | 110.76 |
| JIT | sequence-medium | render | 284 | 311478.7 | 140681 | 1096.76 | 495.36 |
| JIT | sequence-small | render | 84 | 232017.8 | 106592 | 2762.12 | 1268.95 |
| JIT | state-large | parse | 689 | 5987.3 | 20120 | 8.69 | 29.20 |
| JIT | state-large | render | 689 | 488798.2 | 233210 | 709.43 | 338.48 |
| JIT | state-medium | parse | 323 | 1592.5 | 5634 | 4.93 | 17.44 |
| JIT | state-medium | render | 323 | 350368.8 | 108978 | 1084.73 | 337.39 |
| JIT | state-small | parse | 63 | 2908.3 | 2881 | 46.16 | 45.73 |
| JIT | state-small | render | 63 | 318146.0 | 81553 | 5049.94 | 1294.49 |
| JIT | timeline-large | render | 942 | 1053964.1 | 235918 | 1118.86 | 250.44 |
| JIT | timeline-medium | render | 463 | 543453.9 | 209168 | 1173.77 | 451.77 |
| JIT | timeline-small | render | 77 | 263989.8 | 109854 | 3428.44 | 1426.68 |
| JIT | treeView-large | render | 491 | 290558.5 | 115994 | 591.77 | 236.24 |
| JIT | treeView-medium | render | 119 | 164021.8 | 81019 | 1378.33 | 680.83 |
| JIT | treeView-small | render | 32 | 67102.3 | 51437 | 2096.95 | 1607.41 |
| JIT | treemap-large | render | 762 | 640316.2 | 203067 | 840.31 | 266.49 |
| JIT | treemap-medium | render | 405 | 583995.2 | 112977 | 1441.96 | 278.96 |
| JIT | treemap-small | render | 147 | 310123.6 | 138817 | 2109.68 | 944.33 |
| JIT | user-journey-large | render | 885 | 2086298.5 | 298431 | 2357.40 | 337.21 |
| JIT | user-journey-medium | render | 493 | 498616.2 | 207778 | 1011.39 | 421.46 |
| JIT | user-journey-small | render | 116 | 55213.9 | 50845 | 475.98 | 438.32 |
| JIT | venn-large | render | 534 | 154103.6 | 60800 | 288.58 | 113.86 |
| JIT | venn-medium | render | 251 | 165293.8 | 78411 | 658.54 | 312.39 |
| JIT | venn-small | render | 76 | 71811.2 | 56818 | 944.88 | 747.61 |
| JIT | xychart-large | render | 707 | 828063.5 | 182541 | 1171.24 | 258.19 |
| JIT | xychart-medium | render | 395 | 572842.2 | 107440 | 1450.23 | 272.00 |
| JIT | xychart-small | render | 76 | 190511.7 | 93083 | 2506.73 | 1224.78 |

## Cases Mermaider does not render

Mermaider throws on these, so they have no baseline.

- agentflow-small (`agentflow/parser-agentflow-empty-metadata-001`)
- agentflow-medium (`agentflow/parser-agentflow-comments-013`)
- agentflow-large (`agentflow/e2e-agentflow-visual-check-11-redirect-cross-boundary-edges-to-the-collapsed-parent`)
- cynefin-small (`cynefin/parser-cynefin-integration-011`)
- cynefin-medium (`cynefin/parser-cynefin-integration-008`)
- cynefin-large (`cynefin/e2e-cynefin-pr-check-should-render-cynefin-with-many-items-per-domain`)
- eventmodeling-small (`eventmodeling/docs-eventmodeling-010`)
- eventmodeling-medium (`eventmodeling/parser-eventmodeling-010`)
- eventmodeling-large (`eventmodeling/docs-eventmodeling-005`)
- info-small (`info/parser-info-001`)
- info-medium (`info/parser-langium-info-001`)
- info-large (`info/e2e-state-diagram-v2-nightly-check-v2-should-render-a-simple-info`)
- ishikawa-small (`ishikawa/parser-ishikawa-003`)
- ishikawa-medium (`ishikawa/e2e-ishikawa-pr-check-9-should-render-with-custom-diagrampadding`)
- ishikawa-large (`ishikawa/e2e-ishikawa-pr-check-13-should-render-a-very-deep-nested-diagram`)
- railroad-small (`railroad/parser-pegdiagram-009`)
- railroad-medium (`railroad/parser-pegdiagram-011`)
- railroad-large (`railroad/e2e-railroad-pr-check-ir-optional-and-repetition-operators`)
- swimlanes-small (`swimlanes/parser-swimlanesdiagram-003`)
- swimlanes-medium (`swimlanes/e2e-swimlanes-pr-check-16-intake-cycle-lr`)
- swimlanes-large (`swimlanes/e2e-swimlanes-pr-check-5-car-fun-sales-wide-tb`)
- usecase-small (`usecase/parser-usecase-014`)
- usecase-medium (`usecase/docs-usecase-014`)
- usecase-large (`usecase/e2e-usecase-pr-check-themed-elements-dark-theme`)
- wardley-small (`wardley/docs-wardley-002`)
- wardley-medium (`wardley/docs-wardley-015`)
- wardley-large (`wardley/docs-wardley-019`)
- zenuml-small (`zenuml/mermaider-zenuml-divider`)
- zenuml-medium (`zenuml/mermaider-zenuml-else-if`)
- zenuml-large (`zenuml/docs-zenuml-008`)
