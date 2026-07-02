# ConnectorRouter Unit Verification

Part of the Rendering Layout Verification.

This document maps the connector-router unit requirements to named test scenarios.

## ConnectorRouter Unit Scenarios

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

## Requirements Coverage

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
