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
/// The five [3 - Display] options are not consulted for a humanoid costume: they hid YOUR
/// gear from a costume that now wears its own. They keep their exact meaning on every other
/// costume, and on every machine that draws you as a player (Patch_VisEquipment).
///
/// HideCreatureGear decides WHAT is published: the owner's own option (published next to
/// the mask by the reassert), this machine's option on a relaying server whose owner runs no
/// DLL and published nothing. The flag is a normal Set, so toggling it bumps the
/// DataRevision and the new state reaches every machine with the next packet - while the
/// swap itself is silent, its values travelling inside the packet the revision already
/// carries.
///
/// The roll is cached per prefab and seed: the lists of a prefab never move inside a
/// session, and one player is one seed. The variants and qualities are not published: a
/// creature is drawn from the ZDO's hashes alone, its VisEquipment reading the rest from the
/// prefab (UpdateEquipmentVisuals reads them inside `if (m_isPlayer)`).
/// </summary>
internal static class CostumeGear
{
    /// <summary>
    /// The owner's choice: 1 (default) draws the gear the creature is born with, 0 the bare
    /// costume. Written into the local player ZDO by Publish, next to the mask.
    /// </summary>
    private static readonly int CdGearKey = Key("cd_costumeGear");

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
    /// the reassert). The owner is the only machine allowed to decide, and the reassert
    /// publishes it every ReassertSeconds: ZDO.Set bumps the DataRevision only when the
    /// value actually changes, so the steady state is free - and the one time it does change
    /// is exactly when every machine has to be handed the new keys.
    /// </summary>
    internal static void Publish(ZDO? zdo)
    {
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }
        int value = DisguiseConfig.ConfigHideCreatureGear.Value ? 0 : 1;
        if (zdo.GetInt(CdGearKey, -1) != value)
        {
            zdo.Set(CdGearKey, value);
        }
    }

    /// <summary>
    /// Whether this ZDO's costume is drawn with its gear: the owner's published choice, or
    /// this machine's own option when the owner runs no DLL and published nothing - the same
    /// rule the mask follows for its own keys.
    /// </summary>
    private static int Published(ZDO zdo)
    {
        int published = zdo.GetInt(CdGearKey, -1);
        return published < 0 ? (DisguiseConfig.ConfigHideCreatureGear.Value ? 0 : 1) : published;
    }

    /// <summary>
    /// The option moved while you are disguised: the new flag is published at once, which
    /// bumps the DataRevision, so every machine receives the costume's new keys with the
    /// next packet instead of keeping the state of the last one (the same rule as
    /// EquipmentMask.OnSettingChanged).
    /// </summary>
    internal static void OnSettingChanged()
    {
        Publish(GameAccess.GetLocalPlayerZDO());
    }

    // ------------------------------------------------------------------ swap window

    /// <summary>
    /// ZDO.Serialize: re-dresses the eight rows for the duration of the outgoing packet.
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
        bool bare = Published(zdo) == 0;

        s_activeZdo = zdo;
        bool swapped = false;
        int pub = zdo.GetInt(EquipmentMask.PublishKey, 0);
        int previous = pub;
        try
        {
            for (int i = 0; i < s_rows.Length; i++)
            {
                int row = s_rows[i];
                int vanillaKey = EquipmentMask.s_vanillaKeys[row];
                int current = zdo.GetInt(vanillaKey, 0);
                int wanted = bare ? 0 : Value(gear, i);
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

    private static GearSkin.Gear? s_cachedGear;

    /// <summary>
    /// The gear that ZDO's costume is born with, one cache for the machine: the lists of a
    /// prefab never move inside a session and one player is one seed, so the usual case - the
    /// one player this machine serializes - is a pure hit. A second disguised player simply
    /// takes the cache over, which costs one roll of a handful of field reads per packet -
    /// cheap enough to stand the ping-pong, and always the same answer for that (prefab,
    /// seed) pair, on every machine.
    /// </summary>
    private static GearSkin.Gear? Roll(ZDO zdo)
    {
        int prefabHash = zdo.GetPrefab();
        int seed = GearSkin.Seed(zdo);
        if (prefabHash == s_cachedPrefab && seed == s_cachedSeed)
        {
            return s_cachedGear;
        }
        ZNetScene? zns = ZNetScene.instance;
        GameObject? prefab = zns != null ? zns.GetPrefab(prefabHash) : null;
        s_cachedPrefab = prefabHash;
        s_cachedSeed = seed;
        s_cachedGear = GearSkin.Compute(prefab, seed);
        return s_cachedGear;
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
