#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Disguise - replaces the network prefab of YOUR player with a creature prefab,
/// so that players WITHOUT the mod see a creature in your place.
///
/// How it works (derived from the game code):
///  - ZDO.Serialize / ZDO.Deserialize carry the prefab hash (ZDO.m_prefab)
///    -> every vanilla client instantiates that prefab through ZNetScene.CreateObject.
///  - You are the OWNER of your player ZDO: your prefab change travels to the server
///    (IncreaseDataRevision -> ZDOMan.ClientChanged), which relays it to all other
///    clients (ZDOMan.RPC_ZDOData -> ZDO.Deserialize).
///  - Modded machines (you + dedicated server) intercept ZNetScene.CreateObject and
///    still instantiate the real "Player" prefab (cd_disguise marker), so that the
///    server side game logic stays normal.
///  - You still see your own viking: your Player object is created by Game.SpawnPlayer
///    (Object.Instantiate of the Player prefab), never by CreateObject.
///
/// Observer refresh (a client that already had you instantiated):
///  - ZDO.Deserialize NEVER re-creates an existing instance when m_prefab changes, and
///    vanilla only re-creates objects that leave and re-enter its active area
///    (ZNetScene.RemoveObjects / CreateObjectsSorted). So after a prefab change we make the
///    modded server relay our player ZDO with a FAKE far-away position for
///    ~RefreshHoldSeconds (1.5 s by default)
///    (cd_refresh flag): every vanilla client destroys its local instance and re-creates it
///    at the next tick with the current prefab. The server's own ZDO position is never
///    modified (only the outgoing packet is), so saves and game logic stay untouched.
/// </summary>
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class Disguise : BaseUnityPlugin
{
    public const string PluginGUID = "orax.Disguise";
    public const string PluginName = "Disguise";
    public const string PluginVersion = "0.1.0";

    /// <summary>Int flag set on the player ZDO to mean "this player is handled by Disguise".</summary>
    public const string MarkerKey = "cd_disguise";

    /// <summary>
    /// Transient int flag (set by the OWNER for RefreshHoldSeconds, then removed) telling the
    /// modded server: "relay my player ZDO with a fake far-away position". Vanilla clients then
    /// destroy their instance and build it again with the current prefab.
    /// </summary>
    public const string RefreshKey = "cd_refresh";

    /// <summary>
    /// Int bitmask written by the OWNER of a disguised player: which equipment the other
    /// machines must hide (see EquipmentMask). A relaying server masks what this value says
    /// and only falls back to its own [3 - Display] config when the key is absent - that is,
    /// when the owner runs no DLL (a disguise kept in the save, server alone).
    /// </summary>
    public const string HideKey = "cd_hide";

    /// <summary>
    /// Int flag written by the OWNER of a disguised player: how the other machines must
    /// display it (0 = enemy with a red health bar, 1 = ally with a green one - see
    /// AllyView). Relaying machines follow this value instead of their own ShowAsAlly
    /// config, which is only the fallback for an owner running no DLL.
    /// </summary>
    public const string RelationKey = "cd_rel";

    /// <summary>
    /// Int flag written by the OWNER of a player: 1 while the other maps must display a
    /// fake position instead of the real one (see FakePosition). Disabled by writing an
    /// explicit 0, like cd_refresh: a removed key never reaches the server, which would
    /// keep faking the position forever.
    /// </summary>
    public const string FakeKey = "cd_fake";

    /// <summary>
    /// Vector3 written by the OWNER next to cd_fake: the position the other maps show
    /// instead of the real one. X and Z are the map plane (Minimap draws those two), Y is
    /// only carried along so that the stored value stays a sane world position.
    /// </summary>
    public const string FakePositionKey = "cd_fpos";

    internal static Harmony? PatchHarmony;

    private static Disguise? s_instance;
    private static float s_nextReassertTime;
    internal static bool s_loggedSummary;

    // -- Logging: the plugin instance stays private, every other class logs through these -
    // Info and Debug follow the Logs option ([1 - General], disguise logs); warnings and
    // errors are never gated, they are what tells a broken install from a working one.
    internal static void LogDebug(string message)
    {
        if (DisguiseConfig.ConfigLogs.Value)
        {
            s_instance?.Logger.LogDebug(message);
        }
    }

    internal static void LogInfo(string message)
    {
        if (DisguiseConfig.ConfigLogs.Value)
        {
            s_instance?.Logger.LogInfo(message);
        }
    }

    internal static void LogWarning(string message) => s_instance?.Logger.LogWarning(message);

    internal static void LogError(string message) => s_instance?.Logger.LogError(message);

    // ------------------------------------------------------------------ lifecycle

    public void Awake()
    {
        s_instance = this;

        DisguiseConfig.Bind(Config);

        PatchHarmony = new Harmony(PluginGUID);
        PatchHarmony.PatchAll(Assembly.GetExecutingAssembly());

        // Diagnostics: these members are private/protected in the game, they are only
        // reachable through reflection (see the s_* fields in GameAccess).
        if (GameAccess.s_zdoPrefabField == null)
        {
            Logger.LogError("ZDO.m_prefab not found: save protection is disabled.");
        }
        if (GameAccess.s_characterNViewField == null)
        {
            Logger.LogError("Character.m_nview not found: disguise is disabled.");
        }
        if (GameAccess.s_increaseDataRevisionMethod == null)
        {
            Logger.LogWarning("ZDO.IncreaseDataRevision not found: falling back to ZDO.Set().");
        }
        if (GameAccess.s_zdoPositionField == null)
        {
            Logger.LogWarning(
                "ZDO.m_position not found: observer refresh disabled (ForceObserverRefresh)."
            );
        }

        DisguiseCommand.RegisterConsoleCommands();

        LogInfo(
            $"{PluginName} {PluginVersion} loaded - disguise: {DisguiseConfig.ConfigEnabled.Value}, creature: \"{DisguiseConfig.ConfigCreature.Value}\""
        );
    }

    public void OnDestroy()
    {
        SelfView.Shutdown();
        PatchHarmony?.UnpatchSelf();
        PatchHarmony = null;
        s_instance = null;
    }

    public void Update()
    {
        // Frame time of the window "disguise perf" reports: five float operations, no
        // allocation - the meter must not become part of the cost it measures.
        FrameMeter.Sample(Time.unscaledDeltaTime);

        // Runs every frame on both sides: ends the observer-refresh window (owner) and
        // guarantees the fake relay position can never outlive its safety timeout (server).
        ObserverRefresh.TickObserverRefresh();

        // Own view (purely local): builds / synchronises / removes the creature clone that
        // replaces your own viking on your screen. No-op without a local player, without
        // the option, or while your ZDO still carries the Player prefab.
        SelfView.Tick();

        // Only the owner of a ZDO can change its prefab reliably, so we re-apply
        // regularly on the LOCAL player (Player.m_localPlayer is null on a dedicated
        // server: this path then never runs).
        if (Time.unscaledTime < s_nextReassertTime)
        {
            return;
        }
        s_nextReassertTime =
            Time.unscaledTime + Mathf.Max(0.25f, DisguiseConfig.ConfigReassert.Value);

        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }

        // Self-healing: a cd_refresh flag left over (crash, disconnect or scene change while a
        // refresh was running) must never survive, or the server would keep relaying a fake
        // position for your player forever.
        if (
            ObserverRefresh.s_ownerRefreshZdo == null
            && zdo.GetInt(PrefabResolver.RefreshHash, 0) == 1
        )
        {
            // Explicit 0, not RemoveInt: removals do not propagate (ZDO.Deserialize only
            // adds/overwrites), so the server would keep relaying a fake position.
            zdo.Set(PrefabResolver.RefreshHash, 0);
            GameAccess.BumpDataRevision(zdo);
            LogInfo("Cleared a stale cd_refresh flag on your player ZDO.");
        }

        if (!s_loggedSummary)
        {
            s_loggedSummary = true;
            bool server = ZNet.instance != null && ZNet.instance.IsServer();
            LogInfo(
                $"Local player (ZDO {zdo.m_uid}) - current prefab: {PrefabResolver.PrefabName(zdo.GetPrefab())}, "
                    + "desired prefab: "
                    + PrefabResolver.PrefabName(PrefabResolver.DesiredPrefabHash())
                    + (server ? " [local world / host]" : " [client]")
                    + ", marker: "
                    + (DisguiseApplier.HasMarker(zdo) ? "yes" : "no")
            );
        }

        DisguiseApplier.ApplyTo(zdo, "reassert");

        // The owner publishes which equipment must stay hidden from the others: relaying
        // servers mask according to this value instead of their own [3 - Display] config
        // (whose defaults would hide the gear whatever your client says). Set() only bumps
        // the DataRevision when the mask really changes, so the steady state is free.
        EquipmentMask.PublishMask(zdo);

        // Same rule for the health bar: the owner publishes the relation it wants to be
        // seen with, relaying machines write it into the packet and restore it right after
        // (AllyView) - their own ShowAsAlly config only ever is the fallback.
        AllyView.Publish(zdo);

        // And the same rule for the map: the owner publishes the position the other maps
        // must display (cd_fake / cd_fpos), the modded server is the one writing it into
        // the player list it broadcasts (Patch_WritePlayerInfo). Nothing else - the ZDO
        // position, peer.m_refPos, the save, your own screen - ever moves.
        FakePosition.Publish(zdo);
    }

    public void LateUpdate()
    {
        // Own view, second half: every game script has moved the player by now, so this is
        // where the creature clone is landed on it (one transform copy) and where the rare
        // renderer scan runs. Keeps the option free of any per-frame hierarchy walk.
        SelfView.LateTick();
    }
}
