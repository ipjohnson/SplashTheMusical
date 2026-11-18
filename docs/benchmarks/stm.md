# STM benchmark results

STM's stages on the benchmark set in `corpus/benchmarks/cases.txt`. Written by `mise run bench`.

| Property | Value |
|---|---|
| Date | 2026-11-18 |
| Machine | Apple M3 Pro, 11 logical cores |
| OS | macOS 27.0.1 (osx-arm64) |
| Runtime | .NET 10.0.14 |
| BenchmarkDotNet | 0.15.8 |
| Jobs | JIT: 20 warmup, AOT: 3 warmup, and 10 measured iterations, of about 100 ms each |

## Results

| Job | Case | Stage | Chars | ns | Bytes | ns/char | Bytes/char |
|---|---|---|---:|---:|---:|---:|---:|
| AOT | flowchart-large | layout | 951 | 737885.0 | 1576791 | 775.90 | 1658.03 |
| AOT | flowchart-large | parse | 951 | 22377.8 | 10425 | 23.53 | 10.96 |
| AOT | flowchart-large | render | 951 | 1111724.3 | 2262155 | 1169.01 | 2378.71 |
| AOT | flowchart-large | write | 951 | 4393.8 | 16832 | 4.62 | 17.70 |
| AOT | flowchart-medium | layout | 336 | 154956.2 | 362284 | 461.18 | 1078.23 |
| AOT | flowchart-medium | parse | 336 | 6678.0 | 4352 | 19.87 | 12.95 |
| AOT | flowchart-medium | render | 336 | 447755.3 | 887063 | 1332.61 | 2640.07 |
| AOT | flowchart-medium | write | 336 | 900.3 | 3920 | 2.68 | 11.67 |
| AOT | flowchart-small | layout | 33 | 158301.4 | 393956 | 4797.01 | 11938.06 |
| AOT | flowchart-small | parse | 33 | 2110.1 | 1928 | 63.94 | 58.42 |
| AOT | flowchart-small | render | 33 | 476120.7 | 1004623 | 14427.90 | 30443.12 |
| AOT | flowchart-small | write | 33 | 504.4 | 2488 | 15.28 | 75.39 |
| JIT | flowchart-large | layout | 951 | 910901.2 | 1529816 | 957.84 | 1608.64 |
| JIT | flowchart-large | parse | 951 | 16139.7 | 10424 | 16.97 | 10.96 |
| JIT | flowchart-large | render | 951 | 1340338.7 | 2213753 | 1409.40 | 2327.82 |
| JIT | flowchart-large | write | 951 | 3016.0 | 16896 | 3.17 | 17.77 |
| JIT | flowchart-medium | layout | 336 | 125961.9 | 348912 | 374.89 | 1038.43 |
| JIT | flowchart-medium | parse | 336 | 4171.7 | 4296 | 12.42 | 12.79 |
| JIT | flowchart-medium | render | 336 | 638347.9 | 871904 | 1899.84 | 2594.95 |
| JIT | flowchart-medium | write | 336 | 660.6 | 3952 | 1.97 | 11.76 |
| JIT | flowchart-small | layout | 33 | 171618.7 | 385064 | 5200.57 | 11668.61 |
| JIT | flowchart-small | parse | 33 | 1301.1 | 1928 | 39.43 | 58.42 |
| JIT | flowchart-small | render | 33 | 528255.3 | 993168 | 16007.74 | 30096.00 |
| JIT | flowchart-small | write | 33 | 390.1 | 2488 | 11.82 | 75.39 |
