#nullable enable
using System;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Publishes the gear of a HUMANOID costume in the equipment keys of its ZDO - the one
/// channel a machine running no DLL of ours draws a costume from, i.e. how a vanilla client
/// is served its armor and its weapon.
///
/// Why nothing reaches it today: EquipmentMask hides YOUR gear in those keys (a vanilla
/// client would glue your helmet, cape and sword to the creature rig), so they arrive at 0
/// and the costume is drawn bare - and the client's own game code cannot dress it either:
/// Humanoid.Start does roll the very same gear there (GiveDefaultItems, seeded by the ZDO's
/// s_seed) but the VisEquipment setters only write a ZDO FOR ITS OWNER, so that roll never
/// reaches the keys its own UpdateEquipmentVisuals reads back every frame.
///
/// What is done, on every machine serializing this ZDO - the owner publishing, the relaying
/// server finding the same values already there and changing nothing: each of the eight rows
/// a creature is drawn with (head, chest, legs, shoulder, both hands, utility, trinket - the
/// keys UpdateEquipmentVisuals reads before its `if (m_isPlayer)`) takes the costume's own
/// hash, rolled by GearSkin with the ZDO's own s_seed - the seed the game itself rolls that
/// costume with, so every machine agrees on one single pick. Before the first write of a
/// row, what the row held - YOUR gear, or an empty slot, which a 0 in cd_eq_* alone could
/// never be told from "nothing was ever put aside" - moves into our own cd_eq_* key and the
/// row is flagged in cd_eq_pub: Patch_VisEquipment restores from there and keeps drawing
/// YOUR gear on the real Player the modded machines build for you, and
/// EquipmentMask.UnhideForSave writes it into the snapshot, so saves keep the viking's
/// equipment and never the costume's. The two windows are nested with EquipmentMask's - it
/// opens first, closes last, so each one hands back the value it found (the PublishKey they
/// both write is saved and restored once per window, in that order).
///
/// ShowArmor and the weapons pair ([3 - Display]) decide WHAT is published, in the two
/// halves every piece of gear belongs to: the armor rows (the worn utility and trinket
/// with them) take the costume's own hash while ShowArmor is on, and nothing at all while
/// it is off - a WIELDED utility (the shamans' staffs, bound to a hand joint) being the
/// costume's weapon, it answers ShowWeapons with the hands instead; the two hand rows take
/// the weapon the costume is born with while ShowWeapons is on and WhoseWeapons says
/// "Creature" - or ONE item of that very pool, which the choice key
/// below hands over as well - YOUR items being exactly what every machine has to draw there
/// while it says "Player" (this window then leaves those rows alone, and EquipmentMask no
/// longer hides them either) - and nothing at all while ShowWeapons is off,
/// empty hands being what every screen has to draw. A
/// costume carrying no Humanoid is written by none of this: it has no gear of its own, and
/// the mask alone keeps its rows as they were.
///
/// The owner publishes that pair next to the mask (the reassert) - and the fixed weapon, if
/// WhoseWeapons names one - and a machine serializing
/// the same ZDO without an owner's value - the relaying server of a player running no DLL -
/// falls back on its own [3 - Display] config. The pair travels under one key, as the two
/// bits below, and so does the fixed weapon under its own (a normal Set each): moving any of
/// them bumps the DataRevision and the new
/// state reaches every machine with the next packet - while the swap itself is silent, its
/// values travelling inside the packet the revision already carries.
///
/// The roll is cached per prefab and seed: the lists of a prefab never move inside a
/// session, and one player is one seed. The variants and qualities are not published: a
/// creature is drawn from the ZDO's hashes alone, its VisEquipment reading the rest from the
/// prefab (UpdateEquipmentVisuals reads them inside `if (m_isPlayer)`).
/// </summary>
internal static class CostumeGear
{
    /// <summary>
    /// The owner's choice, as the bits below - what the costume wears, published next to
    /// the mask by Publish. It used to be a plain 0 (bare costume) / 1 (the gear it is born
    /// with), and an older build reading it only ever asks whether the value is 0 at all:
    /// both readings still land right, 1 now meaning "the armor at least" and 0 "nothing of
    /// it anywhere" - which is what an old build would draw anyway, since it cannot dress
    /// the hands of a costume whose weapons come from the player. The one answer an older
    /// build cannot follow is the empty-handed one (GearWeaponsOff, the newest bit): it
    /// reads it as "something is on", draws the armor its own bit speaks of and puts the
    /// creature's weapon in the hands - i.e. a build older than this one on the same
    /// connection shows those hands armed, never yours.
    /// </summary>
    private static readonly int CdGearKey = Key("cd_costumeGear");

    /// <summary>
    /// The ONE item the hands are held with while WhoseWeapons names one out of the
    /// creature's pool, as the item prefab's stable hash - 0 while the roll decides (the two
    /// words of that entry, or ShowWeapons off), and never published at all by an owner
    /// running no DLL, which is the -1 the readers below ask for.
    ///
    /// Why an answer of its own: the equipment keys a window hands out (BeginOutgoing) hold
    /// the roll only for the duration of ONE outgoing packet and are given straight back, so
    /// every machine serializing this ZDO rolls the weapon again for itself - and a forced
    /// one is nowhere inside that roll. The pair above can only say WHICH KIND of hands to
    /// draw (empty, the player's, the costume's own), which leaves the item itself nothing
    /// to travel in: this key is it. The owner decides once, every machine with this DLL
    /// writes the same hash into those keys, and a screen running no DLL at all never has
    /// to be told anything - the keys are what it reads. An older build of ours knows of no
    /// such key, reads nothing here and follows its own roll, which is exactly what it would
    /// draw anyway.
    /// </summary>
    private static readonly int CdWeaponKey = Key("cd_whoseWeapon");

    /// <summary>The bits CdGearKey carries, one answer of the [3 - Display] gear options.</summary>
    private const int GearArmor = 1;

    /// <summary>The hands hold the LOCAL player's items (WhoseWeapons = "Player").</summary>
    private const int GearPlayerWeapons = 2;

    /// <summary>
    /// The hands hold nothing (ShowWeapons off). Never set together with the bit above: the
    /// pair has ONE answer for the hands - empty, the player's, or the creature's own (no
    /// bit) - and Choice keeps it that way.
    /// </summary>
    private const int GearWeaponsOff = 4;

    /// <summary>
    /// The rows this class re-dresses, as indices of EquipmentMask's arrays: the slots a
    /// creature is drawn with. The two back rows are missing on purpose - a creature is
    /// never drawn with them (their keys are read inside `if (m_isPlayer)`) - so those keys
    /// stay with the mask alone.
    /// </summary>
    private static readonly int[] s_rows =
    {
        EquipmentMask.RowHelmet,
        EquipmentMask.RowChest,
        EquipmentMask.RowLegs,
        EquipmentMask.RowCape,
        EquipmentMask.RowLeft,
        EquipmentMask.RowRight,
        EquipmentMask.RowUtility,
        EquipmentMask.RowTrinket,
    };

    // Active swap window, own buffer: this window is nested INSIDE the mask's outgoing one,
    // and each of the two hands back what it found - which is why the caller closes them in
    // the reverse order (Patch_ZDOSerialize).
    private static ZDO? s_activeZdo;

    private static int s_savedCount;

    private static readonly int[] s_savedKeys = new int[s_rows.Length * 2 + 1];

    private static readonly int[] s_savedValues = new int[s_rows.Length * 2 + 1];

    private static int Key(string name) => StringExtensionMethods.GetStableHashCode(name);

    // ------------------------------------------------------------------ owner's decision

    /// <summary>
    /// Writes the owner's choice into the local player ZDO, next to the mask (Disguise.cs,
    /// the reassert) - and, under its own key, the weapon the hands are fixed on when
    /// WhoseWeapons names one (0 being every other answer: the two words of that entry, the
    /// switch off, or a pool that holds no such item). The owner is the only machine allowed
    /// to decide, and the reassert publishes both every ReassertSeconds: ZDO.Set bumps the
    /// DataRevision only when the value actually changes, so the steady state is free - and
    /// the one time it does change is exactly when every machine has to be handed the new
    /// keys.
    /// </summary>
    internal static void Publish(ZDO? zdo)
    {
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }
        int value = Choice();
        if (zdo.GetInt(CdGearKey, -1) != value)
        {
            zdo.Set(CdGearKey, value);
        }
        int weapon = GearSkin.LocalWeapon();
        if (zdo.GetInt(CdWeaponKey, -1) != weapon)
        {
            zdo.Set(CdWeaponKey, weapon);
        }
    }

    /// <summary>The choice of THIS machine: what its own [3 - Display] options ask for.</summary>
    private static int Choice()
    {
        int value = 0;
        if (DisguiseConfig.ConfigShowArmor.Value)
        {
            value |= GearArmor;
        }
        if (!DisguiseConfig.ConfigShowWeapons.Value)
        {
            value |= GearWeaponsOff;
        }
        else if (DisguiseConfig.PlayerWeapons())
        {
            value |= GearPlayerWeapons;
        }
        return value;
    }

    /// <summary>
    /// The choice this ZDO's costume follows: the owner's published one, or this machine's
    /// own option when the owner runs no DLL and published nothing - the same rule the mask
    /// follows for its own keys.
    /// </summary>
    private static int Published(ZDO zdo)
    {
        int published = zdo.GetInt(CdGearKey, -1);
        return published < 0 ? Choice() : published;
    }

    /// <summary>
    /// One of the gear options moved while you are disguised: the new choice is published at
    /// once, which bumps the DataRevision, so every machine receives the costume's new keys
    /// with the next packet instead of keeping the state of the last one (the same rule as
    /// EquipmentMask.OnSettingChanged).
    /// </summary>
    internal static void OnSettingChanged()
    {
        Publish(GameAccess.GetLocalPlayerZDO());
    }

    // ------------------------------------------------------------------ swap window

    /// <summary>
    /// ZDO.Serialize: re-dresses the rows the costume is drawn with for the duration of the
    /// outgoing packet - every one of them but the hands while they are the player's own.
    /// Opens right AFTER the mask's own window (Patch_ZDOSerialize calls it in that order)
    /// and follows the same route - save what the row held, write, hand it back in End().
    /// Returns true when something was actually swapped - the caller must then call End().
    /// </summary>
    internal static bool BeginOutgoing(ZDO? zdo)
    {
        if (s_activeZdo != null || zdo == null || !zdo.IsValid())
        {
            return false;
        }
        // Most ZDOs stop here: this runs for every single ZDO put in an outgoing packet.
        if (!DisguiseApplier.HasMarker(zdo))
        {
            return false;
        }

        GearSkin.Gear? gear = Roll(zdo);
        if (gear == null)
        {
            // No humanoid: nothing a costume of that prefab could be born with, and no slot
            // of a creature to dress - the mask alone keeps that costume exactly as it was.
            return false;
        }
        int published = Published(zdo);
        bool armor = (published & GearArmor) != 0;
        bool playerWeapons = (published & GearPlayerWeapons) != 0;
        bool weaponsOff = (published & GearWeaponsOff) != 0;

        s_activeZdo = zdo;
        bool swapped = false;
        int pub = zdo.GetInt(EquipmentMask.PublishKey, 0);
        int previous = pub;
        try
        {
            for (int i = 0; i < s_rows.Length; i++)
            {
                int row = s_rows[i];
                bool hand = row == EquipmentMask.RowLeft || row == EquipmentMask.RowRight;
                if (hand && playerWeapons)
                {
                    // Your own weapons are what every machine has to draw in those two rows
                    // (WhoseWeapons = "Player"): the mask left them in the packet, and this
                    // window hands them over untouched - a row never touched keeps holding
                    // YOUR value, which is also what the save reads back from it.
                    continue;
                }
                int vanillaKey = EquipmentMask.s_vanillaKeys[row];
                int current = zdo.GetInt(vanillaKey, 0);
                // The hands keep the weapon the costume is born with whatever ShowArmor says
                // - that is the other half of the pair - and so does the utility slot while
                // what sits in it is WIELDED: bound to a hand joint it is the creature's own
                // weapon (the shamans' staffs, drawn by no other slot) and answers
                // ShowWeapons with the hands, not the armor. Every other row is the armor,
                // the worn utility and the trinket included (they are the rest of the gear,
                // not a weapon): nothing at all while ShowArmor is off, which is how a
                // costume is drawn bare. The hands are empty as well while ShowWeapons is
                // off, which is the one answer a costume with nothing in them can be drawn
                // with.
                int wanted;
                if (hand || (row == EquipmentMask.RowUtility && gear.UtilityWielded))
                {
                    wanted = weaponsOff ? 0 : Value(gear, i);
                }
                else
                {
                    wanted = armor ? Value(gear, i) : 0;
                }
                if (current == wanted)
                {
                    // Already what every screen has to draw - and a row never touched keeps
                    // holding YOUR value, which is what the save reads back from it.
                    continue;
                }
                if ((pub & (1 << row)) == 0)
                {
                    // The value this row held - yours, or the empty slot it may well be -
                    // goes aside first, and the flag says so: the readers can then tell an
                    // empty slot recorded from no record at all (see PublishKey).
                    Swap(zdo, EquipmentMask.s_overrideKeys[row], current);
                    pub |= 1 << row;
                }
                Swap(zdo, vanillaKey, wanted);
                swapped = true;
            }
            if (pub != previous)
            {
                // One write for the whole window, like the mask's.
                Swap(zdo, EquipmentMask.PublishKey, pub);
                swapped = true;
            }
        }
        catch (Exception ex)
        {
            End(); // never leave the local ZDO half swapped
            Disguise.LogError($"CostumeGear outgoing: {ex}");
            return false;
        }

        if (!swapped)
        {
            s_activeZdo = null;
        }
        return swapped;
    }

    /// <summary>Writes back every value saved by the last opened window.</summary>
    internal static void End()
    {
        ZDO? zdo = s_activeZdo;
        int count = s_savedCount;
        s_activeZdo = null;
        s_savedCount = 0;
        if (zdo == null || count == 0)
        {
            return;
        }
        try
        {
            for (int i = 0; i < count; i++)
            {
                GameAccess.SetZdoIntSilent(zdo, s_savedKeys[i], s_savedValues[i]);
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"CostumeGear restore: {ex}");
        }
    }

    private static void Swap(ZDO zdo, int key, int value)
    {
        // Capacity is one more than the rows allow: every row writes 2 values at most, plus
        // the PublishKey above.
        s_savedKeys[s_savedCount] = key;
        s_savedValues[s_savedCount] = zdo.GetInt(key, 0);
        s_savedCount++;
        GameAccess.SetZdoIntSilent(zdo, key, value);
    }

    // ------------------------------------------------------------------ the roll

    private static int s_cachedPrefab;

    private static int s_cachedSeed;

    private static int s_cachedForced;

    private static GearSkin.Gear? s_cachedGear;

    /// <summary>
    /// The gear that ZDO's costume is born with, one cache for the machine: the lists of a
    /// prefab never move inside a session and one player is one seed, so the usual case - the
    /// one player this machine serializes - is a pure hit. A second disguised player simply
    /// takes the cache over, which costs one roll of a handful of field reads per packet -
    /// cheap enough to stand the ping-pong, and always the same answer for that (prefab,
    /// seed, fixed weapon) triple, on every machine. The third is part of the key because it
    /// is not in the roll anywhere: it is read out of the owner's choice above (Forced),
    /// which is a value that can move while the other two stand still.
    /// </summary>
    private static GearSkin.Gear? Roll(ZDO zdo)
    {
        int prefabHash = zdo.GetPrefab();
        int seed = GearSkin.Seed(zdo);
        ZNetScene? zns = ZNetScene.instance;
        GameObject? prefab = zns != null ? zns.GetPrefab(prefabHash) : null;
        if (prefab == null)
        {
            // Nothing to dress that costume with - and nothing the cache could hold a
            // straight answer for either: a slot is only ever filled from a prefab this
            // machine really has, so it can never be left keyed on a roll it did not make.
            return null;
        }
        int forced = Forced(zdo, prefab);
        if (prefabHash == s_cachedPrefab && seed == s_cachedSeed && forced == s_cachedForced)
        {
            return s_cachedGear;
        }
        s_cachedPrefab = prefabHash;
        s_cachedSeed = seed;
        s_cachedForced = forced;
        s_cachedGear = GearSkin.Compute(prefab, seed, forced);
        return s_cachedGear;
    }

    /// <summary>
    /// The weapon that ZDO's costume's hands are fixed on: the owner's published hash (0
    /// while the roll decides), or - when the owner runs no DLL and published nothing at all
    /// - this machine's own entry, the very rule the bits above follow. Checked against THIS
    /// prefab's pool either way, so a choice that creature cannot hold answers the roll
    /// instead of drawing an item of another creature: a pool that moved under a value
    /// published before it did costs one packet of the roll, never one packet of a stray
    /// item (GearSkin.WeaponHash).
    /// </summary>
    private static int Forced(ZDO zdo, GameObject? prefab)
    {
        int published = zdo.GetInt(CdWeaponKey, -1);
        return published >= 0
            ? GearSkin.WeaponHash(prefab, published)
            : GearSkin.LocalWeaponHash(prefab);
    }

    /// <summary>The gear value of s_rows[position] (0 where the roll drew nothing).</summary>
    private static int Value(GearSkin.Gear gear, int position)
    {
        switch (position)
        {
            case 0:
                return gear.Helmet;
            case 1:
                return gear.Chest;
            case 2:
                return gear.Leg;
            case 3:
                return gear.Shoulder;
            case 4:
                return gear.Left;
            case 5:
                return gear.Right;
            case 6:
                return gear.Utility;
            default:
                return gear.TrinketHash;
        }
    }
}
