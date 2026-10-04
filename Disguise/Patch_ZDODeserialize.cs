#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// Server: the OWNER raised (cd_refresh = 1) or removed the flag on its player ZDO ->
/// start / stop relaying that ZDO with a fake far-away position.
/// Runs inside ZDOMan.RPC_ZDOData, always BEFORE ZDOMan sends anything in that frame,
/// so the very packet carrying the new prefab is already relayed with the fake position.
/// Never armed on a client: a client always sends its real position.
/// </summary>
[HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
internal static class Patch_ZDODeserialize
{
    private static void Postfix(ZDO __instance)
    {
        try
        {
            if (__instance == null || !__instance.IsValid())
            {
                return;
            }
            if (DisguiseApplier.HasMarker(__instance))
            {
                // The health-bar relation is a display value for the VANILLA machines only
                // (AllyView): a modded machine renders a real Player here, whose bar is
                // already the ally one, so the flag is dropped instead of stored - which
                // also keeps it out of the save file.
                AllyView.ClearStored(__instance);
            }
            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                return;
            }
            if (__instance.GetInt(PrefabResolver.RefreshHash, 0) == 1)
            {
                ObserverRefresh.ArmObserverRefresh(__instance);
            }
            else if (ObserverRefresh.s_refreshZdo == __instance)
            {
                ObserverRefresh.DisarmObserverRefresh();
                Disguise.LogInfo(
                    $"Observer refresh flag cleared on ZDO {__instance.m_uid}: relaying the real position again."
                );
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZDO.Deserialize: {ex}");
        }
    }
}
