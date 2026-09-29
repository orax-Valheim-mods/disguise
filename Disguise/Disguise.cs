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
///    modded server relay our player ZDO with a FAKE far-away position for ~0.6 s
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

    internal static Harmony? PatchHarmony;

    private static Disguise? s_instance;
    private static float s_nextReassertTime;
    internal static bool s_loggedSummary;

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
    private static ZDO? s_ownerRefreshZdo;

    /// <summary>Owner: time at which cd_refresh must be removed.</summary>
    private static float s_refreshEnd;

    /// <summary>True only while ZNetScene.CreateDestroyObjects runs (scopes the safety net below).</summary>
    internal static bool s_inCreateDestroy;

    /// <summary>How far (metres) the fake relay position is moved: 64 zones, well out of any active area.</summary>
    private const float RefreshFarOffset = 4096f;

    /// <summary>Safety: never keep faking a position longer than this (owner vanished mid-refresh).</summary>
    private const float RefreshMaxHoldSeconds = 5f;

    private static bool s_consoleCommandRegistered;

    // -- Logging: the plugin instance stays private, the patches log through these -------
    internal static void LogDebug(string message) => s_instance?.Logger.LogDebug(message);

    internal static void LogInfo(string message) => s_instance?.Logger.LogInfo(message);

    internal static void LogWarning(string message) => s_instance?.Logger.LogWarning(message);

    internal static void LogError(string message) => s_instance?.Logger.LogError(message);

    // ------------------------------------------------------------------ cycle de vie

    public void Awake()
    {
        s_instance = this;

        DisguiseConfig.Bind(Config);

        PatchHarmony = new Harmony(PluginGUID);
        PatchHarmony.PatchAll(Assembly.GetExecutingAssembly());

        // Diagnostics: these members are private/protected in the game, they are only
        // reachable through reflection (see the s_* fields above).
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

        RegisterConsoleCommands();

        Logger.LogInfo(
            $"{PluginName} {PluginVersion} loaded - disguise: {DisguiseConfig.ConfigEnabled.Value}, creature: \"{DisguiseConfig.ConfigCreature.Value}\""
        );
    }

    public void OnDestroy()
    {
        PatchHarmony?.UnpatchSelf();
        PatchHarmony = null;
        s_instance = null;
    }

    public void Update()
    {
        // Runs every frame on both sides: ends the observer-refresh window (owner) and
        // guarantees the fake relay position can never outlive its safety timeout (server).
        TickObserverRefresh();

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
        if (s_ownerRefreshZdo == null && zdo.GetInt(PrefabResolver.RefreshHash, 0) == 1)
        {
            // Explicit 0, not RemoveInt: removals do not propagate (ZDO.Deserialize only
            // adds/overwrites), so the server would keep relaying a fake position.
            zdo.Set(PrefabResolver.RefreshHash, 0);
            GameAccess.BumpDataRevision(zdo);
            Logger.LogInfo("Cleared a stale cd_refresh flag on your player ZDO.");
        }

        if (!s_loggedSummary)
        {
            s_loggedSummary = true;
            bool server = ZNet.instance != null && ZNet.instance.IsServer();
            Logger.LogInfo(
                $"Local player (ZDO {zdo.m_uid}) - current prefab: {PrefabResolver.PrefabName(zdo.GetPrefab())}, "
                    + "desired prefab: "
                    + PrefabResolver.PrefabName(PrefabResolver.DesiredPrefabHash())
                    + (server ? " [local world / host]" : " [client]")
                    + ", marker: "
                    + (DisguiseApplier.HasMarker(zdo) ? "yes" : "no")
            );
        }

        DisguiseApplier.ApplyTo(zdo, "reassert");
    }

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
            LogInfo(
                $"Observer refresh for ZDO {zdo.m_uid}: other players re-create your object in ~{hold:0.0} s"
            );
        }
        catch (Exception ex)
        {
            LogError($"StartObserverRefresh: {ex}");
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
                LogInfo(
                    $"Observer refresh armed for ZDO {zdo.m_uid}: relaying a fake position (+"
                        + $"{RefreshFarOffset:0} m on X) to other clients."
                );
            }
        }
        catch (Exception ex)
        {
            LogError($"ArmObserverRefresh: {ex}");
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
    private static void TickObserverRefresh()
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
                LogInfo(
                    "Observer refresh done: your new look is now visible to every loaded player."
                );
            }

            if (s_refreshZdo != null && now - s_refreshStart > RefreshMaxHoldSeconds)
            {
                ZDO zdo = s_refreshZdo;
                DisarmObserverRefresh();
                if (zdo != null && zdo.IsValid() && zdo.RemoveInt(RefreshKey))
                {
                    LogWarning(
                        $"Observer refresh flag still set after {RefreshMaxHoldSeconds:0} s on ZDO {zdo.m_uid} "
                            + "(owner never cleared it): fake position relay stopped."
                    );
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"TickObserverRefresh: {ex}");
        }
    }

    // ------------------------------------------------------------------ console command

    private static void RegisterConsoleCommands()
    {
        try
        {
            // The ConsoleCommand constructor registers itself into the Terminal table.
            // NEVER touch Terminal.commands: that field is "protected" in the game and a
            // direct access would throw a FieldAccessException.
            if (s_consoleCommandRegistered)
            {
                return;
            }
            _ = new Terminal.ConsoleCommand(
                "disguise",
                "Disguise: status | set <prefab> | enable | disable | refresh | list [filter] | help",
                OnDisguiseCommand,
                isCheat: false,
                isNetwork: false,
                onlyServer: false,
                isSecret: false,
                allowInDevBuild: false,
                hideBehindDevCommands: false,
                optionsFetcher: DisguiseOptions,
                alwaysRefreshTabOptions: true
            );
            s_consoleCommandRegistered = true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to register the console command: {ex}");
        }
    }

    private static List<string> DisguiseOptions()
    {
        List<string> options = new() { "status", "help", "enable", "disable", "refresh", "list" };
        try
        {
            string current = DisguiseConfig.ConfigCreature.Value;
            if (!string.IsNullOrEmpty(current) && !options.Contains(current))
            {
                options.Insert(1, current);
            }
        }
        catch
        {
            // ignored
        }
        return options;
    }

    private static void OnDisguiseCommand(Terminal.ConsoleEventArgs args)
    {
        Terminal ctx = args.Context;
        string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "status";

        switch (sub)
        {
            case "enable":
                DisguiseConfig.ConfigEnabled.Value = true;
                ApplyNow(ctx, "enable");
                Say(ctx, "[Disguise] Disguise enabled.");
                break;

            case "disable":
                DisguiseConfig.ConfigEnabled.Value = false;
                ApplyNow(ctx, "disable");
                Say(ctx, "[Disguise] Disabled: your player looks normal again for everyone.");
                Say(
                    ctx,
                    "Keep ProtectSaveFile=true (default) or restart the server once you are undisguised."
                );
                Say(
                    ctx,
                    "Other players re-create your object automatically (ForceObserverRefresh); "
                        + "if one still shows the old look, run \"disguise refresh\"."
                );
                break;

            case "refresh":
                RefreshObservers(ctx);
                break;

            case "set":
                if (args.Length < 3)
                {
                    Say(ctx, "Usage: disguise set <prefab_name>   (disguise list for the names)");
                    break;
                }
                DisguiseConfig.ConfigCreature.Value = args[2];
                DisguiseConfig.ConfigEnabled.Value = true;
                ApplyNow(ctx, "set");
                Say(
                    ctx,
                    $"[Disguise] Creature set to \"{DisguiseConfig.ConfigCreature.Value}\" (resolved prefab: {PrefabResolver.PrefabName(PrefabResolver.CreaturePrefabHash)})."
                );
                if (PrefabResolver.CreaturePrefabHash == 0)
                {
                    Say(ctx, "Warning: this prefab does not exist here (see disguise list).");
                }
                break;

            case "list":
                ListCreatures(ctx, args.Length > 2 ? args[2] : null);
                break;

            case "help":
            case "?":
                PrintHelp(ctx);
                break;

            case "status":
                PrintStatus(ctx);
                break;

            default:
                // sub defaults to "status" when there is no argument, so this branch is only
                // reached with an unknown word in position 1 -> report it and show the help.
                Say(ctx, $"[Disguise] Unknown subcommand \"{args[1]}\".");
                PrintHelp(ctx);
                break;
        }
    }

    private static void PrintHelp(Terminal ctx)
    {
        Say(ctx, "[Disguise] Available commands:");
        Say(ctx, "  disguise status           current state (creature, marker, save protection)");
        Say(ctx, "  disguise list [filter]    list creature prefabs (e.g. disguise list troll)");
        Say(ctx, "  disguise set <prefab>     change creature and enable the disguise");
        Say(ctx, "  disguise enable           enable the disguise");
        Say(ctx, "  disguise disable          disable it: you become a normal viking again");
        Say(ctx, "  disguise refresh          force other players to re-create your object now");
        Say(ctx, "  disguise help             show this help");
        Say(ctx, "  Config file: BepInEx\\config\\orax.Disguise.cfg");
        Say(ctx, "  Install on the dedicated server AND on your PC only, never for other players.");
    }

    private static void ApplyNow(Terminal ctx, string reason)
    {
        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        if (zdo == null || !zdo.IsValid())
        {
            Say(
                ctx,
                "[Disguise] No local player (main menu): the change will be applied on the next spawn."
            );
            return;
        }
        DisguiseApplier.ApplyTo(zdo, reason);
    }

    /// <summary>
    /// "disguise refresh": re-applies the disguise and immediately asks the server for an
    /// observer refresh - the manual fallback when a vanilla client still shows the old look.
    /// </summary>
    private static void RefreshObservers(Terminal ctx)
    {
        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        if (zdo == null || !zdo.IsValid())
        {
            Say(ctx, "[Disguise] No local player (main menu): nothing to refresh.");
            return;
        }
        DisguiseApplier.ApplyTo(zdo, "refresh");
        StartObserverRefresh(zdo, force: true);
        if (!DisguiseConfig.ConfigForceRefresh.Value)
        {
            Say(ctx, "Note: ForceObserverRefresh is false, so this refresh was forced manually.");
        }
        Say(
            ctx,
            $"[Disguise] Observer refresh requested: other players re-create your object "
                + $"in ~{Mathf.Clamp(DisguiseConfig.ConfigRefreshHold.Value, 0.2f, 3f):0.0} s (this mod is required on the dedicated server)."
        );
    }

    private static void PrintStatus(Terminal ctx)
    {
        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        bool isServer = ZNet.instance != null && ZNet.instance.IsServer();

        Say(
            ctx,
            $"[Disguise] v{PluginVersion} - {(DisguiseConfig.ConfigEnabled.Value ? "enabled" : "disabled")}"
                + (isServer ? " (this process is a server)" : " (client)")
        );
        Say(
            ctx,
            $"  Configured creature: \"{DisguiseConfig.ConfigCreature.Value}\" -> {PrefabResolver.PrefabName(PrefabResolver.CreaturePrefabHash)}"
                + (PrefabResolver.CreaturePrefabHash == 0 ? "  (NOT FOUND)" : "")
        );
        Say(ctx, $"  Save protection: {(DisguiseConfig.ConfigProtectSave.Value ? "yes" : "no")}");
        if (zdo != null && zdo.IsValid())
        {
            int desired = PrefabResolver.DesiredPrefabHash();
            Say(
                ctx,
                $"  Player ZDO {zdo.m_uid}: current prefab = {PrefabResolver.PrefabName(zdo.GetPrefab())}, desired = {PrefabResolver.PrefabName(desired)}, marker = {(DisguiseApplier.HasMarker(zdo) ? "yes" : "no")}"
            );
            Say(
                ctx,
                zdo.GetPrefab() == desired
                    ? "  State: OK, your disguise is applied."
                    : "  State: PENDING (automatic re-application in "
                        + Mathf.Max(0.25f, DisguiseConfig.ConfigReassert.Value).ToString("0.0")
                        + " s)"
            );
        }
        else
        {
            Say(ctx, "  No local player right now.");
        }
        Say(
            ctx,
            "  Install this DLL on the dedicated server AND on your PC, never on other players."
        );
    }

    private static void ListCreatures(Terminal ctx, string? filter)
    {
        ZNetScene? zns = ZNetScene.instance;
        if (zns == null)
        {
            Say(
                ctx,
                "[Disguise] ZNetScene unavailable (main menu): the list is available in game."
            );
            return;
        }
        List<string> names = new();
        foreach (string name in zns.GetPrefabNames())
        {
            if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }
            GameObject? go = zns.GetPrefab(name);
            if (go != null && go.GetComponent<Character>() != null)
            {
                names.Add(name);
            }
        }
        names.Sort(StringComparer.OrdinalIgnoreCase);
        Say(
            ctx,
            $"[Disguise] {names.Count} creature prefab(s)"
                + (filter != null ? $" containing \"{filter}\"" : "")
                + ":"
        );
        for (int i = 0; i < names.Count; i += 4)
        {
            int count = Math.Min(4, names.Count - i);
            Say(ctx, "  " + string.Join(", ", names.GetRange(i, count)));
        }
    }

    private static void Say(Terminal ctx, string text)
    {
        try
        {
            ctx.AddString(text);
        }
        catch (Exception ex)
        {
            LogInfo(text + "  (console display error: " + ex.Message + ")");
        }
    }
}
