# Clean-room rules for the layered layout engine

On 2026-11-20 the owner decided that STM does not port ELK. STM designs and writes its own layered layout library, in
`src/SplashTheMusical.Layout`, that reproduces the layouts mermaid.js gets from ELK. The work is tracked in
ipjohnson/SplashTheMusical#25.

ELK is licensed `EPL-2.0 OR GPL-3.0-or-later`, and STM is MIT. Every port in STM so far is from MIT code. A port of ELK,
or code written by someone who has read ELK's source, could be a derived work of ELK. So the engine is built from what
ELK does, never from how its source does it.

These rules apply to every session, subagent and person working on the engine, its tests, its tools and its design
documents.

## What a session may read

- **Published papers on layered graph drawing.** For example Sugiyama, Tagawa and Toda, "Methods for Visual
  Understanding of Hierarchical System Structures" (1981); Gansner, Koutsofios, North and Vo, "A Technique for Drawing
  Directed Graphs" (1993); Brandes and Köpf, "Fast and Simple Horizontal Coordinate Assignment" (2001); and Schulze,
  Spönemann and von Hanxleden, "Drawing Layered Graphs with Port Constraints" (2014). Papers by ELK's authors are
  allowed, as is their pseudocode. Code listings or repositories that accompany a paper are not, when they are ELK's
  code.
- **ELK's public option reference.** The reference pages of the ELK website, which name the algorithms and describe
  each layout option, its values and its default. Not the developer pages that show or link to ELK's source.
- **The committed `.elk.txt` recordings.** The oracle's record of the graph and options mermaid.js gives ELK and the
  graph ELK returns, for each corpus case mermaid.js lays out with ELK (ipjohnson/SplashTheMusical#200).
- **The output of running ELK as a black box.** What the oracle or a probe writes when it runs elkjs on a graph, such as
  the stage-by-stage probe of ipjohnson/SplashTheMusical#204. The probe runs elkjs and reads its result. It does not
  open, copy or patch elkjs's files.
- **mermaid's own MIT code, including its ELK glue.** The code under
  `packages/mermaid/src/rendering-util/layout-algorithms/elk` of mermaid-js/mermaid at the pinned tag, which builds the
  graph ELK is given and draws what ELK returns, and the rest of mermaid's source.
- **Mermaider's `Sugiyama` project.** `src/Sugiyama` of ipjohnson/mermaider, which is MIT. Its notices name dagre as
  the only project it derives from.
- Everything else STM already ports from: dagre-d3-es, d3-shape and the other MIT projects in
  [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).

## What a session may not read or paste into a prompt

- **ELK's Java source.** Any file of the eclipse/elk repository or a fork of it that is source code, its tests and its
  generated code, wherever it appears: GitHub, a Maven or source jar, a code search result, a blog post, an issue or an
  answer that quotes it.
- **Any file under `node_modules/elkjs`.** elkjs is GWT output of ELK's Java. `tools/oracle` runs it and never opens it.
  Node prints the line of code that throws an uncaught exception, which for elkjs is a line of its build, so a script
  that runs elkjs catches its exceptions and prints only their message, as `tools/oracle/elk-probe.mjs` does.
- **Bundled builds that contain elkjs.** mermaid depends on elkjs, and its bundled builds under `node_modules/mermaid/dist`
  can inline it. Read mermaid's code in its TypeScript source at the pinned tag instead.

A session also does not ask a model, a subagent or a search to recall, reproduce or describe ELK's source: its classes,
methods or code paths. Ask about the papers, the option reference or the recordings instead. A question to a model about
ELK names the behaviour, such as "which layer does a node with `layering.layerChoiceConstraint` set go to", and is
answered from the recordings or a probe.

A session that sees forbidden material by accident stops work on the part of the engine the material is about, says so
in its task notes and pull request, and leaves that part to another session.

## What a pull request that changes the engine records

A pull request that adds or changes an algorithm of the engine, or the design it follows, records where each algorithm
came from. Its description has a **Sources** section that gives, for each algorithm or stage it touches:

- the papers it follows, with the authors, the title, the year and the section or algorithm number;
- the option reference pages it relied on;
- the `.elk.txt` recordings and probe outputs it was designed against or tuned on, by corpus case or probe name;
- the mermaid or Mermaider code it adapts, by path;
- a statement that no forbidden source was read or pasted into a prompt.

The same pull request adds or updates the stage's row of the provenance table below, so a later session finds the
sources without reading old pull requests.

## Provenance

| Stage | Source file | Papers | Recordings and probes | Other sources | Pull request |
|---|---|---|---|---|---|
| Design of every stage | [design.md](design.md), spikes in `tests/SplashTheMusical.Conformance/ElkStageSpikes.cs` | Those of each stage below | All 2,304 recordings of mermaid 12.3.0; the probe files in `tests/SplashTheMusical.Conformance/Fixtures/elk-probes`; the pinned `--check` run on every recording | The option reference pages listed under "Sources" in design.md; mermaid 12.3.0's `rendering-util/layout-algorithms/elk/render.ts`, `algorithms.ts`, `rendering-util/render.ts` and `rendering-elements/edges.js` | #231 |
| S0 Components | not written | none | All recordings; `components.txt` | Option reference: `separateConnectedComponents` | #231, design only |
| S1 Cycle breaking | not written | Tarjan 1972; Gansner, Koutsofios, North and Vo 1993, section 2.1 | All recordings | Option reference: `layered.cycleBreaking.strategy` | #231, design only |
| S2 Layering | not written | Gansner, Koutsofios, North and Vo 1993, section 2 | All recordings; `label-layer.txt` | The dagre port's network simplex, from dagre-d3-es; option reference: `layered.layering.strategy` | #231, design only |
| S3 Ordering | not written | Sugiyama, Tagawa and Toda 1981; Barth, Jünger and Mutzel 2004; Forster 2002 | All recordings; `order-tie.txt` | Option reference: `layered.considerModelOrder.strategy`, `crossingMinimization.forceNodeModelOrder` | #231, design only |
| S4 Ports | not written | Schulze, Spönemann and von Hanxleden 2014 | All recordings; `ports.txt`, `order-tie.txt`, `label-layer.txt` | Option reference: `spacing.portsSurrounding` | #231, design only |
| S5 Node placement | not written | Brandes and Köpf 2001; Brandes, Walter and Zink 2020 | All recordings; `layer-alignment.txt` | The dagre port's Brandes–Köpf, from dagre-d3-es; option reference: `layered.nodePlacement.*`, `spacing.nodeNode`, `layered.spacing.nodeNodeBetweenLayers` | #231, design only |
| S6 Edge routing | not written | Sander 2003, "Layout of Directed Hypergraphs with Orthogonal Hyperedges" | All recordings; `order-tie.txt` | Option reference: `edgeRouting`, `layered.unnecessaryBendpoints` | #231, design only |
| S7 Self loops | not written | Schulze, Spönemann and von Hanxleden 2014 | All recordings; `self-loop.txt` | Option reference: `layered.edgeRouting.selfLoopDistribution` | #231, design only |
| S8 Labels | not written | Gansner, Koutsofios, North and Vo 1993 | All recordings; `label-layer.txt` | Option reference: `edgeLabels.placement`, `edgeLabels.inline` | #231, design only |
| S9 Nested graphs | not written | Sander 1996, "Layout of Compound Directed Graphs"; Forster 2002 | All recordings | Option reference: `hierarchyHandling`, `padding`, `nodeSize.constraints`, `nodeSize.minimum`, `layered.mergeHierarchyEdges` | #231, design only |
