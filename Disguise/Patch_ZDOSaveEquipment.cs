#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// ZDO.Save only feeds the disk (the network uses ZDO.Serialize, its only other writer).
///
/// The values it writes are NOT the live ones: they come from the snapshot cloned by
/// ZDOExtraData.PrepareSave() when the save starts. That snapshot therefore contains the
/// masked equipment keys (0), and EquipmentMask writes the real hashes back into it for
/// this ZDO only - the world file keeps your equipment, like ProtectSaveFile keeps the
/// Player prefab.
///
/// No swap to undo: the snapshot is discarded by ClearSave() at the end of the save, and
/// the live ZDO - network, game logic, your own view - is never touched.
/// </summary>
[HarmonyPatch(typeof(ZDO), nameof(ZDO.Save))]
internal static class Patch_ZDOSaveEquipment
{
    private static void Prefix(ZDO __instance)
    {
        try
        {
            EquipmentMask.UnhideForSave(__instance);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZDO.Save (prefix): {ex}");
        }
    }
}
