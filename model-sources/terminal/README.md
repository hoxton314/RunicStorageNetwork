# Storage Codex model integration

Stable prefab ID: `RSN_RunicStorageTerminal`. Player-facing names: **Storage Codex** (English), **Кодекс запасов** (Russian).

This is a copy of the latest `export_v19` FBX and its numeric material specification, derived from the approved v18 authoring model. The existing Terminal prefab in RunicRelayPreview still contains v10 (11 groups); it is deliberately not the source for this integration. V19 has 13 groups and 3,710 triangles, including silver book trim and the red cloth border. Hashes in `source.json` identify the source snapshot.

The local asset pipeline copies these files into its separate UnityBuild project. `tools/BuildTerminalAssets.cs` imports the FBX with the original normals and scale, prepares materials, adds physical surface colliders, and adds the prefab and menu icon to the existing bundle. Source projects are not modified. The standalone DLL compile cannot generate this bundle.

The plugin registers a buildable piece in Hammer → Crafting. Its initial placement cost is 6 Stone and 4 Fine Wood at a workbench; balance is provisional while the model is reviewed. Placement, damage and destruction effects come from the vanilla stone floor. The piece does not yet open storage, join networks or supply resources. The existing core terminal UI stays attached to the core.

The model has been imported and rendered in Unity 6000.0.75f1 using the prepared material bindings. The import checks passed for 13 meshes, 3,710 triangles, dimensions and floor origin. **No DLL, asset bundle or test package has been built for this model integration.** Bundle validation and runtime lighting/placement still need review after the model is ready.

## Material preparation

Wood, stone and iron reuse the verified native door, stone wall and iron-floor materials. The stone/wood UV mapping is prepared on build-owned mesh copies, with gentle normal strengths (0.25 and 0.35). Surface movement and noise are disabled to keep the geometric runes aligned.

Silver, leather, page surfaces/edges and both cloth colors use neutral copies of the native building shader with the exported colors, metallic values and roughness. They do not yet have new surface textures or normal maps. Directly copying the item leather or banner materials was avoided: the inspected game assets use Creature/Vegetation shaders. The three rune groups retain their original independent emission. `src/TerminalMaterials.cs` is shared with the Editor icon renderer so that its material assignments match the plugin.

## Hooks in the private asset pipeline

Copy this folder's FBX and `materials.json` into `Assets/RunicStorage/TerminalSource` in the isolated build project. Copy `tools/BuildTerminalAssets.cs`, `tools/BuildSurfaceUv.cs` and `src/TerminalMaterials.cs` into its Editor folder alongside the existing partial `BuildAssets` sources. `SurfaceUv` now lives in `BuildSurfaceUv.cs`; remove its older definition from the private partial class. Terminal mesh assets have their own `RSN_Terminal_` prefix so they cannot overwrite core or relay meshes. After `BuildRelay`, call `BuildTerminal(output)`; include `TerminalAsset` and `TerminalIcon` in the existing bundle's asset list. After reopening the bundle, call `ValidateTerminal(bundle, terminal, output)` and destroy the prepared terminal instance. These hooks have been added to the local pipeline, which remains excluded from this public source repository.

Before importing the model, run Blender in background mode with `tools/BuildTerminalEngraving.py -- <output-directory>`. Copy the resulting `TerminalEngraving.json` into `Assets/RunicStorage/TerminalSource`. Both the full local pipeline and model preview use the same derived geometry. The importer rejects engraving data whose FBX hash is outdated.

For model-only renders, also copy `tools/PreviewTerminal.cs` into the Editor folder and invoke `RunicStorage.Build.BuildAssets.PreviewTerminal` with `-rsnOutput <local-output-directory>`. This imports the visual prefab and renders general, book, rear, pedestal and cover views without calling the plugin compiler or AssetBundle pipeline. The additional isolated-cover image hides the pages and stand only for material inspection.

## Pedestal rune and leather revision

The pedestal rune is cut into the stone by an exact Boolean on the single block beneath it. All four strokes are recessed by 4 mm, and their emissive faces sit 0.3 mm above the groove floor. The source FBX stays untouched. Blender ray checks measured the floor depth with a maximum error below 0.000001 m, and Unity verified the source coordinate mapping before replacing only the stone and pedestal rune meshes. The derived model has 3,806 triangles (452 stone triangles); the 3,710 count above describes the original v19 export.

The leather cover and spine now have a solid dark-brown albedo with low smoothness and an explicit non-metal mask. Silver has its own metal mask. The cover is visually checked separately from the pages and silver trim. These changes were rendered in Unity only; no plugin DLL or release package was built.
