# EdgeRouting Option Unit Design

Part of the Rendering Layout system.

## EdgeRouting Option Overview

The EdgeRouting option is the Layout-owned routing-selection behavior used when connectors are routed
among already-placed boxes. The closed `EdgeRouting` enum and the `CoreOptions.EdgeRouting` property key
are declared in the Rendering model so the open option system can carry the value at graph, node, edge,
or standalone option scope. Rendering.Layout realizes the behavior by reading that option and dispatching
`ConnectorRouter` to the corresponding routing implementation.

## EdgeRouting Option Data Model

- `CoreOptions.EdgeRouting` — the open property key with id `rendering.edgerouting`, defaulting to
  `EdgeRouting.Orthogonal`.
- `EdgeRouting.Orthogonal` — the shipped routing style value. The enum type is defined by the Rendering
  model, not by the Layout project.
- `ConnectorRouteOptions(EdgeRouting, Clearance)` — the Layout-side route options record consumed by
  `ConnectorRouter`, defaulting to orthogonal routing and twelve logical pixels of clearance.

## EdgeRouting Option Behavior

A caller can set `CoreOptions.EdgeRouting` on any property holder and read the selected routing style
back through the same open option system. An unset scope returns the declared orthogonal default.
`ConnectorRouter` consumes the effective `EdgeRouting` value from `ConnectorRouteOptions`; the current
single shipped value dispatches to the internal orthogonal router. The switch is structured for additive
future routing styles while preserving the current default behavior.

## EdgeRouting Option Scope Note

The enum declaration lives in the Rendering model project because `CoreOptions` belongs to the shared
configuration vocabulary. This Layout unit therefore owns the behavior of consuming the option for
routing, not the model file that declares the enum.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-EdgeRouting-Selection | Option key/defaults and Layout-side routing consumption |
| Rendering-Layout-EdgeRouting-Defaults | Option key/defaults and Layout-side routing consumption |
