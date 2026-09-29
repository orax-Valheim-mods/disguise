#nullable enable
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Harmony patches of the Disguise plugin.
///
/// This file is the patch half of the <c>Disguise</c> class (partial class): the patch
/// classes stay nested inside the plugin, so they keep access to its private state
/// (s_instance, s_savedZdo, s_inCreateDestroy, InstantiateAs, GetNView...) without
/// widening any visibility.
///
/// Every patch is defensive: it only ever touches ZDOs carrying the "cd_disguise"
/// marker, and any exception is caught and logged so a patch can never break the game.
/// </summary>
public partial class Disguise
{
    // ------------------------------------------------------------------ patches

    /// <summary>
    /// On a modded machine, a disguised player ZDO must ALWAYS produce a real Player object
    /// (the server needs it for game logic), even though its network prefab is a creature.
    /// Only ZDOs marked by this mod are touched: other disguise mods are left alone.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.CreateObject))]
    private static class Patch_CreateObject
    {
        private static bool Prefix(ZDO zdo, ref GameObject __result)
        {
            try
            {
                if (!HasMarker(zdo))
                {
                    return true; // not our business (normal creature, other vanilla player...)
                }
                int playerHash = PlayerPrefabHash;
                if (playerHash == 0 || zdo.GetPrefab() == playerHash)
                {
                    return true; // already normal
                }
                // Disguised player ZDO: the disguise stays in the network data,
                // but the real Player prefab is built locally.
                __result = InstantiateAs(zdo, playerHash);
                return false;
            }
            catch (Exception ex)
            {
                s_instance?.Logger.LogError($"Patch CreateObject: {ex}");
                return true;
            }
        }
    }

    /// <summary>
    /// World load (server / local world): re-applies the disguise to marked ZDOs, since the
    /// protected save contains "Player".
    /// Runs before opening to peers: no ZDO revision war.
    /// </summary>
    [HarmonyPatch(typeof(ZDO), nameof(ZDO.Load))]
    private static class Patch_ZDOLoad
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
                _ = __instance.RemoveInt(RefreshKey);
                if (HasMarker(__instance))
                {
                    ApplyTo(__instance, "load");
                    return;
                }
                // Older save without the marker: our creature prefab + the player field
                // means this is our disguised player.
                int playerHash = PlayerPrefabHash;
                if (playerHash == 0 || __instance.GetPrefab() == playerHash)
                {
                    return;
                }
                if (
                    __instance.GetPrefab() == CreaturePrefabHash
                    && __instance.GetLong(ZDOVars.s_playerID, 0L) != 0L
                )
                {
                    ApplyTo(__instance, "load-legacy");
                }
            }
            catch (Exception ex)
            {
                s_instance?.Logger.LogError($"Patch ZDO.Load: {ex}");
            }
        }
    }

    /// <summary>Your player spawns (connect, respawn): apply the disguise immediately.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    private static class Patch_PlayerOnSpawned
    {
        private static void Postfix(Player __instance)
        {
            try
            {
                if (__instance == null || __instance != Player.m_localPlayer)
                {
                    return;
                }
                ZNetView? nview = GetNView(__instance);
                if (nview == null)
                {
                    return;
                }
                ApplyTo(nview.GetZDO(), "spawn");
            }
            catch (Exception ex)
            {
                s_instance?.Logger.LogError($"Patch Player.OnSpawned: {ex}");
            }
        }
    }

    /// <summary>
    /// Save protection: we write "Player" to disk so the world does not become unusable if the
    /// mod is ever removed. The disguise is re-applied on load.
    /// The m_prefab field is written directly (not through SetPrefab) so the DataRevision is
    /// NOT bumped: otherwise the server would fight with the client-driven ZDO.
    /// (ZDO.Save only feeds the disk, the network uses ZDO.Serialize.)
    /// </summary>
    [HarmonyPatch(typeof(ZDO), nameof(ZDO.Save))]
    private static class Patch_ZDOSave
    {
        private static bool Prefix(ZDO __instance, out int __state)
        {
            __state = 0;
            try
            {
                if (!ConfigProtectSave.Value)
                {
                    return true;
                }
                if (__instance == null || !__instance.IsValid() || !HasMarker(__instance))
                {
                    return true;
                }
                int playerHash = PlayerPrefabHash;
                int current = __instance.GetPrefab();
                if (playerHash == 0 || current == playerHash)
                {
                    return true;
                }
                if (s_zdoPrefabField == null)
                {
                    return true;
                }
                __state = current;
                s_zdoPrefabField.SetValue(__instance, playerHash);
            }
            catch (Exception ex)
            {
                s_instance?.Logger.LogError($"Patch ZDO.Save (prefix): {ex}");
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
            if (s_zdoPrefabField != null)
            {
                s_zdoPrefabField.SetValue(__instance, __state);
            }
        }
    }

    /// <summary>
    /// Server: the OWNER raised (cd_refresh = 1) or removed the flag on its player ZDO ->
    /// start / stop relaying that ZDO with a fake far-away position.
    /// Runs inside ZDOMan.RPC_ZDOData, always BEFORE ZDOMan sends anything in that frame,
    /// so the very packet carrying the new prefab is already relayed with the fake position.
    /// Never armed on a client: a client always sends its real position.
    /// </summary>
    [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
    private static class Patch_ZDODeserialize
    {
        private static void Postfix(ZDO __instance)
        {
            try
            {
                if (__instance == null || !__instance.IsValid())
                {
                    return;
                }
                if (ZNet.instance == null || !ZNet.instance.IsServer())
                {
                    return;
                }
                if (__instance.GetInt(RefreshHash, 0) == 1)
                {
                    ArmObserverRefresh(__instance);
                }
                else if (s_refreshZdo == __instance)
                {
                    DisarmObserverRefresh();
                    s_instance?.Logger.LogInfo(
                        $"Observer refresh flag cleared on ZDO {__instance.m_uid}: relaying the real position again."
                    );
                }
            }
            catch (Exception ex)
            {
                s_instance?.Logger.LogError($"Patch ZDO.Deserialize: {ex}");
            }
        }
    }

    /// <summary>
    /// Server: while an observer refresh is armed, ZDOMan.SendZDOs writes a fake far-away
    /// position for the disguised player ZDO into the outgoing packet (the receiving vanilla
    /// client then re-creates its instance with the current prefab).
    ///
    /// The real position is stashed in the Prefix and written back in the Postfix / Finalizer:
    /// only the packet is faked, the server's ZDO state - and therefore saves and game logic -
    /// always keeps the real position.
    ///
    /// NOTE: no per-peer filtering here (ZDOMan.ZDOPeer is a private type of the game): your own
    /// connection cannot apply the fake anyway, because a ZDO update is only applied when its
    /// DataRevision is STRICTLY newer than yours, and you are the author of every revision of
    /// your own player ZDO (RPC_ZDOData, "num4 <= zDO.DataRevision -> continue").
    /// Patch_FindSectorObjects is the second safety net, on top of that.
    /// </summary>
    [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SendZDOs))]
    private static class Patch_SendZDOs
    {
        private static void Prefix()
        {
            try
            {
                if (s_savedPosition.HasValue)
                {
                    // Already inside a swap (defensive: no nested SendZDOs expected).
                    return;
                }
                ZDO? zdo = s_refreshZdo;
                if (zdo == null || s_zdoPositionField == null)
                {
                    return;
                }
                s_savedPosition = (Vector3?)s_zdoPositionField.GetValue(zdo);
                s_savedZdo = zdo;
                s_zdoPositionField.SetValue(zdo, s_refreshFarPos);
            }
            catch (Exception ex)
            {
                s_savedPosition = null;
                s_savedZdo = null;
                s_instance?.Logger.LogError($"Patch SendZDOs (prefix): {ex}");
            }
        }

        private static void Postfix()
        {
            RestoreRealPosition();
        }

        // The finalizer runs even if the original method threw: a fake position must never
        // survive the packet-building call.
        private static Exception? Finalizer(Exception? __exception)
        {
            RestoreRealPosition();
            return __exception;
        }

        private static void RestoreRealPosition()
        {
            if (s_savedPosition is not Vector3 saved)
            {
                return;
            }
            s_savedPosition = null;
            ZDO? zdo = s_savedZdo;
            s_savedZdo = null;
            if (zdo == null || s_zdoPositionField == null)
            {
                return;
            }
            try
            {
                s_zdoPositionField.SetValue(zdo, saved);
            }
            catch (Exception ex)
            {
                s_instance?.Logger.LogError($"Patch SendZDOs (restore): {ex}");
            }
        }
    }

    /// <summary>
    /// Safety net: on a machine with a local player (you), YOUR player object must never be
    /// destroyed by the local create/destroy tick - Player.OnDestroy would null
    /// Player.m_localPlayer and break everything. It is only ever out of its own active area
    /// while an observer refresh relays a fake position, so this changes nothing normally.
    /// Scoped to ZNetScene.CreateDestroyObjects: the network relay path is left untouched.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.CreateDestroyObjects))]
    private static class Patch_CreateDestroyObjects
    {
        private static void Prefix()
        {
            s_inCreateDestroy = true;
        }

        private static void Postfix()
        {
            s_inCreateDestroy = false;
        }

        // Runs even if the original method throws: never leave the flag stuck.
        private static Exception? Finalizer(Exception? __exception)
        {
            s_inCreateDestroy = false;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.FindSectorObjects))]
    private static class Patch_FindSectorObjects
    {
        private static void Postfix(List<ZDO> sectorObjects)
        {
            try
            {
                if (!s_inCreateDestroy || sectorObjects == null)
                {
                    return;
                }
                ZDO? local = GetLocalPlayerZDO();
                if (local == null || !local.IsValid() || sectorObjects.Contains(local))
                {
                    return;
                }
                sectorObjects.Add(local);
            }
            catch (Exception ex)
            {
                s_instance?.Logger.LogError($"Patch FindSectorObjects: {ex}");
            }
        }
    }
}
