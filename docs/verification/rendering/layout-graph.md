## Layout Graph Unit Verification

Part of the Rendering Model Verification.

This document describes the verification design for the layout-graph unit of the
`DemaConsulting.Rendering` system. It maps every layout-graph unit requirement to at least one named
test scenario so a reviewer can confirm coverage without reading the test code. The test project is
`DemaConsulting.Rendering.Tests` (`LayoutGraphTests.cs`).

### Verification Approach

`LayoutGraph` is verified by direct in-process xUnit unit tests against the real `LayoutGraph`,
`LayoutGraphNode`, and `LayoutGraphEdge` types. The unit is a pure in-memory model, so no mocking or
test doubles are required: each scenario constructs a graph, performs the node/edge operation under
test, and asserts on the returned objects, graph contents, per-container scoping behavior, or thrown
exceptions.

### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Tests/LayoutGraphTests.cs`.
- **Target frameworks**: .NET 8, .NET 9, and .NET 10.
- **Dependencies**: no external services, files, or network access; every test uses only in-memory
  graph objects.

### Acceptance Criteria

A verification run passes when every named scenario below executes without unexpected exception and
the observed graph structure matches the requirement under test. Any wrong node or edge count, wrong
per-container scoping behavior, wrong stored property value, or unexpected exception constitutes a
failure.

### Test Scenarios

#### AddNode appends and returns the node

Test `AddNode_AppendsNodeAndReturnsIt` calls `AddNode` on a fresh graph and asserts that the graph
contains one node and that the returned node carries the requested id, width, and height.

**Covers**: `Rendering-Model-LayoutGraph-AddNode`.

#### AddEdge appends an edge with endpoints

Test `AddEdge_AppendsEdgeWithEndpoints` adds two nodes and an edge referencing them, then asserts that
the graph contains one edge whose `Source` and `Target` are the same node instances supplied.

**Covers**: `Rendering-Model-LayoutGraph-AddEdge`.

#### Node carries per-element properties

Test `Node_CarriesPerElementProperties` sets the `CoreOptions.Direction` property on a node and reads
it back, asserting that the read returns the value set on that node.

**Covers**: `Rendering-Model-LayoutGraph-PerElementProperties`.

#### Container node holds nested children and a leaf reports none

Tests `LayoutGraphNode_Children_ContainerNode_HoldsChildNodesAndEdges` and
`LayoutGraphNode_HasChildren_LeafNode_ReturnsFalse` populate a node's `Children` subgraph with two
nested nodes and an intra-container edge and assert the node reports `HasChildren`, its children and
contained edge are retrievable, and the edge references the nested endpoints; the companion test
asserts that a freshly-added node with no accessed `Children` reports `HasChildren` false, confirming
a leaf allocates no child subgraph.

**Covers**: `Rendering-Model-LayoutGraph-ContainerNodes`.

#### Identifiers are scoped per container

Tests `LayoutGraph_AddNode_ChildScope_AllowsIdReuseAcrossScopes` and
`LayoutGraph_AddNode_ChildScope_DuplicateId_ThrowsArgumentException` add a node named `x` inside two
different container scopes and assert both are accepted as distinct instances, then add a duplicate id
within one scope and assert it is rejected and not appended — confirming per-scope uniqueness with
cross-scope reuse. `LayoutGraph_AddEdge_ChildScope_AllowsEdgeIdReuseAcrossScopes` adds an edge with
the same id inside two sibling container scopes and asserts both are accepted as distinct edges,
confirming edge identifiers are scoped per container just as node identifiers are.

**Covers**: `Rendering-Model-LayoutGraph-ScopedIdentifiers`.

#### Cross-container edge references a descendant node

Test `LayoutGraphEdge_CrossContainer_ReferencingDescendant_ConstructibleAtRoot` adds, at the root
graph, an edge between a root-level leaf node and a node nested inside a container, then asserts the
edge is held at the root yet references the descendant endpoint and that the descendant is not itself
a root-level node. `LayoutGraphEdge_CrossContainer_BetweenSiblingContainers_ConstructibleAtRoot` adds,
at the root, an edge between descendant nodes living in two different sibling containers and asserts
the edge is held at the root yet references both descendants — together confirming a cross-container
edge is expressible at the lowest common ancestor, including the sibling-container case.

**Covers**: `Rendering-Model-LayoutGraph-CrossContainerEdge`.

### Requirements Coverage

- **`Rendering-Model-LayoutGraph-AddNode`**: AddNode_AppendsNodeAndReturnsIt
- **`Rendering-Model-LayoutGraph-AddEdge`**: AddEdge_AppendsEdgeWithEndpoints
- **`Rendering-Model-LayoutGraph-PerElementProperties`**: Node_CarriesPerElementProperties
- **`Rendering-Model-LayoutGraph-ContainerNodes`**:
  LayoutGraphNode_Children_ContainerNode_HoldsChildNodesAndEdges,
  LayoutGraphNode_HasChildren_LeafNode_ReturnsFalse
- **`Rendering-Model-LayoutGraph-ScopedIdentifiers`**:
  LayoutGraph_AddNode_ChildScope_AllowsIdReuseAcrossScopes,
  LayoutGraph_AddNode_ChildScope_DuplicateId_ThrowsArgumentException,
  LayoutGraph_AddEdge_ChildScope_AllowsEdgeIdReuseAcrossScopes
- **`Rendering-Model-LayoutGraph-CrossContainerEdge`**:
  LayoutGraphEdge_CrossContainer_ReferencingDescendant_ConstructibleAtRoot,
  LayoutGraphEdge_CrossContainer_BetweenSiblingContainers_ConstructibleAtRoot
