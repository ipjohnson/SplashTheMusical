Differential fuzzing of STM against mermaid.js 12.3.0 with seed 1: 4 finds.

| Type | Seed cases | Sources | Compared | Finds |
|---|--:|--:|--:|--:|
| `flowchart` | 852 | 3293 | 3177 | 4 |

## `flowchart/2280f01ceb2b0234`: L0 mermaid.js-accepts

Source 88 of the run, minimized from 2 lines to 2 in 131 tests.

```text
flowchart
A@{:}
```

```text
mermaid.js: accepts
STM: A plain scalar cannot start with ':'.
```

Add it to the corpus with `mise run fuzz-add <run folder>/finds/flowchart/2280f01ceb2b0234.mmd`.

## `flowchart/2b0e5d36d51bef38`: L0 STM-accepts

Source 610 of the run, minimized from 1 lines to 1 in 56 tests.

```text
graph TD;classDef , background
```

```text
mermaid.js: Failed to execute 'insertRule' on 'CSSStyleSheet': Failed to parse the rule '. > * { background !important; background !important; }'.
STM: accepts
```

Add it to the corpus with `mise run fuzz-add <run folder>/finds/flowchart/2b0e5d36d51bef38.mmd`.

## `flowchart/dad5513634ce1b56`: L1 node

Source 832 of the run, minimized from 5 lines to 3 in 204 tests.

```text
graph
A[" "]-->B["`*
*`"]
```

```text
+ node	B		rect
- node	B	* *	rect
```

Add it to the corpus with `mise run fuzz-add <run folder>/finds/flowchart/dad5513634ce1b56.mmd`.

## `flowchart/70250137941d9433`: L1 edge

Source 1327 of the run, minimized from 26 lines to 2 in 156 tests.

```text
flowchart
n22===n44
```

```text
+ edge	n22	n44	none	none	solid
- edge	n22	n44	none	none	thick
```

Add it to the corpus with `mise run fuzz-add <run folder>/finds/flowchart/70250137941d9433.mmd`.
