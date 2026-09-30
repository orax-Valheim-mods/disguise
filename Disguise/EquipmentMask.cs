#nullable enable
using System;

namespace Disguise;

/// <summary>
/// Hides the equipped items of a disguised player from the other machines.
///
/// Why: VisEquipment.UpdateEquipmentVisuals() reads the equipment hashes of a ZDO
/// (HelmetItem, ChestItem, LegItem, ShoulderItem, LeftItem...) every frame and attaches
/// those items to the bones of whatever prefab is bound to the ZDO. On a vanilla client a
/// disguised player is a CREATURE, so your helmet, cape, leggings and weapons get glued to
/// the creature rig and show up at nonsense positions.
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

    private const int OptShoulder = 3;

    private const int OptHeld = 4;

    /// <summary>Game keys read by VisEquipment - one row per hidden item.</summary>
    private static readonly int[] s_vanillaKeys =
    {
        ZDOVars.s_helmetItem,
        ZDOVars.s_chestItem,
        ZDOVars.s_legItem,
        ZDOVars.s_shoulderItem,
        ZDOVars.s_leftItem,
        ZDOVars.s_rightItem,
        ZDOVars.s_leftBackItem,
        ZDOVars.s_rightBackItem,
    };

    /// <summary>Our own keys carrying the real hash while the vanilla key is hidden.</summary>
    private static readonly int[] s_overrideKeys =
    {
        Key("cd_eq_helmet"),
        Key("cd_eq_chest"),
        Key("cd_eq_leg"),
        Key("cd_eq_shoulder"),
        Key("cd_eq_left"),
        Key("cd_eq_right"),
        Key("cd_eq_leftback"),
        Key("cd_eq_rightback"),
    };

    /// <summary>Option that gates each row (index of the Opt* constants above).</summary>
    private static readonly int[] s_option =
    {
        OptHelmet,
        OptChest,
        OptLegs,
        OptShoulder,
        OptHeld,
        OptHeld,
        OptHeld,
        OptHeld,
    };

    // Active swap window. The game is single threaded and the two swap windows (outgoing
    // packet, rendered frame) never nest, but every patch still keeps its own __state and
    // only calls End() when its own Begin actually opened the window.
    private static ZDO? s_activeZdo;

    private static int s_savedCount;

    private static readonly int[] s_savedKeys = new int[s_vanillaKeys.Length * 2];

    private static readonly int[] s_savedValues = new int[s_vanillaKeys.Length * 2];

    private static int Key(string name) => StringExtensionMethods.GetStableHashCode(name);

    /// <summary>True as soon as one "hide" option is enabled.</summary>
    internal static bool IsAnyOptionEnabled()
    {
        return OptionEnabled(OptHelmet)
            || OptionEnabled(OptChest)
            || OptionEnabled(OptLegs)
            || OptionEnabled(OptShoulder)
            || OptionEnabled(OptHeld);
    }

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
            case OptShoulder:
                return DisguiseConfig.ConfigHideShoulder.Value;
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
        if (!DisguiseConfig.ConfigEnabled.Value || !IsAnyOptionEnabled())
        {
            return false;
        }

        s_activeZdo = zdo;
        bool swapped = false;
        try
        {
            for (int i = 0; i < s_vanillaKeys.Length; i++)
            {
                if (!OptionEnabled(s_option[i]))
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
        try
        {
            for (int i = 0; i < s_vanillaKeys.Length; i++)
            {
                int hidden = zdo.GetInt(s_overrideKeys[i], 0);
                if (hidden == 0 || hidden == zdo.GetInt(s_vanillaKeys[i], 0))
                {
                    continue; // not hidden, or already the visible value
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
    /// ZDO.Save: writes the real hashes into the snapshot the save reads. The live values
    /// are stored masked (that is the whole point of the mask), so without this the world
    /// file would record your player with no equipment at all - same reasoning as
    /// ProtectSaveFile for the prefab. Nothing to restore: the snapshot is discarded at the
    /// end of the save.
    /// </summary>
    internal static void UnhideForSave(ZDO? zdo)
    {
        if (zdo == null || !zdo.IsValid() || !DisguiseApplier.HasMarker(zdo))
        {
            return;
        }
        for (int i = 0; i < s_vanillaKeys.Length; i++)
        {
            int hidden = zdo.GetInt(s_overrideKeys[i], 0);
            if (hidden == 0)
            {
                continue; // nothing hidden in that slot
            }
            GameAccess.SetSaveInt(zdo, s_vanillaKeys[i], hidden);
        }
    }

    // ------------------------------------------------------------------ config binding

    /// <summary>
    /// A "hide" option changed while you are disguised: the other machines still hold the
    /// state of the last packet, so bump the DataRevision to send the current one now.
    /// </summary>
    internal static void OnSettingChanged()
    {
        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        if (zdo == null || !zdo.IsValid() || !DisguiseApplier.HasMarker(zdo))
        {
            return;
        }
        GameAccess.BumpDataRevision(zdo);
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
