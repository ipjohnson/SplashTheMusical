# Parity of flowchart

STM, as `stm`, against mermaid.js 12.3.0 on the flowchart cases of `corpus/`. `mise run parity` writes
this file, and `ParityReportTests` in `tests/SplashTheMusical.Conformance` fails when it is out of date.
[The README](README.md) describes the columns.

| Type | Cases | Refused by mermaid.js | L0 | L1 | L2 | L3 | L4 | bug | by-design | out-of-scope |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| flowchart | 891 | 17 | 100.0% (891 of 891) | 99.5% (870 of 874) | 100.0% (874 of 874) | 25.6% (224 of 874) | 20.6% (180 of 874) | 654 | 0 | 0 |
