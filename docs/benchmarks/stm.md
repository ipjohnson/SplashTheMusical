# STM benchmark results

STM's stages on the benchmark set in `corpus/benchmarks/cases.txt`. Written by `mise run bench`.

| Property | Value |
|---|---|
| Date | 2026-11-19 |
| Machine | Apple M3 Pro, 11 logical cores |
| OS | macOS 27.0.1 (osx-arm64) |
| Runtime | .NET 10.0.15 |
| BenchmarkDotNet | 0.15.8 |
| Jobs | JIT: 20 warmup, AOT: 3 warmup, and 10 measured iterations, of about 100 ms each |

## Results

| Job | Case | Stage | Chars | ns | Bytes | ns/char | Bytes/char |
|---|---|---|---:|---:|---:|---:|---:|
| AOT | flowchart-large | layout | 951 | 707491.5 | 1577781 | 743.94 | 1659.08 |
| AOT | flowchart-large | parse | 951 | 21783.5 | 10425 | 22.91 | 10.96 |
| AOT | flowchart-large | render | 951 | 1327940.5 | 2263147 | 1396.36 | 2379.75 |
| AOT | flowchart-large | write | 951 | 4927.7 | 16832 | 5.18 | 17.70 |
| AOT | flowchart-medium | layout | 336 | 112256.5 | 362515 | 334.10 | 1078.91 |
| AOT | flowchart-medium | parse | 336 | 5947.3 | 4352 | 17.70 | 12.95 |
| AOT | flowchart-medium | render | 336 | 579402.8 | 887299 | 1724.41 | 2640.77 |
| AOT | flowchart-medium | write | 336 | 1081.1 | 3920 | 3.22 | 11.67 |
| AOT | flowchart-small | layout | 33 | 160766.9 | 394116 | 4871.72 | 11942.91 |
| AOT | flowchart-small | parse | 33 | 2208.7 | 1928 | 66.93 | 58.42 |
| AOT | flowchart-small | render | 33 | 488271.6 | 1004784 | 14796.11 | 30448.00 |
| AOT | flowchart-small | write | 33 | 522.0 | 2488 | 15.82 | 75.39 |
| AOT | state-large | layout | 689 | 221507.9 | 580606 | 321.49 | 842.68 |
| AOT | state-large | parse | 689 | 4748.1 | 4256 | 6.89 | 6.18 |
| AOT | state-large | render | 689 | 555501.3 | 1143881 | 806.24 | 1660.20 |
| AOT | state-large | write | 689 | 483.5 | 2496 | 0.70 | 3.62 |
| AOT | state-medium | layout | 323 | 98276.1 | 266906 | 304.26 | 826.33 |
| AOT | state-medium | parse | 323 | 1779.8 | 2880 | 5.51 | 8.92 |
| AOT | state-medium | render | 323 | 389703.8 | 809986 | 1206.51 | 2507.70 |
| AOT | state-medium | write | 323 | 226.9 | 1024 | 0.70 | 3.17 |
| AOT | state-small | layout | 63 | 70555.0 | 206122 | 1119.92 | 3271.78 |
| AOT | state-small | parse | 63 | 570.8 | 584 | 9.06 | 9.27 |
| AOT | state-small | render | 63 | 343390.9 | 734585 | 5450.65 | 11660.08 |
| AOT | state-small | write | 63 | 90.9 | 480 | 1.44 | 7.62 |
| JIT | flowchart-large | layout | 951 | 571967.6 | 1530664 | 601.44 | 1609.53 |
| JIT | flowchart-large | parse | 951 | 14658.6 | 10424 | 15.41 | 10.96 |
| JIT | flowchart-large | render | 951 | 1194560.6 | 2213761 | 1256.11 | 2327.82 |
| JIT | flowchart-large | write | 951 | 3581.6 | 16896 | 3.77 | 17.77 |
| JIT | flowchart-medium | layout | 336 | 151829.4 | 348824 | 451.87 | 1038.17 |
| JIT | flowchart-medium | parse | 336 | 4171.4 | 4296 | 12.41 | 12.79 |
| JIT | flowchart-medium | render | 336 | 737543.5 | 872136 | 2195.07 | 2595.64 |
| JIT | flowchart-medium | write | 336 | 754.3 | 3952 | 2.24 | 11.76 |
| JIT | flowchart-small | layout | 33 | 171005.5 | 385224 | 5181.99 | 11673.45 |
| JIT | flowchart-small | parse | 33 | 1368.8 | 1928 | 41.48 | 58.42 |
| JIT | flowchart-small | render | 33 | 514537.6 | 993328 | 15592.05 | 30100.85 |
| JIT | flowchart-small | write | 33 | 414.2 | 2488 | 12.55 | 75.39 |
| JIT | state-large | layout | 689 | 260075.4 | 563144 | 377.47 | 817.34 |
| JIT | state-large | parse | 689 | 4207.2 | 4200 | 6.11 | 6.10 |
| JIT | state-large | render | 689 | 1060783.0 | 1124880 | 1539.60 | 1632.63 |
| JIT | state-large | write | 689 | 356.8 | 2496 | 0.52 | 3.62 |
| JIT | state-medium | layout | 323 | 99183.7 | 259240 | 307.07 | 802.60 |
| JIT | state-medium | parse | 323 | 1570.0 | 2824 | 4.86 | 8.74 |
| JIT | state-medium | render | 323 | 410327.0 | 802016 | 1270.36 | 2483.02 |
| JIT | state-medium | write | 323 | 173.9 | 1024 | 0.54 | 3.17 |
| JIT | state-small | layout | 63 | 51361.3 | 200312 | 815.26 | 3179.56 |
| JIT | state-small | parse | 63 | 476.6 | 584 | 7.57 | 9.27 |
| JIT | state-small | render | 63 | 320353.0 | 728368 | 5084.97 | 11561.40 |
| JIT | state-small | write | 63 | 74.1 | 480 | 1.18 | 7.62 |
