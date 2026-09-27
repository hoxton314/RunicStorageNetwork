# Storage Codex model integration

Stable prefab ID: `RSN_RunicStorageTerminal`. Player-facing names: **Storage Codex** (English), **Кодекс запасов** (Russian).

This is a copy of the latest `export_v19` FBX and its numeric material specification, derived from the approved v18 authoring model. The existing Terminal prefab in RunicRelayPreview still contains v10 (11 groups); it is deliberately not the source for this integration. V19 has 13 groups and 3,710 triangles, including silver book trim and the red cloth border. Hashes in `source.json` identify the source snapshot.

The local asset pipeline copies these files into its separate UnityBuild project. `tools/BuildTerminalAssets.cs` imports the FBX with the original normals and scale, prepares materials, adds physical surface colliders, and adds the prefab and menu icon to the existing bundle. Source projects are not modified. The standalone DLL compile cannot generate this bundle.

The plugin registers a buildable piece in Hammer → Crafting. Its initial placement cost is 6 Stone and 4 Fine Wood at a workbench; balance is provisional while the model is reviewed. Placement, damage and destruction effects come from the vanilla stone floor. The piece does not yet open storage, join networks or supply resources. The existing core terminal UI stays attached to the core.

Per the user's request, **no Unity import, DLL build, asset bundle or test package has been produced for this model integration**. Geometry/material checks in the Editor recipe still need to run after the model is ready. Runtime lighting and placement also need in-game review.

## Material preparation

Wood, stone and iron reuse the verified native door, stone wall and iron-floor materials. The stone/wood UV mapping is prepared on build-owned mesh copies, with gentle normal strengths (0.25 and 0.35). Surface movement and noise are disabled to keep the geometric runes aligned.

Silver, leather, page surfaces/edges and both cloth colors use neutral copies of the native building shader with the exported colors, metallic values and roughness. They do not yet have new surface textures or normal maps. Directly copying the item leather or banner materials was avoided: the inspected game assets use Creature/Vegetation shaders. The three rune groups retain their original independent emission. `src/TerminalMaterials.cs` is shared with the Editor icon renderer so that its material assignments match the plugin.

## Hooks in the private asset pipeline

Copy this folder's FBX and `materials.json` into `Assets/RunicStorage/TerminalSource` in the isolated build project. Copy `tools/BuildTerminalAssets.cs` and `src/TerminalMaterials.cs` into its Editor folder alongside the existing partial `BuildAssets` sources. After `BuildRelay`, call `BuildTerminal(output)`; include `TerminalAsset` and `TerminalIcon` in the existing bundle's asset list. After reopening the bundle, call `ValidateTerminal(bundle, terminal, output)` and destroy the prepared terminal instance. These hooks have been added to the local pipeline, which remains excluded from this public source repository.
