## EdgeRouting Option Unit Verification

Part of the Rendering Layout Verification.

This document maps the edge-routing-option behavior requirements to named test scenarios.

### EdgeRouting Option Unit Scenarios

- **Per-scope selection** (`Rendering-Layout-EdgeRouting-Selection`):
  `CoreOptions_EdgeRouting_DefaultsToOrthogonal` and `CoreOptions_EdgeRouting_HasStableId` confirm the
  `rendering.edgerouting` key defaults to `Orthogonal` and carries the ELK-flavored id;
  `CoreOptions_EdgeRouting_SelectablePerScope` sets and reads the style back through the property
  system, and `CoreOptions_EdgeRouting_UnsetReturnsDefault` confirms an unset scope falls back to the
  orthogonal default.
- **Route-option defaults** (`Rendering-Layout-EdgeRouting-Defaults`):
  `ConnectorRouteOptions_Defaults_AreOrthogonalWithTwelvePixelClearance` confirms the default style is
  orthogonal, the default clearance is twelve logical pixels, and the clearance is caller-overridable.

### Requirements Coverage

- **`Rendering-Layout-EdgeRouting-Selection`**:
  CoreOptions_EdgeRouting_DefaultsToOrthogonal, CoreOptions_EdgeRouting_HasStableId,
  CoreOptions_EdgeRouting_SelectablePerScope, CoreOptions_EdgeRouting_UnsetReturnsDefault
- **`Rendering-Layout-EdgeRouting-Defaults`**:
  ConnectorRouteOptions_Defaults_AreOrthogonalWithTwelvePixelClearance
