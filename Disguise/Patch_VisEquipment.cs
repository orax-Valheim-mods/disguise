#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// VisEquipment.UpdateEquipmentVisuals reads the equipment hashes from the ZDO (every frame,
/// through IMonoUpdater.CustomUpdate) and renders those items on the model.
///
/// On a machine running this DLL the disguised player is instantiated as a real Player, but
/// the ZDO it carries holds 0 for the hidden keys (Patch_ZDOSerialize masked them): this
/// patch restores the real hashes (cd_eq_* keys) for the duration of the call, so modded
/// machines keep seeing your gear while vanilla clients keep seeing none.
///
/// Vanilla clients have no such patch, which is exactly the point. Only player objects are
/// considered (m_isPlayer), and the swap happens for a single call - no DataRevision bump,
/// nothing changes in the ZDO state.
/// </summary>
[HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.UpdateEquipmentVisuals))]
internal static class Patch_VisEquipment
{
    private static bool Prefix(VisEquipment __instance, out bool __state)
    {
        __state = false;
        try
        {
            if (__instance == null || !__instance.m_isPlayer)
            {
                return true;
            }
            ZDO? zdo = GameAccess.GetZDO(__instance);
            if (zdo == null)
            {
                return true;
            }
            __state = EquipmentMask.BeginVisibleValues(zdo);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch VisEquipment (prefix): {ex}");
        }
        return true;
    }

    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            EquipmentMask.End();
        }
        return __exception;
    }
}
