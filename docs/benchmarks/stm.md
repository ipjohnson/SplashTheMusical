# STM benchmark results

STM's stages on the benchmark set in `corpus/benchmarks/cases.txt`. Written by `mise run bench`.

| Property | Value |
|---|---|
| Date | 2026-11-20 |
| Machine | Apple M3 Pro, 11 logical cores |
| OS | macOS 27.0.1 (osx-arm64) |
| Runtime | .NET 10.0.15 |
| BenchmarkDotNet | 0.15.8 |
| Jobs | JIT: 20 warmup, AOT: 3 warmup, and 10 measured iterations, of about 100 ms each |

## Results

| Job | Case | Stage | Chars | ns | Bytes | ns/char | Bytes/char |
|---|---|---|---:|---:|---:|---:|---:|
| AOT | class-large | layout | 663 | 284890.4 | 1115549 | 429.70 | 1682.58 |
| AOT | class-large | parse | 663 | 4804.0 | 5168 | 7.25 | 7.79 |
| AOT | class-large | render | 663 | 624818.7 | 1658685 | 942.41 | 2501.79 |
| AOT | class-large | write | 663 | 2606.5 | 3112 | 3.93 | 4.69 |
| AOT | class-medium | layout | 272 | 40244.0 | 142137 | 147.96 | 522.56 |
| AOT | class-medium | parse | 272 | 1211.6 | 2032 | 4.45 | 7.47 |
| AOT | class-medium | render | 272 | 337773.6 | 664784 | 1241.81 | 2444.06 |
| AOT | class-medium | write | 272 | 260.3 | 768 | 0.96 | 2.82 |
| AOT | class-small | layout | 53 | 55038.7 | 196449 | 1038.47 | 3706.58 |
| AOT | class-small | parse | 53 | 1183.5 | 736 | 22.33 | 13.89 |
| AOT | class-small | render | 53 | 352233.3 | 718760 | 6645.91 | 13561.51 |
| AOT | class-small | write | 53 | 806.8 | 672 | 15.22 | 12.68 |
| AOT | flowchart-large | layout | 951 | 638256.1 | 1580371 | 671.14 | 1661.80 |
| AOT | flowchart-large | parse | 951 | 19141.6 | 10425 | 20.13 | 10.96 |
| AOT | flowchart-large | render | 951 | 1061490.0 | 2266507 | 1116.18 | 2383.29 |
| AOT | flowchart-large | write | 951 | 4376.7 | 16832 | 4.60 | 17.70 |
| AOT | flowchart-medium | layout | 336 | 99572.5 | 363043 | 296.35 | 1080.49 |
| AOT | flowchart-medium | parse | 336 | 5164.7 | 4352 | 15.37 | 12.95 |
| AOT | flowchart-medium | render | 336 | 476172.7 | 888016 | 1417.18 | 2642.90 |
| AOT | flowchart-medium | write | 336 | 993.4 | 3920 | 2.96 | 11.67 |
| AOT | flowchart-small | layout | 33 | 146518.7 | 394612 | 4439.96 | 11957.94 |
| AOT | flowchart-small | parse | 33 | 1909.5 | 1928 | 57.86 | 58.42 |
| AOT | flowchart-small | render | 33 | 458093.6 | 1005470 | 13881.62 | 30468.79 |
| AOT | flowchart-small | write | 33 | 506.9 | 2488 | 15.36 | 75.39 |
| AOT | state-large | layout | 689 | 205740.0 | 582165 | 298.61 | 844.94 |
| AOT | state-large | parse | 689 | 4309.2 | 4256 | 6.25 | 6.18 |
| AOT | state-large | render | 689 | 525497.1 | 1145743 | 762.70 | 1662.91 |
| AOT | state-large | write | 689 | 439.8 | 2496 | 0.64 | 3.62 |
| AOT | state-medium | layout | 323 | 86305.4 | 267178 | 267.20 | 827.18 |
| AOT | state-medium | parse | 323 | 1786.4 | 2880 | 5.53 | 8.92 |
| AOT | state-medium | render | 323 | 362782.5 | 810370 | 1123.17 | 2508.89 |
| AOT | state-medium | write | 323 | 228.1 | 1024 | 0.71 | 3.17 |
| AOT | state-small | layout | 63 | 70079.8 | 206282 | 1112.38 | 3274.32 |
| AOT | state-small | parse | 63 | 579.5 | 584 | 9.20 | 9.27 |
| AOT | state-small | render | 63 | 340632.0 | 734809 | 5406.86 | 11663.63 |
| AOT | state-small | write | 63 | 75.6 | 480 | 1.20 | 7.62 |
| JIT | class-large | layout | 663 | 679019.9 | 1076064 | 1024.16 | 1623.02 |
| JIT | class-large | parse | 663 | 4197.5 | 5112 | 6.33 | 7.71 |
| JIT | class-large | render | 663 | 795767.7 | 1616400 | 1200.25 | 2438.01 |
| JIT | class-large | write | 663 | 1994.7 | 3112 | 3.01 | 4.69 |
| JIT | class-medium | layout | 272 | 34555.6 | 138640 | 127.04 | 509.71 |
| JIT | class-medium | parse | 272 | 1012.7 | 1976 | 3.72 | 7.26 |
| JIT | class-medium | render | 272 | 351841.2 | 659272 | 1293.53 | 2423.79 |
| JIT | class-medium | write | 272 | 202.3 | 768 | 0.74 | 2.82 |
| JIT | class-small | layout | 53 | 46403.1 | 190752 | 875.53 | 3599.09 |
| JIT | class-small | parse | 53 | 937.5 | 736 | 17.69 | 13.89 |
| JIT | class-small | render | 53 | 358116.3 | 710464 | 6756.91 | 13404.98 |
| JIT | class-small | write | 53 | 605.3 | 672 | 11.42 | 12.68 |
| JIT | flowchart-large | layout | 951 | 1222143.9 | 1538904 | 1285.11 | 1618.20 |
| JIT | flowchart-large | parse | 951 | 14322.5 | 10424 | 15.06 | 10.96 |
| JIT | flowchart-large | render | 951 | 1563103.5 | 2218105 | 1643.64 | 2332.39 |
| JIT | flowchart-large | write | 951 | 3168.3 | 16896 | 3.33 | 17.77 |
| JIT | flowchart-medium | layout | 336 | 104038.9 | 349672 | 309.64 | 1040.69 |
| JIT | flowchart-medium | parse | 336 | 3855.9 | 4352 | 11.48 | 12.95 |
| JIT | flowchart-medium | render | 336 | 1152720.2 | 875416 | 3430.71 | 2605.40 |
| JIT | flowchart-medium | write | 336 | 689.6 | 3952 | 2.05 | 11.76 |
| JIT | flowchart-small | layout | 33 | 113991.2 | 384824 | 3454.28 | 11661.33 |
| JIT | flowchart-small | parse | 33 | 1263.7 | 1928 | 38.29 | 58.42 |
| JIT | flowchart-small | render | 33 | 537111.0 | 994528 | 16276.09 | 30137.21 |
| JIT | flowchart-small | write | 33 | 395.0 | 2488 | 11.97 | 75.39 |
| JIT | state-large | layout | 689 | 215466.8 | 564704 | 312.72 | 819.60 |
| JIT | state-large | parse | 689 | 3793.7 | 4200 | 5.51 | 6.10 |
| JIT | state-large | render | 689 | 880782.6 | 1126312 | 1278.35 | 1634.71 |
| JIT | state-large | write | 689 | 328.0 | 2496 | 0.48 | 3.62 |
| JIT | state-medium | layout | 323 | 94365.3 | 260088 | 292.15 | 805.23 |
| JIT | state-medium | parse | 323 | 1430.5 | 2824 | 4.43 | 8.74 |
| JIT | state-medium | render | 323 | 412269.5 | 803056 | 1276.38 | 2486.24 |
| JIT | state-medium | write | 323 | 165.1 | 1024 | 0.51 | 3.17 |
| JIT | state-small | layout | 63 | 49340.5 | 200472 | 783.18 | 3182.10 |
| JIT | state-small | parse | 63 | 557.9 | 584 | 8.86 | 9.27 |
| JIT | state-small | render | 63 | 308873.0 | 728672 | 4902.75 | 11566.22 |
| JIT | state-small | write | 63 | 64.7 | 480 | 1.03 | 7.62 |
