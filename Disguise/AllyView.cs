#nullable enable
using System;

namespace Disguise;

/// <summary>
/// Chooses how the other machines DISPLAY your creature: the red health bar of an enemy, or
/// the green one of an ally (the ShowAsAlly option of [3 - Display]).
///
/// Why this exact flag: EnemyHud picks its two bars through
/// BaseAI.IsEnemy(the player looking, your ZDO), and that function only reads the group and
/// the factions - prefab fields, never synced - plus the tamed flag of the ZDO. Tamed
/// against a player always answers "not enemy", whatever your creature is, so s_tamed is
/// the only value able to change that display. It is stored as an int, which means an
/// explicit 1 or 0 does reach a vanilla client: the machines we cannot patch.
///
/// Like the equipment mask, the OWNER is the one deciding: it publishes its choice under
/// Disguise.RelationKey (cd_rel), and every machine serializing that ZDO writes s_tamed for
/// the duration of the packet only - the stored ZDO, the save file and your own view never
/// change. A machine whose owner runs no DLL falls back on its own ShowAsAlly config.
///
/// A modded machine drops the flag the moment it receives it (ClearStored): it renders a
/// real Player, which needs no flag, so nothing but a vanilla client - the one that renders
/// the creature - ever keeps it, and only until the next packet.
/// </summary>
internal static class AllyView
{
    private static ZDO? s_activeZdo;

    private static int s_savedValue;

    /// <summary>
    /// What this machine asks the others to display: 1 = ally, 0 = enemy. 0 while the
    /// disguise is off, so the published value always tells the truth.
    /// </summary>
    internal static int BuildRelation()
    {
        if (
            !DisguiseConfig.ConfigEnabled.Value
            || PrefabResolver.DesiredPrefabHash() == PrefabResolver.PlayerPrefabHash
        )
        {
            return 0;
        }
        return DisguiseConfig.ConfigShowAsAlly.Value ? 1 : 0;
    }

    /// <summary>
    /// Writes the choice into the local player ZDO - the owner is the only machine allowed
    /// to decide, relaying machines only follow (their ShowAsAlly config is the fallback
    /// for an owner running no DLL). Set() bumps the DataRevision only when the value
    /// changes, so publishing it every reassert costs nothing.
    /// </summary>
    internal static void Publish(ZDO? zdo)
    {
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }
        int relation = BuildRelation();
        if (zdo.GetInt(Disguise.RelationKey, -1) != relation)
        {
            zdo.Set(Disguise.RelationKey, relation);
        }
    }

    /// <summary>
    /// The relation changed while you are disguised: publishing it bumps the DataRevision,
    /// so the relaying server and the other clients receive the new bar right away instead
    /// of keeping the state of the last packet.
    /// </summary>
    internal static void OnSettingChanged()
    {
        Publish(GameAccess.GetLocalPlayerZDO());
    }

    /// <summary>The relation to apply to the outgoing packet: the owner's, when it published one.</summary>
    private static int Wanted(ZDO zdo)
    {
        int relation = zdo.GetInt(Disguise.RelationKey, -1);
        if (relation >= 0)
        {
            return relation != 0 ? 1 : 0;
        }
        return DisguiseConfig.ConfigShowAsAlly.Value ? 1 : 0;
    }

    /// <summary>
    /// ZDO.Serialize: writes the relation into ZDOVars.s_tamed for the duration of the
    /// packet. Returns true when the window is open - the caller must then call End().
    /// </summary>
    internal static bool BeginSerialize(ZDO? zdo)
    {
        if (s_activeZdo != null || zdo == null || !zdo.IsValid())
        {
            return false;
        }
        // Most ZDOs stop here: this runs for every single ZDO put in an outgoing packet.
        if (!DisguiseApplier.HasMarker(zdo))
        {
            return false;
        }

        s_activeZdo = zdo;
        try
        {
            s_savedValue = zdo.GetInt(ZDOVars.s_tamed, 0);
            // Explicit 0 in the enemy case too: removals never propagate, so writing the
            // value every time is what clears a stale "ally" on the machines already
            // holding one.
            GameAccess.SetZdoIntSilent(zdo, ZDOVars.s_tamed, Wanted(zdo));
        }
        catch (Exception ex)
        {
            End();
            Disguise.LogError($"AllyView serialize: {ex}");
            return false;
        }
        return true;
    }

    /// <summary>Writes back the value saved by the last opened window.</summary>
    internal static void End()
    {
        ZDO? zdo = s_activeZdo;
        int saved = s_savedValue;
        s_activeZdo = null;
        s_savedValue = 0;
        if (zdo == null)
        {
            return;
        }
        try
        {
            GameAccess.SetZdoIntSilent(zdo, ZDOVars.s_tamed, saved);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"AllyView restore: {ex}");
        }
    }

    /// <summary>
    /// ZDO.Deserialize: what a modded machine keeps of the flag. It renders a real Player
    /// (the bar is already the ally one), so the value is dropped instead of stored - and
    /// therefore never reaches the save file either. No-op on a vanilla client: it has no
    /// patch, keeps the value, and that is exactly what the display needs.
    /// </summary>
    internal static void ClearStored(ZDO zdo)
    {
        if (zdo.GetInt(ZDOVars.s_tamed, 0) != 0)
        {
            GameAccess.SetZdoIntSilent(zdo, ZDOVars.s_tamed, 0);
        }
    }
}
