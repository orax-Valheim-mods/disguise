#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// Save protection: we write "Player" to disk so the world does not become unusable if the
/// mod is ever removed. The disguise is re-applied on load.
/// The m_prefab field is written directly (not through SetPrefab) so the DataRevision is
/// NOT bumped: otherwise the server would fight with the client-driven ZDO.
/// (ZDO.Save only feeds the disk, the network uses ZDO.Serialize.)
/// </summary>
[HarmonyPatch(typeof(ZDO), nameof(ZDO.Save))]
internal static class Patch_ZDOSave
{
    private static bool Prefix(ZDO __instance, out int __state)
    {
        __state = 0;
        try
        {
            if (!DisguiseConfig.ConfigProtectSave.Value)
            {
                return true;
            }
            if (
                __instance == null
                || !__instance.IsValid()
                || !DisguiseApplier.HasMarker(__instance)
            )
            {
                return true;
            }
            int playerHash = PrefabResolver.PlayerPrefabHash;
            int current = __instance.GetPrefab();
            if (playerHash == 0 || current == playerHash)
            {
                return true;
            }
            if (GameAccess.s_zdoPrefabField == null)
            {
                return true;
            }
            __state = current;
            GameAccess.s_zdoPrefabField.SetValue(__instance, playerHash);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZDO.Save (prefix): {ex}");
            __state = 0;
        }
        return true; // let the original body run anyway
    }

    private static void Postfix(ZDO __instance, int __state)
    {
        if (__state == 0 || __instance == null)
        {
            return;
        }
        if (GameAccess.s_zdoPrefabField != null)
        {
            GameAccess.s_zdoPrefabField.SetValue(__instance, __state);
        }
    }
}
