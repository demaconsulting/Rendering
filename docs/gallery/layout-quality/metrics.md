# Layout quality metrics (#48, #60)

Each cell reads `before → after`. *Before* is the baseline of the previous layout engine (main@219650c), recorded as
constants in the test project; *after* is measured when this page is generated, on the very graphs rendered in the
[layout-quality group](README.md). The generating gallery test fails if any scene is worse than its baseline.

- **Crossings**: proper crossings between distinct connectors (touches at a shared end excluded).
- **B4**: side of the largest empty square divided by the mean block height (pass: at most 3).
- **B5**: total block area divided by the canvas area (pass: at least 0.20).
- **Canvas**: width x height and area in logical pixels.

## Crossings and canvas

| Scene | Crossings | Canvas | Area |
| --- | --- | --- | --- |
| `Repro2Right` | 1 → 0 | 938x308 → 866.7x264.6 | 288904 → 229303 (-20.6%) |
| `Repro3Right` | 2 → 0 | 1148x308 → 1076.7x264.6 | 353584 → 284860 (-19.4%) |
| `NestedDepthRight` | 1 → 0 | 1276x548 → 1276x542 | 699248 → 691592 (-1.1%) |
| `NestedPackagesRight` | 3 → 3 | 1564x616 → 1550x616 | 963424 → 954800 (-0.9%) |
| `NestedPackagesUp` | 7 → 4 | 879.4x1808 → 857.1x1776.7 | 1589955 → 1522924 (-4.2%) |
| `GeneralViewUp` | 22 → 12 | 1310.4x1690 → 1098x1746 | 2214576 → 1917108 (-13.4%) |
| `DenseUsageTree` | 43 → 43 | 2474x898 → 2474x794 | 2221652 → 1964356 (-11.6%) |
| `InterconnectionLabeledCorridors` | 0 → 0 | 2237.1x319.6 → 2158.6x319.6 | 714984 → 689891 (-3.5%) |
| `InterconnectionLayerSpacing40` | 0 → 0 | 2237.1x319.6 → 2158.6x319.6 | 714984 → 689891 (-3.5%) |
| `ActionFlow` | 0 → 0 | 432.8x1086 → 432.8x1086 | 470066 → 470066 (0.0%) |
| `StateMachine` | 0 → 0 | 318.2x654 → 318.2x654 | 208108 → 208108 (0.0%) |
| `AutoTallPlusIsolated` | 0 → 0 | 492x910 → 344x726 | 447720 → 249744 (-44.2%) |

## Empty space and block area

| Scene | Largest empty square (px) | B4 | B5 |
| --- | --- | --- | --- |
| `Repro2Right` | 132 → 108 | 2.36 → 1.93 | 0.136 → 0.171 |
| `Repro3Right` | 132 → 108 | 2.36 → 1.93 | 0.133 → 0.165 |
| `NestedDepthRight` | 344 → 344 | 6.14 → 6.14 | 0.078 → 0.079 |
| `NestedPackagesRight` | 268 → 268 | 4.79 → 4.79 | 0.138 → 0.140 |
| `NestedPackagesUp` | 496 → 484 | 8.86 → 8.64 | 0.084 → 0.088 |
| `GeneralViewUp` | 440 → 412 | 7.86 → 7.36 | 0.094 → 0.108 |
| `DenseUsageTree` | 388 → 244 | 6.93 → 4.36 | 0.100 → 0.113 |
| `InterconnectionLabeledCorridors` | 128 → 128 | 1.04 → 1.04 | 0.204 → 0.212 |
| `InterconnectionLayerSpacing40` | 128 → 128 | 1.04 → 1.04 | 0.204 → 0.212 |
| `ActionFlow` | 232 → 232 | 5.80 → 5.80 | 0.109 → 0.109 |
| `StateMachine` | 96 → 96 | 2.18 → 2.18 | 0.209 → 0.209 |
| `AutoTallPlusIsolated` | 332 → 184 | 5.93 → 3.29 | 0.210 → 0.377 |

## Connector routes

How the connectors themselves read. *Length* is the total connector length in logical pixels, *bends* the number of
direction changes, *hairpins* the legs that run back against the direction of the leg two before them, and *close
parallel* the length, in logical pixels, of runs of different connectors that lie closer than 16 px for more than 20 px.
The generating test holds length and bends to within 10% of the baseline and hairpins and close parallel runs to the
baseline, without exceptions.

| Scene | Length | Bends | Hairpins | Close parallel |
| --- | --- | --- | --- | --- |
| `Repro2Right` | 1084 → 840 (-22.4%) | 6 → 2 | 1 → 0 | 60 → 0 |
| `Repro3Right` | 1594 → 1448 (-9.1%) | 6 → 6 | 1 → 1 | 0 → 0 |
| `NestedDepthRight` | 2214 → 2052 (-7.3%) | 10 → 10 | 2 → 2 | 76 → 0 |
| `NestedPackagesRight` | 4266 → 3672 (-13.9%) | 28 → 28 | 7 → 4 | 482 → 433 |
| `NestedPackagesUp` | 5439 → 5207 (-4.3%) | 23 → 24 | 8 → 8 | 684 → 368 |
| `GeneralViewUp` | 12061 → 12034 (-0.2%) | 49 → 41 | 7 → 7 | 897 → 521 |
| `DenseUsageTree` | 20219 → 20216 (0.0%) | 57 → 57 | 4 → 4 | 929 → 669 |
| `InterconnectionLabeledCorridors` | 3481 → 3316 (-4.7%) | 10 → 10 | 1 → 1 | 0 → 0 |
| `InterconnectionLayerSpacing40` | 3481 → 3316 (-4.7%) | 10 → 10 | 1 → 1 | 0 → 0 |
| `ActionFlow` | 1696 → 1696 (0.0%) | 8 → 8 | 0 → 0 | 0 → 0 |
| `StateMachine` | 1297 → 1297 (0.0%) | 10 → 10 | 0 → 0 | 0 → 0 |
| `AutoTallPlusIsolated` | 350 → 350 (0.0%) | 0 → 0 | 0 → 0 | 0 → 0 |

## Canvas margin

The canvas of every scene is its content (boxes, connectors, labels, ports) plus one padding of 20 px on all four sides;
the generating test fails when a margin is not 20 px. The previous engine left the content of a Left or Up flow against
the top or left edge with a double margin at the opposite edge, and put a connector that detoured beyond the origin 10
px from it.

| Scene | Margin (left, top, right, bottom) |
| --- | --- |
| `Repro2Right` | 20, 20, 20, 20 |
| `Repro3Right` | 20, 20, 20, 20 |
| `NestedDepthRight` | 20, 20, 20, 20 |
| `NestedPackagesRight` | 20, 20, 20, 20 |
| `NestedPackagesUp` | 20, 20, 20, 20 |
| `GeneralViewUp` | 20, 20, 20, 20 |
| `DenseUsageTree` | 20, 20, 20, 20 |
| `InterconnectionLabeledCorridors` | 20, 20, 20, 20 |
| `InterconnectionLayerSpacing40` | 20, 20, 20, 20 |
| `ActionFlow` | 20, 20, 20, 20 |
| `StateMachine` | 20, 20, 20, 20 |
| `AutoTallPlusIsolated` | 20, 20, 20, 20 |

## LayerSpacing

The source feeding two targets of the [layout-quality group](README.md) laid out with different
`CoreOptions.LayerSpacing` values. The option is a floor for the corridor between adjacent layers; the previous engine
ignored it, so all three canvases were the unset one.

| LayerSpacing | Canvas |
| --- | --- |
| unset (historical 70 px corridor) | 310x170 |
| 100 | 340x170 |
| 150 | 390x170 |
