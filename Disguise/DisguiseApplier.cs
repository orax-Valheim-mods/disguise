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

            // We are the live owner of this ZDO: what is written below is OURS to publish.
            // A ZDO the world load re-applies - another player's costume, on a server - keeps
            // the level its own machine chose; the server cannot know it, and the owner
            // re-asserts it within ReassertSeconds anyway.
            ZDO? local = GameAccess.GetLocalPlayerZDO();
            bool mine = local != null && local.m_uid == zdo.m_uid;

            // The costume's level: what makes every machine that draws you pick the skin of
            // its star level (Character.Awake reads this key - and never for a player, so it
            // can not touch the viking under the costume).
            bool levelChanged = false;
            if (mine)
            {
                if (desired != playerHash)
                {
                    int level = DisguiseConfig.ConfigCreatureLevel.Value;
                    levelChanged = zdo.GetInt(PrefabResolver.LevelHash, 1) != level;
                    zdo.Set(PrefabResolver.LevelHash, level);
                }
                else if (zdo.GetInt(PrefabResolver.LevelHash, 1) != 1)
                {
                    // Explicit 1, for the same reason as the marker above: an absent key
                    // already reads as the default level, and would only weigh down the
                    // payload of every player who ever took a costume off.
                    zdo.Set(PrefabResolver.LevelHash, 1);
                }
            }

            int current = zdo.GetPrefab();
            if (current == desired)
            {
                // The costume kept its prefab - but a level that just moved still has to
                // reach the machines already drawing it: a creature reads its level once,
                // when it is born (Character.Awake), and a vanilla client keeps that
                // instance forever after.
                if (mine && levelChanged)
                {
                    ObserverRefresh.StartObserverRefresh(zdo, force: false);
                }
                return;
            }
            zdo.SetPrefab(desired);
            Disguise.LogInfo(
                $"[{reason}] ZDO {zdo.m_uid}: prefab {PrefabResolver.PrefabName(current)} -> {PrefabResolver.PrefabName(desired)}"
            );

            // The network prefab changed: ask the server for an observer refresh, but only when
            // we are the live owner of that ZDO (never while the server re-applies disguises at
            // world load, where there is no local player).
            if (mine)
            {
                ObserverRefresh.StartObserverRefresh(zdo, force: false);
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
