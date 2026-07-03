## EdgeRouting Option Unit Verification

Part of the Rendering Layout Verification.

This document maps the edge-routing-option behavior requirements to named test scenarios.

### Verification Approach

The unit is a configuration realization with no methods of its own, so verification is by direct
xUnit unit tests that (1) read the declared property key metadata, (2) exercise the open property
system round-trip on `IPropertyHolder` scopes, and (3) read the defaults of the Layout-side
`ConnectorRouteOptions` record. No mocks or fakes are used — the tests operate on real
`LayoutGraph`, `LayoutOptions`, and `ConnectorRouteOptions` instances so the observed behavior is
the same as production callers see.

### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/EdgeRoutingOptionTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory property holders and options records.
- **Isolation**: each test builds its own inputs; nothing in the option or route-options types
  carries static state between tests.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-EdgeRouting-*` requirement. Any change to the
default value, property-key identifier, per-scope selection round-trip, or `ConnectorRouteOptions`
defaults constitutes a failure.

### Test Scenarios

- **Per-scope selection** (`Rendering-Layout-EdgeRouting-Selection`):
  `CoreOptions_EdgeRouting_DefaultValue_IsOrthogonal` and
  `CoreOptions_EdgeRouting_Id_IsStableDottedIdentifier` confirm the
  `rendering.edgerouting` key defaults to `Orthogonal` and carries the ELK-flavored id;
  `CoreOptions_EdgeRouting_SetThenGet_RoundTripsValue` sets and reads the style back through the property
  system, and `CoreOptions_EdgeRouting_UnsetHolder_ReturnsOrthogonalDefault` confirms an unset scope falls
  back to the orthogonal default.
- **Route-option defaults** (`Rendering-Layout-EdgeRouting-Defaults`):
  `ConnectorRouteOptions_Constructor_Defaults_AreOrthogonalWithTwelvePixelClearance` confirms the default style is
  orthogonal, the default clearance is twelve logical pixels, and the clearance is caller-overridable.

### Requirements Coverage

- **`Rendering-Layout-EdgeRouting-Selection`**:
  CoreOptions_EdgeRouting_DefaultValue_IsOrthogonal, CoreOptions_EdgeRouting_Id_IsStableDottedIdentifier,
  CoreOptions_EdgeRouting_SetThenGet_RoundTripsValue, CoreOptions_EdgeRouting_UnsetHolder_ReturnsOrthogonalDefault
- **`Rendering-Layout-EdgeRouting-Defaults`**:
  ConnectorRouteOptions_Constructor_Defaults_AreOrthogonalWithTwelvePixelClearance
