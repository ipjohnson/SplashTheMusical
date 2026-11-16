# Upstream delta: mermaid 12.3.0 to 12.2.0

Changed: Grammar files, Config keys, Shapes, edge types and layout algorithms, Corpus cases, Conformance.

## Diagram types

No change.

## Grammar files

### `packages/mermaid/src/diagrams/flowchart/parser/flow.jison`, changed

No STM parser names it.

```diff
@@ -143,7 +143,7 @@ that id.
 .*direction\s+LR[^\n]*       return 'direction_lr';
 .*direction\s+TD[^\n]*       return 'direction_td';
 
-[^\s\"\[\]\(\)\{\}\|]+\@(?=[^\{\"])   { return 'LINK_ID'; }
+[^\s\"]+\@(?=[^\{\"])               { return 'LINK_ID'; }
 [0-9]+                       return 'NUM';
 \#                           return 'BRKT';
 ":::"                        return 'STYLE_SEPARATOR';
@@ -402,10 +402,6 @@ shapeData:
 
 vertexStatement: vertexStatement link node shapeData
         { /* console.warn('vs shapeData',$vertexStatement.stmt,$node, $shapeData);*/ yy.addVertex($node[$node.length-1],undefined,undefined,undefined, undefined,undefined, undefined,$shapeData); yy.addLink($vertexStatement.stmt,$node,$link); $$ = { stmt: $node, nodes: $node.concat($vertexStatement.nodes) } }
-    // Same action as the rule above: `spaceList` cannot be empty (spaceList: SPACE spaceList | SPACE),
-    // so making the trailing spaces optional in a single rule would introduce a grammar conflict.
-    | vertexStatement link node shapeData spaceList
-        { /* console.warn('vs shapeData',$vertexStatement.stmt,$node, $shapeData);*/ yy.addVertex($node[$node.length-1],undefined,undefined,undefined, undefined,undefined, undefined,$shapeData); yy.addLink($vertexStatement.stmt,$node,$link); $$ = { stmt: $node, nodes: $node.concat($vertexStatement.nodes) } }
     | vertexStatement link node
         { /*console.warn('vs',$vertexStatement.stmt,$node);*/ yy.addLink($vertexStatement.stmt,$node,$link); $$ = { stmt: $node, nodes: $node.concat($vertexStatement.nodes) } }
     |  vertexStatement link node spaceList
@@ -416,13 +412,6 @@ vertexStatement: vertexStatement link node shapeData
         yy.addVertex($node[$node.length-1],undefined,undefined,undefined, undefined,undefined, undefined,$shapeData);
         $$ = {stmt: $node, nodes:$node, shapeData: $shapeData}
     }
-    // Same action as `node shapeData`: `spaceList` cannot be empty (spaceList: SPACE spaceList | SPACE),
-    // so making the trailing spaces optional in a single rule would introduce a grammar conflict.
-    |node shapeData spaceList {
-        /*console.warn('vertexStatement: node shapeData', $node[0], $shapeData);*/
-        yy.addVertex($node[$node.length-1],undefined,undefined,undefined, undefined,undefined, undefined,$shapeData);
-        $$ = {stmt: $node, nodes:$node, shapeData: $shapeData}
-    }
     |node { /* console.warn('vertexStatement: single node', $node); */ $$ = {stmt: $node, nodes:$node }}
     ;
 
```


## Config keys

Removed, 6:

- `key c4.flowchart.stepDuration`: `1`
- `key c4.flowchart.stepNumbers`: `false`
- `key flowchart.stepDuration`: `1`
- `key flowchart.stepNumbers`: `false`
- `name stepDuration`
- `name stepNumbers`


## Theme variables

No change.

## Shapes, edge types and layout algorithms

Shape table:

Removed, 18:

- `key bpmn-activity`: `bpmnActivity`
- `key bpmn-annotation`: `bpmnAnnotation`
- `key bpmn-boundary`: `bpmnBoundary`
- `key bpmn-data`: `bpmnDataObject`
- `key bpmn-data-store`: `bpmnDataStore`
- `key bpmn-end`: `bpmnEnd`
- `key bpmn-gateway`: `bpmnGateway`
- `key bpmn-intermediate`: `bpmnIntermediate`
- `key bpmn-start`: `bpmnStart`
- `undocumented bpmn-activity`: `bpmnActivity`
- `undocumented bpmn-annotation`: `bpmnAnnotation`
- `undocumented bpmn-boundary`: `bpmnBoundary`
- `undocumented bpmn-data`: `bpmnDataObject`
- `undocumented bpmn-data-store`: `bpmnDataStore`
- `undocumented bpmn-end`: `bpmnEnd`
- `undocumented bpmn-gateway`: `bpmnGateway`
- `undocumented bpmn-intermediate`: `bpmnIntermediate`
- `undocumented bpmn-start`: `bpmnStart`


Edge markers removed, 2:

- `hollowCircle`
- `openArrow`

Layout folders removed, 1:

- `grid`


## Corpus cases

Cases added, 42:

- `flowchart/docs-examples-004`
- `flowchart/docs-examples-005`
- `git/docs-examples-009`
- `pie/docs-examples-001`
- `pie/docs-examples-002`
- `sequence/docs-examples-003`
- `sequence/docs-examples-006`
- `sequence/docs-examples-007`
- `sequence/docs-examples-008`
- `swimlanes/e2e-swimlanes-nightly-check-12-disagree-exits-b-c`
- `swimlanes/e2e-swimlanes-nightly-check-13-disagree-exits-b-c-d`
- `swimlanes/e2e-swimlanes-nightly-check-14-messy-layout`
- `swimlanes/e2e-swimlanes-nightly-check-15-border-hugging`
- `swimlanes/e2e-swimlanes-nightly-check-15-border-hugging-lr`
- `swimlanes/e2e-swimlanes-nightly-check-16-intake-cycle-lr`
- `swimlanes/e2e-swimlanes-nightly-check-8-query-process-2`
- `swimlanes/e2e-swimlanes-nightly-check-classdef-and-class-statements`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-handdrawn-look-redux-color-theme`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-handdrawn-look-redux-dark-color-theme`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-redux-color-theme`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-redux-dark-color-theme`
- `swimlanes/e2e-swimlanes-nightly-check-commant`
- `swimlanes/e2e-swimlanes-nightly-check-custom-theme-variables`
- `swimlanes/e2e-swimlanes-nightly-check-default-layout-two-lanes`
- `swimlanes/e2e-swimlanes-nightly-check-default-swimlane-for-unassigned-nodes`
- `swimlanes/e2e-swimlanes-nightly-check-explicit-lane-style-over-palette`
- `swimlanes/e2e-swimlanes-nightly-check-intake-review-complete`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-1`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-2`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-3`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-4`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-5`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-6`
- `swimlanes/e2e-swimlanes-nightly-check-mermaid-work`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-1-duplicate-process-in-substate-no-render`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-2-edge-intrudes-lane-label`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-3-process-too-wide-in-lane`
- `swimlanes/e2e-swimlanes-nightly-check-sales-process`
- `swimlanes/e2e-swimlanes-nightly-check-simple-2`
- `swimlanes/e2e-swimlanes-nightly-check-style-and-linkstyle-statements`
- `swimlanes/e2e-swimlanes-nightly-check-synthetic-default-lane-colour`
- `swimlanes/e2e-swimlanes-nightly-check-synthetic-default-lane-handdrawn`

Cases removed, 61:

- `agentflow/e2e-agentflow-pr-check-13-should-render-the-short-input-form`
- `agentflow/parser-agentflow-shape-catalogue-007`
- `flowchart/docs-flowchart-115`
- `flowchart/docs-flowchart-116`
- `flowchart/parser-flow-edges-008`
- `flowchart/parser-flow-edges-009`
- `flowchart/parser-flow-edges-010`
- `flowchart/parser-flow-edges-011`
- `flowchart/parser-flow-node-data-031`
- `flowchart/parser-flow-node-data-032`
- `flowchart/parser-flow-node-data-033`
- `flowchart/parser-flow-node-data-034`
- `flowchart/parser-flow-node-data-035`
- `flowchart/parser-flowdb-dropped-subgraph-members-001`
- `flowchart/parser-flowdb-dropped-subgraph-members-002`
- `flowchart/parser-flowdb-dropped-subgraph-members-003`
- `flowchart/parser-flowdb-dropped-subgraph-members-004`
- `flowchart/parser-flowdb-dropped-subgraph-members-005`
- `flowchart/parser-flowdb-dropped-subgraph-members-006`
- `flowchart/parser-flowdb-dropped-subgraph-members-007`
- `swimlanes/e2e-swimlanes-pr-check-12-disagree-exits-b-c`
- `swimlanes/e2e-swimlanes-pr-check-13-disagree-exits-b-c-d`
- `swimlanes/e2e-swimlanes-pr-check-14-messy-layout`
- `swimlanes/e2e-swimlanes-pr-check-15-border-hugging`
- `swimlanes/e2e-swimlanes-pr-check-15-border-hugging-lr`
- `swimlanes/e2e-swimlanes-pr-check-16-intake-cycle-lr`
- `swimlanes/e2e-swimlanes-pr-check-8-query-process-2`
- `swimlanes/e2e-swimlanes-pr-check-classdef-and-class-statements`
- `swimlanes/e2e-swimlanes-pr-check-coloured-lanes-handdrawn-look-redux-color-theme`
- `swimlanes/e2e-swimlanes-pr-check-coloured-lanes-handdrawn-look-redux-dark-color-theme`
- `swimlanes/e2e-swimlanes-pr-check-coloured-lanes-redux-color-theme`
- `swimlanes/e2e-swimlanes-pr-check-coloured-lanes-redux-dark-color-theme`
- `swimlanes/e2e-swimlanes-pr-check-commant`
- `swimlanes/e2e-swimlanes-pr-check-custom-theme-variables`
- `swimlanes/e2e-swimlanes-pr-check-default-layout-two-lanes`
- `swimlanes/e2e-swimlanes-pr-check-default-swimlane-for-unassigned-nodes`
- `swimlanes/e2e-swimlanes-pr-check-explicit-lane-style-over-palette`
- `swimlanes/e2e-swimlanes-pr-check-intake-review-complete`
- `swimlanes/e2e-swimlanes-pr-check-issue-7802-1`
- `swimlanes/e2e-swimlanes-pr-check-issue-7802-2`
- `swimlanes/e2e-swimlanes-pr-check-issue-7802-3`
- `swimlanes/e2e-swimlanes-pr-check-issue-7802-4`
- `swimlanes/e2e-swimlanes-pr-check-issue-7802-5`
- `swimlanes/e2e-swimlanes-pr-check-issue-7802-6`
- `swimlanes/e2e-swimlanes-pr-check-mermaid-work`
- `swimlanes/e2e-swimlanes-pr-check-pebr-1-duplicate-process-in-substate-no-render`
- `swimlanes/e2e-swimlanes-pr-check-pebr-2-edge-intrudes-lane-label`
- `swimlanes/e2e-swimlanes-pr-check-pebr-3-process-too-wide-in-lane`
- `swimlanes/e2e-swimlanes-pr-check-sales-process`
- `swimlanes/e2e-swimlanes-pr-check-shared-node-in-two-lanes`
- `swimlanes/e2e-swimlanes-pr-check-simple-2`
- `swimlanes/e2e-swimlanes-pr-check-style-and-linkstyle-statements`
- `swimlanes/e2e-swimlanes-pr-check-subgraph-in-lane-lr`
- `swimlanes/e2e-swimlanes-pr-check-subgraph-in-lane-tb`
- `swimlanes/e2e-swimlanes-pr-check-synthetic-default-lane-colour`
- `swimlanes/e2e-swimlanes-pr-check-synthetic-default-lane-handdrawn`
- `swimlanes/e2e-swimlanes-pr-check-synthetic-shared-node-nested-lane`
- `swimlanes/parser-swimlanesdiagram-003`
- `swimlanes/parser-swimlanesdiagram-004`
- `swimlanes/parser-swimlanesdiagram-005`
- `swimlanes/parser-swimlanesdiagram-006`

Cases whose oracle facts changed, 32:

- `flowchart/docs-flowchart-090`
  - `- edge API Cache none arrow solid`
  - `- edge API Client none arrow solid`
  - `- edge Cache DB none arrow solid miss`
  - `- edge Client API none arrow solid`
  - `- edge DB API none arrow solid`
  - `- node API API rect`
  - `- node Cache Cache rect`
  - `- node Client Client rect`
  - and 4 more
- `flowchart/docs-flowchart-091`
  - `- config flowchart.stepDuration 0.5`
  - `- edge API Client none arrow solid`
  - `- edge Client API none arrow solid`
  - `- node API API rect`
  - `- node Client Client rect`
  - `- preamble-config flowchart.stepDuration 0.5`
  - `+ edge A B none cross solid`
  - `+ node A A rect`
  - and 1 more
- `flowchart/docs-flowchart-092`
  - `- edge A B none circle solid`
  - `+ edge A B circle circle solid`
  - `+ edge B C arrow arrow solid`
  - `+ edge C D cross cross solid`
  - `+ node C C rect`
  - `+ node D D rect`
- `flowchart/docs-flowchart-093`
  - `- edge A B none cross solid`
  - `- node A A rect`
  - `- node B B rect`
  - `+ edge A B none arrow solid`
  - `+ edge B C none arrow solid Yes`
  - `+ edge B E none arrow solid No`
  - `+ edge C D none arrow solid`
  - `+ edge D B none arrow solid`
  - and 5 more
- `flowchart/docs-flowchart-094`
  - `- edge A B circle circle solid`
  - `- edge B C arrow arrow solid`
  - `- edge C D cross cross solid`
  - `- node A A rect`
  - `- node B B rect`
  - `- node C C rect`
  - `- node D D rect`
  - `+ edge A B none arrow solid`
  - and 9 more
- `flowchart/docs-flowchart-095`
  - `- edge A B none arrow solid`
  - `- edge B C none arrow solid Yes`
  - `- edge B E none arrow solid No`
  - `- edge C D none arrow solid`
  - `- edge D B none arrow solid`
  - `- node A Start rect`
  - `- node B Is it? diam`
  - `- node C OK rect`
  - and 3 more
- `flowchart/docs-flowchart-096`
  - `- edge B C none arrow solid Yes`
  - `- edge B E none arrow solid No`
  - `- edge C D none arrow solid`
  - `- edge D B none arrow solid`
  - `- node A Start rect`
  - `- node B Is it? diam`
  - `- node C OK rect`
  - `- node D Rethink rect`
  - and 3 more
- `flowchart/docs-flowchart-097`
  - `- node id1 This is the (text) in the box rect`
  - `+ edge a1 a2 none arrow solid`
  - `+ edge b1 b2 none arrow solid`
  - `+ edge c1 a2 none arrow solid`
  - `+ edge c1 c2 none arrow solid`
  - `+ group one one a1,a2`
  - `+ group three three c1,c2`
  - `+ group two two b1,b2`
  - and 6 more
- `flowchart/docs-flowchart-098`
  - `- edge A B none arrow solid`
  - `- node A A double quote:" rect`
  - `- node B A dec char:♥ rect`
  - `+ edge a1 a2 none arrow solid`
  - `+ edge c1 a2 none arrow solid`
  - `+ group ide1 one a1,a2`
  - `+ node a1 a1 rect`
  - `+ node a2 a2 rect`
  - and 1 more
- `flowchart/docs-flowchart-099`
  - `+ edge one two none arrow solid`
  - `+ edge three two none arrow solid`
  - `+ edge two c2 none arrow solid`
- `flowchart/docs-flowchart-100`
  - `- edge a1 a2 none arrow solid`
  - `- edge c1 a2 none arrow solid`
  - `- group ide1 one a1,a2`
  - `- node a1 a1 rect`
  - `- node a2 a2 rect`
  - `- node c1 c1 rect`
  - `+ edge A TOP none arrow solid`
  - `+ edge B1 B2 none arrow solid`
  - and 12 more
- `flowchart/docs-flowchart-101`
  - `- edge a1 a2 none arrow solid`
  - `- edge b1 b2 none arrow solid`
  - `- edge c1 a2 none arrow solid`
  - `- edge c1 c2 none arrow solid`
  - `- edge one two none arrow solid`
  - `- edge three two none arrow solid`
  - `- edge two c2 none arrow solid`
  - `- group one one a1,a2`
  - and 19 more
- `flowchart/docs-flowchart-102`
  - `- edge A TOP none arrow solid`
  - `- edge B1 B2 none arrow solid`
  - `- edge TOP B none arrow solid`
  - `- edge i1 f1 none arrow solid`
  - `- edge i2 f2 none arrow solid`
  - `- group B1 B1 f1,i1`
  - `- group B2 B2 f2,i2`
  - `- group TOP TOP B1,B2`
  - and 11 more
- `flowchart/docs-flowchart-103`
  - `- edge outside subgraph1 none arrow solid`
  - `- edge outside top2 none arrow solid`
  - `- edge top1 bottom1 none arrow solid`
  - `- edge top2 bottom2 none arrow solid`
  - `- group subgraph1 subgraph1 bottom1,top1`
  - `- group subgraph2 subgraph2 bottom2,top2`
  - `- node bottom1 bottom rect`
  - `- node bottom2 bottom rect`
  - and 13 more
- `flowchart/docs-flowchart-104`
  - `- edge Start one none arrow solid`
  - `- edge one End none arrow solid`
  - `- node End End rect`
  - `- node Start Start rect`
  - `- node one My Group collapsedGroup`
  - `+ edge A B none arrow solid`
  - `+ edge B C none arrow solid`
  - `+ edge C D none arrow solid`
  - and 4 more
- `flowchart/docs-flowchart-105`
  - `- config htmlLabels false`
  - `- edge a b none arrow solid edge label`
  - `- edge c d none arrow solid Bold edge label`
  - `- group **Two** Two c,d`
  - `- group One One a,b`
  - `- node a The catin the hat rounded`
  - `- node b The dog in the hog hex`
  - `- node c The catin the hat rounded`
  - and 11 more
- `flowchart/docs-flowchart-106`
  - `- edge A B none arrow solid`
  - `- edge B C none arrow solid`
  - `- edge C D none arrow solid`
  - `- node D D rect`
  - `+ edge A B none arrow solid text`
  - `+ edge B C none arrow solid text2`
- `flowchart/docs-flowchart-107`
  - `- edge A B none arrow solid`
  - `- edge B C none arrow solid`
  - `- edge C D none arrow solid`
  - `- edge D E none arrow solid`
  - `- node D D rect`
  - `- node E E rect`
  - `+ edge A B none arrow thick`
  - `+ edge A C none arrow solid`
- `flowchart/docs-flowchart-108`
  - `- edge A B none arrow solid text`
  - `- edge B C none arrow solid text2`
  - `- node A A rect`
  - `- node B B rect`
  - `- node C C rect`
  - `+ edge id1 id2 none arrow solid`
  - `+ node id1 Start rounded`
  - `+ node id2 Stop rounded`
- `flowchart/docs-flowchart-109`
  - `- edge A B none arrow thick`
  - `- edge A C none arrow solid`
  - `- node C C rect`
  - `+ edge A B none arrow solid`
- `flowchart/docs-flowchart-110`
  - `- edge id1 id2 none arrow solid`
  - `- node id1 Start rounded`
  - `- node id2 Stop rounded`
  - `+ edge A C none arrow solid`
  - `+ edge B C none arrow solid`
  - `+ node A A rect`
  - `+ node B B rect`
  - `+ node C C rect`
- `flowchart/docs-flowchart-112`
  - `- edge A C none arrow solid`
  - `- node A A rect`
  - `- node B B rect`
  - `- node C C rect`
  - `+ edge B D none arrow solid`
  - `+ edge B E none arrow solid`
  - `+ icon B fa:fa-twitter`
  - `+ icon C fa:fa-ban`
  - and 6 more
- `flowchart/docs-flowchart-113`
  - `- edge A B none arrow solid`
  - `- node A A rect`
  - `- node B B rect`
  - `+ edge B C none arrow solid`
  - `+ icon B fa:fa-twitter`
  - `+ icon C fab:fa-truck-bold`
  - `+ node B for peace rect`
  - `+ node C a custom icon rect`
- `flowchart/docs-flowchart-114`
  - `- edge B D none arrow solid`
  - `- edge B E none arrow solid`
  - `- icon B fa:fa-twitter`
  - `- icon C fa:fa-ban`
  - `- icon D fa:fa-spinner`
  - `- icon E fa:fa-camera-retro`
  - `- node B for peace rect`
  - `- node C forbidden rect`
  - and 10 more
- `flowchart/parser-flow-edges-007`
  - `- edge A B none arrow solid`
  - `- edge B C none arrow solid`
  - `- node C C rect`
  - `+ edge A B none none solid This is the 123 s text`
  - `+ edge A B none none solid This is the second edge`
- `info/e2e-info-pr-check-should-handle-an-info-definition`
  - `- label 12.3.0`
  - `+ label 12.2.0`
- `info/e2e-info-pr-check-should-handle-an-info-definition-with-showinfo`
  - `- label 12.3.0`
  - `+ label 12.2.0`
- `info/e2e-state-diagram-v2-nightly-check-v2-should-render-a-simple-info`
  - `- label 12.3.0`
  - `+ label 12.2.0`
- `info/parser-info-001`
  - `- label 12.3.0`
  - `+ label 12.2.0`
- `info/parser-info-002`
  - `- label 12.3.0`
  - `+ label 12.2.0`
- `info/parser-langium-info-001`
  - `- label 12.3.0`
  - `+ label 12.2.0`
- `info/parser-langium-info-002`
  - `- label 12.3.0`
  - `+ label 12.2.0`


## Conformance

Corpus levels that newly fail, 168:

- `flowchart/docs-examples-004 L0`
- `flowchart/docs-examples-004 L1`
- `flowchart/docs-examples-004 L2`
- `flowchart/docs-examples-004 L3`
- `flowchart/docs-examples-005 L0`
- `flowchart/docs-examples-005 L1`
- `flowchart/docs-examples-005 L2`
- `flowchart/docs-examples-005 L3`
- `git/docs-examples-009 L0`
- `git/docs-examples-009 L1`
- `git/docs-examples-009 L2`
- `git/docs-examples-009 L3`
- `pie/docs-examples-001 L0`
- `pie/docs-examples-001 L1`
- `pie/docs-examples-001 L2`
- `pie/docs-examples-001 L3`
- `pie/docs-examples-002 L0`
- `pie/docs-examples-002 L1`
- `pie/docs-examples-002 L2`
- `pie/docs-examples-002 L3`
- `sequence/docs-examples-003 L0`
- `sequence/docs-examples-003 L1`
- `sequence/docs-examples-003 L2`
- `sequence/docs-examples-003 L3`
- `sequence/docs-examples-006 L0`
- `sequence/docs-examples-006 L1`
- `sequence/docs-examples-006 L2`
- `sequence/docs-examples-006 L3`
- `sequence/docs-examples-007 L0`
- `sequence/docs-examples-007 L1`
- `sequence/docs-examples-007 L2`
- `sequence/docs-examples-007 L3`
- `sequence/docs-examples-008 L0`
- `sequence/docs-examples-008 L1`
- `sequence/docs-examples-008 L2`
- `sequence/docs-examples-008 L3`
- `swimlanes/e2e-swimlanes-nightly-check-12-disagree-exits-b-c L0`
- `swimlanes/e2e-swimlanes-nightly-check-12-disagree-exits-b-c L1`
- `swimlanes/e2e-swimlanes-nightly-check-12-disagree-exits-b-c L2`
- `swimlanes/e2e-swimlanes-nightly-check-12-disagree-exits-b-c L3`
- `swimlanes/e2e-swimlanes-nightly-check-13-disagree-exits-b-c-d L0`
- `swimlanes/e2e-swimlanes-nightly-check-13-disagree-exits-b-c-d L1`
- `swimlanes/e2e-swimlanes-nightly-check-13-disagree-exits-b-c-d L2`
- `swimlanes/e2e-swimlanes-nightly-check-13-disagree-exits-b-c-d L3`
- `swimlanes/e2e-swimlanes-nightly-check-14-messy-layout L0`
- `swimlanes/e2e-swimlanes-nightly-check-14-messy-layout L1`
- `swimlanes/e2e-swimlanes-nightly-check-14-messy-layout L2`
- `swimlanes/e2e-swimlanes-nightly-check-14-messy-layout L3`
- `swimlanes/e2e-swimlanes-nightly-check-15-border-hugging L0`
- `swimlanes/e2e-swimlanes-nightly-check-15-border-hugging L1`
- `swimlanes/e2e-swimlanes-nightly-check-15-border-hugging L2`
- `swimlanes/e2e-swimlanes-nightly-check-15-border-hugging L3`
- `swimlanes/e2e-swimlanes-nightly-check-15-border-hugging-lr L0`
- `swimlanes/e2e-swimlanes-nightly-check-15-border-hugging-lr L1`
- `swimlanes/e2e-swimlanes-nightly-check-15-border-hugging-lr L2`
- `swimlanes/e2e-swimlanes-nightly-check-15-border-hugging-lr L3`
- `swimlanes/e2e-swimlanes-nightly-check-16-intake-cycle-lr L0`
- `swimlanes/e2e-swimlanes-nightly-check-16-intake-cycle-lr L1`
- `swimlanes/e2e-swimlanes-nightly-check-16-intake-cycle-lr L2`
- `swimlanes/e2e-swimlanes-nightly-check-16-intake-cycle-lr L3`
- `swimlanes/e2e-swimlanes-nightly-check-8-query-process-2 L0`
- `swimlanes/e2e-swimlanes-nightly-check-8-query-process-2 L1`
- `swimlanes/e2e-swimlanes-nightly-check-8-query-process-2 L2`
- `swimlanes/e2e-swimlanes-nightly-check-8-query-process-2 L3`
- `swimlanes/e2e-swimlanes-nightly-check-classdef-and-class-statements L0`
- `swimlanes/e2e-swimlanes-nightly-check-classdef-and-class-statements L1`
- `swimlanes/e2e-swimlanes-nightly-check-classdef-and-class-statements L2`
- `swimlanes/e2e-swimlanes-nightly-check-classdef-and-class-statements L3`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-handdrawn-look-redux-color-theme L0`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-handdrawn-look-redux-color-theme L1`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-handdrawn-look-redux-color-theme L2`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-handdrawn-look-redux-color-theme L3`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-handdrawn-look-redux-dark-color-theme L0`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-handdrawn-look-redux-dark-color-theme L1`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-handdrawn-look-redux-dark-color-theme L2`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-handdrawn-look-redux-dark-color-theme L3`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-redux-color-theme L0`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-redux-color-theme L1`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-redux-color-theme L2`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-redux-color-theme L3`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-redux-dark-color-theme L0`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-redux-dark-color-theme L1`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-redux-dark-color-theme L2`
- `swimlanes/e2e-swimlanes-nightly-check-coloured-lanes-redux-dark-color-theme L3`
- `swimlanes/e2e-swimlanes-nightly-check-commant L0`
- `swimlanes/e2e-swimlanes-nightly-check-commant L1`
- `swimlanes/e2e-swimlanes-nightly-check-commant L2`
- `swimlanes/e2e-swimlanes-nightly-check-commant L3`
- `swimlanes/e2e-swimlanes-nightly-check-custom-theme-variables L0`
- `swimlanes/e2e-swimlanes-nightly-check-custom-theme-variables L1`
- `swimlanes/e2e-swimlanes-nightly-check-custom-theme-variables L2`
- `swimlanes/e2e-swimlanes-nightly-check-custom-theme-variables L3`
- `swimlanes/e2e-swimlanes-nightly-check-default-layout-two-lanes L0`
- `swimlanes/e2e-swimlanes-nightly-check-default-layout-two-lanes L1`
- `swimlanes/e2e-swimlanes-nightly-check-default-layout-two-lanes L2`
- `swimlanes/e2e-swimlanes-nightly-check-default-layout-two-lanes L3`
- `swimlanes/e2e-swimlanes-nightly-check-default-swimlane-for-unassigned-nodes L0`
- `swimlanes/e2e-swimlanes-nightly-check-default-swimlane-for-unassigned-nodes L1`
- `swimlanes/e2e-swimlanes-nightly-check-default-swimlane-for-unassigned-nodes L2`
- `swimlanes/e2e-swimlanes-nightly-check-default-swimlane-for-unassigned-nodes L3`
- `swimlanes/e2e-swimlanes-nightly-check-explicit-lane-style-over-palette L0`
- `swimlanes/e2e-swimlanes-nightly-check-explicit-lane-style-over-palette L1`
- `swimlanes/e2e-swimlanes-nightly-check-explicit-lane-style-over-palette L2`
- `swimlanes/e2e-swimlanes-nightly-check-explicit-lane-style-over-palette L3`
- `swimlanes/e2e-swimlanes-nightly-check-intake-review-complete L0`
- `swimlanes/e2e-swimlanes-nightly-check-intake-review-complete L1`
- `swimlanes/e2e-swimlanes-nightly-check-intake-review-complete L2`
- `swimlanes/e2e-swimlanes-nightly-check-intake-review-complete L3`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-1 L0`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-1 L1`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-1 L2`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-1 L3`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-2 L0`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-2 L1`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-2 L2`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-2 L3`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-3 L0`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-3 L1`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-3 L2`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-3 L3`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-4 L0`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-4 L1`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-4 L2`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-4 L3`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-5 L0`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-5 L1`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-5 L2`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-5 L3`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-6 L0`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-6 L1`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-6 L2`
- `swimlanes/e2e-swimlanes-nightly-check-issue-7802-6 L3`
- `swimlanes/e2e-swimlanes-nightly-check-mermaid-work L0`
- `swimlanes/e2e-swimlanes-nightly-check-mermaid-work L1`
- `swimlanes/e2e-swimlanes-nightly-check-mermaid-work L2`
- `swimlanes/e2e-swimlanes-nightly-check-mermaid-work L3`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-1-duplicate-process-in-substate-no-render L0`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-1-duplicate-process-in-substate-no-render L1`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-1-duplicate-process-in-substate-no-render L2`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-1-duplicate-process-in-substate-no-render L3`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-2-edge-intrudes-lane-label L0`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-2-edge-intrudes-lane-label L1`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-2-edge-intrudes-lane-label L2`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-2-edge-intrudes-lane-label L3`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-3-process-too-wide-in-lane L0`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-3-process-too-wide-in-lane L1`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-3-process-too-wide-in-lane L2`
- `swimlanes/e2e-swimlanes-nightly-check-pebr-3-process-too-wide-in-lane L3`
- `swimlanes/e2e-swimlanes-nightly-check-sales-process L0`
- `swimlanes/e2e-swimlanes-nightly-check-sales-process L1`
- `swimlanes/e2e-swimlanes-nightly-check-sales-process L2`
- `swimlanes/e2e-swimlanes-nightly-check-sales-process L3`
- `swimlanes/e2e-swimlanes-nightly-check-simple-2 L0`
- `swimlanes/e2e-swimlanes-nightly-check-simple-2 L1`
- `swimlanes/e2e-swimlanes-nightly-check-simple-2 L2`
- `swimlanes/e2e-swimlanes-nightly-check-simple-2 L3`
- `swimlanes/e2e-swimlanes-nightly-check-style-and-linkstyle-statements L0`
- `swimlanes/e2e-swimlanes-nightly-check-style-and-linkstyle-statements L1`
- `swimlanes/e2e-swimlanes-nightly-check-style-and-linkstyle-statements L2`
- `swimlanes/e2e-swimlanes-nightly-check-style-and-linkstyle-statements L3`
- `swimlanes/e2e-swimlanes-nightly-check-synthetic-default-lane-colour L0`
- `swimlanes/e2e-swimlanes-nightly-check-synthetic-default-lane-colour L1`
- `swimlanes/e2e-swimlanes-nightly-check-synthetic-default-lane-colour L2`
- `swimlanes/e2e-swimlanes-nightly-check-synthetic-default-lane-colour L3`
- `swimlanes/e2e-swimlanes-nightly-check-synthetic-default-lane-handdrawn L0`
- `swimlanes/e2e-swimlanes-nightly-check-synthetic-default-lane-handdrawn L1`
- `swimlanes/e2e-swimlanes-nightly-check-synthetic-default-lane-handdrawn L2`
- `swimlanes/e2e-swimlanes-nightly-check-synthetic-default-lane-handdrawn L3`

Tests that newly fail, 8:

- `EveryAcceptedCaseIsDetectedAsTheTypeTheOracleRecorded`
- `EveryCaseMergesToTheConfigTheOracleRecorded`
- `EveryCaseReadsTheTitleAndConfigTheOracleRecorded`
- `EveryCaseResolvesToAMermaidConfigWithTheValuesTheOracleRecorded`
- `EveryCaseWithAPreambleRoundTrips`
- `StmDiffersOnlyWhereTheCommittedLedgersListABug`
- `TheCommittedFileHasOneOutcomePerCorpusCase`
- `TheDefaultConfigIsMermaidsDefaultConfig`

