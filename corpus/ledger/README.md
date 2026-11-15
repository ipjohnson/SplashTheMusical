# Parity ledgers

Each `<type>.txt` lists the corpus cases of one diagram type where STM differs from mermaid.js, one level per line.
Section 7 of [the parity plan](../../docs/mermaid-parity-plan.md) describes the rules. `LedgerTests` in
`tests/SplashTheMusical.Conformance` enforces them, and it needs no Node.

- A case that fails a level and is not listed fails the test.
- A listed case and level that no longer fails fails the test, so a ledger cannot go stale.
- A type whose ledger holds more `bug` entries than `bugs.txt` records fails the test, unless `bugs.txt` names another
  mermaid version than `tools/oracle/package.json` pins.

## Format

Lines are separated by LF. A line that starts with `#` is a comment. A line without a tab starts a category:

| Line | Category |
|---|---|
| `bug #N` or `bug owner/repo#N` | A bug, tracked by the issue |
| `by-design` | STM differs on purpose |
| `out-of-scope` | STM does not support it |

Every other line is an entry under the category above it: the case's path below its type folder without `.mmd`, a tab,
and the level it fails, `L0`, `L1`, `L2` or `L3`.

```text
bug #13
docs-pie-001	L0
by-design
e2e-pie-004	L2
```

`bugs.txt` starts with `# mermaid <version>`, and then each type, a tab and its count of `bug` entries.

## Rewriting

```bash
STM_WRITE_LEDGER='#13' dotnet run --project tests/SplashTheMusical.Conformance -- --treenode-filter "/*/*/LedgerTests/*"
```

This rewrites every ledger to list exactly the current differences. A listed entry keeps its category, and a new one goes
under `bug` with the issue the variable names. Review the diff before committing it.

The rewrite lowers a count in `bugs.txt` when its ledger holds fewer bugs, and leaves a higher one failing. It writes
the ledgers' counts as they are only when `bugs.txt` names another mermaid version, as after a mermaid bump.
