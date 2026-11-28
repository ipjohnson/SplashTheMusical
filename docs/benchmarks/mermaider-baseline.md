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
| Date | 2026-11-28 |
| Machine | Apple M1 (Virtual), 3 logical cores |
| OS | macOS 26.7.2 (osx-arm64) |
| Runtime | .NET 10.0.15 |
| BenchmarkDotNet | 0.15.8 |
| Jobs | JIT: 60 warmup, AOT: 3 warmup, and 10 measured iterations, of about 100 ms each |

## Results

| Job | Case | Stage | Chars | ns | Bytes | ns/char | Bytes/char |
|---|---|---|---:|---:|---:|---:|---:|
| AOT | architecture-large | render | 1305 | 580627.3 | 456326 | 444.93 | 349.68 |
| AOT | architecture-medium | render | 280 | 179666.1 | 203314 | 641.66 | 726.12 |
| AOT | architecture-small | render | 50 | 22305.5 | 48081 | 446.11 | 961.62 |
| AOT | block-large | render | 564 | 191835.4 | 214896 | 340.13 | 381.02 |
| AOT | block-medium | render | 220 | 127464.6 | 159171 | 579.38 | 723.50 |
| AOT | block-small | render | 36 | 61786.6 | 80395 | 1716.29 | 2233.19 |
| AOT | c4-large | render | 2178 | 450696.1 | 408089 | 206.93 | 187.37 |
| AOT | c4-medium | render | 351 | 145318.1 | 109922 | 414.01 | 313.17 |
| AOT | c4-small | render | 49 | 99059.1 | 67423 | 2021.62 | 1375.98 |
| AOT | class-large | render | 663 | 1114106.7 | 356279 | 1680.40 | 537.37 |
| AOT | class-medium | render | 272 | 122856.7 | 118741 | 451.68 | 436.55 |
| AOT | class-small | render | 53 | 87534.0 | 112860 | 1651.59 | 2129.43 |
| AOT | er-large | render | 585 | 464743.6 | 687969 | 794.43 | 1176.02 |
| AOT | er-medium | render | 330 | 232026.8 | 386010 | 703.11 | 1169.73 |
| AOT | er-small | render | 36 | 19866.1 | 42324 | 551.84 | 1175.67 |
| AOT | flowchart-large | parse | 951 | 73586.5 | 56297 | 77.38 | 59.20 |
| AOT | flowchart-large | render | 951 | 1389351.5 | 927798 | 1460.94 | 975.60 |
| AOT | flowchart-medium | parse | 336 | 15188.8 | 11975 | 45.20 | 35.64 |
| AOT | flowchart-medium | render | 336 | 126409.7 | 180479 | 376.22 | 537.14 |
| AOT | flowchart-small | parse | 33 | 6520.9 | 5789 | 197.60 | 175.42 |
| AOT | flowchart-small | render | 33 | 103715.3 | 129303 | 3142.89 | 3918.27 |
| AOT | gantt-large | render | 1547 | 322653.0 | 253838 | 208.57 | 164.08 |
| AOT | gantt-medium | render | 422 | 126224.1 | 124810 | 299.11 | 295.76 |
| AOT | gantt-small | render | 109 | 33334.6 | 50547 | 305.82 | 463.73 |
| AOT | git-large | render | 987 | 285649.9 | 257884 | 289.41 | 261.28 |
| AOT | git-medium | render | 467 | 106201.1 | 101800 | 227.41 | 217.99 |
| AOT | git-small | render | 90 | 110438.1 | 75518 | 1227.09 | 839.09 |
| AOT | kanban-large | render | 809 | 547111.8 | 345153 | 676.28 | 426.64 |
| AOT | kanban-medium | render | 77 | 81116.3 | 77245 | 1053.46 | 1003.18 |
| AOT | kanban-small | render | 32 | 61181.2 | 73762 | 1911.91 | 2305.06 |
| AOT | mindmap-large | render | 950 | 443291.2 | 410838 | 466.62 | 432.46 |
| AOT | mindmap-medium | render | 130 | 69788.0 | 91292 | 536.83 | 702.25 |
| AOT | mindmap-small | render | 26 | 37267.5 | 58990 | 1433.36 | 2268.85 |
| AOT | packet-large | render | 690 | 236288.6 | 257089 | 342.45 | 372.59 |
| AOT | packet-medium | render | 115 | 49114.4 | 70397 | 427.08 | 612.15 |
| AOT | packet-small | render | 18 | 41026.0 | 61394 | 2279.22 | 3410.78 |
| AOT | pie-large | render | 449 | 288392.8 | 260485 | 642.30 | 580.14 |
| AOT | pie-medium | render | 90 | 79186.1 | 88114 | 879.85 | 979.04 |
| AOT | pie-small | render | 23 | 22808.3 | 47255 | 991.67 | 2054.57 |
| AOT | quadrant-chart-large | render | 1158 | 82586.2 | 103040 | 71.32 | 88.98 |
| AOT | quadrant-chart-medium | render | 88 | 38162.6 | 55639 | 433.67 | 632.26 |
| AOT | quadrant-chart-small | render | 31 | 40779.2 | 61790 | 1315.46 | 1993.23 |
| AOT | radar-large | render | 389 | 56300.6 | 77094 | 144.73 | 198.19 |
| AOT | radar-medium | render | 183 | 71314.1 | 85307 | 389.69 | 466.16 |
| AOT | radar-small | render | 59 | 72781.6 | 75314 | 1233.59 | 1276.51 |
| AOT | requirement-large | render | 1566 | 638931.4 | 629125 | 408.00 | 401.74 |
| AOT | requirement-medium | render | 494 | 162608.7 | 192354 | 329.17 | 389.38 |
| AOT | requirement-small | render | 323 | 96533.7 | 140304 | 298.87 | 434.38 |
| AOT | sankey-large | render | 1262 | 238561.3 | 267217 | 189.03 | 211.74 |
| AOT | sankey-medium | render | 227 | 45729.8 | 77138 | 201.45 | 339.81 |
| AOT | sankey-small | render | 110 | 84020.4 | 93408 | 763.82 | 849.16 |
| AOT | sequence-large | render | 1094 | 98699.7 | 119663 | 90.22 | 109.38 |
| AOT | sequence-medium | render | 284 | 124146.9 | 139717 | 437.14 | 491.96 |
| AOT | sequence-small | render | 84 | 87493.2 | 105539 | 1041.59 | 1256.42 |
| AOT | state-large | parse | 689 | 9788.1 | 20120 | 14.21 | 29.20 |
| AOT | state-large | render | 689 | 146147.1 | 236778 | 212.11 | 343.65 |
| AOT | state-medium | parse | 323 | 2923.7 | 5634 | 9.05 | 17.44 |
| AOT | state-medium | render | 323 | 58566.6 | 108935 | 181.32 | 337.26 |
| AOT | state-small | parse | 63 | 1714.2 | 2881 | 27.21 | 45.73 |
| AOT | state-small | render | 63 | 45431.7 | 80644 | 721.14 | 1280.06 |
| AOT | timeline-large | render | 942 | 419987.0 | 234121 | 445.85 | 248.54 |
| AOT | timeline-medium | render | 463 | 210651.6 | 206844 | 454.97 | 446.75 |
| AOT | timeline-small | render | 77 | 99879.4 | 109063 | 1297.14 | 1416.40 |
| AOT | treeView-large | render | 491 | 113810.4 | 115090 | 231.79 | 234.40 |
| AOT | treeView-medium | render | 119 | 82750.4 | 81128 | 695.38 | 681.75 |
| AOT | treeView-small | render | 32 | 30469.9 | 51043 | 952.18 | 1595.09 |
| AOT | treemap-large | render | 762 | 218806.5 | 202409 | 287.15 | 265.63 |
| AOT | treemap-medium | render | 405 | 103437.5 | 112717 | 255.40 | 278.31 |
| AOT | treemap-small | render | 147 | 128578.7 | 138183 | 874.69 | 940.02 |
| AOT | user-journey-large | render | 885 | 399766.2 | 296221 | 451.71 | 334.71 |
| AOT | user-journey-medium | render | 493 | 244270.3 | 205782 | 495.48 | 417.41 |
| AOT | user-journey-small | render | 116 | 32536.3 | 50405 | 280.49 | 434.53 |
| AOT | venn-large | render | 534 | 64451.9 | 60382 | 120.70 | 113.07 |
| AOT | venn-medium | render | 251 | 92196.6 | 77929 | 367.32 | 310.47 |
| AOT | venn-small | render | 76 | 42502.3 | 56394 | 559.24 | 742.03 |
| AOT | xychart-large | render | 707 | 256798.8 | 181976 | 363.22 | 257.39 |
| AOT | xychart-medium | render | 395 | 142687.8 | 106587 | 361.23 | 269.84 |
| AOT | xychart-small | render | 76 | 87081.7 | 92353 | 1145.81 | 1215.17 |
| JIT | architecture-large | render | 1305 | 409936.6 | 452844 | 314.13 | 347.01 |
| JIT | architecture-medium | render | 280 | 165764.1 | 201816 | 592.01 | 720.77 |
| JIT | architecture-small | render | 50 | 14004.8 | 47717 | 280.10 | 954.34 |
| JIT | block-large | render | 564 | 174904.2 | 210682 | 310.11 | 373.55 |
| JIT | block-medium | render | 220 | 146150.4 | 157473 | 664.32 | 715.79 |
| JIT | block-small | render | 36 | 48490.9 | 79331 | 1346.97 | 2203.64 |
| JIT | c4-large | render | 2178 | 653315.2 | 409058 | 299.96 | 187.81 |
| JIT | c4-medium | render | 351 | 255504.2 | 110568 | 727.93 | 315.01 |
| JIT | c4-small | render | 49 | 74350.4 | 67183 | 1517.35 | 1371.08 |
| JIT | class-large | render | 663 | 1703605.6 | 353822 | 2569.54 | 533.67 |
| JIT | class-medium | render | 272 | 141089.4 | 117191 | 518.71 | 430.85 |
| JIT | class-small | render | 53 | 87363.2 | 110282 | 1648.36 | 2080.79 |
| JIT | er-large | render | 585 | 497886.8 | 691768 | 851.09 | 1182.51 |
| JIT | er-medium | render | 330 | 314210.5 | 388240 | 952.15 | 1176.48 |
| JIT | er-small | render | 36 | 21225.5 | 41843 | 589.60 | 1162.31 |
| JIT | flowchart-large | parse | 951 | 58887.1 | 56266 | 61.92 | 59.17 |
| JIT | flowchart-large | render | 951 | 3125274.2 | 927835 | 3286.30 | 975.64 |
| JIT | flowchart-medium | parse | 336 | 12270.5 | 11982 | 36.52 | 35.66 |
| JIT | flowchart-medium | render | 336 | 272713.6 | 178710 | 811.65 | 531.88 |
| JIT | flowchart-small | parse | 33 | 5233.9 | 5784 | 158.60 | 175.27 |
| JIT | flowchart-small | render | 33 | 187373.7 | 126384 | 5677.99 | 3829.82 |
| JIT | gantt-large | render | 1547 | 257448.2 | 254812 | 166.42 | 164.71 |
| JIT | gantt-medium | render | 422 | 129001.9 | 124955 | 305.69 | 296.10 |
| JIT | gantt-small | render | 109 | 20181.2 | 50113 | 185.15 | 459.75 |
| JIT | git-large | render | 987 | 296655.4 | 256108 | 300.56 | 259.48 |
| JIT | git-medium | render | 467 | 97686.4 | 101148 | 209.18 | 216.59 |
| JIT | git-small | render | 90 | 65736.9 | 74863 | 730.41 | 831.81 |
| JIT | kanban-large | render | 809 | 341631.8 | 346006 | 422.29 | 427.70 |
| JIT | kanban-medium | render | 77 | 100769.7 | 77003 | 1308.70 | 1000.04 |
| JIT | kanban-small | render | 32 | 61909.6 | 73538 | 1934.68 | 2298.06 |
| JIT | mindmap-large | render | 950 | 414987.9 | 411596 | 436.83 | 433.26 |
| JIT | mindmap-medium | render | 130 | 182010.4 | 91063 | 1400.08 | 700.48 |
| JIT | mindmap-small | render | 26 | 22746.0 | 58623 | 874.85 | 2254.73 |
| JIT | packet-large | render | 690 | 248633.5 | 258087 | 360.34 | 374.04 |
| JIT | packet-medium | render | 115 | 35794.1 | 70126 | 311.25 | 609.79 |
| JIT | packet-small | render | 18 | 26367.7 | 61056 | 1464.87 | 3392.00 |
| JIT | pie-large | render | 449 | 225384.5 | 261611 | 501.97 | 582.65 |
| JIT | pie-medium | render | 90 | 105695.1 | 87742 | 1174.39 | 974.91 |
| JIT | pie-small | render | 23 | 18220.8 | 46820 | 792.21 | 2035.65 |
| JIT | quadrant-chart-large | render | 1158 | 58142.9 | 102878 | 50.21 | 88.84 |
| JIT | quadrant-chart-medium | render | 88 | 42225.7 | 55280 | 479.84 | 628.18 |
| JIT | quadrant-chart-small | render | 31 | 28276.7 | 61472 | 912.15 | 1982.97 |
| JIT | radar-large | render | 389 | 46632.9 | 76850 | 119.88 | 197.56 |
| JIT | radar-medium | render | 183 | 52412.5 | 85126 | 286.41 | 465.17 |
| JIT | radar-small | render | 59 | 42455.4 | 75068 | 719.58 | 1272.34 |
| JIT | requirement-large | render | 1566 | 590588.8 | 621224 | 377.13 | 396.69 |
| JIT | requirement-medium | render | 494 | 166193.6 | 190157 | 336.42 | 384.93 |
| JIT | requirement-small | render | 323 | 112265.4 | 139836 | 347.57 | 432.93 |
| JIT | sankey-large | render | 1262 | 192922.7 | 267478 | 152.87 | 211.95 |
| JIT | sankey-medium | render | 227 | 44423.8 | 76238 | 195.70 | 335.85 |
| JIT | sankey-small | render | 110 | 49579.5 | 92902 | 450.72 | 844.56 |
| JIT | sequence-large | render | 1094 | 85703.4 | 119522 | 78.34 | 109.25 |
| JIT | sequence-medium | render | 284 | 116335.8 | 139086 | 409.63 | 489.74 |
| JIT | sequence-small | render | 84 | 61872.0 | 104518 | 736.57 | 1244.26 |
| JIT | state-large | parse | 689 | 8567.7 | 20120 | 12.43 | 29.20 |
| JIT | state-large | render | 689 | 177599.7 | 230742 | 257.76 | 334.89 |
| JIT | state-medium | parse | 323 | 2405.1 | 5634 | 7.45 | 17.44 |
| JIT | state-medium | render | 323 | 71552.4 | 106915 | 221.52 | 331.01 |
| JIT | state-small | parse | 63 | 1733.1 | 2881 | 27.51 | 45.73 |
| JIT | state-small | render | 63 | 58101.2 | 79347 | 922.24 | 1259.48 |
| JIT | timeline-large | render | 942 | 298920.1 | 234890 | 317.32 | 249.35 |
| JIT | timeline-medium | render | 463 | 211192.2 | 208264 | 456.14 | 449.81 |
| JIT | timeline-small | render | 77 | 96168.0 | 108966 | 1248.93 | 1415.14 |
| JIT | treeView-large | render | 491 | 119270.5 | 115169 | 242.91 | 234.56 |
| JIT | treeView-medium | render | 119 | 68939.4 | 81026 | 579.32 | 680.89 |
| JIT | treeView-small | render | 32 | 21071.8 | 50590 | 658.49 | 1580.94 |
| JIT | treemap-large | render | 762 | 162151.5 | 199565 | 212.80 | 261.90 |
| JIT | treemap-medium | render | 405 | 88647.6 | 111552 | 218.88 | 275.44 |
| JIT | treemap-small | render | 147 | 171770.7 | 136957 | 1168.51 | 931.68 |
| JIT | user-journey-large | render | 885 | 374422.3 | 296629 | 423.08 | 335.17 |
| JIT | user-journey-medium | render | 493 | 275139.8 | 206354 | 558.09 | 418.57 |
| JIT | user-journey-small | render | 116 | 34156.1 | 49992 | 294.45 | 430.97 |
| JIT | venn-large | render | 534 | 41368.3 | 59794 | 77.47 | 111.97 |
| JIT | venn-medium | render | 251 | 80586.7 | 77142 | 321.06 | 307.34 |
| JIT | venn-small | render | 76 | 27006.8 | 55811 | 355.35 | 734.36 |
| JIT | xychart-large | render | 707 | 214109.6 | 182435 | 302.84 | 258.04 |
| JIT | xychart-medium | render | 395 | 78894.1 | 106568 | 199.73 | 269.79 |
| JIT | xychart-small | render | 76 | 64944.2 | 92230 | 854.53 | 1213.55 |

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
