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
| Date | 2026-11-18 |
| Machine | Apple M3 Pro, 11 logical cores |
| OS | macOS 27.0.1 (osx-arm64) |
| Runtime | .NET 10.0.14 |
| BenchmarkDotNet | 0.15.8 |
| Jobs | JIT: 20 warmup, AOT: 3 warmup, and 10 measured iterations, of about 100 ms each |

## Results

| Job | Case | Stage | Chars | ns | Bytes | ns/char | Bytes/char |
|---|---|---|---:|---:|---:|---:|---:|
| AOT | architecture-large | render | 1305 | 326698.5 | 456194 | 250.34 | 349.57 |
| AOT | architecture-medium | render | 280 | 130684.7 | 203204 | 466.73 | 725.73 |
| AOT | architecture-small | render | 50 | 15749.2 | 48070 | 314.98 | 961.40 |
| AOT | block-large | render | 564 | 135591.6 | 214692 | 240.41 | 380.66 |
| AOT | block-medium | render | 220 | 102578.9 | 159105 | 466.27 | 723.20 |
| AOT | block-small | render | 36 | 38379.2 | 80373 | 1066.09 | 2232.58 |
| AOT | c4-large | render | 2178 | 278332.5 | 407730 | 127.79 | 187.20 |
| AOT | c4-medium | render | 351 | 58986.0 | 109822 | 168.05 | 312.88 |
| AOT | c4-small | render | 49 | 26129.9 | 67380 | 533.26 | 1375.10 |
| AOT | class-large | render | 663 | 453532.6 | 355989 | 684.06 | 536.94 |
| AOT | class-medium | render | 272 | 57194.3 | 118711 | 210.27 | 436.44 |
| AOT | class-small | render | 53 | 46959.1 | 112823 | 886.02 | 2128.74 |
| AOT | er-large | render | 585 | 208833.2 | 687577 | 356.98 | 1175.35 |
| AOT | er-medium | render | 330 | 117754.1 | 385863 | 356.83 | 1169.28 |
| AOT | er-small | render | 36 | 13271.8 | 42315 | 368.66 | 1175.42 |
| AOT | flowchart-large | parse | 951 | 40389.6 | 56275 | 42.47 | 59.17 |
| AOT | flowchart-large | render | 951 | 670369.4 | 927113 | 704.91 | 974.88 |
| AOT | flowchart-medium | parse | 336 | 7781.8 | 11971 | 23.16 | 35.63 |
| AOT | flowchart-medium | render | 336 | 86793.3 | 180362 | 258.31 | 536.79 |
| AOT | flowchart-small | parse | 33 | 3422.3 | 5788 | 103.71 | 175.39 |
| AOT | flowchart-small | render | 33 | 63240.9 | 129211 | 1916.39 | 3915.48 |
| AOT | gantt-large | render | 1547 | 184150.7 | 253780 | 119.04 | 164.05 |
| AOT | gantt-medium | render | 422 | 86518.6 | 124774 | 205.02 | 295.67 |
| AOT | gantt-small | render | 109 | 17103.1 | 50536 | 156.91 | 463.63 |
| AOT | git-large | render | 987 | 183119.5 | 257713 | 185.53 | 261.11 |
| AOT | git-medium | render | 467 | 55915.3 | 101746 | 119.73 | 217.87 |
| AOT | git-small | render | 90 | 36166.8 | 75468 | 401.85 | 838.53 |
| AOT | kanban-large | render | 809 | 240184.4 | 345012 | 296.89 | 426.47 |
| AOT | kanban-medium | render | 77 | 41103.1 | 77223 | 533.81 | 1002.90 |
| AOT | kanban-small | render | 32 | 35576.1 | 73748 | 1111.75 | 2304.62 |
| AOT | mindmap-large | render | 950 | 312789.6 | 410576 | 329.25 | 432.19 |
| AOT | mindmap-medium | render | 130 | 45467.4 | 91269 | 349.75 | 702.07 |
| AOT | mindmap-small | render | 26 | 28249.4 | 58975 | 1086.52 | 2268.27 |
| AOT | packet-large | render | 690 | 178370.0 | 257019 | 258.51 | 372.49 |
| AOT | packet-medium | render | 115 | 30972.7 | 70383 | 269.33 | 612.03 |
| AOT | packet-small | render | 18 | 24983.4 | 61378 | 1387.97 | 3409.89 |
| AOT | pie-large | render | 449 | 195483.1 | 260403 | 435.37 | 579.96 |
| AOT | pie-medium | render | 90 | 47193.8 | 88090 | 524.38 | 978.78 |
| AOT | pie-small | render | 23 | 15310.0 | 47246 | 665.65 | 2054.17 |
| AOT | quadrant-chart-large | render | 1158 | 52960.4 | 103027 | 45.73 | 88.97 |
| AOT | quadrant-chart-medium | render | 88 | 31568.5 | 55628 | 358.73 | 632.14 |
| AOT | quadrant-chart-small | render | 31 | 29250.2 | 61778 | 943.56 | 1992.84 |
| AOT | radar-large | render | 389 | 48987.8 | 77074 | 125.93 | 198.13 |
| AOT | radar-medium | render | 183 | 53796.9 | 85288 | 293.97 | 466.05 |
| AOT | radar-small | render | 59 | 40953.3 | 75289 | 694.12 | 1276.08 |
| AOT | requirement-large | render | 1566 | 614393.1 | 629075 | 392.33 | 401.71 |
| AOT | requirement-medium | render | 494 | 117311.8 | 192277 | 237.47 | 389.22 |
| AOT | requirement-small | render | 323 | 73839.7 | 140269 | 228.61 | 434.27 |
| AOT | sankey-large | render | 1262 | 183543.3 | 267117 | 145.44 | 211.66 |
| AOT | sankey-medium | render | 227 | 34173.6 | 77116 | 150.54 | 339.72 |
| AOT | sankey-small | render | 110 | 48395.8 | 93380 | 439.96 | 848.91 |
| AOT | sequence-large | render | 1094 | 71090.7 | 119623 | 64.98 | 109.34 |
| AOT | sequence-medium | render | 284 | 93520.4 | 139611 | 329.30 | 491.59 |
| AOT | sequence-small | render | 84 | 60820.7 | 105484 | 724.06 | 1255.76 |
| AOT | state-large | parse | 689 | 6873.8 | 20120 | 9.98 | 29.20 |
| AOT | state-large | render | 689 | 109378.0 | 236711 | 158.75 | 343.56 |
| AOT | state-medium | parse | 323 | 1925.8 | 5634 | 5.96 | 17.44 |
| AOT | state-medium | render | 323 | 44953.3 | 108870 | 139.17 | 337.06 |
| AOT | state-small | parse | 63 | 1084.2 | 2881 | 17.21 | 45.73 |
| AOT | state-small | render | 63 | 31850.1 | 80619 | 505.56 | 1279.67 |
| AOT | timeline-large | render | 942 | 176162.5 | 234066 | 187.01 | 248.48 |
| AOT | timeline-medium | render | 463 | 137958.3 | 206823 | 297.97 | 446.70 |
| AOT | timeline-small | render | 77 | 61969.5 | 109033 | 804.80 | 1416.01 |
| AOT | treeView-large | render | 491 | 79253.2 | 115060 | 161.41 | 234.34 |
| AOT | treeView-medium | render | 119 | 42197.7 | 81106 | 354.60 | 681.56 |
| AOT | treeView-small | render | 32 | 19440.8 | 51032 | 607.52 | 1594.75 |
| AOT | treemap-large | render | 762 | 138810.6 | 202377 | 182.17 | 265.59 |
| AOT | treemap-medium | render | 405 | 66754.7 | 112706 | 164.83 | 278.29 |
| AOT | treemap-small | render | 147 | 287062.7 | 138147 | 1952.81 | 939.78 |
| AOT | user-journey-large | render | 885 | 223557.6 | 296123 | 252.61 | 334.60 |
| AOT | user-journey-medium | render | 493 | 143897.9 | 205741 | 291.88 | 417.32 |
| AOT | user-journey-small | render | 116 | 16394.7 | 50392 | 141.33 | 434.41 |
| AOT | venn-large | render | 534 | 23580.9 | 60367 | 44.16 | 113.05 |
| AOT | venn-medium | render | 251 | 40502.6 | 77908 | 161.36 | 310.39 |
| AOT | venn-small | render | 76 | 22747.8 | 56381 | 299.31 | 741.86 |
| AOT | xychart-large | render | 707 | 142151.5 | 181905 | 201.06 | 257.29 |
| AOT | xychart-medium | render | 395 | 72031.5 | 106550 | 182.36 | 269.75 |
| AOT | xychart-small | render | 76 | 60574.9 | 92334 | 797.04 | 1214.92 |
| JIT | architecture-large | render | 1305 | 858331.6 | 456765 | 657.73 | 350.01 |
| JIT | architecture-medium | render | 280 | 331778.1 | 204220 | 1184.92 | 729.36 |
| JIT | architecture-small | render | 50 | 17144.2 | 47715 | 342.88 | 954.30 |
| JIT | block-large | render | 564 | 350928.6 | 212656 | 622.21 | 377.05 |
| JIT | block-medium | render | 220 | 260425.3 | 158844 | 1183.75 | 722.02 |
| JIT | block-small | render | 36 | 43190.5 | 79792 | 1199.74 | 2216.44 |
| JIT | c4-large | render | 2178 | 849141.2 | 409873 | 389.87 | 188.19 |
| JIT | c4-medium | render | 351 | 122360.8 | 109811 | 348.61 | 312.85 |
| JIT | c4-small | render | 49 | 30666.6 | 67159 | 625.85 | 1370.59 |
| JIT | class-large | render | 663 | 955873.0 | 356428 | 1441.74 | 537.60 |
| JIT | class-medium | render | 272 | 170779.0 | 119788 | 627.86 | 440.40 |
| JIT | class-small | render | 53 | 173672.0 | 112917 | 3276.83 | 2130.51 |
| JIT | er-large | render | 585 | 591413.4 | 691766 | 1010.96 | 1182.51 |
| JIT | er-medium | render | 330 | 315624.2 | 389288 | 956.44 | 1179.66 |
| JIT | er-small | render | 36 | 14759.6 | 41955 | 409.99 | 1165.42 |
| JIT | flowchart-large | parse | 951 | 32020.2 | 56261 | 33.67 | 59.16 |
| JIT | flowchart-large | render | 951 | 2455831.4 | 930822 | 2582.37 | 978.78 |
| JIT | flowchart-medium | parse | 336 | 7825.1 | 11981 | 23.29 | 35.66 |
| JIT | flowchart-medium | render | 336 | 322484.9 | 180451 | 959.78 | 537.06 |
| JIT | flowchart-small | parse | 33 | 2755.7 | 5784 | 83.50 | 175.27 |
| JIT | flowchart-small | render | 33 | 195970.9 | 128392 | 5938.51 | 3890.67 |
| JIT | gantt-large | render | 1547 | 622684.5 | 255597 | 402.51 | 165.22 |
| JIT | gantt-medium | render | 422 | 92962.5 | 125741 | 220.29 | 297.96 |
| JIT | gantt-small | render | 109 | 18114.2 | 50219 | 166.19 | 460.72 |
| JIT | git-large | render | 987 | 432765.9 | 259229 | 438.47 | 262.64 |
| JIT | git-medium | render | 467 | 104815.3 | 102347 | 224.44 | 219.16 |
| JIT | git-small | render | 90 | 52626.5 | 75257 | 584.74 | 836.19 |
| JIT | kanban-large | render | 809 | 503241.6 | 347331 | 622.05 | 429.33 |
| JIT | kanban-medium | render | 77 | 82497.0 | 77826 | 1071.39 | 1010.73 |
| JIT | kanban-small | render | 32 | 54326.6 | 73640 | 1697.71 | 2301.25 |
| JIT | mindmap-large | render | 950 | 696874.1 | 412445 | 733.55 | 434.15 |
| JIT | mindmap-medium | render | 130 | 88055.0 | 91056 | 677.35 | 700.43 |
| JIT | mindmap-small | render | 26 | 59090.7 | 59439 | 2272.72 | 2286.12 |
| JIT | packet-large | render | 690 | 352604.9 | 258909 | 511.02 | 375.23 |
| JIT | packet-medium | render | 115 | 40936.3 | 70234 | 355.97 | 610.73 |
| JIT | packet-small | render | 18 | 31266.0 | 61165 | 1737.00 | 3398.06 |
| JIT | pie-large | render | 449 | 343364.7 | 262444 | 764.73 | 584.51 |
| JIT | pie-medium | render | 90 | 63237.5 | 87732 | 702.64 | 974.80 |
| JIT | pie-small | render | 23 | 19122.6 | 46933 | 831.42 | 2040.57 |
| JIT | quadrant-chart-large | render | 1158 | 70026.5 | 102994 | 60.47 | 88.94 |
| JIT | quadrant-chart-medium | render | 88 | 27198.1 | 55388 | 309.07 | 629.41 |
| JIT | quadrant-chart-small | render | 31 | 59058.5 | 61582 | 1905.11 | 1986.52 |
| JIT | radar-large | render | 389 | 60690.0 | 77000 | 156.02 | 197.94 |
| JIT | radar-medium | render | 183 | 45961.3 | 85273 | 251.15 | 465.97 |
| JIT | radar-small | render | 59 | 49199.6 | 75224 | 833.89 | 1274.98 |
| JIT | requirement-large | render | 1566 | 1648159.0 | 625385 | 1052.46 | 399.35 |
| JIT | requirement-medium | render | 494 | 377526.1 | 192398 | 764.22 | 389.47 |
| JIT | requirement-small | render | 323 | 191508.1 | 141367 | 592.90 | 437.67 |
| JIT | sankey-large | render | 1262 | 334853.1 | 267601 | 265.34 | 212.05 |
| JIT | sankey-medium | render | 227 | 46373.8 | 76335 | 204.29 | 336.28 |
| JIT | sankey-small | render | 110 | 107741.7 | 93002 | 979.47 | 845.47 |
| JIT | sequence-large | render | 1094 | 139494.0 | 119760 | 127.51 | 109.47 |
| JIT | sequence-medium | render | 284 | 110717.9 | 139227 | 389.85 | 490.24 |
| JIT | sequence-small | render | 84 | 142376.3 | 106288 | 1694.96 | 1265.33 |
| JIT | state-large | parse | 689 | 5541.9 | 20120 | 8.04 | 29.20 |
| JIT | state-large | render | 689 | 436300.9 | 233211 | 633.24 | 338.48 |
| JIT | state-medium | parse | 323 | 1680.2 | 5634 | 5.20 | 17.44 |
| JIT | state-medium | render | 323 | 162733.8 | 108645 | 503.82 | 336.36 |
| JIT | state-small | parse | 63 | 926.0 | 2881 | 14.70 | 45.73 |
| JIT | state-small | render | 63 | 90422.7 | 81240 | 1435.28 | 1289.52 |
| JIT | timeline-large | render | 942 | 246950.5 | 235880 | 262.16 | 250.40 |
| JIT | timeline-medium | render | 463 | 228698.9 | 208878 | 493.95 | 451.14 |
| JIT | timeline-small | render | 77 | 83496.5 | 109847 | 1084.37 | 1426.58 |
| JIT | treeView-large | render | 491 | 94197.7 | 115989 | 191.85 | 236.23 |
| JIT | treeView-medium | render | 119 | 80048.1 | 81716 | 672.67 | 686.69 |
| JIT | treeView-small | render | 32 | 23512.5 | 50732 | 734.76 | 1585.38 |
| JIT | treemap-large | render | 762 | 340261.7 | 201312 | 446.54 | 264.19 |
| JIT | treemap-medium | render | 405 | 89665.6 | 112957 | 221.40 | 278.91 |
| JIT | treemap-small | render | 147 | 235584.3 | 138829 | 1602.61 | 944.41 |
| JIT | user-journey-large | render | 885 | 394905.8 | 298411 | 446.22 | 337.19 |
| JIT | user-journey-medium | render | 493 | 340407.9 | 207828 | 690.48 | 421.56 |
| JIT | user-journey-small | render | 116 | 19749.0 | 50139 | 170.25 | 432.23 |
| JIT | venn-large | render | 534 | 31894.7 | 60099 | 59.73 | 112.54 |
| JIT | venn-medium | render | 251 | 67652.8 | 77129 | 269.53 | 307.29 |
| JIT | venn-small | render | 76 | 28802.8 | 56109 | 378.98 | 738.28 |
| JIT | xychart-large | render | 707 | 237322.5 | 182540 | 335.68 | 258.19 |
| JIT | xychart-medium | render | 395 | 119628.0 | 106722 | 302.86 | 270.18 |
| JIT | xychart-small | render | 76 | 66751.4 | 92381 | 878.31 | 1215.54 |

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
