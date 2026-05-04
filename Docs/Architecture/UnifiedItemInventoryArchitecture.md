# Unified Item And Inventory Architecture

This document defines the durable architecture for the unified item, inventory, resource, equipment, modifier, enchantment, harvesting, and VR manifestation framework.

## Goals

- Support a VR Factory Automation RPG with Terraria-style progression, Satisfactory-style building progression, Diablo-style item rolls, Skyrim-like equipment presentation, and Hurricane VR physical interaction.
- Keep authored game data in ScriptableObjects while keeping runtime player state serializable, ID-based, and ready for save/network transport.
- Make inventory unlimited, tools non-degradable, equipment unstackable, and non-equipment resources/placeables infinitely stackable.
- Keep Hurricane VR integration outside core item state.
- Preserve clear extension points for reforging stations, anvil/hammer smithing, gem-based enchantment upgrades, harvesting rules, and future server authority.

## Non-Goals For The First Pass

- Do not implement the full VR inventory UI art pass.
- Do not implement all combat styles; first gameplay target is melee weapons only.
- Do not build the complete factory/building/electrical system yet.
- Do not hard-wire item data into one scene or one player prefab.

## Authored Data Model

### ItemDefinition

`ItemDefinition` is a ScriptableObject that describes an item type. It is not a player-owned item instance.

Recommended fields:

| Field | Purpose |
| --- | --- |
| `definitionId` | Stable string/GUID-like ID used by saves, networking, databases, and references. |
| `displayName` | Localizable player-facing name. |
| `description` | Optional localizable description. |
| `categoryPath` | Hierarchical path, for example `Equipment > Tool > Mining > Pickaxe`. |
| `itemKind` | Broad behavior: Equipment, Resource, Placeable, Consumable, Quest, etc. |
| `stackPolicy` | Equipment is unstackable; non-equipment resources/placeables are infinitely stackable. |
| `physicalPrefab` | World/VR prefab used for manifestation and icon generation. |
| `generatedIcon` | Editor-generated 2D icon derived from the physical prefab. |
| `baseStats` | Data-driven stat values identified by stat IDs. |
| `equipmentProfile` | Present only for equipment; valid slots, grip/hand use tags, combat/tool metadata. |
| `harvestingProfile` | Present for tools/weapons that can harvest or damage harvestable targets. |
| `actionPresets` | Item use, reforge, enchant, socket/gem, harvest, or station interaction presets. |
| `tags` | Stable tags for search/filtering/compatibility, not save ownership. |

Every item/resource/placeable/equipment definition should reference a physical prefab. The prefab is the world counterpart and the source for generated inventory icons.

### ItemDefinitionDatabase

`ItemDefinitionDatabase` resolves stable IDs to authored definitions. It should be a ScriptableObject catalog with editor validation.

Responsibilities:

- Map `definitionId` to `ItemDefinition`.
- Detect duplicate IDs.
- Detect missing physical prefabs.
- Detect invalid category paths.
- Detect equipment definitions without equipment profiles.
- Detect non-equipment definitions that accidentally enable modifiers or enchantments.
- Provide read-only lookup APIs for runtime services.

Runtime state stores `definitionId`; it does not store a direct `ItemDefinition` reference.

### Supporting Definition Catalogs

Use ScriptableObject definitions for durable IDs and authored rules:

- `StatDefinition`
- `EquipmentSlotDefinition`
- `ModifierDefinition`
- `ModifierSetDefinition`
- `ModifierApplicationRuleDefinition`
- `EnchantmentDefinition`
- `EnchantmentSetDefinition`
- `HarvestingToolTypeDefinition`
- `HarvestableMaterialDefinition`
- `ItemActionPresetDefinition`

Each definition should have a stable ID, display data, validation rules, and data-only fields. Behavior is implemented by small runtime services that interpret these definitions.

## Runtime Player State Model

Runtime player inventory state must be serializable DTO/value data. It should be valid for local saves and future network replication.

Recommended root:

```csharp
[Serializable]
public sealed class PlayerInventoryState
{
    public string playerId;
    public long revision;
    public List<InventoryStackRecord> stacks;
    public List<EquipmentInstanceRecord> equipmentInstances;
    public EquipmentLoadoutState equipmentLoadout;
    public List<HeldItemState> heldItems;
}
```

### InventoryStackRecord

For stackable non-equipment items:

```csharp
[Serializable]
public sealed class InventoryStackRecord
{
    public string definitionId;
    public string quantity;
}
```

Use a string-backed quantity or a wrapped quantity type if truly huge stack counts become part of the economy. A `long` is acceptable for an initial implementation if the domain API does not expose slot or stack caps.

### EquipmentInstanceRecord

For unstackable equipment:

```csharp
[Serializable]
public sealed class EquipmentInstanceRecord
{
    public string instanceId;
    public string definitionId;
    public int rollSeed;
    public List<ModifierInstanceRecord> modifiers;
    public List<EnchantmentInstanceRecord> enchantments;
    public List<StatValueRecord> rolledStats;
    public ItemLifecycleState lifecycleState;
}
```

Equipment instances are the only items that can have modifiers or enchantments. The instance record stores IDs and rolled values only, never ScriptableObject or prefab references.

### EquipmentLoadoutState

Equipment loadout maps slot IDs to equipment instance IDs:

```csharp
[Serializable]
public sealed class EquipmentLoadoutState
{
    public List<EquippedSlotRecord> equippedSlots;
}

[Serializable]
public sealed class EquippedSlotRecord
{
    public string slotId;
    public int slotIndex;
    public string instanceId;
}
```

Use `slotIndex` for indexed slots such as rings. Ring count should be configured by slot definitions or loadout rules, up to 10.

### HeldItemState

Held VR items are temporary physical manifestations of inventory state:

```csharp
[Serializable]
public sealed class HeldItemState
{
    public string manifestationId;
    public string handId;
    public string definitionId;
    public string instanceId;
    public string quantity;
    public HeldItemSource source;
}
```

Held state should be enough to reconcile ownership, equip on valid slot release, return to inventory, drop to world, or destroy after consuming. The Hurricane VR object is an adapter around this record, not the record itself.

## Stackable Versus Unstackable Behavior

### Equipment

- Always unstackable.
- Stored as `EquipmentInstanceRecord`.
- Has a stable `instanceId`.
- Can hold modifiers, enchantments, rolled stats, binding state, and future ownership flags.
- Can be manifested into the hand without being equipped.
- Can be equipped by releasing the physical item over a valid equipment slot.

### Non-Equipment Resources And Placeables

- Infinitely stackable by `definitionId`.
- Stored as `InventoryStackRecord`.
- No per-item instance ID while in inventory.
- No modifiers or enchantments.
- Manifestation creates a transient `manifestationId` and optional reservation/transaction for the quantity represented by the physical object.

## Item Lifecycle States

Recommended lifecycle values:

- `InventoryStack`: stackable quantity is stored in inventory.
- `InventoryInstance`: unstackable equipment is stored in inventory but not equipped.
- `Manifesting`: an authority/local service has reserved the item and is spawning a physical object.
- `HeldWorldItem`: a Hurricane-compatible object is held by a player hand.
- `DroppedWorldItem`: a physical object exists in the world and can be picked up.
- `Equipping`: a transaction is moving a held equipment instance into a loadout slot.
- `Equipped`: an equipment instance is active in an equipment slot.
- `ConsumedOrDestroyed`: a terminal state used for audit/debug, not usually retained forever in player save data.

Transitions should be command-driven:

- Inventory to held through manifestation portal.
- Held to inventory through recall/stow/cancel.
- Held to dropped world item through release into the world.
- Held to equipped through valid slot drop.
- Equipped to inventory or held through unequip/manipulation interactions.
- Inventory/equipped to modified instance through reforge/enchant actions.

## Action Preset And Event Hook System

Actions should be data-driven so many future mechanics can mutate or inspect items without rewriting the core item model.

Recommended concepts:

- `ItemActionPresetDefinition`: authored recipe/action profile for use, reforge, enchant, socket, repair-like effects, station interaction, harvesting, and combat activation.
- `ActionConditionDefinition`: required item kind, category path, tool type, station ID, skill threshold, resource cost, gem type, or world context.
- `ActionEffectDefinition`: apply modifier, reroll modifier, upgrade enchantment, add enchantment, consume ingredient, produce item, adjust stat roll, harvest node, spawn world prefab.
- `ItemActionContext`: runtime value object containing actor ID, source item IDs, target item IDs, station ID, skill values, random seed, and world context.
- `ItemActionResult`: serializable result with changed instances, changed stack quantities, emitted events, and failure reasons.

The first implementation can use C# strategy classes keyed by action/effect IDs. The data chooses the action; code interprets it. Do not put reforging-only assumptions inside `EquipmentInstanceRecord`.

## Modifier System

Modifiers are Terraria-like rerollable affixes on equipment.

Recommended model:

- `ModifierDefinition`: stable ID, display name, tier, stat operations, allowed item categories, allowed equipment kinds, incompatibility tags.
- `ModifierSetDefinition`: weighted list or table used by a station/rule to pick valid modifiers.
- `ModifierInstanceRecord`: modifier ID, roll seed, rolled stat values if needed.
- `ModifierApplicationRuleDefinition`: controls how a modifier is added, replaced, improved, downgraded, or removed.

Rules:

- Only equipment instances can have modifiers.
- Modifiers should be replaceable through generic actions, not a hard-coded `Reforge` method on the item.
- Annealed/Hardened style treatments can be modeled as modifiers if they affect tool hardness/effective stats.
- Modifier application must be deterministic from action context, rule definition, item definition, player skill data, and random seed.

## Enchantment System

Enchantments are Diablo-like durable item powers on equipment.

Recommended model:

- `EnchantmentDefinition`: stable ID, display name, level range, stat operations, triggered effects, allowed categories, allowed gem families.
- `EnchantmentInstanceRecord`: enchantment ID, level, roll seed, rolled values.
- `EnchantmentSetDefinition`: weighted/curated list for gems, stations, scripted events, loot tables, or skill perks.
- `EnchantActionPresetDefinition`: data defining whether an action adds, upgrades, replaces, merges, or rejects enchantments.

Rules:

- Only equipment instances can have enchantments.
- Gem interactions should be actions that target an equipment instance and ingredient stack/instance.
- Enchantment upgrades should be data-driven and validated by station rules, gem type, current enchantment state, and player skill.

## Reforging, Anvil, And Gem Extension Points

### Reforging Station

Reforging is a generic modifier-change action:

- Input: equipment instance, station ID, cost, optional player skill/context.
- Rule: choose from a `ModifierSetDefinition`, reroll or improve according to station data.
- Output: updated equipment instance record and audit event.

### Anvil And Hammer Smithing

Future physical mechanic:

- Player places a tool/weapon on an anvil.
- Player strikes it with a hammer item.
- A station interaction service gathers source equipment instance, hammer definition/instance, station definition, hit quality, skill values, and random seed.
- A data-driven action applies, upgrades, or rerolls modifiers.

No item instance should know about "anvil" directly. The action context supplies station and physical interaction data.

### Gem Enchanting

Future physical mechanic:

- Player places or holds a gem near a tool/weapon.
- A gem action consumes or reserves a stack quantity.
- Rules determine allowed enchantment families and upgrade paths.
- The result updates the target equipment instance's enchantment records.

## Stat Aggregation Model

Use stable stat IDs and deterministic aggregation.

Recommended stat sources, in order:

1. Base stats from `ItemDefinition`.
2. Rolled instance stats from `EquipmentInstanceRecord`.
3. Modifier stat operations.
4. Enchantment stat operations and triggered passives.
5. Equipped loadout bonuses.
6. Player skill/perk bonuses.
7. Temporary buffs/debuffs.

Recommended operation types:

- `SetBase`
- `AddFlat`
- `AddPercentOfBase`
- `Multiply`
- `OverrideFinal`
- `ClampMinMax`

Keep aggregation deterministic and testable. The initial stat service should work without scene objects so it can be covered by edit mode tests.

Important stat IDs to plan for:

- `damage.melee`
- `attack.speed`
- `crit.chance`
- `tool.hardness.effective`
- `tool.harvest.speed`
- `tool.harvest.yield`
- `equipment.armor`
- `movement.speed`
- `magic.power`
- `summon.capacity`

## Harvesting, Tool Type, And Material Hardness

Harvesting is type selective and score-based.

Harvesting domains and subtypes:

- Combat: Melee, Magic, Summoner, Ranged
- Mining: Pickaxe, Drill
- Lumber: Axe, Chainsaw
- Construction/Architecture: Hammer, Jackhammer
- Fishing: Fishing Rod, Traps

Harvestable targets should have definitions or components that identify:

- `resourceDefinitionId`
- required harvesting domain
- required tool subtype or allowed subtype set
- material hardness score
- output item definition IDs and quantities
- hit/harvest action rules
- optional required enchantments/modifier tags

Tool definitions should identify:

- harvesting domain
- tool subtype
- base material hardness score
- base harvest speed
- base yield/bonus stats
- valid target tags or exclusions

Rules:

- Pickaxes cannot mine wood.
- Axes cannot mine stone or metals.
- Correct domain and subtype are required.
- Tool effective hardness must be greater than or equal to target material hardness.
- Annealed/Hardened treatments modify effective hardness or related stats through data-driven stat operations.
- Hardness is a game design score, not a strict real-world HRC simulation.

## Hurricane VR Integration Strategy

The physical object exists to represent an authoritative logical item record.

Recommended runtime boundaries:

- `IItemManifestationService`: core-facing service that turns item records into manifestation requests/results.
- `IWorldItemRegistry`: maps transient `manifestationId` to spawned world objects during a session.
- `IWorldItemAdapter`: component on physical prefabs that stores manifestation ID and exposes lifecycle callbacks.
- `HurricaneWorldItemAdapter`: Hurricane-specific implementation using `HVRGrabbable`, Rigidbody, colliders, hand poses, and sockets.
- `EquipmentSlotDropTarget`: Unity UI/VR target that validates slot compatibility and sends equip commands.
- `ManifestationPortal`: VR interaction component that requests a held item when grip is pressed over the portal.

Core item code should not import Hurricane namespaces. Direct Hurricane references belong in adapter components, prefab setup, or integration assemblies.

Milestone 5 implementation notes:

- Runtime, Hurricane-free Unity components live in `VRGame.Runtime`.
- `WorldItemIdentity` stores transient world/session identity: item definition ID, optional item instance ID, stack quantity, lifecycle state, owner ID, and manifestation request ID.
- `WorldItemView` binds an `ItemDefinition` and optional `ItemInstanceState` at runtime, exposes manifestation/grab/release/drop/return/destroy lifecycle events, and does not serialize permanent ScriptableObject save data.
- `ItemManifestationService` owns local manifestation reservations and performs stack reserve/remove, exact item instance state transitions, spawn, return, and drop.
- `ManifestationPortal` is a world-space foundation component that accepts a selected stack or item instance and asks the manifestation service to spawn it.
- `IVRHandItemSpawner` is the core-facing hand spawn facade. The default implementation only places prefabs at transforms.
- `VRGame.Integration.HurricaneVR.HurricaneVRHeldItemAdapter` is the Hurricane adapter. It detects `HVRGrabbable` and `TryGrab` through reflection so API/version differences degrade to transform spawning instead of breaking the core runtime assembly.

Physical prefab requirements:

- Rigidbody.
- Appropriate colliders.
- `HVRGrabbable` where the item can be held.
- Grip points and hand poses for equipment/tools/weapons.
- Optional socket metadata for equipment slot interactions or station placement.
- Stable prefab assignment in the corresponding `ItemDefinition`.

## VR Inventory UI Model

Target inventory layout:

1. Left panel: Bethesda-style category/list inventory.
2. Middle panel: item manifestation portal.
3. Right panel: Diablo-style equipment panel.

Interaction:

- Player selects an item on the left panel.
- Player presses grip on the portal.
- The item appears in the player's hand as a Hurricane VR grabbable object.
- Player releases the held physical item over a valid equipment slot.
- The physical object disappears and the equipment instance becomes equipped.

The UI should read inventory state through view models. It should not mutate inventory lists directly. UI actions should emit commands.

## Future MMO And Server Authority Strategy

Prepare for server authority now by using command/result APIs locally.

Recommended command examples:

- `AddStackCommand`
- `CreateEquipmentInstanceCommand`
- `ManifestItemCommand`
- `ReturnHeldItemCommand`
- `DropHeldItemCommand`
- `EquipHeldItemCommand`
- `UnequipItemCommand`
- `ApplyItemActionCommand`
- `HarvestHitCommand`

Each command should contain:

- actor/player ID
- source item IDs and quantities
- target IDs
- context IDs such as station ID, hand ID, world object ID, or resource node ID
- expected inventory revision where useful
- request/transaction ID

Each result should contain:

- success/failure code
- new revision
- changed stack records
- changed equipment instance records
- changed held/world records
- events for UI/audio/VFX/prediction reconciliation

Duping prevention principles:

- Manifestation should reserve, lock, or move the logical item before spawning a physical object.
- Equipping should consume/resolve the held manifestation token.
- Dropping should create a world item record that the authority owns.
- Reforging/enchanting should validate input ownership, station rules, and costs before changing item records.

## Initial Milestones

1. Core IDs, item definitions, database, stack policy, and validation.
2. Serializable player inventory state and local command processor.
3. Equipment slots, equipment instances, loadout records, and stat aggregation.
4. Modifier and enchantment definitions plus generic item action application.
5. Hurricane VR world item adapter and manifestation service.
6. Manifestation portal prototype.
7. VR inventory UI view models and rough three-panel UI.
8. Melee weapon foundation.
9. Harvesting foundation.
10. Editor icon generation pipeline.
11. Building/placeable/electrical integration.
12. Tests, validators, and debug utilities.
