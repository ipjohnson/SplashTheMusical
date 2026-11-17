# Dagre benchmark results

The time and allocation of the dagre port's whole layout, from `DagreLayout.BuildLayoutGraph` through
`RunPositionPhases`, on the seeded flowchart-shaped graphs of `DagreGraphs.Flowchart` in
`tests/SplashTheMusical.Benchmarks`. `ns` is the mean time per layout and `Bytes` the bytes allocated per layout.
Written by `mise run bench-dagre`.

| Property | Value |
|---|---|
| Date | 2026-11-16 |
| Machine | Apple M3 Pro, 11 logical cores |
| OS | macOS 27.0.1 (osx-arm64) |
| Runtime | .NET 10.0.14 |
| BenchmarkDotNet | 0.15.8 |
| Jobs | JIT, AOT: 3 warmup and 10 measured iterations of about 100 ms each |

## Results

| Job | Nodes | Edges | ns | Bytes | ns/node | Bytes/node |
|---|---:|---:|---:|---:|---:|---:|
| AOT | 10 | 11 | 168088 | 695074 | 16809 | 69507 |
| AOT | 100 | 130 | 2552439 | 6259660 | 25524 | 62597 |
| AOT | 1000 | 1305 | 54039288 | 72412564 | 54039 | 72413 |
| JIT | 10 | 11 | 299731 | 665296 | 29973 | 66530 |
| JIT | 100 | 130 | 6868598 | 6051064 | 68686 | 60511 |
| JIT | 1000 | 1305 | 110970212 | 70025760 | 110970 | 70026 |
