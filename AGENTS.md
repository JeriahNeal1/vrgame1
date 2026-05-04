# Codex Project Instructions

This repository is a Unity 6.4 HDRP VR game project for a VR Factory Automation RPG inspired by Terraria, Satisfactory, Diablo, and Skyrim. Future Codex sessions should treat this file as the durable project contract before making changes.

## Current Repository Facts

- Unity editor version: `6000.4.5f1` from `ProjectSettings/ProjectVersion.txt`.
- Render pipeline: HDRP package `17.4.0`; HDRP assets live under `Assets/Settings`.
- Primary build scene currently listed in build settings: `Assets/OutdoorsScene.unity`.
- Windows standalone graphics APIs are explicitly configured in `ProjectSettings/ProjectSettings.asset`; serialized order indicates Vulkan first, then D3D12, then D3D11.
- XR packages are present through Unity's VR feature set:
  - `com.unity.xr.management` `4.6.0`
  - `com.unity.xr.openxr` `1.16.1` through `Packages/packages-lock.json`
  - `com.unity.xr.oculus` `4.5.4`
  - `com.unity.xr.interaction.toolkit` `3.4.1`
  - `com.unity.inputsystem` `1.19.0`
- Imported vendor assets currently include:
  - `Assets/HurricaneVR`
  - `Assets/polyperfect`
  - `Assets/PlayMaker`
  - `Assets/Plugins/PlayMaker`
  - `Assets/XR`
  - `Assets/XRI`
- Existing assembly definitions are vendor-owned only:
  - `Assets/HurricaneVR/Framework/Scripts/HurricaneVR.Framework.asmdef`
  - `Assets/HurricaneVR/Framework/Editor/HurricaneVR.Editor.asmdef`
  - `Assets/polyperfect/Common/Polyperfect.Common.asmdef`
  - `Assets/polyperfect/Poly Universal Pack/Scripts/Polyperfect.Universal.asmdef`
- There is no first-party gameplay code namespace yet. Use the conventions below when adding project code.

## First-Party Folder And Namespace Convention

Use `Assets/_Project` for first-party game code and authored game assets. Do not place project scripts inside imported vendor folders.

Preferred folders:

- `Assets/_Project/Scripts/Core` for pure or mostly pure gameplay/domain code.
- `Assets/_Project/Scripts/Runtime` for Unity-facing runtime services and MonoBehaviours.
- `Assets/_Project/Scripts/Integrations/HurricaneVR` for Hurricane VR adapters/facades.
- `Assets/_Project/Scripts/Editor` for editor-only tooling, validation, and icon generation.
- `Assets/_Project/Data` for ScriptableObject databases, definitions, stat catalogs, modifier catalogs, and authored presets.
- `Assets/_Project/Prefabs` for first-party gameplay prefabs.
- `Assets/_Project/Scenes` for first-party scenes created after the initial template scene.
- `Assets/_Project/Tests` for edit mode and play mode tests when test infrastructure is added.

Preferred namespaces:

- `VRGame.Core` for serializable DTOs, item identity, stat math, and rules that should not depend on Unity scene objects.
- `VRGame.Items` for item definitions, item instances, inventories, equipment, modifiers, enchantments, and harvesting.
- `VRGame.Runtime` for Unity runtime services that bridge definitions/state to scene behavior.
- `VRGame.Integration.HurricaneVR` for Hurricane VR specific code.
- `VRGame.Editor` for editor utilities, validators, asset creation menus, and icon generation.

When adding asmdefs, keep dependency direction clean:

- Core/domain assemblies should not reference HurricaneVR, scene MonoBehaviours, or editor assemblies.
- Integration assemblies may reference HurricaneVR and the core/domain assemblies.
- Editor assemblies may reference runtime assemblies and UnityEditor only.

## Asset And Scene Safety

- Do not move, rename, or mass-edit imported asset folders such as `Assets/HurricaneVR`, `Assets/polyperfect`, `Assets/PlayMaker`, `Assets/XR`, or `Assets/XRI` unless the user explicitly asks.
- Do not modify scenes unless the task specifically requires scene work. Prefer prefab, ScriptableObject, and code changes first.
- Before touching a scene, check `git status --short` and verify whether the scene is already modified by the user or Unity import process.
- Never revert user or Unity-imported changes without explicit instruction.
- Keep project settings and package manifests stable unless the task is specifically about project setup, packages, or platform configuration.
- Do not store generated icons, databases, or test assets inside vendor folders.

## Core Item And Inventory Contract

Inventory and item work must follow these rules:

- Inventory capacity is truly unlimited. There are no slot limits, weight limits, or hard stack caps in core state.
- Tools do not have durability.
- Held VR items are separate from equipment. A sword, pickaxe, axe, or hammer can be an inventory item manifested into the hand without occupying armor/accessory equipment slots.
- Equipment is unstackable and exists as individual item instances with stable instance IDs.
- All non-equipment resources and placeables are infinitely stackable by item definition ID.
- Only equipment can have modifiers and enchantments.
- Modifiers are rerollable like Terraria reforging.
- Modifier and enchantment application must remain data-driven. Do not hard-code a single reforging path into item instances.
- Every item, resource, placeable, and equipment definition should reference a physical prefab for world manifestation and inventory icon generation.
- Runtime player inventory/save/network state must store IDs and serializable value records, not direct `ScriptableObject`, prefab, GameObject, `MonoBehaviour`, or Hurricane VR references.

## Definitions Versus Runtime State

Use ScriptableObjects for authored definitions and catalogs:

- `ItemDefinition`
- `ItemDefinitionDatabase`
- `EquipmentSlotDefinition`
- `StatDefinition`
- `ModifierDefinition`
- `ModifierSetDefinition`
- `EnchantmentDefinition`
- `HarvestingToolTypeDefinition`
- `HarvestableResourceDefinition`
- action/effect presets for reforging, smithing, gem enchanting, harvesting, and item use

Use serializable DTO/state classes for player data:

- `PlayerInventoryState`
- `InventoryStackRecord`
- `EquipmentInstanceRecord`
- `EquipmentLoadoutState`
- `HeldItemState`
- `ModifierInstanceRecord`
- `EnchantmentInstanceRecord`
- command/result records for inventory transactions

Runtime state may store stable IDs such as `definitionId`, `instanceId`, `modifierId`, `enchantmentId`, `statId`, and `equipmentSlotId`. Runtime state must resolve definitions through a database/service boundary when needed.

## MMO And Server Authority Readiness

Design inventory changes as commands validated by an authority, even while the first implementation is local-only:

- Prefer methods such as `TryApplyCommand(command, out result)` over direct public mutation.
- Include revision numbers or transaction IDs in state-changing requests where useful.
- Model manifestation, dropping, equipping, reforging, and enchanting as transactions so they can later be server-validated.
- Do not trust client-side physical objects as ownership proof.
- Avoid storing scene object IDs as item ownership. Use stable item instance IDs and explicit world spawn records.
- Keep deterministic stat aggregation and modifier rolls reproducible from serializable records plus authored definitions.
- Multi-step inventory/world actions such as manifestation, equip-from-world, placement, harvesting drops, and wire creation must preflight validation before mutation where practical and must roll back inventory/world side effects on failure.
- Runtime-only Unity bindings such as `WorldItemView.BoundDefinition` may cache resolved definitions for the current session, but these caches must stay nonserialized and must never become save/network state.

## Hurricane VR Integration Boundary

Hurricane VR is the physical interaction layer, not the source of truth for inventory data.

- Core inventory, equipment, stats, modifiers, enchantments, and harvesting logic must not directly depend on Hurricane VR types.
- Keep all direct references to types such as `HVRGrabbable`, `HVRSocket`, grabbers, hand poses, and Hurricane rigidbody player components inside `VRGame.Integration.HurricaneVR` or scene/prefab adapter components.
- Use adapter/facade interfaces for core-to-VR operations, for example `IItemManifestationService`, `IWorldItemAdapter`, `IHandItemSpawnService`, or `IEquipmentSlotDropTarget`.
- Physical item prefabs should be Hurricane-compatible: Rigidbody, colliders, `HVRGrabbable`, grip points/hand poses where needed, and optional socket metadata.
- The item definition references the physical prefab for spawning and editor icon generation, but save data stores only the item definition ID and item instance value records.
- Authored item definition prefab references must point to persistent prefab/model assets, not scene instances. Scene objects belong in runtime adapters, previews, or placed-world records.

## Equipment Slots

Initial equipment slots:

- Head
- Shoulders
- Gauntlets
- Chest
- Leggings
- Boots
- Wings
- Cape/Cloak
- Amulet
- Rings, configurable up to 10 indexed ring slots

Equipment slot definitions should be data-driven and identified by stable IDs. Do not bake ring count assumptions into item instances.

## Item Categories And Harvesting

Item categories must support hierarchical type/subtype paths such as:

- `Equipment > Weapon > Melee > True Melee`
- `Equipment > Armor > Head`
- `Resource > Natural`
- `Placeable > Wall`
- `Equipment > Tool > Mining > Pickaxe`
- `Equipment > Tool > Lumber > Axe`

Harvesting is type selective:

- Pickaxes and drills are for mining.
- Axes and chainsaws are for lumber.
- Hammers and jackhammers are for construction/architecture.
- Fishing rods and traps are for fishing.
- Combat begins with melee weapons only for the first gameplay implementation target.

Correct tool type and subtype are required. Tools can harvest resources only at or below their effective material hardness score. Treat material hardness as a game design score, not a real-world metallurgy simulation. Treatments such as `Annealed I/II/III` and `Hardened I/II/III` should be modeled as data-driven modifiers or effects that adjust effective stats.

## Validation And Compile Checks

- For documentation-only changes, verify file contents and `git status --short`.
- For code changes, prefer a Unity compile/check through the editor or batchmode if available in the environment. Report clearly if Unity cannot be launched.
- Do not assume generated `.csproj` files are authoritative for architecture decisions; Unity regenerates them.
- Add edit mode tests for pure item/stat/inventory rules as soon as code exists.
- Add validators for duplicate IDs, missing physical prefabs, missing icons, invalid category paths, invalid modifier/enchantment applicability, and invalid equipment slot references.

## Documentation Map

- Architecture: `Docs/Architecture/UnifiedItemInventoryArchitecture.md`
- Roadmap: `Docs/Architecture/ImplementationRoadmap.md`
- Risks and open questions: `Docs/Architecture/RisksAndOpenQuestions.md`

Update these docs when changing the system design in a durable way.
