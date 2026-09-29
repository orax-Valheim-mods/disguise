#nullable enable
using System;
using HarmonyLib;
using UnityEngine;

namespace Disguise;

/// <summary>
/// On a modded machine, a disguised player ZDO must ALWAYS produce a real Player object
/// (the server needs it for game logic), even though its network prefab is a creature.
/// Only ZDOs marked by this mod are touched: other disguise mods are left alone.
/// </summary>
[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.CreateObject))]
internal static class Patch_CreateObject
{
    private static bool Prefix(ZDO zdo, ref GameObject __result)
    {
        try
        {
            if (!Disguise.HasMarker(zdo))
            {
                return true; // not our business (normal creature, other vanilla player...)
            }
            int playerHash = Disguise.PlayerPrefabHash;
            if (playerHash == 0 || zdo.GetPrefab() == playerHash)
            {
                return true; // already normal
            }
            // Disguised player ZDO: the disguise stays in the network data,
            // but the real Player prefab is built locally.
            __result = Disguise.InstantiateAs(zdo, playerHash);
            return false;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch CreateObject: {ex}");
            return true;
        }
    }
}
