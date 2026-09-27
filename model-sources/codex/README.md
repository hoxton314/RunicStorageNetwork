# Runic Codex item model

`RSN_RunicCodex` is the closed, portable **Runic Codex / Рунный кодекс**. It is distinct from the open **Storage Codex** build piece (`RSN_RunicStorageTerminal`). The asset pipeline prepares its visual prefab, body collider and transparent 256 × 256 icon. `src/RunicCodexItem.cs` registers it as a regular material item with weight 1, stack size 1 and no upgrades. Its level 1 forge recipe makes one book from Silver ×4, Crystal ×2, GreydwarfEye ×6, LinenThread ×4 and LeatherScraps ×4, using vanilla ingredient discovery.

The source snapshot is `RunicCodex/export_v04`, derived from `model_v03/RunicCodex_Artifact_v03.blend`. `RunicCodex.fbx` and `materials.json` are copies of that export. The FBX has 813 triangles and seven material groups. Hashes in `source.json` describe the original exported bytes; Git may normalize JSON line endings. The source FBX and authoring project are untouched.

## Prepared appearance

- The dark-brown leather uses the same material binding and albedo as the open book on the stand.
- Silver, parchment and the bookmark use neutral copies of the same native `Custom/Piece` shader. The closed book's silver uses slightly rougher, less purely metallic settings so its horizontal fittings remain visible outside a direct highlight.
- Rune and clasp-crystal colors match the open book's cyan palette. Primary markings, small symbols and the crystal retain their separate emission strengths. The bookmark glyph remains matte.
- The three clasp-strap islands have their own warmer leather material. The silver mount around the crystal is 18% wider, making the clasp easier to distinguish from the cover. This uses derived meshes; the total remains 813 triangles, now in eight groups, with the same outer dimensions.

There are no external texture atlases or normal maps in the export. These plain surfaces use numeric material colors and solid albedos, matching the existing stand; no grain or embossing texture has been invented. Native game assets are resolved for preview/runtime material binding, never copied into the outgoing prefab or bundle. `src/TerminalMaterials.cs` shares the bindings for both books.

## Unity preparation

Copy the FBX and `materials.json` into `Assets/RunicStorage/CodexSource` in the separate UnityBuild project. Also provide the existing `Assets/RunicStorage/TerminalSource/materials.json` for the shared palette. Copy `tools/BuildCodexAssets.cs` alongside the existing partial `BuildAssets` Editor sources and shared material binder. The importer keeps source normals, metre scale and orientation: closed cover up (+Y), spine at −X, bookmark towards −Z, ground at Y = 0. Dimensions are 0.25335 × 0.0985 × 0.382 m.

The generated prefab is `Assets/RunicStorageGame/RSN_RunicCodex.prefab`, with its renderers under `attach`. The collider excludes the bookmark and emissive details. The asset itself contains no game scripts; registration adds `ItemDrop`, persistent `ZNetView`, `ZSyncTransform` and `Rigidbody` at runtime. The generated icon is `Assets/RunicStorageGame/RSN_CodexIcon.png`. `icon.png` here is the reviewed preview snapshot.

For model-only rendering, also copy `tools/PreviewCodex.cs` and `tools/PreviewTerminal.cs` into the Editor folder and invoke `RunicStorage.Build.BuildAssets.PreviewCodex` with `-rsnOutput <output-directory>`. It imports the visual, checks dimensions/normals/materials, and renders cover, spine, underside, 256/64-pixel icons and the existing open book under the same lighting. It does not enter Play Mode or build a DLL/AssetBundle.

The private full asset pipeline calls `BuildCodex(output)` after `BuildTerminal(output)`, includes `CodexAsset` and `CodexIcon` in the bundle, and calls `ValidateCodex(bundle, codex, output)` after reopening it. Dropped-item physics and actual in-game lighting require in-game verification.
