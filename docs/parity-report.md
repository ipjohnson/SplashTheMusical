# Parity report

STM, as `stm`, against mermaid.js 12.3.0 on the 4,744 cases of `corpus/`. `mise run parity` writes
this file, and `ParityReportTests` in `tests/SplashTheMusical.Conformance` fails when it is out of date.

A level's rate is the cases that pass it out of the cases it applies to. L1 to L4 do not apply to a case that both
mermaid.js and STM refuse. Section 7 of [the parity plan](mermaid-parity-plan.md) describes the levels,
[the tolerances README](../corpus/tolerances/README.md) the L3 and L4 tolerances, and
[the ledger README](../corpus/ledger/README.md) the categories of the ledger counts. L4 comes from `corpus/l4.txt`,
which `mise run raster` writes with Node, and a report-only type's L4 scores are not judged.

| Type | Cases | Refused by mermaid.js | L0 | L1 | L2 | L3 | L4 | bug | by-design | out-of-scope |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| agentflow | 208 | 13 | 6.3% (13 of 208) | 0.0% (0 of 195) | 0.0% (0 of 195) | 0.0% (0 of 195) | 0.0% (0 of 195) | 780 | 0 | 0 |
| architecture | 70 | 8 | 11.4% (8 of 70) | 0.0% (0 of 62) | 0.0% (0 of 62) | 0.0% (0 of 62) | report-only | 248 | 0 | 0 |
| block | 125 | 1 | 0.8% (1 of 125) | 0.0% (0 of 124) | 0.0% (0 of 124) | 0.0% (0 of 124) | 0.0% (0 of 124) | 496 | 0 | 0 |
| c4 | 67 | 4 | 6.0% (4 of 67) | 0.0% (0 of 63) | 0.0% (0 of 63) | 0.0% (0 of 63) | 0.0% (0 of 63) | 252 | 0 | 0 |
| class | 498 | 58 | 11.6% (58 of 498) | 0.0% (0 of 440) | 0.0% (0 of 440) | 0.0% (0 of 440) | 0.0% (0 of 440) | 1,760 | 0 | 0 |
| cynefin | 31 | 0 | 0.0% (0 of 31) | 0.0% (0 of 31) | 0.0% (0 of 31) | 0.0% (0 of 31) | 0.0% (0 of 31) | 124 | 0 | 0 |
| er | 358 | 7 | 2.0% (7 of 358) | 0.0% (0 of 351) | 0.0% (0 of 351) | 0.0% (0 of 351) | 0.0% (0 of 351) | 1,404 | 0 | 0 |
| eventmodeling | 35 | 2 | 5.7% (2 of 35) | 0.0% (0 of 33) | 0.0% (0 of 33) | 0.0% (0 of 33) | 0.0% (0 of 33) | 132 | 0 | 0 |
| flowchart | 891 | 17 | 100.0% (891 of 891) | 99.5% (870 of 874) | 100.0% (874 of 874) | 25.6% (224 of 874) | 14.1% (123 of 874) | 654 | 0 | 0 |
| gantt | 85 | 1 | 1.2% (1 of 85) | 0.0% (0 of 84) | 0.0% (0 of 84) | 0.0% (0 of 84) | 0.0% (0 of 84) | 336 | 0 | 0 |
| git | 258 | 32 | 12.4% (32 of 258) | 0.0% (0 of 226) | 0.0% (0 of 226) | 0.0% (0 of 226) | 0.0% (0 of 226) | 904 | 0 | 0 |
| info | 9 | 2 | 100.0% (9 of 9) | 100.0% (7 of 7) | 100.0% (7 of 7) | 100.0% (7 of 7) | 100.0% (7 of 7) | 0 | 0 | 0 |
| ishikawa | 17 | 0 | 0.0% (0 of 17) | 0.0% (0 of 17) | 0.0% (0 of 17) | 0.0% (0 of 17) | 0.0% (0 of 17) | 68 | 0 | 0 |
| kanban | 47 | 1 | 2.1% (1 of 47) | 0.0% (0 of 46) | 0.0% (0 of 46) | 0.0% (0 of 46) | 0.0% (0 of 46) | 184 | 0 | 0 |
| mindmap | 83 | 2 | 2.4% (2 of 83) | 0.0% (0 of 81) | 0.0% (0 of 81) | 0.0% (0 of 81) | report-only | 324 | 0 | 0 |
| packet | 30 | 8 | 26.7% (8 of 30) | 0.0% (0 of 22) | 0.0% (0 of 22) | 0.0% (0 of 22) | 0.0% (0 of 22) | 88 | 0 | 0 |
| pie | 67 | 2 | 3.0% (2 of 67) | 0.0% (0 of 65) | 0.0% (0 of 65) | 0.0% (0 of 65) | 0.0% (0 of 65) | 260 | 0 | 0 |
| quadrant-chart | 52 | 1 | 1.9% (1 of 52) | 0.0% (0 of 51) | 0.0% (0 of 51) | 0.0% (0 of 51) | 0.0% (0 of 51) | 204 | 0 | 0 |
| radar | 38 | 14 | 36.8% (14 of 38) | 0.0% (0 of 24) | 0.0% (0 of 24) | 0.0% (0 of 24) | 0.0% (0 of 24) | 96 | 0 | 0 |
| railroad | 120 | 14 | 11.7% (14 of 120) | 0.0% (0 of 106) | 0.0% (0 of 106) | 0.0% (0 of 106) | 0.0% (0 of 106) | 424 | 0 | 0 |
| requirement | 188 | 8 | 4.3% (8 of 188) | 0.0% (0 of 180) | 0.0% (0 of 180) | 0.0% (0 of 180) | 0.0% (0 of 180) | 720 | 0 | 0 |
| sankey | 33 | 4 | 12.1% (4 of 33) | 0.0% (0 of 29) | 0.0% (0 of 29) | 0.0% (0 of 29) | 0.0% (0 of 29) | 116 | 0 | 0 |
| sequence | 422 | 7 | 1.7% (7 of 422) | 0.0% (0 of 415) | 0.0% (0 of 415) | 0.0% (0 of 415) | 0.0% (0 of 415) | 1,660 | 0 | 0 |
| state | 288 | 3 | 1.0% (3 of 288) | 0.0% (0 of 285) | 0.0% (0 of 285) | 0.0% (0 of 285) | 0.0% (0 of 285) | 1,140 | 0 | 0 |
| swimlanes | 69 | 0 | 0.0% (0 of 69) | 0.0% (0 of 69) | 0.0% (0 of 69) | 0.0% (0 of 69) | 0.0% (0 of 69) | 276 | 0 | 0 |
| timeline | 59 | 0 | 0.0% (0 of 59) | 0.0% (0 of 59) | 0.0% (0 of 59) | 0.0% (0 of 59) | 0.0% (0 of 59) | 236 | 0 | 0 |
| treeView | 75 | 0 | 0.0% (0 of 75) | 0.0% (0 of 75) | 0.0% (0 of 75) | 0.0% (0 of 75) | 0.0% (0 of 75) | 300 | 0 | 0 |
| treemap | 53 | 2 | 3.8% (2 of 53) | 0.0% (0 of 51) | 0.0% (0 of 51) | 0.0% (0 of 51) | 0.0% (0 of 51) | 204 | 0 | 0 |
| usecase | 186 | 73 | 39.2% (73 of 186) | 0.0% (0 of 113) | 0.0% (0 of 113) | 0.0% (0 of 113) | 0.0% (0 of 113) | 452 | 0 | 0 |
| user-journey | 30 | 0 | 0.0% (0 of 30) | 0.0% (0 of 30) | 0.0% (0 of 30) | 0.0% (0 of 30) | 0.0% (0 of 30) | 120 | 0 | 0 |
| venn | 48 | 3 | 6.3% (3 of 48) | 0.0% (0 of 45) | 0.0% (0 of 45) | 0.0% (0 of 45) | 0.0% (0 of 45) | 180 | 0 | 0 |
| wardley | 54 | 2 | 3.7% (2 of 54) | 0.0% (0 of 52) | 0.0% (0 of 52) | 0.0% (0 of 52) | 0.0% (0 of 52) | 208 | 0 | 0 |
| xychart | 120 | 44 | 36.7% (44 of 120) | 0.0% (0 of 76) | 0.0% (0 of 76) | 0.0% (0 of 76) | 0.0% (0 of 76) | 304 | 0 | 0 |
| zenuml | 30 | 0 | 0.0% (0 of 30) | 0.0% (0 of 30) | 0.0% (0 of 30) | 0.0% (0 of 30) | 0.0% (0 of 30) | 120 | 0 | 0 |
| **Total** | 4,744 | 333 | 25.6% (1,214 of 4,744) | 19.9% (877 of 4,411) | 20.0% (881 of 4,411) | 5.2% (231 of 4,411) | 3.0% (130 of 4,268) | 14,774 | 0 | 0 |
