#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Disguise;

/// <summary>
/// The gear a humanoid costume is drawn with: the armor and the weapon a REAL creature of
/// that prefab is born with, on the inert copy the mod wears in your place. Both halves are
/// a setting of their own ([3 - Display]): ShowArmor takes the armor off the copy, and the
/// weapons pair decides its hands - whether it holds any at all (ShowWeapons) and, while it
/// does, whose they are: the creature's own (the default, on: a Fuling costume shows its
/// spear, like a Fuling in the world) or the ones the local player carries in both hands.
///
/// The game hands the gear out in two steps at spawn: Humanoid.Start -> GiveDefaultItems
/// walks the prefab's default items and one pick of each random pool (m_randomShield, then
/// m_randomWeapon, m_randomArmor, one m_randomSets set, then the m_randomItems list), and
/// MonsterAI.Start -> EquipBestWeapon takes the best weapon of that inventory into a hand.
/// The copy has neither of those scripts - the strip removes them, and they would need a
/// ZDO - so this class walks the very same lists in the very same order, with the very same
/// seeded pick (the ZDO's own s_seed, the value every machine rolls the costume with; a
/// copy with no ZDO falls back to the prefab's own name), and writes the very same slots on
/// the copy's VisEquipment: the slots Humanoid.EquipItem
/// would have filled, its own type switch replicated here, which are the fields
/// VisEquipment.UpdateEquipmentVisuals reads back when the ZDO is absent - exactly the
/// state of an inert copy, whose GhostNetView carries none (the else branch). VisEquipment
/// then draws the gear itself in Start and once per frame after, attaching each item to the
/// copy's own bones the way it does for a real creature: nothing of the attaching is
/// replicated here, it is the game's.
///
/// The same roll is what the machines running no DLL receive: CostumeGear publishes it in
/// the equipment keys of the ZDO, seeded the same way, so one single pick is what every
/// screen drawing that costume agrees on.
///
/// The roll is the answer of WhoseWeapons while it still holds one of its two words. The
/// third answer it takes is the NAME of one item of that very pool, and the hands are then
/// that item ALONE - the choice CostumeGear publishes next to the pair, so a machine running
/// no DLL of ours draws it as well. Weapons() is the pool such a name has to belong to
/// (the window's list and the console are built from it), and LocalWeaponHash is the one
/// answer every reader asks for it.
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
    /// <summary>The bits a look is made of: the halves of what a copy wears.</summary>
    internal const int LookArmor = 1;

    /// <summary>The hands hold the LOCAL player's items - the second answer of the pair.</summary>
    internal const int LookPlayerWeapons = 2;

    /// <summary>
    /// The hands hold nothing at all: ShowWeapons is off, so WhoseWeapons has nothing to be
    /// about and the copy is drawn with empty hands (a pristine copy holds 0 in those slots
    /// already, which is what makes this the same as writing nothing).
    /// </summary>
    internal const int LookNoWeapons = 4;

    /// <summary>
    /// What a copy of this prefab is drawn with, as those bits: the armor a humanoid
    /// costume is born with, and the hands - empty, the creature's own, or the local
    /// player's. The armor bit is
    /// ShowArmor answered as a bit - it is cleared for a prefab carrying no
    /// humanoid, since there is no gear of its own to draw on it, and that expensive test
    /// (the humanoid's six lists) is only ever made while ShowArmor is on: with the option
    /// standing where it starts, the whole answer is one field read.
    /// </summary>
    internal static int Look(GameObject? prefab)
    {
        int look = Options();
        if ((look & LookArmor) != 0 && !HasGear(prefab))
        {
            look &= ~LookArmor;
        }
        return look;
    }

    /// <summary>The same answer for SelfView, which holds a hash.</summary>
    internal static int Look(int prefabHash)
    {
        ZNetScene? zns = ZNetScene.instance;
        return Look(zns != null ? zns.GetPrefab(prefabHash) : null);
    }

    /// <summary>The same answer for GuiPreview, which holds a name.</summary>
    internal static int Look(string prefabName)
    {
        ZNetScene? zns = ZNetScene.instance;
        return Look(zns != null ? zns.GetPrefab(prefabName) : null);
    }

    /// <summary>
    /// The options alone, as the same bits - what every copy would be drawn with, before
    /// the prefab has been asked whether it carries any gear at all. This is the cheap set
    /// SelfView and GuiPreview compare every frame: a copy already standing is only rebuilt
    /// when the bits move, and the expensive half of the test above (Look) then runs once
    /// per move. The weapons pair answers as ONE of its three states: hands off (the switch
    /// alone, and the answer it leaves the other one out of), the local player's, or the
    /// creature's own - neither bit while it is on that last one, which is where the options
    /// start.
    /// </summary>
    internal static int Options()
    {
        int look = 0;
        if (DisguiseConfig.ConfigShowArmor.Value)
        {
            look |= LookArmor;
        }
        if (!DisguiseConfig.ConfigShowWeapons.Value)
        {
            look |= LookNoWeapons;
        }
        else if (DisguiseConfig.PlayerWeapons())
        {
            look |= LookPlayerWeapons;
        }
        return look;
    }

    /// <summary>
    /// Writes the gear slots on the copy's VisEquipment while the copy is STILL INACTIVE, so
    /// the component's own Start (UpdateVisuals) draws them the moment it is activated.
    /// Nothing has to remember what was written: a change of an option builds a new copy
    /// (SelfView, GuiPreview) rather than repairing this one, from the pristine prefab.
    ///
    /// What is written is the look the options ask for (Write): the armor of the creature
    /// while ShowArmor is on, and the hands - the weapon it is born with, the ones the local
    /// player holds in both hands, or nothing at all while ShowWeapons is off - nothing in
    /// any row the options took off either, which on a pristine copy is the same as not
    /// writing it. A humanoid without a single item, and a prefab
    /// with no humanoid at all, therefore end here with a handful of zero writes: no slot,
    /// nothing drawn, the same picture as before this class existed.
    /// </summary>
    internal static void Dress(GameObject clone, GameObject prefab, int seed)
    {
        try
        {
            Component? visEquipment = Named(clone, "VisEquipment");
            Gear? gear = Compute(prefab, seed, LocalWeaponHash(prefab));
            int look = Options();
            bool weaponsOff = (look & LookNoWeapons) != 0;
            bool armor = (look & LookArmor) != 0 && gear != null;
            bool playerWeapons = (look & LookPlayerWeapons) != 0;
            if (visEquipment == null || (gear == null && !playerWeapons))
            {
                // Nothing to draw on that copy: no gear to dress it with, hands that are
                // not the player's either - and empty hands (ShowWeapons off) with no gear
                // are already what a pristine copy stands at.
                return;
            }

            Write(visEquipment, gear, armor, playerWeapons, weaponsOff);

            // One line under Logs, so a bare costume can be read apart from a costume the
            // lists left bare: the seed the pick was rolled with, then every slot written.
            if (DisguiseConfig.ConfigLogs.Value && gear != null)
            {
                Disguise.LogInfo(
                    $"Gear: {prefab.name} (seed {seed}) -> "
                        + (
                            weaponsOff ? "no weapon, "
                            : playerWeapons ? "hands of the player, "
                            : $"right {gear.Right} (q{gear.RightQuality}), left {gear.Left}, "
                        )
                        + (
                            armor
                                ? $"chest {gear.Chest}, legs {gear.Leg}, helmet {gear.Helmet}, "
                                    + $"shoulder {gear.Shoulder}, utility {gear.Utility}, "
                                    + $"trinket {gear.Trinket ?? "-"}"
                                : "no armor"
                        )
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

    /// <summary>
    /// The gear a real creature of that prefab is born with - GiveDefaultItems' own lists,
    /// in GiveDefaultItems' own order - rolled with the seed the GAME itself uses: the
    /// s_seed of the ZDO, which Humanoid.Awake writes into every player ZDO from its uid.
    /// That is the very seed a machine running no DLL of ours rolls the costume with (its
    /// own Humanoid.Start runs GiveDefaultItems too, seeded the same way - and drawing
    /// nothing of it, because the VisEquipment setters only write a ZDO for its owner), so
    /// the pick published in the keys (CostumeGear) and the pick that machine's own game
    /// code computes are one and the same. A seed of 0 - the window's picture, a creature
    /// with no ZDO - falls back to the prefab's own name: one pick per creature, the same
    /// at every rebuild.
    ///
    /// Null when the prefab carries no humanoid at all: nothing to wear and nothing to
    /// publish, which is what CostumeGear asks for.
    ///
    /// forcedWeapon is the ONE item the hands are held with instead of the roll's own pair
    /// (WhoseWeapons at an item name), 0 leaving the roll alone. It is a hash already checked
    /// against THIS prefab's pool by whoever asked (LocalWeaponHash, CostumeGear), which is
    /// why nothing is asked again here - a hash nobody checked would draw an item of another
    /// creature, and one that resolves to nothing would silently leave the roll's pair in
    /// place of an answer the console just gave.
    /// </summary>
    internal static Gear? Compute(GameObject? prefab, int seed, int forcedWeapon)
    {
        if (prefab == null || !s_haveTypes)
        {
            return null;
        }
        Component? humanoid = Named(prefab, "Humanoid");
        if (humanoid == null)
        {
            return null;
        }

        Gear gear = new();
        // The game's own roll, seeded the way the game seeds it (see the summary above).
        // The global state is put back around it, exactly like GiveDefaultItems does.
        UnityEngine.Random.State state = UnityEngine.Random.state;
        UnityEngine.Random.InitState(
            seed != 0 ? seed : StringExtensionMethods.GetStableHashCode(prefab.name)
        );
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
        if (forcedWeapon != 0)
        {
            ForceHands(gear, forcedWeapon);
        }
        return gear;
    }

    /// <summary>
    /// The fixed weapon in place of the roll's own pair (WhoseWeapons at an item name): both
    /// hands are cleared first, then the one item goes through the very slot switch Give
    /// replicates - so it lands where Humanoid.EquipItem would have put it (a bow, a shield
    /// and a two-hander in the left hand, everything else in the right) and the other hand
    /// stands empty. Armor and every other row of the Gear are left exactly as the roll
    /// filled them: only the hands answer this option.
    /// </summary>
    private static void ForceHands(Gear gear, int itemHash)
    {
        ZNetScene? zns = ZNetScene.instance;
        GameObject? item = zns != null ? zns.GetPrefab(itemHash) : null;
        if (item == null)
        {
            return;
        }
        gear.Right = 0;
        gear.RightQuality = 0;
        gear.RightType = 0;
        gear.Left = 0;
        gear.LeftVariant = 0;
        gear.LeftQuality = 0;
        Give(gear, item);
    }

    /// <summary>
    /// The seed the game rolls THIS ZDO's creature with: Humanoid.Awake reads s_seed out of
    /// the ZDO and writes its own uid hash into it while the key is empty - the same value
    /// on every machine, the reason a costume's gear cannot disagree with itself. 0 when
    /// there is no ZDO to read (a picture, a probe).
    /// </summary>
    internal static int Seed(ZDO? zdo)
    {
        if (zdo == null || !zdo.IsValid())
        {
            return 0;
        }
        int seed = zdo.GetInt(ZDOVars.s_seed);
        return seed != 0 ? seed : zdo.m_uid.GetHashCode();
    }

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
    private static bool HasGear(GameObject? prefab)
    {
        Component? humanoid = prefab != null ? Named(prefab, "Humanoid") : null;
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

    // ------------------------------------------------------------------ the weapon pool

    /// <summary>The pool each prefab's names were found in (the lists never move in a session).</summary>
    private static readonly Dictionary<string, List<string>> s_weapons = new(
        StringComparer.Ordinal
    );

    /// <summary>
    /// The items that prefab's humanoid can be HELD with, one name each, in the order
    /// GiveDefaultItems walks them: its default items, then the shield, weapon and set pools,
    /// then the m_randomItems list.
    ///
    /// This is the list the window's WhoseWeapons is built from (besides its two words, which
    /// are dropped here so no entry can ever be told from them), the list the console prints
    /// and accepts, and the pool a fixed weapon has to belong to (WeaponHash). Cached by
    /// prefab NAME - the very key ZNetScene itself resolves prefabs by, so a cache slot can
    /// never be told from another prefab's. Null answers an empty one: no humanoid at all,
    /// which is a costume that can hold nothing.
    ///
    /// Three filters stand between the pools and this list, each read off the game's own
    /// rules: IsHeld keeps what the slot switch really puts in a hand (so armor, materials
    /// and ammo simply never appear, and the unarmed weapon is left out as well since
    /// GiveDefaultItems never gives it either), DrawnInHand keeps what a hand would really
    /// DRAW (an item no attach child stands for is an item no screen shows in a hand), and
    /// IsRealItem keeps what every player-facing screen could NAME and ICON - which takes
    /// the creatures' own attack items out: greydwarf_attack, charred_greatsword_thrust and
    /// every such prefab is one attack of the creature's set, listed for EquipBestWeapon to
    /// pick the situational one, and never shown in any player's UI.
    /// </summary>
    internal static IReadOnlyList<string> Weapons(GameObject? prefab)
    {
        if (prefab == null || !s_haveTypes)
        {
            return Array.Empty<string>();
        }
        if (s_weapons.TryGetValue(prefab.name, out List<string>? cached))
        {
            return cached;
        }

        List<string> found = new();
        Component? humanoid = Named(prefab, "Humanoid");
        if (humanoid != null)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            Take(humanoid, GameAccess.s_defaultItemsField, seen, found);
            Take(humanoid, GameAccess.s_randomShieldField, seen, found);
            Take(humanoid, GameAccess.s_randomWeaponField, seen, found);
            TakeSets(humanoid, seen, found);
            TakeItems(humanoid, seen, found);
        }
        s_weapons[prefab.name] = found;
        return found;
    }

    /// <summary>Every held item of one of GiveDefaultItems' GameObject[] pools.</summary>
    private static void Take(
        Component humanoid,
        FieldInfo? field,
        HashSet<string> seen,
        List<string> into
    )
    {
        foreach (
            GameObject item in Read<GameObject[]>(field, humanoid) ?? Array.Empty<GameObject>()
        )
        {
            Take(item, seen, into);
        }
    }

    /// <summary>Every held item of m_randomSets, one set after another.</summary>
    private static void TakeSets(Component humanoid, HashSet<string> seen, List<string> into)
    {
        object[]? sets = Read<object[]>(GameAccess.s_randomSetsField, humanoid);
        if (sets == null)
        {
            return;
        }
        foreach (object set in sets)
        {
            foreach (
                GameObject item in Read<GameObject[]>(GameAccess.s_itemSetItemsField, set)
                    ?? Array.Empty<GameObject>()
            )
            {
                Take(item, seen, into);
            }
        }
    }

    /// <summary>Every held item of m_randomItems - its prefabs, whatever the chance roll says.</summary>
    private static void TakeItems(Component humanoid, HashSet<string> seen, List<string> into)
    {
        object[]? items = Read<object[]>(GameAccess.s_randomItemsField, humanoid);
        if (items == null)
        {
            return;
        }
        foreach (object item in items)
        {
            Take(Read<GameObject>(GameAccess.s_randomItemPrefabField, item), seen, into);
        }
    }

    /// <summary>
    /// One pool entry into the list, if it is one the hands can hold AND draw at all -
    /// IsHeld first (the slot switch above really puts it in a hand), then DrawnInHand (a
    /// hand can really show it), then IsRealItem (it is a weapon of the world, not one of
    /// the creature's own attacks): the three tests of what this list calls a weapon.
    /// </summary>
    private static void Take(GameObject? item, HashSet<string> seen, List<string> into)
    {
        if (
            item == null
            || !IsHeld(item)
            || !DrawnInHand(item)
            || !IsRealItem(item)
            || !seen.Add(item.name)
        )
        {
            return;
        }
        if (
            string.Equals(
                item.name,
                DisguiseConfig.WeaponsCreature,
                StringComparison.OrdinalIgnoreCase
            )
            || string.Equals(
                item.name,
                DisguiseConfig.WeaponsPlayer,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return; // the two words of the pair keep their meaning alone
        }
        into.Add(item.name);
    }

    /// <summary>
    /// Whether that item ends up in one of the two hand slots - the slot switch below asked
    /// on a throwaway Gear (a weapon, a shield, a bow, a torch beside nothing, a tool), every
    /// armor piece and everything the switch has no slot for therefore answering false. The
    /// very test what would be DRAWN with, run over a prefab list once per creature: one
    /// Give of one item, a handful of field reads and no allocation but that one Gear.
    /// </summary>
    private static bool IsHeld(GameObject? item)
    {
        if (item == null || !s_haveTypes)
        {
            return false;
        }
        Gear scratch = new();
        Give(scratch, item);
        return scratch.Right != 0 || scratch.Left != 0;
    }

    /// <summary>
    /// Whether a hand would really DRAW that item - the very test VisEquipment.AttachItem
    /// makes when it dresses a hand slot: it looks for a child of the item prefab named
    /// "attach" (or "attach_skin", the two that slot takes) and returns nothing at all when
    /// it finds neither, so an item without either is an item no screen ever shows in a
    /// hand. (The creatures' own attack items are a second matter, kept out by IsRealItem
    /// just below: some of them carry an attach and draw their weapon that way, which is
    /// exactly how a charred knight shows its sword.) Nothing of the game is touched: this
    /// only walks the prefab's children, so the audit counts nothing for it either.
    /// </summary>
    private static bool DrawnInHand(GameObject item)
    {
        Transform root = item.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            string name = root.GetChild(i).gameObject.name;
            if (string.Equals(name, "attach", StringComparison.Ordinal))
            {
                return true;
            }
            if (string.Equals(name, "attach_skin", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether that item is a real item of the world rather than one of a CREATURE'S OWN
    /// ATTACKS. On every creature whose AI grew an attack set, each attack is an ItemDrop of
    /// its own listed in its Humanoid, and MonsterAI's EquipBestWeapon picks the
    /// situational one out of the inventory: greydwarf_attack is the greydwarf's claws,
    /// greydwarf_throw its thrown stone, charred_greatsword_thrust / _feint the charred
    /// knight's four greatsword attacks. No player's inventory, no shop, no catalogue and no
    /// hover will ever show one, so it carries neither the NAME nor the ICONS every item one
    /// could talk about has - while the weapon the costume should hold (greatsword, spear,
    /// club) carries both. The pool is therefore cut down to those, which also keeps the
    /// window's list free of the blank rows an unnamed entry would draw. Read the very way
    /// Give reads an item, through the two field handles GameAccess holds.
    /// </summary>
    private static bool IsRealItem(GameObject item)
    {
        if (!s_haveTypes)
        {
            return false;
        }
        Component? itemDrop = Named(item, "ItemDrop");
        object? itemData = Read(GameAccess.s_itemDropItemDataField, itemDrop);
        object? shared = Read(GameAccess.s_itemDataSharedField, itemData);
        string name = Read<string>(GameAccess.s_sharedNameField, shared, "");
        object? icons = Read(GameAccess.s_sharedIconsField, shared);
        return !string.IsNullOrEmpty(name) && icons is Array { Length: > 0 };
    }

    /// <summary>
    /// The stable hash of that item when this prefab's pool really holds it, 0 when it does
    /// not - the check every fixed weapon goes through, so a weapon chosen for one creature
    /// is never drawn (nor published) on another one that has no such item.
    /// </summary>
    internal static int WeaponHash(GameObject? prefab, int hash)
    {
        if (hash == 0)
        {
            return 0;
        }
        IReadOnlyList<string> pool = Weapons(prefab);
        for (int i = 0; i < pool.Count; i++)
        {
            if (StringExtensionMethods.GetStableHashCode(pool[i]) == hash)
            {
                return hash;
            }
        }
        return 0;
    }

    /// <summary>The same answer for a NAME, compared the way the two words of the pair are.</summary>
    internal static int WeaponHash(GameObject? prefab, string? name)
    {
        string? found = WeaponName(prefab, name);
        return found != null ? StringExtensionMethods.GetStableHashCode(found) : 0;
    }

    /// <summary>
    /// The name this prefab's pool really spells that argument with (the very string
    /// Weapons holds, so the .cfg, the window's list and the console all spell one item the
    /// same way), null when the pool holds no such item - the check the console makes before
    /// it writes a name into the entry, and the one the window's list is built to agree with.
    /// </summary>
    internal static string? WeaponName(GameObject? prefab, string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        IReadOnlyList<string> pool = Weapons(prefab);
        for (int i = 0; i < pool.Count; i++)
        {
            if (string.Equals(pool[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return pool[i];
            }
        }
        return null;
    }

    /// <summary>
    /// The weapon THIS machine fixes that costume's hands on: 0 while the roll decides -
    /// ShowWeapons off (there are no hands to fill at all), WhoseWeapons on the player's own
    /// items, WhoseWeapons still at Creature, or a name this prefab's pool does not hold.
    /// The one answer the window, the console, the copies drawn here (Dress) and the choice
    /// published on the wire (CostumeGear) all agree on.
    /// </summary>
    internal static int LocalWeaponHash(GameObject? prefab)
    {
        if (!DisguiseConfig.ConfigShowWeapons.Value || DisguiseConfig.PlayerWeapons())
        {
            return 0;
        }
        return WeaponHash(prefab, DisguiseConfig.FixedWeapon());
    }

    /// <summary>The same answer for the CONFIGURED creature - the value CostumeGear publishes.</summary>
    internal static int LocalWeapon() => LocalWeaponHash(PrefabResolver.CreaturePrefab());

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
            gear.TrinketHash = hash;
        }
        // Anything else - ammo above all - has no slot VisEquipment draws.
    }

    /// <summary>
    /// The slots onto the copy's VisEquipment: the fields its own fallback reads. The body
    /// (chest, legs, helmet, shoulder, utility, trinket) is the armor half and follows
    /// ShowArmor; the hands are the weapon half - empty while ShowWeapons is off, the
    /// costume's own while WhoseWeapons says "Creature", the local player's while it says
    /// "Player".
    /// </summary>
    private static void Write(
        Component visEquipment,
        Gear? gear,
        bool armor,
        bool playerWeapons,
        bool weaponsOff
    )
    {
        if (armor && gear != null)
        {
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

        if (weaponsOff)
        {
            // Empty hands, and the copy is built from the pristine prefab - whose slots hold
            // 0, which is exactly what an empty hand draws ("if they exist"). Nothing to
            // write, the same as any row the armor option took off.
            return;
        }

        if (playerWeapons)
        {
            WritePlayerHands(visEquipment);
        }
        else if (gear != null)
        {
            Set(GameAccess.s_visRightItemField, visEquipment, gear.Right);
            Set(GameAccess.s_visRightItemQualityField, visEquipment, gear.RightQuality);
            Set(GameAccess.s_visLeftItemField, visEquipment, gear.Left);
            Set(GameAccess.s_visLeftItemVariantField, visEquipment, gear.LeftVariant);
            Set(GameAccess.s_visLeftItemQualityField, visEquipment, gear.LeftQuality);
        }
    }

    /// <summary>
    /// The items the LOCAL player holds in both hands, written the way the game itself writes
    /// them into a player's own ZDO (the very keys VisEquipment reads back for a player): the
    /// copy then holds your axe and your shield where "Player" says every machine has to draw
    /// them. An empty hand is a 0 and draws nothing - "if they exist" - and the two BACK
    /// slots are left out on purpose: a creature is never drawn with them (their keys are
    /// read inside `if (m_isPlayer)`), so no costume of this mod can show one.
    ///
    /// The ZDO is the source because it is the one the other machines already draw you from:
    /// it keeps your real equipment while you are disguised (EquipmentMask only swaps it for
    /// the duration of an outgoing packet), so it is your current weapon at this very frame.
    /// </summary>
    private static void WritePlayerHands(Component visEquipment)
    {
        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }
        Set(GameAccess.s_visRightItemField, visEquipment, zdo.GetInt(ZDOVars.s_rightItem));
        Set(
            GameAccess.s_visRightItemQualityField,
            visEquipment,
            zdo.GetInt(ZDOVars.s_rightItemQuality)
        );
        Set(GameAccess.s_visLeftItemField, visEquipment, zdo.GetInt(ZDOVars.s_leftItem));
        Set(
            GameAccess.s_visLeftItemVariantField,
            visEquipment,
            zdo.GetInt(ZDOVars.s_leftItemVariant)
        );
        Set(
            GameAccess.s_visLeftItemQualityField,
            visEquipment,
            zdo.GetInt(ZDOVars.s_leftItemQuality)
        );
    }

    /// <summary>What one copy ends up wearing: a hash per slot, 0 where nothing is drawn.</summary>
    internal sealed class Gear
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

        internal int TrinketHash;
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
