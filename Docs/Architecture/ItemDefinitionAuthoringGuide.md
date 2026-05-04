# Item Definition Authoring Guide

Milestone 1 creates code only. No sample item assets were created because the repository does not yet have a first-party sample data convention.

## Manual Asset Creation

1. In Unity, create an item database with `Assets > Create > VRGame > Items > Item Definition Database`.
2. Create item definitions with `Assets > Create > VRGame > Items > Item Definition`.
3. Put first-party item data under `Assets/_Project/Data/Items` unless a later content convention replaces this.
4. Give each item a stable `ItemDefId`. Prefer GUID-like IDs or durable lowercase IDs that will not be renamed casually.
5. Use category path segments rather than a single loose string. Examples:
   - `Equipment > Weapon > Melee > True Melee`
   - `Equipment > Tool > Mining > Pickaxe`
   - `Resource > Natural`
   - `Placeable > Wall`
6. Assign item flags. Equipment should include `Equipment`; held weapons/tools should include `CanBeHeld` and `CanBeManifested`.
7. Leave stack policy as `DefaultByItemFlags` unless a future exception is intentional. Equipment resolves to unstackable; non-equipment resolves to infinitely stackable.
8. Assign a world prefab for manifestable items. The prefab will later be used by Hurricane VR adapters and icon generation.
9. Enable only the optional profiles that apply to the item.
10. Add item definitions to the item database and use `Tools > VRGame > Items > Validate Item Definitions`.

## Notes

- Do not add durability fields to tools.
- Held tools and weapons are inventory items manifested into hands, not equipment loadout slots by default.
- Modifier and enchantment pool references should only be assigned to equipment.
- Runtime player inventory state begins in Milestone 2 and must store IDs/value records, not references to these ScriptableObject assets.
