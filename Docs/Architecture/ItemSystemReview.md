# Item System Review

Date: 2026-05-04

Scope: AGENTS guidance, architecture docs, and the implemented item definition, player inventory, equipment, stat, modifier, enchantment, VR manifestation, UI, melee, harvesting, icon generation, placeable, building, and electrical code under `Assets/_Project/Scripts`.

## Executive Summary

The first-party item system is still in a good shape for continued growth. Core player-owned state is ID/value based, Hurricane VR references are isolated to the integration assembly, and held-world item state is not conflated with equipment loadout state. The most important hardening work from this pass was around transaction safety: equip-from-world rollback, placement spawn rollback, duplicate wire rejection, and harvest drop preflight.

The largest remaining architectural risk is that local world objects are still doing work that a future MMO authority must own: dropped stack world records, manifestation reservations, placed objects, electrical wires, and harvested resources need explicit authoritative records before networked play.

## Review Findings

| Severity | Area | Status | Finding |
| --- | --- | --- | --- |
| High | Manifestation/equipment | Fixed | `VRInventoryUIController.NotifyHeldItemReleaseOverSlot` equipped before resolving the manifestation token and only logged if commit failed. It now rolls back the slot and restores the instance to `HeldInWorld` if manifestation commit fails. |
| High | Harvesting | Fixed | `Harvestable` granted drops one by one. A bad later drop could fail after earlier drops were already added, leaving the target unharvested and repeatable. Drops are now preflighted before stack mutation. |
| Medium | Placement | Fixed | `ItemPlacementService.TryPlace` consumed inventory before spawning and refunded on exceptions, but a post-instantiate exception could leave a placed object in the world. It now destroys partial placed objects and clears snap occupancy during rollback. |
| Medium | Electrical wires | Fixed | `ElectricalConnectionRegistry` allowed duplicate node-to-node wire records when node limits permitted it. It now rejects duplicate connections in either direction, and the wire self-check covers inventory refund. |
| Medium | Equipment lifecycle | Fixed | `EquipmentService.CanEquip` allowed non-terminal states such as `DroppedInWorld` and `ManifestingFromPortal`. It now allows equip only from `InInventory`, `HeldInWorld`, or `Equipped`. |
| Medium | Asset authoring | Improved | Core validation warned about missing prefabs/icons but not scene-object prefab references. ItemDefinition inspector and validation menu now warn when world/placed/preview prefab fields point at scene objects instead of persistent assets. |
| Medium | MMO authority | Documented | Dropped stack items, placed objects, wires, and harvested drops are local runtime objects/transactions today. Future server integration needs durable world item/placeable/electrical records and authoritative transaction IDs. |
| Medium | Manifestation reservations | Documented | `ManifestationReservationStore` serializes reservation records but keeps world object bindings in a nonserialized dictionary. This is correct for local session runtime, but domain reload/save/load needs reconciliation rules. |
| Medium | Unlimited inventory performance | Documented | Inventory stack and instance lookup are currently linear list scans, and the VR UI rebuilds visible rows on refresh. Acceptable for prototypes, but large factory inventories need indexing, paging, or virtualization. |
| Low | Stat aggregation | Documented | `StatAggregator` is deterministic and clears dirty flags on request, but callers must avoid recalculating every frame. Add cached stat services before frequent combat/harvest queries scale up. |
| Low | Modifier/enchantment validation | Documented | Equipment-only validation exists and conflict checks work, but future pools/actions need stronger editor validation for empty pools, unreachable definitions, and roll range/stat source consistency. |
| Low | Unity tests | Documented | Current coverage is editor menu self-checks rather than Unity Test Framework edit-mode tests. These are useful smoke checks but should be formalized as tests before larger gameplay systems depend on them. |

## Architecture Checks

- Runtime save/network DTOs reviewed: `PlayerInventoryState`, `InventoryStackRecord`, `ItemInstanceState`, `EquipmentLoadoutState`, affix records, and held world references store IDs/value records rather than `ScriptableObject`, prefab, `GameObject`, `MonoBehaviour`, or Hurricane VR references.
- Runtime view/session caches reviewed: `WorldItemView.BoundDefinition`, `WorldItemView.BoundInstance`, and `InventoryUiEntry.ItemDefinition` are runtime-only references used for UI/session binding, not save state.
- Authored definition assets intentionally hold prefab references in `ItemDefinition` and `PlaceableProfile`. These should be persistent assets only, not scene instances.
- Hurricane VR references are isolated under `Assets/_Project/Scripts/Integrations/HurricaneVR`; core inventory, equipment, stats, modifiers, harvesting, and placement logic do not import Hurricane namespaces.
- No durability fields or assumptions were found in item instance, tool, harvesting, melee, or equipment runtime state.
- Inventory capacity remains unlimited in core state. Stack quantity uses `long` behind a value object, with comments preserving a future migration path for astronomical quantities.
- Held VR items and equipment remain separate. Held tools/weapons are manifested world items; equipment loadout is slot-to-instance ID state.
- Hard-coded item IDs found are in editor self-check/debug utilities, not gameplay runtime systems.

## Fixes Applied

- Added equip rollback on manifestation commit failure in `VRInventoryUIController`.
- Restricted equipment lifecycle validation to inventory, held, or already-equipped instances.
- Added harvest drop preflight to avoid partial drop grants.
- Added placement rollback cleanup for partially spawned objects and snap occupancy.
- Added duplicate wire connection rejection and exception-safe wire inventory refund.
- Expanded editor validation to warn on scene-object prefab references in item definitions.
- Expanded self-check coverage for dropped equipment rejection and duplicate wire rejection.
- Updated `AGENTS.md` with transaction rollback and prefab-reference conventions.

## Validation Performed

- Unity batchmode compile/import completed successfully with no compiler errors in `Logs/item_system_review_compile.log`.
- Inventory self-check passed, including the new dropped-equipment equip rejection.
- Manifestation self-check passed.
- VR inventory UI self-check passed.
- Melee combat self-check passed.
- Harvesting self-check passed.
- Building/placeable/electrical self-check passed, including the new duplicate wire rejection/refund check.
- Direct `dotnet build --no-restore` was attempted for first-party generated projects, but Unity's generated project assets under `Temp/obj` were absent. Unity batchmode is the authoritative validation path for this project.

## Remaining Risks

1. Add authoritative world records before MMO work: dropped stack items, dropped equipment instances, placed structures, wires, machine state, and harvested resource state should not be inferred from client scene objects.
2. Replace menu self-checks with Unity Test Framework edit-mode tests for core DTOs/services and play-mode tests for runtime adapters.
3. Add indexed inventory lookup or a state-side cache before large inventories become common.
4. Add UI virtualization/paging for the left inventory panel before thousands of stack/instance rows are expected.
5. Add editor validators for affix/enchantment pools, unreachable definitions, duplicate stat source IDs, and invalid roll ranges.
6. Add save/load round-trip tests to prove runtime state remains Unity-object free.
7. Define domain reload behavior for active manifestation reservations and spawned world items.
8. Give placed objects, electrical nodes, and wire connections stable authored/runtime IDs suitable for persistence and server reconciliation.

## Recommended Next Prompt

Implement Milestone 12 hardening tests for the item system. Read `AGENTS.md`, all architecture docs, and `Docs/Architecture/ItemSystemReview.md`. Convert the existing editor menu self-check scenarios into Unity Test Framework edit-mode tests where possible, covering stack operations, equipment lifecycle validation, modifier/enchantment restrictions, manifestation reservation return/drop/commit flows, harvest drop preflight, placement inventory rollback, duplicate wire rejection, and serialization safety that rejects Unity object references in runtime state. Keep tests isolated from scenes and vendor assets, do not add UI polish, and do not change gameplay architecture except for small fixes required to make the tests pass.
