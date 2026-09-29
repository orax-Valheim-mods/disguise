#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// World load (server / local world): re-applies the disguise to marked ZDOs, since the
/// protected save contains "Player".
/// Runs before opening to peers: no ZDO revision war.
/// </summary>
[HarmonyPatch(typeof(ZDO), nameof(ZDO.Load))]
internal static class Patch_ZDOLoad
{
    private static void Postfix(ZDO __instance)
    {
        try
        {
            if (ZNetScene.instance == null || __instance == null || !__instance.IsValid())
            {
                return;
            }
            // A pending observer refresh (cd_refresh) is transient: it must never survive
            // a load, otherwise the server would relay a fake position with nobody to clear it.
            _ = __instance.RemoveInt(Disguise.RefreshKey);
            if (Disguise.HasMarker(__instance))
            {
                Disguise.ApplyTo(__instance, "load");
                return;
            }
            // Older save without the marker: our creature prefab + the player field
            // means this is our disguised player.
            int playerHash = Disguise.PlayerPrefabHash;
            if (playerHash == 0 || __instance.GetPrefab() == playerHash)
            {
                return;
            }
            if (
                __instance.GetPrefab() == Disguise.CreaturePrefabHash
                && __instance.GetLong(ZDOVars.s_playerID, 0L) != 0L
            )
            {
                Disguise.ApplyTo(__instance, "load-legacy");
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZDO.Load: {ex}");
        }
    }
}
