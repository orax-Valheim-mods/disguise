#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Disguise;

/// <summary>
/// The gear a humanoid costume is drawn with: the armor and the weapon a REAL creature of
/// that prefab is born with, on the inert copy the mod wears in your place. On by default -
/// a Fuling costume shows its spear and its armor, like a Fuling in the world - and taken
/// off under one switch, HideCreatureGear (disguise gear).
///
/// The game hands the gear out in two steps at spawn: Humanoid.Start -> GiveDefaultItems
/// walks the prefab's default items and one pick of each random pool (m_randomShield, then
/// m_randomWeapon, m_randomArmor, one m_randomSets set, then the m_randomItems list), and
/// MonsterAI.Start -> EquipBestWeapon takes the best weapon of that inventory into a hand.
/// The copy has neither of those scripts - the strip removes them, and they would need a
/// ZDO - so this class walks the very same lists in the very same order, with the very same
/// seeded pick (the prefab's own name instead of the ZDO uid a copy can never have), and
/// writes the very same slots on the copy's VisEquipment: the slots Humanoid.EquipItem
/// would have filled, its own type switch replicated here, which are the fields
/// VisEquipment.UpdateEquipmentVisuals reads back when the ZDO is absent - exactly the
/// state of an inert copy, whose GhostNetView carries none (the else branch). VisEquipment
/// then draws the gear itself in Start and once per frame after, attaching each item to the
/// copy's own bones the way it does for a real creature: nothing of the attaching is
/// replicated here, it is the game's.
///
/// Nothing of the game is referenced: the components are found by NAME (HealthMask's rule),
/// the types and fields through GameAccess, the item type through the game's own enum
/// looked up by name, and the hashes are the game's stable hashes of the item prefab names -
/// the very ones SetupVisEquipment writes into a ZDO. A copy is dressed once, while it is
/// still inactive: a change of the option builds a new copy (SelfView, GuiPreview) rather
/// than repairing the standing one.
/// </summary>
internal static class GearSkin
{
    /// <summary>
    /// Whether a costume built from this prefab has to be drawn WITHOUT its gear
    /// (HideCreatureGear): asked at every build (GhostFactory.BuildInert) and whenever the
    /// option moves under a copy already standing (SelfView, GuiPreview). The option comes
    /// first on purpose: with it off - which is where it starts - the whole answer is one
    /// field read, and a prefab without a humanoid is one name comparison.
    /// </summary>
    internal static bool HideGear(GameObject? prefab)
    {
        return prefab != null && DisguiseConfig.ConfigHideCreatureGear.Value && HasGear(prefab);
    }

    /// <summary>The same answer for SelfView, which holds a hash.</summary>
    internal static bool HideGear(int prefabHash)
    {
        ZNetScene? zns = ZNetScene.instance;
        return HideGear(zns != null ? zns.GetPrefab(prefabHash) : null);
    }

    /// <summary>The same answer for GuiPreview, which holds a name.</summary>
    internal static bool HideGear(string prefabName)
    {
        ZNetScene? zns = ZNetScene.instance;
        return HideGear(zns != null ? zns.GetPrefab(prefabName) : null);
    }

    /// <summary>
    /// Writes the gear slots on the copy's VisEquipment while the copy is STILL INACTIVE, so
    /// the component's own Start (UpdateVisuals) draws them the moment it is activated.
    /// Nothing has to remember what was written: a change of the option builds a new copy
    /// (SelfView, GuiPreview) rather than repairing this one, from the pristine prefab.
    ///
    /// A humanoid without a single item - most vanilla creatures, everything that is not a
    /// humanoid - ends here with a handful of zero writes: no slot, nothing drawn, the same
    /// picture as before this class existed.
    /// </summary>
    internal static void Dress(GameObject clone, GameObject prefab)
    {
        try
        {
            if (!s_haveTypes)
            {
                return;
            }
            Component? humanoid = Named(prefab, "Humanoid");
            Component? visEquipment = Named(clone, "VisEquipment");
            if (humanoid == null || visEquipment == null)
            {
                return;
            }

            Gear gear = new();
            // The game's own roll, seeded by the ZDO uid the creature is spawned with - a
            // value a copy has none of. The prefab's own name keeps the pick deterministic:
            // every copy of the same creature wears the same random gear, which is what a
            // creature whose look never changes looks like. The global state is put back
            // around it, exactly like GiveDefaultItems does.
            UnityEngine.Random.State state = UnityEngine.Random.state;
            UnityEngine.Random.InitState(StringExtensionMethods.GetStableHashCode(prefab.name));
            try
            {
                GiveAll(gear, Read<GameObject[]>(GameAccess.s_defaultItemsField, humanoid));
                GiveOne(gear, Read<GameObject[]>(GameAccess.s_randomShieldField, humanoid));
                GiveOne(gear, Read<GameObject[]>(GameAccess.s_randomWeaponField, humanoid));
                GiveOne(gear, Read<GameObject[]>(GameAccess.s_randomArmorField, humanoid));
                GiveSet(gear, Read<object[]>(GameAccess.s_randomSetsField, humanoid));
                GiveListed(gear, humanoid);
            }
            finally
            {
                UnityEngine.Random.state = state;
            }

            Write(visEquipment, gear);

            // One line under Logs, so a bare costume can be read apart from a costume the
            // lists left bare: the six list lengths of the prefab, then every slot written.
            if (DisguiseConfig.ConfigLogs.Value)
            {
                Disguise.LogInfo(
                    $"Gear: {prefab.name} lists "
                        + $"[{Length(Read<GameObject[]>(GameAccess.s_defaultItemsField, humanoid))} "
                        + $"{Length(Read<GameObject[]>(GameAccess.s_randomShieldField, humanoid))} "
                        + $"{Length(Read<GameObject[]>(GameAccess.s_randomWeaponField, humanoid))} "
                        + $"{Length(Read<GameObject[]>(GameAccess.s_randomArmorField, humanoid))} "
                        + $"{Length(Read<object[]>(GameAccess.s_randomSetsField, humanoid))} "
                        + $"{Length(Read<object[]>(GameAccess.s_randomItemsField, humanoid))}] -> "
                        + $"right {gear.Right} (q{gear.RightQuality}), left {gear.Left}, "
                        + $"chest {gear.Chest}, legs {gear.Leg}, helmet {gear.Helmet}, "
                        + $"shoulder {gear.Shoulder}, utility {gear.Utility}, "
                        + $"trinket {gear.Trinket ?? "-"}"
                );
            }
        }
        catch (Exception ex)
        {
            // A copy with no gear at all is how it was drawn before this class existed: the
            // costume keeps standing, only its armament is late.
            Disguise.LogWarning($"Gear: cannot dress the copy of {prefab.name}: {ex.Message}");
        }
    }

    /// <summary>The length of a list for the log line above (null reads as nothing there).</summary>
    private static int Length(System.Collections.ICollection? list) => list?.Count ?? 0;

    // ------------------------------------------------------------------ GiveDefaultItems

    /// <summary>The six lists GiveDefaultItems walks, in its own order.</summary>
    private static readonly FieldInfo?[] s_gearLists =
    {
        GameAccess.s_defaultItemsField,
        GameAccess.s_randomShieldField,
        GameAccess.s_randomWeaponField,
        GameAccess.s_randomArmorField,
        GameAccess.s_randomSetsField,
        GameAccess.s_randomItemsField,
    };

    /// <summary>Whether the prefab's humanoid carries any gear at all.</summary>
    private static bool HasGear(GameObject prefab)
    {
        Component? humanoid = Named(prefab, "Humanoid");
        if (humanoid == null)
        {
            return false;
        }
        foreach (FieldInfo? list in s_gearLists)
        {
            if ((Read(list, humanoid) as Array)?.Length > 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Every default item, like the loop GiveDefaultItems starts with.</summary>
    private static void GiveAll(Gear gear, GameObject[]? prefabs)
    {
        if (prefabs == null)
        {
            return;
        }
        foreach (GameObject prefab in prefabs)
        {
            Give(gear, prefab);
        }
    }

    /// <summary>One pick of a pool - the game's own roll over that pool.</summary>
    private static void GiveOne(Gear gear, GameObject[]? prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return;
        }
        Give(gear, prefabs[UnityEngine.Random.Range(0, prefabs.Length)]);
    }

    /// <summary>One set of m_randomSets, then all of its items (GiveDefaultItems' pick).</summary>
    private static void GiveSet(Gear gear, object[]? sets)
    {
        if (sets == null || sets.Length == 0)
        {
            return;
        }
        GiveAll(
            gear,
            Read<GameObject[]>(
                GameAccess.s_itemSetItemsField,
                sets[UnityEngine.Random.Range(0, sets.Length)]
            )
        );
    }

    /// <summary>
    /// The m_randomItems list, with the game's own test and its own one-per-slot rule: the
    /// first item that passes the roll for a given item type owns that slot, and the roll
    /// itself is the game's own - quirks included, it is what a creature in the world wears.
    /// </summary>
    private static void GiveListed(Gear gear, Component humanoid)
    {
        object[]? items = Read<object[]>(GameAccess.s_randomItemsField, humanoid);
        if (items == null || items.Length == 0)
        {
            return;
        }
        HashSet<int> filled = new();
        foreach (object item in items)
        {
            GameObject? prefab = Read<GameObject>(GameAccess.s_randomItemPrefabField, item);
            float chance = Read<float>(GameAccess.s_randomItemChanceField, item, 0.5f);
            if (prefab == null || !(UnityEngine.Random.value > chance))
            {
                continue;
            }
            int type = TypeOf(Read(GameAccess.s_itemDropItemDataField, Named(prefab, "ItemDrop")));
            if (type != Missing && filled.Add(type))
            {
                Give(gear, prefab);
            }
        }
    }

    // ------------------------------------------------------------------ the slot switch

    /// <summary>
    /// One item into the slot Humanoid.EquipItem would have put it in - its own type switch,
    /// replicated: the hands for a weapon, a shield or a torch, one slot each for the armor.
    /// A slot already taken keeps the newcomer, which is what the game's own re-equip ends
    /// up doing - the last one to arrive is the one drawn.
    /// </summary>
    private static void Give(Gear gear, GameObject? itemPrefab)
    {
        if (itemPrefab == null)
        {
            return;
        }
        Component? itemDrop = Named(itemPrefab, "ItemDrop");
        object? itemData = Read(GameAccess.s_itemDropItemDataField, itemDrop);
        int type = TypeOf(itemData);
        if (type == Missing)
        {
            return;
        }
        int hash = StringExtensionMethods.GetStableHashCode(itemPrefab.name);
        int variant = Read<int>(GameAccess.s_itemDataVariantField, itemData);
        int quality = Read<int>(GameAccess.s_itemDataQualityField, itemData, 1);

        if (type == s_oneHanded || type == s_twoHanded || type == s_tool)
        {
            gear.Right = hash;
            gear.RightQuality = quality;
            gear.RightType = type;
        }
        else if (type == s_twoHandedLeft || type == s_bow || type == s_shield)
        {
            gear.Left = hash;
            gear.LeftVariant = variant;
            gear.LeftQuality = quality;
        }
        else if (type == s_torch)
        {
            // The game's own rule: a torch goes BESIDE a one-handed weapon (the weapon keeps
            // the right hand), and with nothing in the right hand it is held like a tool.
            if (gear.RightType == s_oneHanded)
            {
                gear.Left = hash;
                gear.LeftVariant = variant;
                gear.LeftQuality = quality;
            }
            else
            {
                gear.Right = hash;
                gear.RightQuality = quality;
                gear.RightType = type;
            }
        }
        else if (type == s_chest)
        {
            gear.Chest = hash;
        }
        else if (type == s_legs)
        {
            gear.Leg = hash;
        }
        else if (type == s_helmet)
        {
            gear.Helmet = hash;
        }
        else if (type == s_shoulder)
        {
            gear.Shoulder = hash;
            gear.ShoulderVariant = variant;
            gear.ShoulderQuality = quality;
        }
        else if (type == s_utility)
        {
            gear.Utility = hash;
        }
        else if (type == s_trinket)
        {
            gear.Trinket = itemPrefab.name;
        }
        // Anything else - ammo above all - has no slot VisEquipment draws.
    }

    /// <summary>The slots onto the copy's VisEquipment: the fields its own fallback reads.</summary>
    private static void Write(Component visEquipment, Gear gear)
    {
        Set(GameAccess.s_visRightItemField, visEquipment, gear.Right);
        Set(GameAccess.s_visRightItemQualityField, visEquipment, gear.RightQuality);
        Set(GameAccess.s_visLeftItemField, visEquipment, gear.Left);
        Set(GameAccess.s_visLeftItemVariantField, visEquipment, gear.LeftVariant);
        Set(GameAccess.s_visLeftItemQualityField, visEquipment, gear.LeftQuality);
        Set(GameAccess.s_visChestItemField, visEquipment, gear.Chest);
        Set(GameAccess.s_visLegItemField, visEquipment, gear.Leg);
        Set(GameAccess.s_visHelmetItemField, visEquipment, gear.Helmet);
        Set(GameAccess.s_visShoulderItemField, visEquipment, gear.Shoulder);
        Set(GameAccess.s_visShoulderItemVariantField, visEquipment, gear.ShoulderVariant);
        Set(GameAccess.s_visShoulderItemQualityField, visEquipment, gear.ShoulderQuality);
        Set(GameAccess.s_visUtilityItemField, visEquipment, gear.Utility);
        if (gear.Trinket != null)
        {
            Set(GameAccess.s_visTrinketItemField, visEquipment, gear.Trinket);
        }
    }

    /// <summary>What one copy ends up wearing: a hash per slot, 0 where nothing is drawn.</summary>
    private sealed class Gear
    {
        internal int Right;

        internal int RightQuality;

        internal int RightType;

        internal int Left;

        internal int LeftVariant;

        internal int LeftQuality;

        internal int Chest;

        internal int Leg;

        internal int Helmet;

        internal int Shoulder;

        internal int ShoulderVariant;

        internal int ShoulderQuality;

        internal int Utility;

        internal string? Trinket;
    }

    // ------------------------------------------------------------------ the game's enum

    /// <summary>No item type resolved: Give stops before the slot switch.</summary>
    private const int Missing = int.MinValue;

    private static readonly bool s_haveTypes =
        GameAccess.s_itemTypeEnum != null && GameAccess.s_itemTypeEnum.IsEnum;

    // The ItemType values, read FROM THE ENUM ITSELF rather than written against it: a type
    // the game ever renumbers stays right, and a name it ever drops is a Missing, which
    // skips that item instead of putting it in the wrong slot.
    private static readonly int s_oneHanded = ItemType("OneHandedWeapon");

    private static readonly int s_twoHanded = ItemType("TwoHandedWeapon");

    private static readonly int s_twoHandedLeft = ItemType("TwoHandedWeaponLeft");

    private static readonly int s_bow = ItemType("Bow");

    private static readonly int s_torch = ItemType("Torch");

    private static readonly int s_tool = ItemType("Tool");

    private static readonly int s_shield = ItemType("Shield");

    private static readonly int s_helmet = ItemType("Helmet");

    private static readonly int s_chest = ItemType("Chest");

    private static readonly int s_legs = ItemType("Legs");

    private static readonly int s_shoulder = ItemType("Shoulder");

    private static readonly int s_utility = ItemType("Utility");

    private static readonly int s_trinket = ItemType("Trinket");

    private static int ItemType(string name)
    {
        try
        {
            return s_haveTypes ? (int)Enum.Parse(GameAccess.s_itemTypeEnum!, name) : Missing;
        }
        catch (ArgumentException)
        {
            Disguise.LogWarning($"Gear: no ItemType named \"{name}\".");
            return Missing;
        }
    }

    private static int TypeOf(object? itemData)
    {
        object? shared = Read(GameAccess.s_itemDataSharedField, itemData);
        object? type = Read(GameAccess.s_sharedItemTypeField, shared);
        if (type is int value)
        {
            return value;
        }
        // A boxed ENUM is never `is int` - its runtime type is the enum itself, and only an
        // unboxing cast accepts it - so the value goes through Convert, which unboxes it. A
        // null left by a rename stays the Missing below: the item is skipped, not slotted.
        return type is Enum ? Convert.ToInt32(type) : Missing;
    }

    // ------------------------------------------------------------------ reflection helpers

    /// <summary>
    /// The component of that NAME on the object's root, by NAME like everything the audit
    /// counts: a GetComponent&lt;Humanoid&gt;() would be one more game type in the number the
    /// README documents, and a rename is a null here, never an exception.
    /// </summary>
    private static Component? Named(GameObject gameObject, string name)
    {
        foreach (Component component in gameObject.GetComponents<Component>())
        {
            if (
                component != null
                && string.Equals(component.GetType().Name, name, StringComparison.Ordinal)
            )
            {
                return component;
            }
        }
        return null;
    }

    private static object? Read(FieldInfo? field, object? host)
    {
        return field != null && host != null ? field.GetValue(host) : null;
    }

    private static T Read<T>(FieldInfo? field, object? host, T fallback = default!)
    {
        object? value = Read(field, host);
        if (value is T typed)
        {
            return typed;
        }
        if (value != null && typeof(T).IsEnum)
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        return fallback;
    }

    private static void Set(FieldInfo? field, object host, int value) =>
        field?.SetValue(host, value);

    private static void Set(FieldInfo? field, object host, string value) =>
        field?.SetValue(host, value);
}
