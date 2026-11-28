# STM benchmark results

STM's stages on the benchmark set in `corpus/benchmarks/cases.txt`. Written by `mise run bench`.

| Property | Value |
|---|---|
| Date | 2026-11-28 |
| Machine | Apple M1 (Virtual), 3 logical cores |
| OS | macOS 26.7.2 (osx-arm64) |
| Runtime | .NET 10.0.15 |
| BenchmarkDotNet | 0.15.8 |
| Jobs | JIT: 60 warmup, AOT: 3 warmup, and 10 measured iterations, of about 100 ms each |

## Results

| Job | Case | Stage | Chars | ns | Bytes | ns/char | Bytes/char |
|---|---|---|---:|---:|---:|---:|---:|
| AOT | block-large | layout | 564 | 173468.8 | 214780 | 307.57 | 380.82 |
| AOT | block-large | parse | 564 | 15038.8 | 7344 | 26.66 | 13.02 |
| AOT | block-large | render | 564 | 756256.0 | 669770 | 1340.88 | 1187.54 |
| AOT | block-large | write | 564 | 24964.6 | 10233 | 44.26 | 18.14 |
| AOT | block-medium | layout | 220 | 52507.5 | 93250 | 238.67 | 423.86 |
| AOT | block-medium | parse | 220 | 4623.5 | 2208 | 21.02 | 10.04 |
| AOT | block-medium | render | 220 | 580204.6 | 519715 | 2637.29 | 2362.34 |
| AOT | block-medium | write | 220 | 9990.3 | 7032 | 45.41 | 31.96 |
| AOT | block-small | layout | 36 | 17231.1 | 34536 | 478.64 | 959.33 |
| AOT | block-small | parse | 36 | 1194.4 | 576 | 33.18 | 16.00 |
| AOT | block-small | render | 36 | 471158.2 | 446584 | 13087.73 | 12405.11 |
| AOT | block-small | write | 36 | 2005.6 | 1384 | 55.71 | 38.44 |
| AOT | class-large | layout | 663 | 440801.8 | 1115744 | 664.86 | 1682.87 |
| AOT | class-large | parse | 663 | 7534.1 | 5168 | 11.36 | 7.79 |
| AOT | class-large | render | 663 | 1068076.6 | 1658901 | 1610.98 | 2502.11 |
| AOT | class-large | write | 663 | 5064.7 | 3112 | 7.64 | 4.69 |
| AOT | class-medium | layout | 272 | 72379.4 | 142177 | 266.10 | 522.71 |
| AOT | class-medium | parse | 272 | 1786.0 | 2032 | 6.57 | 7.47 |
| AOT | class-medium | render | 272 | 610749.1 | 664829 | 2245.40 | 2444.22 |
| AOT | class-medium | write | 272 | 349.8 | 768 | 1.29 | 2.82 |
| AOT | class-small | layout | 53 | 71804.5 | 196506 | 1354.80 | 3707.66 |
| AOT | class-small | parse | 53 | 1703.7 | 736 | 32.15 | 13.89 |
| AOT | class-small | render | 53 | 655942.9 | 718820 | 12376.28 | 13562.64 |
| AOT | class-small | write | 53 | 1034.1 | 672 | 19.51 | 12.68 |
| AOT | er-large | layout | 585 | 99310.5 | 265018 | 169.76 | 453.02 |
| AOT | er-large | parse | 585 | 5978.0 | 4576 | 10.22 | 7.82 |
| AOT | er-large | render | 585 | 668169.3 | 792240 | 1142.17 | 1354.26 |
| AOT | er-large | write | 585 | 2632.2 | 2536 | 4.50 | 4.34 |
| AOT | er-medium | layout | 330 | 79211.6 | 182762 | 240.04 | 553.82 |
| AOT | er-medium | parse | 330 | 3314.5 | 2984 | 10.04 | 9.04 |
| AOT | er-medium | render | 330 | 538911.8 | 685214 | 1633.07 | 2076.41 |
| AOT | er-medium | write | 330 | 1484.4 | 1472 | 4.50 | 4.46 |
| AOT | er-small | layout | 36 | 92279.4 | 308194 | 2563.32 | 8560.94 |
| AOT | er-small | parse | 36 | 1054.6 | 672 | 29.29 | 18.67 |
| AOT | er-small | render | 36 | 532081.3 | 797622 | 14780.04 | 22156.17 |
| AOT | er-small | write | 36 | 432.0 | 488 | 12.00 | 13.56 |
| AOT | flowchart-large | layout | 951 | 1082518.6 | 1581398 | 1138.30 | 1662.88 |
| AOT | flowchart-large | parse | 951 | 28757.3 | 10425 | 30.24 | 10.96 |
| AOT | flowchart-large | render | 951 | 2445401.0 | 2267509 | 2571.40 | 2384.34 |
| AOT | flowchart-large | write | 951 | 5689.9 | 16832 | 5.98 | 17.70 |
| AOT | flowchart-medium | layout | 336 | 137795.3 | 363276 | 410.10 | 1081.18 |
| AOT | flowchart-medium | parse | 336 | 6538.5 | 4352 | 19.46 | 12.95 |
| AOT | flowchart-medium | render | 336 | 675264.2 | 888255 | 2009.71 | 2643.62 |
| AOT | flowchart-medium | write | 336 | 1380.9 | 3920 | 4.11 | 11.67 |
| AOT | flowchart-small | layout | 33 | 195003.2 | 394829 | 5909.19 | 11964.52 |
| AOT | flowchart-small | parse | 33 | 2520.2 | 1928 | 76.37 | 58.42 |
| AOT | flowchart-small | render | 33 | 673912.6 | 1005693 | 20421.59 | 30475.55 |
| AOT | flowchart-small | write | 33 | 1000.5 | 2488 | 30.32 | 75.39 |
| AOT | requirement-large | layout | 1566 | 450302.9 | 700330 | 287.55 | 447.21 |
| AOT | requirement-large | parse | 1566 | 23296.6 | 4248 | 14.88 | 2.71 |
| AOT | requirement-large | render | 1566 | 1075592.1 | 1248967 | 686.84 | 797.55 |
| AOT | requirement-large | write | 1566 | 5027.2 | 9808 | 3.21 | 6.26 |
| AOT | requirement-medium | layout | 494 | 78158.8 | 194676 | 158.22 | 394.08 |
| AOT | requirement-medium | parse | 494 | 5605.3 | 2496 | 11.35 | 5.05 |
| AOT | requirement-medium | render | 494 | 572322.1 | 684845 | 1158.55 | 1386.33 |
| AOT | requirement-medium | write | 494 | 1377.4 | 3008 | 2.79 | 6.09 |
| AOT | requirement-small | layout | 323 | 50305.6 | 130763 | 155.74 | 404.84 |
| AOT | requirement-small | parse | 323 | 3544.7 | 2192 | 10.97 | 6.79 |
| AOT | requirement-small | render | 323 | 558991.1 | 614054 | 1730.62 | 1901.10 |
| AOT | requirement-small | write | 323 | 1128.3 | 1800 | 3.49 | 5.57 |
| AOT | state-large | layout | 689 | 266306.7 | 582511 | 386.51 | 845.44 |
| AOT | state-large | parse | 689 | 5915.0 | 4256 | 8.58 | 6.18 |
| AOT | state-large | render | 689 | 770745.6 | 1146096 | 1118.64 | 1663.42 |
| AOT | state-large | write | 689 | 528.2 | 2496 | 0.77 | 3.62 |
| AOT | state-medium | layout | 323 | 114244.7 | 267331 | 353.70 | 827.65 |
| AOT | state-medium | parse | 323 | 2231.9 | 2880 | 6.91 | 8.92 |
| AOT | state-medium | render | 323 | 570896.5 | 810527 | 1767.48 | 2509.37 |
| AOT | state-medium | write | 323 | 316.6 | 1024 | 0.98 | 3.17 |
| AOT | state-small | layout | 63 | 107040.2 | 206371 | 1699.05 | 3275.73 |
| AOT | state-small | parse | 63 | 826.9 | 584 | 13.13 | 9.27 |
| AOT | state-small | render | 63 | 500427.5 | 734901 | 7943.29 | 11665.10 |
| AOT | state-small | write | 63 | 107.6 | 480 | 1.71 | 7.62 |
| JIT | block-large | layout | 564 | 129943.1 | 206288 | 230.40 | 365.76 |
| JIT | block-large | parse | 564 | 11054.6 | 7288 | 19.60 | 12.92 |
| JIT | block-large | render | 564 | 456672.2 | 658800 | 809.70 | 1168.09 |
| JIT | block-large | write | 564 | 15169.0 | 10234 | 26.90 | 18.15 |
| JIT | block-medium | layout | 220 | 38201.0 | 90344 | 173.64 | 410.65 |
| JIT | block-medium | parse | 220 | 3999.8 | 2208 | 18.18 | 10.04 |
| JIT | block-medium | render | 220 | 475038.0 | 514616 | 2159.26 | 2339.16 |
| JIT | block-medium | write | 220 | 6113.4 | 7032 | 27.79 | 31.96 |
| JIT | block-small | layout | 36 | 12602.2 | 33368 | 350.06 | 926.89 |
| JIT | block-small | parse | 36 | 1067.7 | 576 | 29.66 | 16.00 |
| JIT | block-small | render | 36 | 315700.9 | 443384 | 8769.47 | 12316.22 |
| JIT | block-small | write | 36 | 1448.8 | 1384 | 40.24 | 38.44 |
| JIT | class-large | layout | 663 | 344508.7 | 1073224 | 519.62 | 1618.74 |
| JIT | class-large | parse | 663 | 5658.6 | 5112 | 8.53 | 7.71 |
| JIT | class-large | render | 663 | 1479636.3 | 1615712 | 2231.73 | 2436.97 |
| JIT | class-large | write | 663 | 3017.8 | 3112 | 4.55 | 4.69 |
| JIT | class-medium | layout | 272 | 50204.7 | 138584 | 184.58 | 509.50 |
| JIT | class-medium | parse | 272 | 1631.8 | 1976 | 6.00 | 7.26 |
| JIT | class-medium | render | 272 | 432298.9 | 658297 | 1589.33 | 2420.21 |
| JIT | class-medium | write | 272 | 274.8 | 768 | 1.01 | 2.82 |
| JIT | class-small | layout | 53 | 60134.9 | 190880 | 1134.62 | 3601.51 |
| JIT | class-small | parse | 53 | 1364.0 | 736 | 25.74 | 13.89 |
| JIT | class-small | render | 53 | 538621.8 | 710280 | 10162.68 | 13401.51 |
| JIT | class-small | write | 53 | 821.8 | 672 | 15.51 | 12.68 |
| JIT | er-large | layout | 585 | 102556.1 | 257656 | 175.31 | 440.44 |
| JIT | er-large | parse | 585 | 5552.6 | 4520 | 9.49 | 7.73 |
| JIT | er-large | render | 585 | 539337.6 | 783576 | 921.94 | 1339.45 |
| JIT | er-large | write | 585 | 1871.6 | 2536 | 3.20 | 4.34 |
| JIT | er-medium | layout | 330 | 61347.5 | 178008 | 185.90 | 539.42 |
| JIT | er-medium | parse | 330 | 2757.5 | 2928 | 8.36 | 8.87 |
| JIT | er-medium | render | 330 | 531675.6 | 678896 | 1611.14 | 2057.26 |
| JIT | er-medium | write | 330 | 857.8 | 1472 | 2.60 | 4.46 |
| JIT | er-small | layout | 36 | 69052.3 | 300192 | 1918.12 | 8338.67 |
| JIT | er-small | parse | 36 | 865.5 | 672 | 24.04 | 18.67 |
| JIT | er-small | render | 36 | 456172.6 | 787872 | 12671.46 | 21885.33 |
| JIT | er-small | write | 36 | 257.4 | 488 | 7.15 | 13.56 |
| JIT | flowchart-large | layout | 951 | 789254.7 | 1533360 | 829.92 | 1612.37 |
| JIT | flowchart-large | parse | 951 | 22169.7 | 10424 | 23.31 | 10.96 |
| JIT | flowchart-large | render | 951 | 1791996.6 | 2217312 | 1884.33 | 2331.56 |
| JIT | flowchart-large | write | 951 | 5121.0 | 16896 | 5.38 | 17.77 |
| JIT | flowchart-medium | layout | 336 | 120927.5 | 349336 | 359.90 | 1039.69 |
| JIT | flowchart-medium | parse | 336 | 4935.5 | 4296 | 14.69 | 12.79 |
| JIT | flowchart-medium | render | 336 | 626957.2 | 872406 | 1865.94 | 2596.45 |
| JIT | flowchart-medium | write | 336 | 929.7 | 3952 | 2.77 | 11.76 |
| JIT | flowchart-small | layout | 33 | 158793.1 | 384832 | 4811.91 | 11661.58 |
| JIT | flowchart-small | parse | 33 | 1794.1 | 1928 | 54.37 | 58.42 |
| JIT | flowchart-small | render | 33 | 640120.4 | 993456 | 19397.59 | 30104.73 |
| JIT | flowchart-small | write | 33 | 503.9 | 2488 | 15.27 | 75.39 |
| JIT | requirement-large | layout | 1566 | 320256.7 | 679872 | 204.51 | 434.15 |
| JIT | requirement-large | parse | 1566 | 15936.1 | 4192 | 10.18 | 2.68 |
| JIT | requirement-large | render | 1566 | 969949.2 | 1225975 | 619.38 | 782.87 |
| JIT | requirement-large | write | 1566 | 4089.1 | 9808 | 2.61 | 6.26 |
| JIT | requirement-medium | layout | 494 | 72369.2 | 189474 | 146.50 | 383.55 |
| JIT | requirement-medium | parse | 494 | 5964.7 | 2440 | 12.07 | 4.94 |
| JIT | requirement-medium | render | 494 | 521085.3 | 677664 | 1054.83 | 1371.79 |
| JIT | requirement-medium | write | 494 | 1258.2 | 3008 | 2.55 | 6.09 |
| JIT | requirement-small | layout | 323 | 44438.7 | 128482 | 137.58 | 397.78 |
| JIT | requirement-small | parse | 323 | 3058.4 | 2136 | 9.47 | 6.61 |
| JIT | requirement-small | render | 323 | 446527.4 | 610151 | 1382.44 | 1889.01 |
| JIT | requirement-small | write | 323 | 521.1 | 1800 | 1.61 | 5.57 |
| JIT | state-large | layout | 689 | 221734.5 | 562424 | 321.82 | 816.29 |
| JIT | state-large | parse | 689 | 5249.3 | 4200 | 7.62 | 6.10 |
| JIT | state-large | render | 689 | 691942.3 | 1125416 | 1004.27 | 1633.40 |
| JIT | state-large | write | 689 | 463.2 | 2496 | 0.67 | 3.62 |
| JIT | state-medium | layout | 323 | 90825.0 | 259464 | 281.19 | 803.29 |
| JIT | state-medium | parse | 323 | 2067.4 | 2824 | 6.40 | 8.74 |
| JIT | state-medium | render | 323 | 481906.7 | 802288 | 1491.97 | 2483.86 |
| JIT | state-medium | write | 323 | 258.7 | 1024 | 0.80 | 3.17 |
| JIT | state-small | layout | 63 | 88134.3 | 200512 | 1398.96 | 3182.73 |
| JIT | state-small | parse | 63 | 684.5 | 584 | 10.87 | 9.27 |
| JIT | state-small | render | 63 | 477869.0 | 728560 | 7585.22 | 11564.44 |
| JIT | state-small | write | 63 | 92.7 | 480 | 1.47 | 7.62 |
