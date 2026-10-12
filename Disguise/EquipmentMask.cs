#nullable enable
using System;

namespace Disguise;

/// <summary>
/// Hides the equipped items of a disguised player from the other machines.
///
/// Why: VisEquipment.UpdateEquipmentVisuals() reads the equipment hashes of a ZDO
/// (HelmetItem, ChestItem, LegItem, ShoulderItem - the cape - , LeftItem...) every frame
/// and attaches those items to the bones of whatever prefab is bound to the ZDO. On a
/// vanilla client a disguised player is a CREATURE, so your helmet, cape, leggings and
/// weapons get glued to the creature rig and show up at nonsense positions.
///
/// What is done - your own view never changes, because the local ZDO keeps the real hashes:
///  - ZDO.Serialize (network only: ZDOMan.SendZDOs is its only caller) swaps the enabled
///    keys for the duration of the packet: the vanilla key carries 0 and the real hash moves
///    into our own cd_eq_* key. Vanilla machines then render nothing, and the modded ones
///    read cd_eq_* back (see Patch_VisEquipment).
///  - ZDO.Save writes the real hashes into the snapshot the save reads from, so the world
///    file keeps your equipment (same idea as ProtectSaveFile).
///  - VisEquipment.UpdateEquipmentVisuals restores the real values around the call, so the
///    machines running this DLL keep rendering your gear.
///
/// WHO decides: the OWNER publishes its choice as a bitmask in its own ZDO (Disguise.HideKey,
/// written by PublishMask). The server relaying the packet follows that value instead of its
/// own [3 - Display] config, whose defaults (Hide* = true) would otherwise hide your gear no
/// matter what your client says. The local config is only the fallback for an owner running
/// no DLL at all (a disguise kept in the save, restored by the server alone).
///
/// A second writer shares exactly these keys: CostumeGear, which re-dresses the rows a
/// humanoid costume is DRAWN with for that same outgoing packet - the costume's own gear
/// where your slots would otherwise be hidden or empty. The two windows are nested (this one
/// opens first and closes last, each putting back what it found), and cd_eq_pub says which
/// rows hold a real value put aside - an EMPTY slot included, which a 0 in cd_eq_* alone
/// could never be told from "nothing was ever put aside". Both readers below go by that key:
/// the restore above, and the save.
///
/// Every swap goes through ZDOExtraData.Set, which does NOT bump the DataRevision, and the
/// save path writes straight into its snapshot: the ZDO state - and therefore the game
/// logic, the other packets and your own view - is never modified.
/// </summary>
internal static class EquipmentMask
{
    // The five settings, one index per option (see DisguiseConfig).
    private const int OptHelmet = 0;

    private const int OptChest = 1;

    private const int OptLegs = 2;

    private const int OptCape = 3;

    private const int OptHeld = 4;

    // The same options as the bitmask published in the ZDO under Disguise.HideKey.
    private const int BitHelmet = 1 << OptHelmet;

    private const int BitChest = 1 << OptChest;

    private const int BitLegs = 1 << OptLegs;

    private const int BitCape = 1 << OptCape;

    private const int BitHeld = 1 << OptHeld;

    /// <summary>
    /// Game keys read by VisEquipment - one row per hidden item. Utility and trinket are
    /// rows like the rest: UpdateEquipmentVisuals reads them for every entity, player or
    /// creature, so they would reach a costume too. CostumeGear re-dresses a subset of these
    /// rows (everything but the two back slots a creature is never drawn with).
    /// </summary>
    internal static readonly int[] s_vanillaKeys =
    {
        ZDOVars.s_helmetItem,
        ZDOVars.s_chestItem,
        ZDOVars.s_legItem,
        ZDOVars.s_shoulderItem,
        ZDOVars.s_leftItem,
        ZDOVars.s_rightItem,
        ZDOVars.s_leftBackItem,
        ZDOVars.s_rightBackItem,
        ZDOVars.s_utilityItem,
        ZDOVars.s_trinketItem,
    };

    /// <summary>Our own keys carrying the real hash while the vanilla key is hidden.</summary>
    internal static readonly int[] s_overrideKeys =
    {
        Key("cd_eq_helmet"),
        Key("cd_eq_chest"),
        Key("cd_eq_leg"),
        Key("cd_eq_cape"),
        Key("cd_eq_left"),
        Key("cd_eq_right"),
        Key("cd_eq_leftback"),
        Key("cd_eq_rightback"),
        Key("cd_eq_utility"),
        Key("cd_eq_trinket"),
    };

    /// <summary>Option that gates each row (index of the Opt* constants above).</summary>
    private static readonly int[] s_option =
    {
        OptHelmet,
        OptChest,
        OptLegs,
        OptCape,
        OptHeld,
        OptHeld,
        OptHeld,
        OptHeld,
        OptHeld,
        OptHeld,
    };

    // The rows, by index into the three arrays above, in their order. CostumeGear dresses a
    // subset of them, so a row has to name the same slot in both classes: these constants
    // are the shared vocabulary (the arrays stay in this order).
    internal const int RowHelmet = 0;

    internal const int RowChest = 1;

    internal const int RowLegs = 2;

    internal const int RowCape = 3;

    internal const int RowLeft = 4;

    internal const int RowRight = 5;

    internal const int RowLeftBack = 6;

    internal const int RowRightBack = 7;

    internal const int RowUtility = 8;

    internal const int RowTrinket = 9;

    /// <summary>
    /// Which rows hold a real value put aside - one bit per row of the arrays above, set by
    /// both windows: this one when it hides a value, CostumeGear when it re-dresses a row.
    /// An empty slot is put aside as 0 and FLAGGED too, because a 0 in cd_eq_* on its own
    /// could equally mean "nothing was ever put aside" - and both readers below would then
    /// leave the costume's gear on your back instead of your own gear (or nothing).
    /// </summary>
    internal static readonly int PublishKey = Key("cd_eq_pub");

    // Active swap window. The game is single threaded and the two swap windows (outgoing
    // packet, rendered frame) never nest, but every patch still keeps its own __state and
    // only calls End() when its own Begin actually opened the window.
    private static ZDO? s_activeZdo;

    private static int s_savedCount;

    private static readonly int[] s_savedKeys = new int[s_vanillaKeys.Length * 2];

    private static readonly int[] s_savedValues = new int[s_vanillaKeys.Length * 2];

    private static int Key(string name) => StringExtensionMethods.GetStableHashCode(name);

    // ------------------------------------------------------------------ display decision

    /// <summary>
    /// The equipment this machine asks the others to hide, as a bitmask: 0 when the disguise
    /// is off or every "hide" option is unchecked.
    /// </summary>
    internal static int BuildMask()
    {
        if (
            !DisguiseConfig.ConfigEnabled.Value
            || PrefabResolver.DesiredPrefabHash() == PrefabResolver.PlayerPrefabHash
        )
        {
            return 0;
        }

        int mask = 0;
        if (OptionEnabled(OptHelmet))
        {
            mask |= BitHelmet;
        }
        if (OptionEnabled(OptChest))
        {
            mask |= BitChest;
        }
        if (OptionEnabled(OptLegs))
        {
            mask |= BitLegs;
        }
        if (OptionEnabled(OptCape))
        {
            mask |= BitCape;
        }
        if (OptionEnabled(OptHeld))
        {
            mask |= BitHeld;
        }
        return mask;
    }

    /// <summary>
    /// Writes the choice into the local player ZDO. The owner is the only machine allowed to
    /// decide, and a reassert publishes it every ReassertSeconds: ZDO.Set bumps the
    /// DataRevision only when the value actually changes, so the steady state is free.
    /// </summary>
    internal static void PublishMask(ZDO? zdo)
    {
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }
        int mask = BuildMask();
        if (zdo.GetInt(Disguise.HideKey, -1) != mask)
        {
            zdo.Set(Disguise.HideKey, mask);
        }
    }

    /// <summary>The mask published by the owner, or -1 when the owner never published one.</summary>
    private static int OwnerMask(ZDO zdo) => zdo.GetInt(Disguise.HideKey, -1);

    private static bool OptionEnabled(int option)
    {
        switch (option)
        {
            case OptHelmet:
                return DisguiseConfig.ConfigHideHelmet.Value;
            case OptChest:
                return DisguiseConfig.ConfigHideChest.Value;
            case OptLegs:
                return DisguiseConfig.ConfigHideLegs.Value;
            case OptCape:
                return DisguiseConfig.ConfigHideCape.Value;
            default:
                return DisguiseConfig.ConfigHideHeldItems.Value;
        }
    }

    // ------------------------------------------------------------------ swap windows

    /// <summary>
    /// ZDO.Serialize: hides the enabled keys for the duration of the outgoing packet.
    /// Returns true when something was actually swapped - the caller must then call End().
    /// </summary>
    internal static bool BeginOutgoingMask(ZDO? zdo)
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
        // What gets hidden is the OWNER's decision (Disguise.HideKey). This machine - the
        // owner itself, the server relaying the packet, any observer - follows it, and only
        // falls back on its own config when the owner runs no DLL and published nothing.
        int mask = OwnerMask(zdo);
        if (mask < 0)
        {
            mask = BuildMask();
        }
        if (mask == 0)
        {
            return false;
        }

        s_activeZdo = zdo;
        bool swapped = false;
        int pub = zdo.GetInt(PublishKey, 0);
        int previous = pub;
        try
        {
            for (int i = 0; i < s_vanillaKeys.Length; i++)
            {
                if ((mask & (1 << s_option[i])) == 0)
                {
                    continue;
                }
                // A server relaying an already masked packet keeps 0 in the vanilla key:
                // the real hash is then the one stored in our own key.
                int real = zdo.GetInt(s_vanillaKeys[i], 0);
                if (real == 0)
                {
                    real = zdo.GetInt(s_overrideKeys[i], 0);
                }
                if (real == 0)
                {
                    continue; // nothing to hide in that slot
                }
                Swap(zdo, s_vanillaKeys[i], 0);
                Swap(zdo, s_overrideKeys[i], real);
                pub |= 1 << i;
                swapped = true;
            }
            if (pub != previous)
            {
                // One write for the whole window: those rows each hold a real value now,
                // which is what both readers below go by (PublishKey).
                Swap(zdo, PublishKey, pub);
                swapped = true;
            }
        }
        catch (Exception ex)
        {
            End(); // never leave the local ZDO half swapped
            Disguise.LogError($"EquipmentMask outgoing: {ex}");
            return false;
        }

        if (!swapped)
        {
            s_activeZdo = null;
        }
        return swapped;
    }

    /// <summary>
    /// VisEquipment: writes the real hashes back for the duration of the call, so that the
    /// machines running this DLL see the equipment again.
    /// Returns true when something was actually swapped - the caller must then call End().
    /// </summary>
    internal static bool BeginVisibleValues(ZDO? zdo)
    {
        if (s_activeZdo != null || zdo == null || !zdo.IsValid())
        {
            return false;
        }
        if (!DisguiseApplier.HasMarker(zdo))
        {
            return false;
        }

        s_activeZdo = zdo;
        bool swapped = false;
        int pub = zdo.GetInt(PublishKey, 0);
        try
        {
            for (int i = 0; i < s_vanillaKeys.Length; i++)
            {
                // Only rows that put a real value aside are restored - and that value may be
                // 0, an empty slot recorded by CostumeGear while its costume wears gear the
                // player has nothing of (see PublishKey).
                if ((pub & (1 << i)) == 0)
                {
                    continue;
                }
                int hidden = zdo.GetInt(s_overrideKeys[i], 0);
                if (hidden == zdo.GetInt(s_vanillaKeys[i], 0))
                {
                    continue; // already the visible value
                }
                Swap(zdo, s_vanillaKeys[i], hidden);
                swapped = true;
            }
        }
        catch (Exception ex)
        {
            End();
            Disguise.LogError($"EquipmentMask visible: {ex}");
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
            Disguise.LogError($"EquipmentMask restore: {ex}");
        }
    }

    /// <summary>
    /// ZDO.Save: writes the real hashes into the snapshot the save reads. On the machine
    /// saving the world, the live values are the masked ones and - while a humanoid costume
    /// is worn - the costume's own gear (both windows are the packet's alone): without this
    /// the world file would record your player with the equipment of the creature it is
    /// dressed as, or with none at all. Same reasoning as ProtectSaveFile for the prefab.
    /// Nothing to restore: the snapshot is discarded at the end of the save.
    /// </summary>
    internal static void UnhideForSave(ZDO? zdo)
    {
        if (zdo == null || !zdo.IsValid() || !DisguiseApplier.HasMarker(zdo))
        {
            return;
        }
        int pub = zdo.GetInt(PublishKey, 0);
        for (int i = 0; i < s_vanillaKeys.Length; i++)
        {
            if ((pub & (1 << i)) == 0)
            {
                continue; // nothing put aside in that slot: the snapshot already holds it
            }
            GameAccess.SetSaveInt(zdo, s_vanillaKeys[i], zdo.GetInt(s_overrideKeys[i], 0));
        }
    }

    // ------------------------------------------------------------------ config binding

    /// <summary>
    /// A "hide" option changed while you are disguised: publishing the new mask bumps the
    /// DataRevision, so the relaying server and the other clients receive the current
    /// equipment values right away instead of keeping the state of the last packet.
    /// </summary>
    internal static void OnSettingChanged()
    {
        PublishMask(GameAccess.GetLocalPlayerZDO());
    }

    // ------------------------------------------------------------------ internals

    private static void Swap(ZDO zdo, int key, int value)
    {
        // Capacity is 2 values per row and each Begin... writes at most 2 values per row.
        s_savedKeys[s_savedCount] = key;
        s_savedValues[s_savedCount] = zdo.GetInt(key, 0);
        s_savedCount++;
        GameAccess.SetZdoIntSilent(zdo, key, value);
    }
}
