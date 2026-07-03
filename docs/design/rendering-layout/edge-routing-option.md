## EdgeRouting Option Unit Design

Part of the Rendering Layout system.

### EdgeRouting Option Overview

The EdgeRouting option is the Layout-owned routing-selection behavior used when connectors are routed
among already-placed boxes. The closed `EdgeRouting` enum and the `CoreOptions.EdgeRouting` property key
are declared in the Rendering model so the open option system can carry the value at graph, node, edge,
or standalone option scope. Rendering.Layout realizes the behavior by reading that option and dispatching
`ConnectorRouter` to the corresponding routing implementation.

### EdgeRouting Option Data Model

- `CoreOptions.EdgeRouting` — the open property key with id `rendering.edgerouting`, defaulting to
  `EdgeRouting.Orthogonal`.
- `EdgeRouting.Orthogonal` — the shipped routing style value. The enum type is defined by the Rendering
  model, not by the Layout project.
- `ConnectorRouteOptions(EdgeRouting, Clearance)` — the Layout-side route options record consumed by
  `ConnectorRouter`, defaulting to orthogonal routing and twelve logical pixels of clearance.

### EdgeRouting Option Behavior

A caller can set `CoreOptions.EdgeRouting` on any property holder and read the selected routing style
back through the same open option system. An unset scope returns the declared orthogonal default.
`ConnectorRouter` consumes the effective `EdgeRouting` value from `ConnectorRouteOptions`; the current
single shipped value dispatches to the internal orthogonal router. The switch is structured for additive
future routing styles while preserving the current default behavior.

### EdgeRouting Option Key Methods

The unit has no methods of its own; it is a configuration realization. Behavior is composed from:

- `IPropertyHolder.Get(CoreOptions.EdgeRouting)` — reads the effective routing style from any option
  scope (graph, node, edge, or standalone `LayoutOptions`), falling back to the declared
  `EdgeRouting.Orthogonal` default when no scope has set the key.
- `IPropertyHolder.Set(CoreOptions.EdgeRouting, value)` — writes an explicit routing style at a
  scope.
- `new ConnectorRouteOptions(EdgeRouting, Clearance)` — constructs the Layout-side route options
  record consumed by `ConnectorRouter.Route`; the no-argument constructor with default values produces
  `Orthogonal` routing with a `Clearance` of `12.0` logical pixels.

Preconditions: the property key `CoreOptions.EdgeRouting` is declared exactly once by the Rendering
model. Post-conditions: reads of any scope return either the caller-set value or the declared default;
`ConnectorRouter.Route` dispatches to the router realizing the returned `EdgeRouting` value.

### EdgeRouting Option Error Handling

The option itself performs no I/O and raises no errors: reads always succeed with either a
caller-set value or the declared default, and writes accept any `EdgeRouting` enum value defined by
the model. Runtime dispatch errors are surfaced downstream by `ConnectorRouter`, which throws
`NotSupportedException` if a shipped router is not available for the effective `EdgeRouting` value
(see _ConnectorRouter Unit Design_).

### EdgeRouting Option Dependencies

- **Rendering model** (`DemaConsulting.Rendering`) — declares the closed `EdgeRouting` enum and the
  `CoreOptions.EdgeRouting` property key with id `rendering.edgerouting`. This unit does not own
  those types; it owns the Layout-side behavior of consuming the option.
- **Options system** (`IPropertyHolder`, `LayoutProperty<T>`, `LayoutOptions`) — the open property
  system that carries the value at any scope.
- **ConnectorRouter unit** — consumes the effective value through `ConnectorRouteOptions` and
  dispatches to the corresponding router.

No OTS runtime component or shared package is consumed.

### EdgeRouting Option Callers

- **ConnectorRouter** — reads the effective `EdgeRouting` value from a `ConnectorRouteOptions`
  record to select the routing implementation on each `Route` call.
- **HierarchicalLayoutAlgorithm** — reads the option from the current options scope when routing
  cross-container edges via `ConnectorRouter`.
- **External application code** — sets `CoreOptions.EdgeRouting` on a graph, node, edge, or
  standalone `LayoutOptions` to select the routing style per scope.

### EdgeRouting Option Scope Note

The enum declaration lives in the Rendering model project because `CoreOptions` belongs to the shared
configuration vocabulary. This Layout unit therefore owns the behavior of consuming the option for
routing, not the model file that declares the enum.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-EdgeRouting-Selection | Option key/defaults and Layout-side routing consumption |
| Rendering-Layout-EdgeRouting-Defaults | Option key/defaults and Layout-side routing consumption |
