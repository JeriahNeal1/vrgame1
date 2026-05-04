using System;

namespace VRGame.Items
{
    [Flags]
    public enum ItemFlags
    {
        None = 0,
        Resource = 1 << 0,
        Equipment = 1 << 1,
        Weapon = 1 << 2,
        Armor = 1 << 3,
        Accessory = 1 << 4,
        Tool = 1 << 5,
        Placeable = 1 << 6,
        Consumable = 1 << 7,
        Material = 1 << 8,
        Electrical = 1 << 9,
        CanBeHeld = 1 << 10,
        CanBeManifested = 1 << 11,
        CanBeEquipped = 1 << 12,
        CanBeSocketed = 1 << 13,
        CanBeCrafted = 1 << 14,
        CanBeHarvested = 1 << 15
    }
}
