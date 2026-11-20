# benchmark.ps1
#
# PURPOSE:
#   Runs the opt-in routing benchmark under benchmarks/. It lays out the synthetic and nested scenes of the layout
#   tests (a package of six folders joined by 5 to 640 cross-container connectors, and nested packages, among them
#   the "everyday" package of four sub-packages of three folders) through the public LayoutEngine.Layout and prints a
#   Markdown table of the layout time, the growth exponent between doublings and a determinism check.
#
#   The benchmark is NOT part of the solution, so build.ps1, lint.ps1 and CI never build or run it. It never judges
#   a time: it exits 1 only when a scene draws fewer connectors than it has, or when two layouts of one scene differ.
#   Performance is a design property recorded in the design documentation, not a test.
#
# USAGE:
#   pwsh ./benchmark.ps1                                  # every scene, 5 measured layouts each (the slow one once)
#   pwsh ./benchmark.ps1 -Scene syn-160,syn-320,syn-640   # chosen scenes
#   pwsh ./benchmark.ps1 -Runs 9                          # more measured layouts per scene
#   pwsh ./benchmark.ps1 -List                            # list the scene names

param(
    [string[]]$Scene,
    [int]$Runs = 0,
    [switch]$List
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'benchmarks/DemaConsulting.Rendering.Layout.Benchmarks/DemaConsulting.Rendering.Layout.Benchmarks.csproj'

$benchmarkArgs = @()
if ($List) { $benchmarkArgs += '--list' }
if ($Scene) { $benchmarkArgs += @('--scene', ($Scene -join ',')) }
if ($Runs -gt 0) { $benchmarkArgs += @('--runs', "$Runs") }

# A Release build is the only one whose times mean anything
dotnet run --project $project --configuration Release -- @benchmarkArgs
exit $LASTEXITCODE
