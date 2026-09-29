#nullable enable
using System;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Applies the disguise to a player ZDO: writes or removes the cd_disguise marker, swaps
/// the network prefab and (re)creates the local object.
/// </summary>
internal static class DisguiseApplier
{
    /// <summary>True if this ZDO is a player marked by Disguise.</summary>
    internal static bool HasMarker(ZDO? zdo)
    {
        return zdo != null && zdo.IsValid() && zdo.GetInt(PrefabResolver.MarkerHash, 0) == 1;
    }

    /// <summary>
    /// Applies (or removes) the disguise on a player ZDO.
    /// Only call this on the local player's ZDO, or on an already marked ZDO (world load).
    /// </summary>
    internal static void ApplyTo(ZDO? zdo, string reason)
    {
        try
        {
            if (zdo == null || !zdo.IsValid())
            {
                return;
            }
            int playerHash = PrefabResolver.PlayerPrefabHash;
            if (playerHash == 0)
            {
                return;
            }
            int desired = PrefabResolver.DesiredPrefabHash();
            if (desired == 0)
            {
                return;
            }

            // Set or remove the marker: it is what tells the modded machines
            // "this creature ZDO is actually a player, spawn a real Player".
            if (desired != playerHash)
            {
                zdo.Set(PrefabResolver.MarkerHash, 1);
            }
            else if (zdo.GetInt(PrefabResolver.MarkerHash, 0) != 0)
            {
                // Explicit 0 instead of RemoveInt: a removed key is absent from the payload
                // and receivers only add/overwrite values, so the marker removal would never
                // reach the server (it would keep thinking you are disguised).
                // Set() bumps the DataRevision by itself when the value actually changes.
                zdo.Set(PrefabResolver.MarkerHash, 0);
            }

            int current = zdo.GetPrefab();
            if (current == desired)
            {
                return;
            }
            zdo.SetPrefab(desired);
            Disguise.LogInfo(
                $"[{reason}] ZDO {zdo.m_uid}: prefab {PrefabResolver.PrefabName(current)} -> {PrefabResolver.PrefabName(desired)}"
            );

            // The network prefab changed: ask the server for an observer refresh, but only when
            // we are the live owner of that ZDO (never while the server re-applies disguises at
            // world load, where there is no local player).
            ZDO? local = GameAccess.GetLocalPlayerZDO();
            if (local != null && local.m_uid == zdo.m_uid)
            {
                Disguise.StartObserverRefresh(zdo, force: false);
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"ApplyTo ({reason}): {ex}");
        }
    }

    /// <summary>
    /// Instantiates the requested prefab while reusing the existing ZDO
    /// (same mechanism as ZNetScene.CreateObject).
    /// </summary>
    internal static GameObject InstantiateAs(ZDO zdo, int prefabHash)
    {
        ZNetScene zns = ZNetScene.instance;
        GameObject prefab = zns.GetPrefab(prefabHash);
        if (prefab == null)
        {
            Disguise.LogWarning($"Prefab not found: {PrefabResolver.PrefabName(prefabHash)}");
            return null!;
        }
        bool useInit = ZNetView.m_useInitZDO;
        try
        {
            ZNetView.m_useInitZDO = true;
            ZNetView.m_initZDO = zdo;
            GameObject result = UnityEngine.Object.Instantiate(
                prefab,
                zdo.GetPosition(),
                zdo.GetRotation()
            );
            if (ZNetView.m_initZDO != null)
            {
                Disguise.LogWarning($"ZDO {zdo.m_uid} not consumed while creating {prefab.name}");
                ZNetView.m_initZDO = null;
            }
            return result;
        }
        finally
        {
            ZNetView.m_useInitZDO = useInit;
        }
    }
}
