## ConnectorRouter Unit Verification

Part of the Rendering Layout Verification.

This document maps the connector-router unit requirements to named test scenarios.

### Verification Approach

`ConnectorRouter` is a stateless static class, so verification is by direct xUnit unit tests that
call `ConnectorRouter.Route` on synthetic `LayoutBox` inputs. No mocks or fakes are used: the tests
exercise the real anchor-selection, obstacle-set construction, and dispatch code paths, letting the
integration with the internal `OrthogonalEdgeRouter` engine run end-to-end so anchor geometry,
obstacle avoidance, and produced `LayoutLine` styling are all observed on real outputs.

### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/ConnectorRouterTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutBox` list and `Connection` records.
- **Isolation**: each test builds its own inputs; the class under test holds no static state, so
  tests are order-independent.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-ConnectorRouter-*` requirement. Any wrong anchor
face, waypoint that enters a non-endpoint obstacle interior, incorrect line styling passthrough,
out-of-order batch result, or non-argument-null exception for invalid input constitutes a failure.

### Test Scenarios

- **Anchors face each other** (`Rendering-Layout-ConnectorRouter-AnchorsFaceEachOther`):
  `Route_TargetToTheRight_AnchorsFaceEachOther` and `Route_TargetBelow_AnchorsFaceEachOther` confirm
  the route starts and ends on the box faces that point at the opposing box.
- **Obstacle avoidance** (`Rendering-Layout-ConnectorRouter-AvoidsObstacles`):
  `Route_ObstacleBetweenEndpoints_RoutesAroundInterior` confirms the route is orthogonal and never
  enters an intervening box's interior.
- **Endpoint exclusion** (`Rendering-Layout-ConnectorRouter-ExcludesEndpoints`):
  `Route_EndpointBoxes_AreExcludedFromObstacles` confirms the connector reaches the endpoints' boundary
  anchors even though both boxes appear in the box list.
- **Styling carried** (`Rendering-Layout-ConnectorRouter-CarriesStyling`):
  `Route_Connection_CarriesRequestedStyling` confirms the requested target marker, line style, and
  label flow onto the produced line while the source end stays unmarked.
- **Batch order** (`Rendering-Layout-ConnectorRouter-BatchOrder`):
  `Route_MultipleConnections_ReturnsOneLinePerConnectionInOrder` confirms one line per connection in
  input order.
- **Validation** (`Rendering-Layout-ConnectorRouter-Validation`): `Route_NullBoxes_Throws`,
  `Route_NullConnections_Throws`, `Route_NullOptions_Throws`, and `Route_NullConnection_Throws` confirm
  null arguments are rejected with an argument-null error.

### Requirements Coverage

- **`Rendering-Layout-ConnectorRouter-AnchorsFaceEachOther`**:
  Route_TargetToTheRight_AnchorsFaceEachOther, Route_TargetBelow_AnchorsFaceEachOther
- **`Rendering-Layout-ConnectorRouter-AvoidsObstacles`**:
  Route_ObstacleBetweenEndpoints_RoutesAroundInterior
- **`Rendering-Layout-ConnectorRouter-ExcludesEndpoints`**:
  Route_EndpointBoxes_AreExcludedFromObstacles
- **`Rendering-Layout-ConnectorRouter-CarriesStyling`**:
  Route_Connection_CarriesRequestedStyling
- **`Rendering-Layout-ConnectorRouter-BatchOrder`**:
  Route_MultipleConnections_ReturnsOneLinePerConnectionInOrder
- **`Rendering-Layout-ConnectorRouter-Validation`**:
  Route_NullBoxes_Throws, Route_NullConnections_Throws, Route_NullOptions_Throws, Route_NullConnection_Throws
