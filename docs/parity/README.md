# Parity report

Each `<type>.md` in this folder holds the parity of one diagram type: STM against mermaid.js on that type's cases of
`corpus/`. `mise run parity` writes them, and `ParityReportTests` in `tests/SplashTheMusical.Conformance` fails when one
is out of date. No file holds a total, so branches that change different types change different files. The
[parity page of the documentation site](https://ipjohnson.github.io/SplashTheMusical/parity) shows every type's row
and adds them up, which `tools/site/parity.mjs` does.

A level's rate is the cases that pass it out of the cases it applies to. L1 to L4 do not apply to a case that both
mermaid.js and STM refuse. Section 7 of [the parity plan](../mermaid-parity-plan.md) describes the levels,
[the tolerances README](../../corpus/tolerances/README.md) the L3 and L4 tolerances, and
[the ledger README](../../corpus/ledger/README.md) the categories of the ledger counts. L4 comes from `corpus/l4`,
which `mise run raster` writes with Node, and a report-only type's L4 scores are not judged.
