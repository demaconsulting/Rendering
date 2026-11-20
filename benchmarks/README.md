# Rendering routing benchmark

An opt-in measurement of how long `LayoutEngine.Layout` takes for the scenes that stress the cross-container connector
routing (Rendering#75). It is **not** a test and not part of the solution: `build.ps1`, `lint.ps1` and CI never build or
run it, and it never fails on a time. The performance properties it measures are recorded in the design documentation
(*HierarchicalLayoutAlgorithm Unit Design*, "Performance design properties").

## Running

```pwsh
pwsh ./benchmark.ps1                                 # every scene, 5 measured layouts each
pwsh ./benchmark.ps1 -Scene syn-160,syn-320,syn-640  # chosen scenes
pwsh ./benchmark.ps1 -Runs 9                         # more measured layouts per scene
pwsh ./benchmark.ps1 -List                           # the scene names
```

The script runs `dotnet run --configuration Release` on
`benchmarks/DemaConsulting.Rendering.Layout.Benchmarks`. Run it on a quiet machine: the times are wall-clock times and a
loaded machine inflates them (the growth exponents are far less sensitive than the times).

## Scenes

The scenes are defined once, in `test/DemaConsulting.Rendering.Layout.Tests/RoutingScenes.cs`, which the benchmark
compiles, so the benchmark and the layout tests lay out exactly the same graphs. They use the public layout API only.

| Scene | Graph |
| --- | --- |
| `syn-5` to `syn-640` | 6 folders of 5 leaves, 5 to 640 seeded random cross-container connectors (doubling) |
| `nested-small` | 2 sub-packages of 2 folders of 3 leaves, 3 inner and 2 package connectors |
| `nested-oversize` | 4 sub-packages of 3 folders of 4 leaves, 6 inner and 80 package connectors |
| `nested-everyday` | 4 sub-packages of 3 folders of 4 leaves, 6 inner and 6 package connectors (66 connectors) |

`nested-everyday` has 65 nodes, 17 containers and depth 4; one layout of it takes seconds.

## Reading the table

For every scene the benchmark lays the graph out once to warm up the runtime (the slow `nested-everyday` scene is not
warmed up and runs once by default), then times the requested number of layouts with a `Stopwatch` and prints:

- **Connectors drawn**: the connectors in the layout and the number a complete layout has. Fewer is a failure.
- **Warnings**: the number of warnings of the layout tree (one reduced-effort warning is expected for an oversize
  scope).
- **Min, Median, Max**: the seconds of the measured layouts.
- **Growth exponent**: `log2(t(2E) / t(E))` of the median times of two consecutive synthetic scenes, which double the
  connector count. 1 would be linear, 2 quadratic and 3 cubic (the cost before the fix); 1.5 to 1.7 is typical at
  160 to 640 connectors. It is shown only between two scenes at the same effort (a scope of 16 or more
  connectors drops from the full to the reduced effort, a different algorithm) and when the earlier time is above
  50 ms, below which the timer and the JIT dominate.
- **Fingerprint**: a SHA-256 digest of every box and connector coordinate rounded to three decimals. All runs of a scene
  must agree (the benchmark exits 1 otherwise), and the digest identifies the layout: it does not change unless the
  layout does.
