# Implementation Roadmap

This roadmap breaks the unified item and inventory framework into small Codex-sized milestones. Each milestone should compile independently and leave the project in a usable state.

## Milestone 1: Core Item Definitions And Database

Goal: Establish the authored item data foundation without implementing full inventory UI.

Deliverables:

- First-party folder and asmdef structure under `Assets/_Project`.
- `VRGame.Items` namespace.
- Stable ID value helpers or validation utilities.
- `ItemDefinition` ScriptableObject.
- `ItemDefinitionDatabase` ScriptableObject.
- Category path model.
- Stack policy model that enforces equipment unstackable and non-equipment stackable.
- Physical prefab and generated icon references on item definitions.
- Editor validation for duplicate IDs, missing prefabs, invalid stack policy, and invalid category paths.
- A few sample definitions for wood, stone, copper pickaxe, copper axe, and one melee weapon if placeholder prefabs are available.

Validation:

- Unity compile passes.
- Editor validation catches duplicate IDs.
- No runtime player save state stores ScriptableObject or prefab references.

## Milestone 2: Player Inventory State And Local Command Processor

Goal: Add serializable player inventory data and safe mutation APIs.

Deliverables:

- `PlayerInventoryState`.
- `InventoryStackRecord`.
- `EquipmentInstanceRecord`.
- Quantity wrapper or clear initial quantity representation.
- Local `InventoryCommandProcessor`.
- Commands for add stack, remove stack, create equipment instance, delete equipment instance, and query inventory.
- Unit/edit mode tests for stacking and unstackable equipment.

Validation:

- Unlimited inventory has no slot capacity checks.
- Stackable resources merge by definition ID.
- Equipment creates individual instance IDs.
- Tools have no durability fields.

## Milestone 3: Equipment Slots, Loadouts, And Stat Aggregation

Goal: Support Diablo-style equipment panel data and deterministic stat math.

Deliverables:

- `EquipmentSlotDefinition`.
- Slot catalog containing Head, Shoulders, Gauntlets, Chest, Leggings, Boots, Wings, Cape/Cloak, Amulet, and indexed Rings up to 10.
- `EquipmentLoadoutState`.
- Equip/unequip commands using slot IDs and ring indices.
- `StatDefinition` and stat operation records.
- Pure stat aggregation service.
- Tests for slot compatibility, ring indexing, and stat operation order.

Validation:

- Held usable items and equipped items remain conceptually separate.
- Weapon/tool use does not require occupying an armor/accessory slot.

## Milestone 4: Modifiers, Enchantments, And Generic Item Actions

Goal: Add data-driven equipment mutation without hard-coding one reforging path.

Deliverables:

- `ModifierDefinition`, `ModifierSetDefinition`, and modifier instance records.
- `EnchantmentDefinition`, `EnchantmentSetDefinition`, and enchantment instance records.
- `ItemActionPresetDefinition`.
- Generic action context/result DTOs.
- Effect handlers for reroll modifier, apply modifier, add enchantment, and upgrade enchantment.
- Tests for equipment-only modifier/enchantment restrictions.

Validation:

- Non-equipment resources/placeables cannot receive modifiers or enchantments.
- Modifier rerolling is deterministic from seed/context.
- Action definitions choose behavior; item instances do not know about reforging stations directly.

## Milestone 5: Hurricane VR World Item Adapter

Goal: Bridge logical item records to physical Hurricane-compatible prefabs.

Deliverables:

- Core-facing manifestation interfaces without Hurricane references.
- `VRGame.Integration.HurricaneVR` assembly/folder.
- `HurricaneWorldItemAdapter` component that references `HVRGrabbable`.
- Manifestation token/ID mapping.
- Spawn/return/drop lifecycle service.
- Basic prefab validation for Rigidbody, colliders, and `HVRGrabbable`.

Validation:

- Core inventory assembly has no Hurricane namespace imports.
- Physical object lifecycle resolves back to item IDs or manifestation IDs.

## Milestone 6: Manifestation Portal Prototype

Goal: Let a selected inventory item appear in hand through a VR portal interaction.

Deliverables:

- `ManifestationPortal` MonoBehaviour.
- Selected item view model/input binding stub.
- Grip-to-manifest request path.
- Held item state update path.
- Return-to-inventory and drop-to-world paths.

Validation:

- Manifesting an equipment item moves/reserves the specific instance.
- Manifesting a resource uses a quantity transaction.
- Equipping or dropping consumes the manifestation token to prevent duplication.

## Milestone 7: VR Inventory UI Foundation

Goal: Build the rough three-panel interaction model without final art polish.

Deliverables:

- Left panel category/list view model.
- Middle manifestation portal view.
- Right equipment panel with valid slots.
- Item detail display with icon, name, category, stats, modifiers, and enchantments.
- Basic filtering by category path.
- Equipment drop target validation.

Validation:

- UI emits commands; it does not mutate inventory state directly.
- Large inventories are virtualized or paged enough to avoid performance cliffs.

## Milestone 8: Melee Weapon Foundation

Goal: Start combat integration with melee weapons only.

Deliverables:

- Melee weapon item definitions.
- Weapon stat IDs such as melee damage, swing speed, reach, stagger, and crit chance.
- Hurricane-compatible melee weapon adapter hooks.
- Hit event bridge that can emit combat commands/events.
- Basic target damage interface independent of full RPG combat systems.

Validation:

- Melee weapons can be held as physical objects.
- Weapon stats resolve from definition plus instance modifiers/enchantments.
- No ranged, magic, or summoner scope creep in this milestone.

## Milestone 9: Harvesting Foundation

Goal: Add selective harvesting rules for tools and resources.

Deliverables:

- Harvesting domain/subtype definitions.
- Tool harvesting profile on item definitions.
- Harvestable target/resource definition.
- Material hardness score model.
- Harvest command/result.
- Tests for pickaxe/axe mismatch and hardness thresholds.

Validation:

- Pickaxes cannot mine wood.
- Axes cannot mine stone/metals.
- Effective hardness includes modifiers/treatments.

## Milestone 10: Editor Icon Generation

Goal: Generate inventory icons from physical prefabs.

Deliverables:

- Editor-only icon generation utility.
- Isolated preview scene/camera setup or prefab preview renderer.
- Batch generation for item definitions.
- Generated icon assignment workflow.
- Validation for missing icons.

Validation:

- Icon generation does not modify gameplay scenes.
- Physical prefab remains the source of both world appearance and inventory icon.

## Milestone 11: Building, Placeable, And Electrical Integration

Goal: Connect placeable inventory items to early factory/building systems.

Deliverables:

- Placeable item definitions.
- Placement command model.
- World placement preview adapter.
- Basic resource cost consumption.
- Initial hooks for machines, belts/pipes, power/electrical definitions, and recipes.

Validation:

- Placeables remain stackable by definition ID.
- World placement is command-driven and server-authority-ready.

## Milestone 12: Testing And Debug Utilities

Goal: Make the system easy to validate over many Codex sessions.

Deliverables:

- Edit mode tests for item IDs, stacks, equipment, stat aggregation, modifiers, enchantments, and harvesting.
- Debug inventory window or console commands.
- Item database validation menu.
- Sample data smoke-test scene or prefab isolated from vendor demo scenes.
- Save/load round-trip tests for DTOs.

Validation:

- Core tests run without opening gameplay scenes.
- Validators report actionable errors with asset paths.
- Debug utilities cannot silently bypass core command validation.
