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
| `Repro2Right` | 1 → 0 | 938x308 → 858x258 | 288904 → 221364 (-23.4%) |
| `Repro3Right` | 2 → 0 | 1148x308 → 1068x258 | 353584 → 275544 (-22.1%) |
| `NestedDepthRight` | 1 → 0 | 1276x548 → 1116x430.5 | 699248 → 480438 (-31.3%) |
| `NestedPackagesRight` | 3 → 3 | 1564x616 → 1364x536 | 963424 → 731104 (-24.1%) |
| `NestedPackagesUp` | 7 → 6 | 879.4x1808 → 838.4x1768 | 1589955 → 1482291 (-6.8%) |
| `GeneralViewUp` | 22 → 14 | 1310.4x1690 → 1132.4x1746 | 2214576 → 1977170 (-10.7%) |
| `DenseUsageTree` | 43 → 43 | 2474x898 → 2474x794 | 2221652 → 1964356 (-11.6%) |
| `InterconnectionLabeledCorridors` | 0 → 0 | 2237.1x319.6 → 2158.6x319.6 | 714984 → 689891 (-3.5%) |
| `InterconnectionLayerSpacing40` | 0 → 0 | 2237.1x319.6 → 2158.6x319.6 | 714984 → 689891 (-3.5%) |
| `ActionFlow` | 0 → 0 | 432.8x1086 → 432.8x1086 | 470066 → 470066 (0.0%) |
| `StateMachine` | 0 → 0 | 318.2x654 → 318.2x654 | 208108 → 208108 (0.0%) |
| `AutoTallPlusIsolated` | 0 → 0 | 492x910 → 344x726 | 447720 → 249744 (-44.2%) |

## Empty space and block area

| Scene | Largest empty square (px) | B4 | B5 |
| --- | --- | --- | --- |
| `Repro2Right` | 132 → 104 | 2.36 → 1.86 | 0.136 → 0.177 |
| `Repro3Right` | 132 → 104 | 2.36 → 1.86 | 0.133 → 0.171 |
| `NestedDepthRight` | 344 → 288 | 6.14 → 5.14 | 0.078 → 0.114 |
| `NestedPackagesRight` | 268 → 228 | 4.79 → 4.07 | 0.138 → 0.182 |
| `NestedPackagesUp` | 496 → 464 | 8.86 → 8.29 | 0.084 → 0.090 |
| `GeneralViewUp` | 440 → 432 | 7.86 → 7.71 | 0.094 → 0.105 |
| `DenseUsageTree` | 388 → 244 | 6.93 → 4.36 | 0.100 → 0.113 |
| `InterconnectionLabeledCorridors` | 128 → 128 | 1.04 → 1.04 | 0.204 → 0.212 |
| `InterconnectionLayerSpacing40` | 128 → 128 | 1.04 → 1.04 | 0.204 → 0.212 |
| `ActionFlow` | 232 → 232 | 5.80 → 5.80 | 0.109 → 0.109 |
| `StateMachine` | 96 → 96 | 2.18 → 2.18 | 0.209 → 0.209 |
| `AutoTallPlusIsolated` | 332 → 184 | 5.93 → 3.29 | 0.210 → 0.377 |

## LayerSpacing

The source feeding two targets of the [layout-quality group](README.md) laid out with different
`CoreOptions.LayerSpacing` values. The option is a floor for the corridor between adjacent layers; the previous engine
ignored it, so all three canvases were the unset one.

| LayerSpacing | Canvas |
| --- | --- |
| unset (historical 70 px corridor) | 310x170 |
| 100 | 340x170 |
| 150 | 390x170 |
