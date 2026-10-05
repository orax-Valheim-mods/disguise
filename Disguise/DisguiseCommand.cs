#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
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
                "Disguise: status | set <prefab> | enable | disable | refresh | view | logs | setFakePosition | perf | list [filter] | help",
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
        List<string> options =
            new()
            {
                "status",
                "help",
                "enable",
                "disable",
                "refresh",
                "view",
                "logs",
                "setFakePosition",
                "perf",
                "list",
            };
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

            case "view":
                SetOwnView(ctx, args.Length > 2 ? args[2] : null);
                break;

            case "logs":
                SetLogs(ctx, args.Length > 2 ? args[2] : null);
                break;

            case "setfakeposition":
                SetFakePosition(ctx, args);
                break;

            case "perf":
                string? frameTime = FrameMeter.Report();
                Say(
                    ctx,
                    frameTime != null
                        ? $"[Disguise] Frame time: {frameTime} - window reset."
                        : "[Disguise] No frames measured yet."
                );
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
        Say(ctx, "  disguise view [on|off]    show the creature on your own screen (local only)");
        Say(
            ctx,
            "  disguise logs [on|off]    write the mod's own Info messages to the BepInEx log"
        );
        Say(
            ctx,
            "  disguise setFakePosition  fake the position the others see on their map ([x z] | off)"
        );
        Say(ctx, "  disguise perf             frame time of the last few seconds (resets it)");
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

    /// <summary>
    /// "disguise view [on|off]": shows or hides your creature on your OWN screen. Purely
    /// local rendering (SelfView): it never changes what the other machines receive.
    /// </summary>
    private static void SetOwnView(Terminal ctx, string? argument)
    {
        bool ownView = DisguiseConfig.ConfigSeeYourDisguise.Value;
        if (argument == null)
        {
            ownView = !ownView;
        }
        else if (!TryParseOnOff(argument, out ownView))
        {
            Say(ctx, "Usage: disguise view [on|off]");
            return;
        }
        DisguiseConfig.ConfigSeeYourDisguise.Value = ownView;

        if (ownView)
        {
            Say(
                ctx,
                "[Disguise] Own view ON: your screen shows the creature in your place, "
                    + "exactly like the other clients see you (your own view only)."
            );
            if (PrefabResolver.CreaturePrefabHash == 0)
            {
                Say(
                    ctx,
                    "Warning: the configured creature does not exist here (see disguise list)."
                );
            }
        }
        else
        {
            Say(ctx, "[Disguise] Own view OFF: your screen shows your viking again.");
        }
    }

    /// <summary>
    /// "disguise logs [on|off]": decides whether the mod writes its own informational
    /// messages (Info / Debug) to the BepInEx log. Warnings and errors are never gated.
    /// </summary>
    private static void SetLogs(Terminal ctx, string? argument)
    {
        bool logs = DisguiseConfig.ConfigLogs.Value;
        if (argument == null)
        {
            logs = !logs;
        }
        else if (!TryParseOnOff(argument, out logs))
        {
            Say(ctx, "Usage: disguise logs [on|off]");
            return;
        }
        DisguiseConfig.ConfigLogs.Value = logs;

        Say(
            ctx,
            logs
                ? "[Disguise] Logs ON: the mod writes its Info messages to the BepInEx log again."
                : "[Disguise] Logs OFF: no more Info messages from the mod "
                    + "(warnings and errors are still written)."
        );
    }

    /// <summary>
    /// Every toggle of this command accepts the same words, so they are parsed in one place.
    /// Returns false for anything else, and the caller then prints its own usage line.
    /// </summary>
    private static bool TryParseOnOff(string argument, out bool value)
    {
        string word = argument.ToLowerInvariant();
        if (word == "on" || word == "true" || word == "1" || word == "enable")
        {
            value = true;
            return true;
        }
        if (word == "off" || word == "false" || word == "0" || word == "disable")
        {
            value = false;
            return true;
        }
        value = false;
        return false;
    }

    /// <summary>
    /// "disguise setFakePosition [x z | off]": decides what the OTHER maps display as your
    /// position. Without an argument the position you are standing on is recorded, so the
    /// others keep seeing you there while you actually move; two numbers are a point of the
    /// map plane (X and Z, your current height is kept); "off" shows the real position
    /// again. Only the player list the server broadcasts moves (FakePosition) - your
    /// character, the ZDO, your own screen and the world save stay at the real position,
    /// and the dedicated server needs this DLL to write the fake into that list.
    /// </summary>
    private static void SetFakePosition(Terminal ctx, Terminal.ConsoleEventArgs args)
    {
        if (args.Length >= 3 && IsOffArgument(args[2]))
        {
            FakePosition.Clear();
            PublishFakePosition(ctx);
            Say(
                ctx,
                "[Disguise] Fake map position removed: the other maps show your real position again."
            );
            return;
        }

        Vector3 position;
        if (args.Length >= 4)
        {
            if (
                !TryParseMapCoordinate(args[2], out float x)
                || !TryParseMapCoordinate(args[3], out float z)
            )
            {
                PrintFakePositionUsage(ctx);
                return;
            }
            position = new Vector3(x, CurrentHeight(), z);
        }
        else if (args.Length == 2)
        {
            if (Player.m_localPlayer == null)
            {
                Say(
                    ctx,
                    "[Disguise] No local player (main menu): nothing to record, "
                        + "use \"disguise setFakePosition <x> <z>\" instead."
                );
                return;
            }
            position = Player.m_localPlayer.transform.position;
        }
        else
        {
            PrintFakePositionUsage(ctx);
            return;
        }

        FakePosition.Set(position);
        PublishFakePosition(ctx);
        Say(
            ctx,
            $"[Disguise] Fake map position: ({position.x:0.0}, {position.z:0.0}) - the other maps show you there "
                + "while you keep moving normally (needs this DLL on the dedicated server)."
        );
        Say(
            ctx,
            "Your character and your own screen never move: only the position the others see on their map does."
        );
    }

    private static void PrintFakePositionUsage(Terminal ctx)
    {
        Say(
            ctx,
            "Usage: disguise setFakePosition          record where you stand and show it to the others"
        );
        Say(
            ctx,
            "       disguise setFakePosition <x> <z>  use that point of the map plane (X and Z)"
        );
        Say(ctx, "       disguise setFakePosition off      show your real position again");
    }

    private static bool IsOffArgument(string value)
    {
        string word = value.ToLowerInvariant();
        return word == "off" || word == "clear" || word == "none" || word == "disable";
    }

    private static bool TryParseMapCoordinate(string text, out float value)
    {
        // InvariantCulture first (the game writes "10.5"), then the current culture, so
        // that "10,5" is also accepted on a French keyboard.
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }
        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    /// <summary>The height at this very moment: only X and Z are asked for, Y is carried over.</summary>
    private static float CurrentHeight()
    {
        return Player.m_localPlayer != null ? Player.m_localPlayer.transform.position.y : 0f;
    }

    private static void PublishFakePosition(Terminal ctx)
    {
        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        if (zdo == null || !zdo.IsValid())
        {
            Say(ctx, "Note: no local player right now, it is published when you spawn.");
            return;
        }
        FakePosition.Publish(zdo);
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
        Say(
            ctx,
            $"  Own view on your screen: {(DisguiseConfig.ConfigSeeYourDisguise.Value ? "on" : "off")} (disguise view)"
        );
        Say(
            ctx,
            $"  Info logs: {(DisguiseConfig.ConfigLogs.Value ? "on" : "off")} (disguise logs)"
                + (
                    DisguiseConfig.ConfigLogs.Value
                        ? ""
                        : " - warnings and errors are still written"
                )
        );
        Say(
            ctx,
            FakePosition.Current is Vector3 fakePosition
                ? $"  Fake map position: ({fakePosition.x:0.0}, {fakePosition.z:0.0}) - the other maps show you there"
                : "  Fake map position: off (the other maps show your real position)"
        );
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
            // A console display failure must stay visible even with Logs off: this is the
            // only place where the text would otherwise be lost entirely.
            Disguise.LogWarning(text + "  (console display error: " + ex.Message + ")");
        }
    }
}
