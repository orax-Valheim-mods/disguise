#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Makes other players re-create your object after a prefab change: the owner arms a short
/// cd_refresh window, the modded server relays the player ZDO with a fake far-away position.
/// </summary>
internal static class ObserverRefresh
{
    // -- Observer refresh state (see the class summary) -------------------------------
    // The fields marked internal below are also read/restored by the Harmony patches
    // (Patch_*.cs): SendZDOs must put the real position back exactly where it found it.
    /// <summary>Server only: ZDO currently relayed with a fake far-away position.</summary>
    internal static ZDO? s_refreshZdo;

    /// <summary>Server only: when the fake relay was armed (safety timeout).</summary>
    private static float s_refreshStart;

    /// <summary>Server only: fake position written into the outgoing relay packets.</summary>
    internal static Vector3 s_refreshFarPos;

    /// <summary>Server only: real position stashed while SendZDOs runs (never left behind).</summary>
    internal static Vector3? s_savedPosition;

    /// <summary>Server only: the ZDO whose position is currently stashed (always restored).</summary>
    internal static ZDO? s_savedZdo;

    /// <summary>Owner: ZDO currently carrying cd_refresh = 1.</summary>
    internal static ZDO? s_ownerRefreshZdo;

    /// <summary>Owner: time at which cd_refresh must be removed.</summary>
    private static float s_refreshEnd;

    /// <summary>How far (metres) the fake relay position is moved: 64 zones, well out of any active area.</summary>
    private const float RefreshFarOffset = 4096f;

    /// <summary>Safety: never keep faking a position longer than this (owner vanished mid-refresh).</summary>
    private const float RefreshMaxHoldSeconds = 5f;

    // ------------------------------------------------------------------ observer refresh

    /// <summary>
    /// Makes other players re-create your object right now.
    ///
    /// Why it is needed: a vanilla client that already instantiated you keeps that GameObject
    /// forever - ZDO.Deserialize only overwrites ZDO.m_prefab and vanilla has no mechanism that
    /// replaces an existing instance when the prefab changes (ZNetScene.CreateDestroyObjects
    /// only creates objects whose instance was destroyed).
    ///
    /// How it works: we raise the transient cd_refresh flag on our player ZDO. The modded
    /// server (see Patch_ZDODeserialize / Patch_SendZDOs) then relays that ZDO with a fake
    /// far-away position for RefreshHoldSeconds: on every vanilla client the ZDO leaves their
    /// active area, RemoveObjects destroys the instance, and the next tick re-creates it with
    /// the CURRENT prefab. The server's own ZDO position (and the save file) never changes.
    ///
    /// On a plain client this only sets the flag: the server arms the trick when it receives it.
    /// </summary>
    internal static void StartObserverRefresh(ZDO zdo, bool force)
    {
        try
        {
            if (
                !force
                && (
                    DisguiseConfig.ConfigForceRefresh == null
                    || !DisguiseConfig.ConfigForceRefresh.Value
                )
            )
            {
                return;
            }
            if (zdo == null || !zdo.IsValid() || ZNet.instance == null)
            {
                return;
            }
            if (GameAccess.s_zdoPositionField == null)
            {
                // ZDO.m_position not reachable: the server could not fake anything anyway.
                return;
            }
            float hold = Mathf.Clamp(DisguiseConfig.ConfigRefreshHold.Value, 0.2f, 3f);
            zdo.Set(PrefabResolver.RefreshHash, 1);
            GameAccess.BumpDataRevision(zdo); // the flag must reach the server even if nothing else changed
            s_ownerRefreshZdo = zdo;
            s_refreshEnd = Time.unscaledTime + hold;
            ArmObserverRefresh(zdo); // no-op on a plain client: the server arms it on Deserialize
            Disguise.LogInfo(
                $"Observer refresh for ZDO {zdo.m_uid}: other players re-create your object in ~{hold:0.0} s"
            );
        }
        catch (Exception ex)
        {
            Disguise.LogError($"StartObserverRefresh: {ex}");
        }
    }

    /// <summary>
    /// Server side only: start writing the fake far-away position into the relay packets of
    /// this player ZDO. A client NEVER fakes anything: it always sends its real position.
    /// </summary>
    internal static void ArmObserverRefresh(ZDO zdo)
    {
        try
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                return;
            }
            List<ZNetPeer>? peers = ZNet.instance.GetPeers();
            if (peers == null || peers.Count == 0)
            {
                // Nobody connected: nothing to refresh (also protects world load).
                return;
            }
            Vector3 pos = zdo.GetPosition();
            s_refreshFarPos = new Vector3(pos.x + RefreshFarOffset, pos.y, pos.z);
            if (s_refreshZdo != zdo)
            {
                s_refreshZdo = zdo;
                // The safety timeout is measured from the FIRST arm: re-arms happen on every
                // incoming update while the flag is still 1, and resetting the timer there
                // would make the timeout impossible to reach.
                s_refreshStart = Time.unscaledTime;
                Disguise.LogInfo(
                    $"Observer refresh armed for ZDO {zdo.m_uid}: relaying a fake position (+"
                        + $"{RefreshFarOffset:0} m on X) to other clients."
                );
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"ArmObserverRefresh: {ex}");
        }
    }

    internal static void DisarmObserverRefresh()
    {
        s_refreshZdo = null;
    }

    /// <summary>
    /// Every frame, on both sides:
    ///  - owner: remove cd_refresh once the hold time has elapsed (the server then relays the
    ///    real position again -> vanilla clients re-create your object with the new prefab);
    ///  - server: safety net, never keep faking a position if the owner vanished mid-refresh.
    /// </summary>
    internal static void TickObserverRefresh()
    {
        try
        {
            if (ZNet.instance == null)
            {
                // Left the session: drop any pending refresh state.
                DisarmObserverRefresh();
                s_ownerRefreshZdo = null;
                s_refreshEnd = 0f;
                return;
            }

            float now = Time.unscaledTime;

            if (s_ownerRefreshZdo != null && s_refreshEnd > 0f && now >= s_refreshEnd)
            {
                s_refreshEnd = 0f;
                ZDO zdo = s_ownerRefreshZdo;
                s_ownerRefreshZdo = null;
                if (zdo.IsValid())
                {
                    // CRITICAL: write an explicit 0 - never RemoveInt here.
                    // A removed key is simply ABSENT from the outgoing payload, and
                    // ZDO.Deserialize only adds/overwrites the keys it receives
                    // (ZDOExtraData.Add never deletes): the server would keep
                    // cd_refresh = 1 forever, relay a fake position permanently and
                    // vanilla clients would destroy your object and never re-create it
                    // (you become invisible). With an explicit 0 the server overwrites it
                    // and disarms (see Patch_ZDODeserialize).
                    zdo.Set(PrefabResolver.RefreshHash, 0);
                    GameAccess.BumpDataRevision(zdo);
                }
                if (s_refreshZdo == zdo)
                {
                    // Local world / host: we are our own server, stop faking right away.
                    DisarmObserverRefresh();
                }
                Disguise.LogInfo(
                    "Observer refresh done: your new look is now visible to every loaded player."
                );
            }

            if (s_refreshZdo != null && now - s_refreshStart > RefreshMaxHoldSeconds)
            {
                ZDO zdo = s_refreshZdo;
                DisarmObserverRefresh();
                if (zdo != null && zdo.IsValid() && zdo.RemoveInt(Disguise.RefreshKey))
                {
                    Disguise.LogWarning(
                        $"Observer refresh flag still set after {RefreshMaxHoldSeconds:0} s on ZDO {zdo.m_uid} "
                            + "(owner never cleared it): fake position relay stopped."
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"TickObserverRefresh: {ex}");
        }
    }
}
