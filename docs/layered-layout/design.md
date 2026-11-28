# Design of the layered layout engine

This is the design of the layered layout library of ipjohnson/SplashTheMusical#25, from issue #231. The library
reproduces the layouts mermaid.js 12.3.0 gets from ELK. It is written under [the clean-room rules](clean-room.md), and
no part of this design comes from ELK's source. The owner approves it in the review of its pull request, and one issue
per row of [the rollout table](#rollout) is filed under #25 after that.

## Decision summary

- **The engine is a pipeline of ten stages**, each a published algorithm with rules read from the recordings and the
  probe. Every stage has a measure that scores it alone, with the stages before it held still at ELK's recorded result.
- **The spikes already reach most of the way on the early stages.** On the cases each measure applies to, depth-first
  cycle breaking reaches 95.7%. Network simplex layering, with a layer for each centre label, reaches 89.4% (99.4% of
  flat graphs). Barycenter ordering reaches 89.5%. The port rule reaches 81.4%, orthogonal routing 82.9% and self
  loops 83.7%. The late stages are further off. Brandes–Köpf placement reaches 47.3%, label placement 66.4% and
  cluster boxes 69.8%. The [stage table](#stages) gives each share and the share below which the approach is replaced.
- **Nested graphs are where the work is.** Nested cases are 487 of 2,304 (21.1%). Layering reaches 99.4% on flat graphs
  and 58.5% on nested ones, and ordering 94.4% against 76.8%. Two rollout steps go to them.
- **The scope leaves out 4 cases of the five types' and agentflow's recordings**: the 3 flowchart cases and the 1
  agentflow case that ask for `elk.box` or `elk.rectpacking`. It also leaves out `elk.portAlignment.default`, which
  only usecase sets and which changes 32 of its layouts. No recorded case uses ports or hyperedges.
- **The finished engine is held to L3 on 80% and L4 on 60%** of the 1,998 flowchart, state, class, ER and requirement
  cases that mermaid.js draws with ELK at default config. Today they pass at 26.7% (533) and 13.9% (278). The cases
  mermaid.js draws with `layout: dagre`, where STM already has an exact port, pass L4 at 82.6% (109 of 132). That is
  the ceiling the rest of the renderer allows.
- **A module picks the engine from `layout` as mermaid.js does.** `elk` goes to the engine and `dagre` to the dagre
  port. Any other name goes to the dagre port with a warning. mermaid.js's own fallback for a layout it cannot load is
  also dagre.
- **Routes are drawn as mermaid's ELK glue draws them**, with its `rounded` curve, which `RoundedPath` already ports.

## Scope

The survey, [corpus-survey.md](corpus-survey.md), counts what the 2,304 recordings ask of ELK. Every root sets the same
block of layered options. The options that vary are the direction, the container options of a subgraph, a node's
layer constraint and an edge label's placement. The library takes only the options that vary as inputs. The rest are
fixed behaviour.

### Options in scope

| Option | Values in the corpus | Cases | In the library |
|---|---|---:|---|
| `elk.algorithm` | `elk.layered` at the root and on 367 containers | 2,302 | The only algorithm |
| `elk.direction` | `DOWN`, `RIGHT`, `LEFT`, `UP` at the root and on containers | 2,304 | `LayeredDirection` per graph |
| `elk.hierarchyHandling` | `INCLUDE_CHILDREN` at the root, `INCLUDE_CHILDREN` or `SEPARATE_CHILDREN` on containers | 2,304 | `LayeredHierarchy` per container |
| `elk.layered.considerModelOrder.strategy` | `NODES_AND_EDGES` | 2,304 | Fixed: input order breaks ties |
| `elk.layered.crossingMinimization.forceNodeModelOrder` | `false` | 2,304 | Fixed |
| `elk.layered.cycleBreaking.strategy` | `DEPTH_FIRST` | 2,304 | Fixed |
| `elk.layered.layering.strategy` | `NETWORK_SIMPLEX` | 2,304 | Fixed |
| `elk.layered.nodePlacement.strategy`, `bk.fixedAlignment` | `BRANDES_KOEPF`, `BALANCED` | 2,304 | Fixed |
| `elk.layered.edgeRouting.selfLoopDistribution` | `EQUALLY` | 2,304 | Fixed |
| `elk.layered.mergeEdges`, `mergeHierarchyEdges` | `false`, `true` | 2,304 | Fixed |
| `elk.layered.unnecessaryBendpoints` | `true` | 2,304 | Fixed. The comparer drops points on a straight run, so it changes no measure |
| `elk.spacing.portsSurrounding` | 12 on every side | 2,304 | `PortsSurrounding` per graph |
| `spacing.baseValue` | 40 at the root, 24 on containers | 2,303 | `SpacingBase` per graph |
| `spacing.nodeNode`, `elk.spacing.edgeEdge`, `elk.layered.spacing.edgeNodeBetweenLayers` | 50, 20, 30 on containers | 486 | Optional overrides per graph |
| `elk.padding` | 24 on containers, and two other values | 487 | `Padding` per container |
| `nodeSize.constraints`, `nodeSize.minimum` | `[MINIMUM_SIZE, NODE_LABELS]` and the title's width | 461 | `MinimumSize` per container |
| `nodeLabels.placement` | `[H_CENTER V_TOP, INSIDE]` | 487 | `Header` per container: the label box at the top |
| `elk.layered.layering.layerConstraint` | `FIRST` on one node | 1 | `FirstLayer` per node |
| `edgeLabels.placement`, `edgeLabels.inline` | `CENTER` inline, `HEAD`, `TAIL` | 1,665 | `LabelPlacement` per edge label |
| `elk.layered.layering.coffmanGraham.layerBound`, `wrapping.multiEdge.*`, `nodePlacement.networkSimplex.nodeFlexibility` | one value each | 2,304 | Ignored. The option reference ties each to a strategy the corpus does not use, and wrapping is off by default |

The option reference pages that back the "Fixed" and "Ignored" rows are listed under [Sources](#sources).

### Left out

| Option or feature | Cases it affects | Why |
|---|---:|---|
| `elk.box` at the root or on a container | 2 flowchart | Not a layered layout. ELK returns no edge sections, and mermaid draws straight lines |
| `elk.rectpacking` at the root or on a container, with its `aspectRatio`, `contentAlignment`, `expandNodes`, padding 15 and rectpacking options | 1 flowchart, 1 agentflow | Not a layered layout |
| `elk.portAlignment.default` `CENTER`, on usecase nodes | 32 usecase | None of the five types sets it. With the option removed, the probe's layout differs from the recording in 32 of the 94 usecase cases that set it. It is built when usecase moves to the engine |
| Ports | 0 | No case gives a port. The engine makes the implicit ports ELK makes, as the [ports stage](#s4-ports) describes |
| Hyperedges, an edge with more than one source or target | 0 | None recorded |
| Edges held by a container | 0 | None recorded. Every edge is in the root's `edges` |
| `config.elk` values other than the default preset | 0 | Every recording has the default preset. A config that sets another value falls back to the dagre port with a warning |

The 4 cases of the two other algorithms are 0.2% of the recordings, and the port alignment affects 32 more, all
usecase. Every other graph feature of the survey is in scope: nested graphs
(487 cases), nesting depth 3 or more (139), self loops (38), edge labels (826), parallel edges (38), reverse pairs
(70), cycles (90), disconnected components (378), hierarchy-crossing edges (210) and edges that end on a container
(153).

## The library

The engine is `src/SplashTheMusical.Layout/Layered`, namespace `SplashTheMusical.Layout.Layered`. Like the dagre port,
its types are internal and the core reaches them through `InternalsVisibleTo`. It does not reference the core and
knows nothing of Mermaid. It takes no option strings. The core's ELK glue reads mermaid's option strings and builds the
typed input.

### Input

```csharp
internal sealed record LayeredInput(LayeredNode Root, IReadOnlyList<LayeredEdge> Edges);

internal sealed record LayeredNode(
	string Id,
	double Width,
	double Height,
	IReadOnlyList<LayeredNode> Children,
	LayeredGraphOptions? Graph = null, // set on the root and on every node with children
	bool FirstLayer = false);

internal sealed record LayeredGraphOptions(
	LayeredDirection Direction,
	LayeredHierarchy Hierarchy, // IncludeChildren or SeparateChildren
	double SpacingBase,
	double? NodeNode = null,
	double? EdgeEdge = null,
	double? EdgeNodeBetweenLayers = null,
	LayeredPadding? Padding = null, // 12 on every side when null, as the option reference gives
	double PortsSurrounding = 0,
	(double Width, double Height)? MinimumSize = null,
	(double Width, double Height)? Header = null);

internal sealed record LayeredEdge(string Id, string Source, string Target, IReadOnlyList<LayeredLabel> Labels);

internal sealed record LayeredLabel(double Width, double Height, LabelPlacement Placement, bool Inline);
```

The input order of children and edges is the model order that breaks ties in every stage. The core's glue keeps
mermaid's order, including the reordering of `orderEdgesByNesting` and the reversed edges of
`reverseSubgraphFeedbackEdges`, because those happen before ELK is called and are in the recordings.

### Output

The output has ELK's shape, so that the port of mermaid's `applyElkLayoutResult` maps it one to one.

```csharp
internal sealed record LayeredLayout(
	IReadOnlyDictionary<string, LayeredBox> Nodes,   // x and y relative to the parent, as ELK gives them
	IReadOnlyDictionary<string, LayeredRoute> Edges);

internal sealed record LayeredRoute(
	string Container,                                // the node whose coordinates the points are in
	IReadOnlyList<LayeredPoint> Points,              // start, bends, end
	IReadOnlyList<LayeredBox> Labels);               // in the container's coordinates
```

`LayeredEngine.Run(LayeredInput)` returns a `LayeredLayout` or throws `LayeredException` on a graph it refuses. A
refused graph is one with an edge to a node that is not in the graph, or with a cycle of containment. It is
deterministic, holds no state and allocates only per call. It is held to the dagre port's limit:
`ResourceLimits.Default.RenderDeadline` for 5,000 nodes.

### Stage interfaces

Each stage is a static class with one entry point that reads and writes a `LayeredGraph`, the engine's working graph of
nodes, dummy nodes, layers and orders. A stage's test can build the graph a stage expects from a recording, which is how
the stage measures hold the earlier stages still.

## How a stage is measured

`ElkLayoutComparer` reads ELK's layers and order from the recorded coordinates. A stage measure gives the stage the
graph the earlier stages would give it, built from ELK's recorded result. The layering stage gets ELK's orientation of
each edge. The ordering stage gets ELK's layers. Placement gets ELK's layers and order. Routing gets ELK's node boxes.
Labels get ELK's routes, and cluster boxes get ELK's child boxes. The stage's output is then scored on one measure, on
the cases it applies to.

The probe's pins hold ELK's own stages still only in part. Run with `--keep-layers --keep-order --place-from-result
--check` on all 2,304 recordings, ELK gives back its recorded result on 1,704 (74.0%). Of the 600 it does not, 325 have
a sized edge label, against 501 of the 1,704. In the two cases looked at, the pinned layout is longer along the layout
direction: by 40 in `state/mermaider-spec-state-simple-lifecycle`, and by 80 and wider in `class/docs-classDiagram-014`. So
the measures take the earlier stages from the recordings, and the pins are kept for graphs the corpus lacks.
`.artifacts/elk-spikes/pinned-check.txt` holds the result of each case on the machine that ran it. The table below is
the run's count by type.

| Type | Recordings | Pinned run equals the recording |
|---|---:|---:|
| agentflow | 194 | 132 |
| class | 382 | 293 |
| er | 349 | 251 |
| flowchart | 804 | 587 |
| requirement | 179 | 155 |
| state | 284 | 185 |
| usecase | 112 | 101 |
| **Total** | 2,304 | 1,704 (74.0%) |

The first rollout step turns the spikes into `ElkStageComparer` and a committed `docs/layered-layout/stages.md`, which
a test keeps current, as `baseline.md` is kept.

## Stages

Each share is of the cases the measure applies to, counted as in `baseline.md`: a case passes when every ELK call it
records does. "Floor" is the share below which the approach is replaced by the named alternative. A stage whose
build stays under its floor after its rollout step's tuning is not shipped. The next step replaces its algorithm
instead. "Target" is the share the stage's rollout step must reach.

| # | Stage | Algorithm | Measure | Applies to | Spike | Floor | Target |
|---|---|---|---|---:|---:|---:|---:|
| S1 | Cycle breaking | Depth-first search, sources first, in model order | Same reversed edges | 93 | 95.7% | 90% | 98% |
| S2 | Layering | Network simplex, a centre label takes a layer | Same layers | 1,653 | 89.4% (flat 99.4%) | 85% (flat 97%) | 95% (flat 99%) |
| S3 | Ordering | Layer sweeps with barycenters, model order start and ties | Same order | 844 | 89.5% (flat 94.4%) | 85% | 95% |
| S4 | Ports | Ends spread (w − 20) / (k + 1) apart round a side's centre | Same route ends | 1,617 | 81.4% (flat 85.2%) | 80% | 95% |
| S5 | Node placement | Brandes–Köpf, balanced, with ELK's spacing | Boxes and canvas | 1,969 | 47.3% (flat 55.2%) | 40% (flat 50%) | 90% |
| S6 | Edge routing | Orthogonal routing between layers | Same bends | 1,653 | 82.9% | 80% | 95% |
| S7 | Self loops | A loop on the side before the layer, 2 bends, loops nested | Same self-loop bends | 43 | 83.7% (flat 100%) | 80% | 95% |
| S8 | Labels | Centre labels in their layer on the edge's run, end labels at the ports | Label boxes | 815 | 66.4% | 60% | 90% |
| S9 | Nested graphs | One layering of a hierarchy with contiguous clusters, a run per separate child | Cluster boxes | 487 | 69.8% | 65% | 90% |
| S0 | Components | Each component alone, packed across the direction in model order | Same component order | 378 | 95.8% | 90% | 98% |

The [appendix](#appendix-spike-shares-by-type) gives each spike's share by type.

### S1 Cycle breaking

- **Algorithm.** A depth-first search reverses the edges it meets that point to a node still on its stack. It starts
  from the nodes without incoming edges, in model order, then from the remaining nodes in model order, and follows each
  node's outgoing edges in model order. Starting from every node in model order reaches 93.5%, so sources first is the
  rule.
- **Sources.** Tarjan, "Depth-first search and linear graph algorithms", SIAM Journal on Computing 1(2), 1972, for the
  back edges of a depth-first search. Gansner et al. (1993), section 2.1, for breaking cycles by reversing the back
  edges of a depth-first search. The option reference for `elk.layered.cycleBreaking.strategy`.
- **Measure.** Same reversed edges. For each edge whose ends ELK put in different layers, the stage reverses it if and
  only if ELK put its target in an earlier layer. It applies to the 93 cases where ELK or the stage reverses an edge.
- **Spike.** 95.7% (89 of 93): 97.8% of flat cases and 93.8% of nested ones. It misses 2 flowchart cases, 1 state case
  and 1 agentflow case. The spike takes an edge to a container as an edge to the container's first leaf, which the
  step replaces with the container's border points of S9.
- **Floor and alternative.** 90%. Below it, the stage becomes the greedy heuristic of Eades, Lin and Smyth, "A fast and
  effective heuristic for the feedback arc set problem", Information Processing Letters 47, 1993, with model order
  ties.

### S2 Layering

- **Algorithm.** Network simplex on the acyclic graph, with the minimum length of each edge 1, or 2 when it has a sized
  centre label. The label's dummy node takes the middle layer. The probe shows the rule: in `label-layer.txt`, an
  unlabelled edge spans a gap of 40. A label 21 high adds a layer, and the gap becomes 40 + 21 + 40 = 101. That is also
  the second most common gap in the corpus (651 of 1,884 gaps in flat graphs laid out down). An empty 0 × 0 label adds
  no layer. `FirstLayer` pins a node to layer 0.
- **Sources.** Gansner, Koutsofios, North and Vo, "A Technique for Drawing Directed Graphs", IEEE Transactions on
  Software Engineering 19(3), 1993, section 2, the network simplex ranking. The dagre port's `NetworkSimplex`,
  `FeasibleTree` and `Rank`, from dagre-d3-es (MIT), which the spike ran. The option reference for
  `elk.layered.layering.strategy`.
- **Measure.** Same layers, with ELK's orientation of every edge given. It applies to the 1,653 cases with more than one
  layer.
- **Spike.** 89.4% (1,477 of 1,653) with the label rule, against 85.2% without it and 76.6% for longest path. The spike
  flattens clusters. It reaches 99.4% of flat graphs and 58.5% of nested ones. S9 adds the nested part.
- **Floor and alternative.** 85% overall and 97% flat. Below it, the stage becomes longest path with the promotion of
  Nikolov and Tarassov, "Graph layering by promotion of nodes", Discrete Applied Mathematics 154(5), 2006.

### S3 Ordering

- **Algorithm.** Long edges get a dummy node in each layer they cross. Each layer starts in model order. Alternate down
  and up sweeps sort each layer by the barycenter of its neighbours in the layer before. A node without neighbours
  there keeps its place, and a tie keeps the current order. The order with the fewest crossings wins, the earlier one
  on a tie. Crossings are counted by the bilayer method of Barth, Jünger and Mutzel. Clusters stay contiguous through
  the constraint graph of Forster's method, which S9 adds.
- **Rule from the probe.** `order-tie.txt` shows that two children of one node take the order of the edges that reach
  them, not of the nodes. Starting each layer in the order of each node's first incoming edge does worse on the corpus,
  at 79.7% against 89.5%. So the tie rule only applies between nodes whose barycenters are equal and is tuned in the
  step.
- **Sources.** Sugiyama, Tagawa and Toda, "Methods for Visual Understanding of Hierarchical System Structures", IEEE
  Transactions on Systems, Man, and Cybernetics 11(2), 1981, the barycenter method. Barth, Jünger and Mutzel,
  "Simple and Efficient Bilayer Cross Counting", Journal of Graph Algorithms and Applications 8(2), 2004. Forster,
  "Applying Crossing Reduction Strategies to Layered Compound Graphs", Graph Drawing 2002, LNCS 2528. The option
  reference for `elk.layered.considerModelOrder.strategy` and `crossingMinimization.forceNodeModelOrder`.
- **Measure.** Same order, with ELK's layers given. It applies to the 844 cases where a layer holds two leaves or more.
- **Spike.** 89.5% (755 of 844). Model order alone reaches 86.3%. Flat graphs reach 94.4%, nested ones 76.8%.
- **Floor and alternative.** 85%. Below it, the stage becomes the weighted median with transposition of Gansner et al.
  (1993), section 3.

### S4 Ports

- **Algorithm.** An edge leaves a node on the side that faces the next layer and enters on the side that faces the
  previous one. The k ends on one side of length w are spaced (w − 20) / (k + 1) apart, centred on the side, in the
  order of the other ends across the layout direction. The rule fits every layout of the probe's `ports.txt`, with
  widths 30, 60 and 200 and 2 and 3 ends, and the two-end layouts of `order-tie.txt` and `label-layer.txt`. All of them
  have `portsSurrounding` of 12. The step learns from the probe how the 20 follows from it.
- **Sources.** Schulze, Spönemann and von Hanxleden, "Drawing Layered Graphs with Port Constraints", Journal of Visual
  Languages and Computing 25(2), 2014, for ports with free positions on a side. The step names the section it follows. The option reference for
  `elk.spacing.portsSurrounding`. The probe file `ports.txt`.
- **Measure.** Same route ends: every route end on a leaf is within 0.5 of ELK's across the layout direction, with ELK's
  node boxes given. It applies to the 1,617 cases with an edge between leaves in different layers.
- **Spike.** 81.4% (1,317 of 1,617). State reaches 15.3%. Its start and end nodes are small circles, and the step
  probes them first to learn the rule their ends follow.
- **Floor and alternative.** 80%. Below it, the ends are ordered by the barycenter of the other ends' positions rather
  than their order.

### S5 Node placement

- **Algorithm.** Brandes–Köpf with the four alignments balanced, along the layers. Across them, the layers follow each
  other at the spacing read from the recordings: 40 between the layers of a flat graph and 40 between neighbours in a
  layer at a base value of 40, and 12 from the canvas's edge. Those are the most common values: 868 of 1,884 layer
  gaps, 986 of 1,416 neighbour gaps and 1,434 of 1,520 margins in flat graphs laid out down. A node shorter than its
  layer sits against the side of the layer that faces its edges. In `layer-alignment.txt`, a node with an edge to the next layer
  sits at the layer's far side, one with an edge from the previous layer at its near side, and one without edges in
  the middle.
- **Sources.** Brandes and Köpf, "Fast and Simple Horizontal Coordinate Assignment", Graph Drawing 2001, LNCS 2265, and
  Brandes, Walter and Zink, "Erratum: Fast and Simple Horizontal Coordinate Assignment", arXiv 2008.01252, 2020. The
  dagre port's `BrandesKopf`, from dagre-d3-es (MIT), which the spike ran. The option reference for
  `elk.layered.nodePlacement.strategy`, `bk.fixedAlignment`, `spacing.nodeNode` and
  `layered.spacing.nodeNodeBetweenLayers`.
- **Measure.** Boxes and canvas, with ELK's layers and order given.
- **Spike.** 47.3% (932 of 1,969), of the cases where the dagre port already gets ELK's layers and order. That holds
  the earlier stages still by choosing the cases, because the dagre port cannot take given layers. It ran with
  nodesep and ranksep 40 and margins 12. It reaches 55.2% of flat graphs and 0% of nested ones, because dagre's
  cluster borders differ from ELK's. ER reaches 16.1% and requirement 23.0%. They are the types with labels on most of
  their edges, 279 of 349 ER cases and 141 of 179 requirement cases, and the spike spaces labels as dagre does.
- **Floor and alternative.** 40% overall and 50% flat. The spike is near the floor because it is dagre's placement and
  not the planned one. The step's first build replaces it. Below the floor, the stage becomes network simplex
  placement, from Gansner et al. (1993), section 4.

### S6 Edge routing

- **Algorithm.** Orthogonal routing. An edge whose ends line up runs straight. Any other edge runs from its port to the
  middle of the gap between the layers, across, and on to the other port: two bends. The probe puts the jog at the
  middle of the gap: 20 into a gap of 40. A long edge runs through its dummy nodes' positions, and jogs where they do
  not line up. Edges that jog in the same gap get separate tracks, `edgeEdge` apart, in an order without crossings
  where one exists.
- **Sources.** Sander, "Layout of Directed Hypergraphs with Orthogonal Hyperedges", Graph Drawing 2003, LNCS 2912, for
  routing segments between layers and ordering their tracks. The option reference for `elk.edgeRouting`, whose layered default is `ORTHOGONAL` on the layered algorithm page.
- **Measure.** Same bends, with ELK's node boxes and route ends given. It applies to the 1,653 cases with more than one
  layer.
- **Spike.** 82.9% (1,370 of 1,653), with the rule "0 bends when the ends line up, else 2". Of the 5,646 routes that
  are not self loops, 2,878 have 0 bends, 2,035 have 2, 715 have 4 and 18 have more. `e1` of `order-tie.txt` is one
  with 4: it jogs twice in one gap, on two tracks.
- **Floor and alternative.** 80%. Below it, the stage routes long edges as one straight run through their dummies'
  positions and adds the tracks of Sander's method only where two segments overlap.

### S7 Self loops

- **Algorithm.** A self loop leaves and enters the node on the side that comes before its layer across the layout
  direction, which is the left in a graph laid out down. It has 2 bends. Several loops on one node nest, each 20
  further out, with their ends spread on the side. `self-loop.txt` shows both.
- **Sources.** Schulze, Spönemann and von Hanxleden (2014), for edges between ports of one node. The option reference
  for `elk.layered.edgeRouting.selfLoopDistribution`. The probe file `self-loop.txt`.
- **Measure.** Same bends on every self loop, with ELK's node boxes given. It applies to the 43 cases with a routed self
  loop.
- **Spike.** 83.7% (36 of 43). Every flat case passes, and 46.2% of nested ones. 49 of 57 loops have 2 bends, 6 have
  none and 2 have 4.
- **Floor and alternative.** 80%. Below it, the loops of a nested graph are routed after its container's border is
  fixed, on the side the probe shows for each case that misses.

### S8 Labels

- **Algorithm.** A centre label is the dummy node of its layer, and the edge runs through it, as `edgeLabels.inline`
  says. Its box is centred on the edge's run in that layer. In `label-layer.txt` the label of `e1` is centred on the
  run at x 137. A head or a tail label is placed at its end of the route, at the place the step learns from the probe
  on the 36 and 34 class cases that have them. mermaid's glue then moves the end labels, as
  [the glue](#how-elk-routes-are-drawn) describes.
- **Sources.** Gansner et al. (1993), which gives an edge label a virtual node in the middle of its edge. The step names
  the section it follows. The option
  reference for `edgeLabels.placement` and `edgeLabels.inline`. The probe file `label-layer.txt`.
- **Measure.** Label boxes: every centre label's box overlaps ELK's by 80%, with ELK's routes given. It applies to the
  815 cases with a sized centre label.
- **Spike.** 66.4% (541 of 815). The spike centres the label on the middle segment of ELK's route. It does not know the
  label's layer, so it misses when the run through the label's layer is not the middle segment.
- **Floor and alternative.** 60%. Below it, the label is placed on the longest segment of its route in the label's
  layer.

### S9 Nested graphs

- **Algorithm.** A container with `IncludeChildren` is laid out in one run with everything inside it. Its leaves share
  the layering of the whole run. Each cluster's nodes stay contiguous in each layer, and its edges are kept inside its
  border. The border is the children's box grown by the padding and the header, and grown to `MinimumSize`. A container
  with `SeparateChildren` is laid out first on its own, with its own direction, and then placed as one node of its
  parent's run. An edge that crosses a border enters the container through a point on its border. As the option
  reference says of `mergeHierarchyEdges`, the edges that cross a border use as few such points as possible.
- **Sources.** Sander, "Layout of Compound Directed Graphs", technical report A/03/96, Universität des Saarlandes,
  1996. Forster (2002), for ordering with clusters. The option reference for `elk.hierarchyHandling`, `elk.padding`,
  `nodeSize.constraints` and `nodeSize.minimum`.
- **Measure.** Cluster boxes: every cluster's box overlaps ELK's by 80%, with its children's boxes given. It applies to
  the 487 cases with containers. The nested shares of S2 and S3, 58.5% and 76.8%, are this stage's other measures.
- **Spike.** 69.8% (340 of 487), with the box of the children grown by the container's `elk.padding` and to its
  `nodeSize.minimum` width. ER reaches 100%, class 93.0%, flowchart 50.9% and state 59.0%. Flowchart and state are
  also the types with the most hierarchy-crossing edges and edges that end on a container: 86 and 43 flowchart cases,
  40 and 82 state cases.
- **Floor and alternative.** 65%. Below it, every container is laid out on its own first, as `SeparateChildren` is,
  and its border points are fixed before its parent's run.

### S0 Components

- **Algorithm.** Each connected component is laid out on its own, and the components are placed side by side across
  the layout direction, in the model order of their first nodes, `nodeNode` apart. `components.txt` shows two
  components 40 apart, aligned at the top. The option reference gives `separateConnectedComponents` as true by default
  for the layered algorithm.
- **Sources.** The option reference for `elk.separateConnectedComponents`. The probe file `components.txt`.
- **Measure.** Same component order: the root's components follow each other across the layout direction in ELK's
  order. It applies to the 378 disconnected cases.
- **Spike.** 95.8% (362 of 378). Class misses 9 of its 88 cases, the most of any type.
- **Floor and alternative.** 90%. Below it, the components are packed in rows by size.

### The whole engine

The engine's assembled measure is the comparer's "All four", with no stage held still. The dagre port reaches 15.8% of
the recordings on it and Mermaider's `Sugiyama` reaches 12.1%, from [baseline.md](baseline.md). The last engine step
must reach 85%.

## How a module chooses the dagre port or the engine

mermaid.js's flowchart renderer, `flowRenderer-v3-unified.ts`, sets `layoutAlgorithm` from
`getRegisteredLayoutAlgorithm(config.layout)` in `rendering-util/render.ts`. The registered names are `dagre`,
`swimlane`, `cose-bilkent`, `elk`, and `elk.stress`, `elk.force`, `elk.mrtree`, `elk.sporeOverlap`, `elk.box` and
`elk.rectpacking`. A name that is not registered falls back to `dagre` with a warning. `elk` is ELK layered.

STM's flowchart, state, class, ER and requirement modules read the resolved `layout` once in their layout step, through
one core helper, `GraphLayoutChoice.For(config)`:

| `layout` | Engine | Diagnostics |
|---|---|---|
| `elk`, the default | The layered engine, through the ELK glue | none |
| `dagre` | The dagre port, through `FlowchartDagre` as today | none |
| any other ELK name, `swimlane` or `cose-bilkent` | The dagre port | A warning that STM lays out the name with dagre |
| a name mermaid does not register | The dagre port | The warning mermaid.js logs |
| `elk` with a `config.elk` value outside the [scope](#scope) | The dagre port | A warning that names the value |

Each module gets a second layout class next to its `*Layout`: `FlowchartElk` holds the glue, as `FlowchartDagre` holds
mermaid's dagre glue, and state, class, ER and requirement call it as they call `FlowchartDagre` today. `ILayoutEngine`
stays as it is. Its `LayoutGraph` has no clusters, labels or sections, so the engine does not implement it. An
[open question](#open-questions) asks whether it should.

## How ELK routes are drawn

STM ports mermaid's ELK glue, `packages/mermaid/src/rendering-util/layout-algorithms/elk/render.ts` at `mermaid@12.3.0`,
as `FlowchartElk`. The glue is mermaid's MIT code, and the port follows it function by function.

- **Input.** `buildElkGraphFromLayoutData`: `createRootElkGraph` with `resolveElkPreset`, `addSubGraphs`,
  `addVertices`, `addEdgesToElkGraph` with `terminalElkLabels`, `configureSubgraphNodes`,
  `configureCrossHierarchyEdges`, `setIncludeChildrenPolicy`, `reverseSubgraphFeedbackEdges` from
  `subgraphFeedbackEdges.ts`, `orderEdgesByNesting` from `nestedEdgeOrder.ts`, and `applyCyclicEntryConstraint`. Group
  labels are measured unwrapped, as `measureLayout` asks. A test compares the input STM builds with the input lines of
  every recording, as `RequirementElkInputTests` compares sizes today.
- **Nodes.** `applyElkLayoutResult` and `applyElkNodePositions`, with `evenGroupFrames`, and `orderNodesForElkPaint`.
- **Edges.** `applyElkEdgeLayout`. A route is the first section's start, bends and end, offset to the root by
  `calcOffset`, and reversed back for an edge `reverseSubgraphFeedbackEdges` turned. `alignDegenerateNodeToAnchor` moves
  a small node onto its line. The node centres are added at both ends unless the shape is `rect33`, then
  `sanitizeElkEdgePoints` clips them at the node borders, because the paint step skips the intersect. Then
  `ensureEndMarkerSegmentLength` and `ensureStartMarkerSegmentLength` keep room for the markers. The curve is
  `rounded`, which `insertEdge` draws with `generateRoundedPath`. That is STM's `RoundedPath` in
  `src/SplashTheMusical.Layout/Paths`. An edge without sections, from `elk.box` or `elk.rectpacking`, is a straight line
  between the node centres with a `linear` curve.
- **Labels.** The centre label goes where ELK put it. `straightenEdgeTerminals` removes a step of up to 16 at either
  end of a route that turns within 30 of the node, and `projectLabelOntoStraightenedRun` moves the label with the run. Then
  `followMovedEndpoints`, `slideTerminalLabelsOffFrames`, `putTerminalLabelsOnTheirSide` and
  `separateOppositeEdgeLabels` place the end labels.
- **After paint.** `applyElkLineJumps` from `lineHops.ts` draws the hops where `config.elk.lineHops` asks.

The renderers draw the result through `FlowchartRenderer` as they draw dagre's. Only the points and the curve differ.

## Rollout

Each row is one pull request, in order. A row's targets are measured as the stage measures above, on the cases they
apply to, and by type for flowchart (fc), state (st), class (cl), ER (er) and requirement (rq). Agentflow and usecase
are scored too, and switch when their modules exist. A row that misses its floor replaces its approach, as the stage
says.

| Step | Pull request | Target |
|---:|---|---|
| 1 | `ElkStageComparer` and `docs/layered-layout/stages.md`: the measures of this design as tests, with the spikes as their first entries, and a test that fails when the file is out of date | The file lists every measure for all 7 types. Its spike rows equal this design's shares |
| 2 | `FlowchartElk` input: mermaid's `buildElkGraphFromLayoutData` for the five types, compared with every recording's input | The input STM builds equals the recorded input lines for fc 95%, st 90%, cl 95%, er 95%, rq 95% of the recordings. Differences are listed under their issue, as `requirement-elk-inputs.txt` lists them |
| 3 | Engine skeleton: `LayeredInput`, `LayeredLayout`, `LayeredEngine.Run`, S0 components, and placement of graphs without edges | S0 at 98% overall. "All four" on 95% of the 639 cases without edges, fc, st, cl, er and rq each 90% |
| 4 | S1 cycle breaking and S2 layering on flat graphs | S1 98% overall. S2 on flat graphs: fc 99%, st 98%, cl 99%, er 98%, rq 99% |
| 5 | S3 ordering on flat graphs | Flat graphs: fc 95%, st 95%, cl 94%, er 90%, rq 90% |
| 6 | S5 node placement on flat graphs | Boxes and canvas on flat graphs with ELK's layers and order: fc 90%, st 80%, cl 90%, er 85%, rq 85% |
| 7 | S4 ports and S6 orthogonal routing on flat graphs | Flat graphs: S4 and S6 each fc 95%, st 85%, cl 95%, er 95%, rq 95% |
| 8 | S7 self loops and S8 labels | S7 on flat graphs 100%. S8 overall: fc 85%, st 85%, cl 85%, er 90%, rq 95% |
| 9 | S9 nested graphs with `IncludeChildren`: contiguous clusters in S2 and S3, cluster boxes, border points | Nested cases: S2 fc 85%, st 80%, cl 90%, er 90%. S3 fc 85%, st 85%. Cluster boxes fc 85%, st 80%, cl 95%, er 100% |
| 10 | S9 with `SeparateChildren` and container directions, and edges that end on a container | The 124 cases with a separate container: "All four" on 80%. Nested cases: S9 cluster boxes st 90%, fc 90% |
| 11 | The assembled engine: "All four" with no stage held still | fc 85%, st 75%, cl 90%, er 85%, rq 90%. Overall 85% |
| 12 | `FlowchartElk` output and drawing: the glue of [How ELK routes are drawn](#how-elk-routes-are-drawn), `GraphLayoutChoice`, flowchart on the engine | Flowchart's ELK cases: L3 85%, L4 75% |
| 13 | State, class, ER and requirement on the engine. Remove their `bug #25` ledger entries that pass | L3: st 75%, cl 85%, er 85%, rq 85%. L4: st 50%, cl 50%, er 70%, rq 50% |

When step 13 is done, the 1,998 cases of the five types that mermaid.js draws with ELK at default config pass L3 at
80% or more (1,599 cases) and L4 at 60% or more (1,199 cases). The per-type targets of steps 12 and 13 sum to 83.6% at
L3 and 63.6% at L4. Today these cases pass L3 at 26.7% (533) and L4 at 13.9% (278).

| Type | ELK cases | L3 today | L3 target | L4 today | L4 target | L4 of the `layout: dagre` cases today |
|---|---:|---:|---:|---:|---:|---:|
| flowchart | 804 | 19.3% (155) | 85% | 13.9% (112) | 75% | 97.1% (68 of 70) |
| state | 284 | 13.0% (37) | 75% | 3.5% (10) | 50% | 1 of 1 |
| class | 382 | 42.1% (161) | 85% | 10.2% (39) | 50% | 63.8% (37 of 58) |
| er | 349 | 42.4% (148) | 85% | 33.0% (115) | 70% | 2 of 2 |
| requirement | 179 | 17.9% (32) | 85% | 1.1% (2) | 50% | 1 of 1 |
| **Total** | 1,998 | 26.7% (533) | 80% | 13.9% (278) | 60% | 82.6% (109 of 132) |

The L4 targets of class, state and requirement are low because those renderers fail L4 for reasons other than layout.
Class's `layout: dagre` cases, whose layout is exact, pass L4 at 63.8%. The "today" columns count the ELK cases not in
the type's ledger for L3, and the `pass` lines of `corpus/l4` for L4.

## Where the spikes are

- **Code.** `tests/SplashTheMusical.Conformance/ElkStageSpikes.cs`, run by `ElkStageSpikeTests.RunsWhenAsked`, which
  does nothing unless `STM_ELK_SPIKES_DIR` is set. `mise run elk-spikes` runs it and writes
  `.artifacts/elk-spikes/report.md`, with each spike's share by type, flat and nested, and the histograms this design
  quotes. It reads only the committed recordings and runs in about 15 seconds. Nothing under `src` uses it.
- **Probe graphs.** `tests/SplashTheMusical.Conformance/Fixtures/elk-probes`: `label-layer.txt`, `order-tie.txt`,
  `components.txt`, `self-loop.txt`, `layer-alignment.txt` and `ports.txt`. `mise run elk-probe <file>` prints ELK's
  layout of each.
- **Pinned check.** `--keep-layers --keep-order --place-from-result --check` of `elk-probe.mjs` on every recording.
  `.artifacts/elk-spikes/pinned-check.txt` holds the result of each case. It is not committed, and the counts are in
  [How a stage is measured](#how-a-stage-is-measured).

## Sources

The papers each stage follows are under its heading. The option reference pages read are these, at
`https://eclipse.dev/elk/reference/`: the ELK Layered algorithm page `algorithms/org-eclipse-elk-layered.html`, and
the option pages for `layered.cycleBreaking.strategy`, `layered.layering.strategy`,
`layered.considerModelOrder.strategy`, `layered.crossingMinimization.strategy`,
`layered.crossingMinimization.forceNodeModelOrder`, `layered.nodePlacement.strategy`,
`layered.nodePlacement.bk.fixedAlignment`, `edgeRouting`, `layered.unnecessaryBendpoints`, `layered.spacing.baseValue`,
`spacing.nodeNode`, `layered.spacing.nodeNodeBetweenLayers`, `spacing.edgeEdge`,
`layered.spacing.edgeNodeBetweenLayers`, `spacing.portsSurrounding`, `separateConnectedComponents`,
`hierarchyHandling`, `edgeLabels.placement`, `edgeLabels.inline`, `layered.mergeEdges`, `layered.mergeHierarchyEdges`,
`layered.edgeRouting.selfLoopDistribution`, `layered.wrapping.strategy`, `padding`, `nodeSize.constraints` and
`nodeSize.minimum`. The pages give no description per value, so the rules of each stage come from the recordings and
the probe. mermaid's code is read at `mermaid@12.3.0`: `rendering-util/layout-algorithms/elk/render.ts`,
`algorithms.ts`, `rendering-util/render.ts`, `rendering-util/rendering-elements/edges.js` and
`diagrams/flowchart/flowRenderer-v3-unified.ts`. No ELK source and no file under `node_modules/elkjs` was read.

## Open questions

Each needs the owner's decision. The recommendation comes first.

1. **The L3 and L4 share of #25.** Recommended: L3 80% and L4 60% of the 1,998 ELK cases of the five types, with the
   per-type targets of steps 12 and 13. The other choice is to hold L4 to the share each type reaches with `layout:
   dagre`, which would make the target depend on renderer work outside #25.
2. **What `layout` names other than `elk` and `dagre` do.** Recommended: the dagre port with a warning, as mermaid's
   tiny build does for ELK. The other choice is to refuse the diagram, which mermaid.js does not do.
3. **Whether the engine implements `ILayoutEngine`.** Recommended: no, for now. `LayoutGraph` lacks clusters, labels and
   routes, so it would need a breaking change to the public API. A host engine can come later with a richer graph.
4. **Agentflow and usecase.** Their modules do not exist yet. Recommended: score the engine on their 306 recordings in
   every step, as this design does, and switch them to the engine when their modules land, outside #25.
5. **The probe's pins.** They reproduce 74.0% of the recordings. Recommended: leave them, because the stage measures
   take their held-still inputs from the recordings. The other choice is a step that finds why the pinned runs add a
   layer, so the pins hold for every recording.
6. **Whether a stage below its floor blocks the rollout.** Recommended: yes. The step that misses its floor replaces
   the stage's approach before the next step starts, because each later measure holds the earlier stages still and
   would hide the miss only on the corpus, not in STM's output.

## Appendix: spike shares by type

From `mise run elk-spikes` on the committed recordings of mermaid 12.3.0. Each cell is the share of the type's cases
the spike's measure applies to.

| Spike | flowchart | state | class | er | requirement | agentflow | usecase | all |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| S1 cycles: depth first, sources first | 89.5% (17 of 19) | 98.4% (62 of 63) | 100.0% (1 of 1) | 100.0% (7 of 7) | n/a | 66.7% (2 of 3) | n/a | 95.7% (89 of 93) |
| S1 cycles: depth first, model order | 84.2% (16 of 19) | 96.8% (61 of 63) | 100.0% (1 of 1) | 100.0% (7 of 7) | n/a | 66.7% (2 of 3) | n/a | 93.5% (87 of 93) |
| S2 layers: network simplex, a label takes a layer | 89.3% (533 of 597) | 65.1% (157 of 241) | 98.9% (181 of 183) | 97.5% (275 of 282) | 100.0% (141 of 141) | 91.5% (118 of 129) | 90.0% (72 of 80) | 89.4% (1477 of 1653) |
| S2 layers: network simplex | 87.6% (523 of 597) | 59.8% (144 of 241) | 83.1% (152 of 183) | 97.5% (275 of 282) | 100.0% (141 of 141) | 89.9% (116 of 129) | 72.5% (58 of 80) | 85.2% (1409 of 1653) |
| S2 layers: longest path | 81.9% (489 of 597) | 59.8% (144 of 241) | 66.7% (122 of 183) | 78.7% (222 of 282) | 95.0% (134 of 141) | 78.3% (101 of 129) | 67.5% (54 of 80) | 76.6% (1266 of 1653) |
| S3 order: barycenter sweeps | 85.1% (257 of 302) | 94.0% (94 of 100) | 95.2% (138 of 145) | 90.1% (118 of 131) | 90.0% (54 of 60) | 87.0% (40 of 46) | 90.0% (54 of 60) | 89.5% (755 of 844) |
| S3 order: model order | 85.1% (257 of 302) | 93.0% (93 of 100) | 93.1% (135 of 145) | 87.8% (115 of 131) | 76.7% (46 of 60) | 87.0% (40 of 46) | 70.0% (42 of 60) | 86.3% (728 of 844) |
| S3 order: barycenter sweeps, edge order first | 79.1% (239 of 302) | 55.0% (55 of 100) | 95.2% (138 of 145) | 77.1% (101 of 131) | 81.7% (49 of 60) | 84.8% (39 of 46) | 86.7% (52 of 60) | 79.7% (673 of 844) |
| S4 ports: (w − 20) / (k + 1) apart | 90.2% (522 of 579) | 15.3% (36 of 236) | 96.7% (177 of 183) | 96.0% (263 of 274) | 100.0% (141 of 141) | 100.0% (124 of 124) | 67.5% (54 of 80) | 81.4% (1317 of 1617) |
| S5 placement: dagre's Brandes–Köpf, ELK spacing | 59.8% (417 of 697) | 18.0% (32 of 178) | 69.4% (238 of 343) | 16.1% (53 of 330) | 23.0% (37 of 161) | 59.7% (105 of 176) | 59.5% (50 of 84) | 47.3% (932 of 1969) |
| S6 routing: Z routes between ELK's ends | 81.3% (486 of 598) | 65.4% (157 of 240) | 94.1% (174 of 185) | 87.2% (245 of 281) | 87.9% (124 of 141) | 89.8% (115 of 128) | 86.3% (69 of 80) | 82.9% (1370 of 1653) |
| S7 self loops: 2 bends | 80.0% (4 of 5) | 75.0% (6 of 8) | 100.0% (4 of 4) | 100.0% (19 of 19) | n/a | 33.3% (2 of 6) | 100.0% (1 of 1) | 83.7% (36 of 43) |
| S8 labels: centre of the middle segment | 54.4% (92 of 169) | 54.8% (46 of 84) | 59.0% (62 of 105) | 72.2% (200 of 277) | 87.2% (123 of 141) | 30.8% (4 of 13) | 53.8% (14 of 26) | 66.4% (541 of 815) |
| S9 nested: children's box and padding | 50.9% (82 of 161) | 59.0% (79 of 134) | 93.0% (40 of 43) | 100.0% (37 of 37) | n/a | 93.5% (72 of 77) | 85.7% (30 of 35) | 69.8% (340 of 487) |
| S0 components: model order across | 98.3% (117 of 119) | 100.0% (18 of 18) | 89.8% (79 of 88) | 95.0% (57 of 60) | 100.0% (42 of 42) | 96.4% (27 of 28) | 95.7% (22 of 23) | 95.8% (362 of 378) |
