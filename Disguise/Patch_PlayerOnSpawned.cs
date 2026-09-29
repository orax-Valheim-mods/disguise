#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>Your player spawns (connect, respawn): apply the disguise immediately.</summary>
[HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
internal static class Patch_PlayerOnSpawned
{
    private static void Postfix(Player __instance)
    {
        try
        {
            if (__instance == null || __instance != Player.m_localPlayer)
            {
                return;
            }
            ZNetView? nview = Disguise.GetNView(__instance);
            if (nview == null)
            {
                return;
            }
            Disguise.ApplyTo(nview.GetZDO(), "spawn");
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch Player.OnSpawned: {ex}");
        }
    }
}
