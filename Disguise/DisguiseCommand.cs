#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// The 'disguise' console command (F5) and all of its sub-commands.
/// </summary>
internal static class DisguiseCommand
{
    private static bool s_consoleCommandRegistered;

    // ------------------------------------------------------------------ console command

    internal static void RegisterConsoleCommands()
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
            Disguise.LogError($"Failed to register the console command: {ex}");
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
        ObserverRefresh.StartObserverRefresh(zdo, force: true);
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
            $"[Disguise] v{Disguise.PluginVersion} - {(DisguiseConfig.ConfigEnabled.Value ? "enabled" : "disabled")}"
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
            Disguise.LogInfo(text + "  (console display error: " + ex.Message + ")");
        }
    }
}
