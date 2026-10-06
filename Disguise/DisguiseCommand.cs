#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Disguise;

/// <summary>
/// The 'disguise' console command (F5) and all of its sub-commands - also run by the four
/// buttons of the window (Run), which has no console to print to and hands the answer to
/// GuiStatus instead (Say).
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
                "Disguise: status | set <prefab> | enable | disable | refresh | view | logs | fly | setFakePosition | perf | list [filter] | gui | help",
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
        List<string> options = new()
        {
            "status",
            "help",
            "enable",
            "disable",
            "refresh",
            "view",
            "logs",
            "fly",
            "setFakePosition",
            "perf",
            "list",
            "gui",
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
        string[] words = new string[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            words[i] = args[i];
        }
        Execute(args.Context, words);
    }

    /// <summary>
    /// Runs one sub-command with no console behind it: a button of the window (GuiWindow).
    /// There is no terminal to print to, so Say hands the message to the window's own status
    /// line (GuiStatus) - the console and the window report their own way, on the same text.
    /// </summary>
    internal static void Run(string sub)
    {
        // One action = one line on the window: the notes a command goes on printing in the
        // console after its headline are dropped there.
        GuiStatus.Clear();
        Execute(null, new[] { "disguise", sub });
    }

    /// <summary>The sub-command table: the console passes its terminal, the window none.</summary>
    private static void Execute(Terminal? ctx, string[] words)
    {
        string sub = words.Length > 1 ? words[1].ToLowerInvariant() : "status";

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
                SetOwnView(ctx, words.Length > 2 ? words[2] : null);
                break;

            case "logs":
                SetLogs(ctx, words.Length > 2 ? words[2] : null);
                break;

            case "fly":
                SetFly(ctx, words.Length > 2 ? words[2] : null);
                break;

            case "gui":
                SetGui(ctx, words.Length > 2 ? words[2] : null);
                break;

            case "setfakeposition":
                SetFakePosition(ctx, words);
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
                if (words.Length < 3)
                {
                    Say(ctx, "Usage: disguise set <prefab_name>   (disguise list for the names)");
                    break;
                }
                bool applied = ApplyCreature(words[2], "set");
                Say(
                    ctx,
                    $"[Disguise] Prefab set to \"{DisguiseConfig.ConfigCreature.Value}\" (resolved prefab: {PrefabResolver.PrefabName(PrefabResolver.CreaturePrefabHash)})."
                );
                if (!applied)
                {
                    Say(
                        ctx,
                        "[Disguise] No local player (main menu): the change will be applied on the next spawn."
                    );
                }
                if (PrefabResolver.CreaturePrefabHash == 0)
                {
                    Say(ctx, "Warning: this prefab does not exist here (see disguise list).");
                }
                break;

            case "list":
                ListPrefabs(ctx, words.Length > 2 ? words[2] : null);
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
                Say(ctx, $"[Disguise] Unknown subcommand \"{words[1]}\".");
                PrintHelp(ctx);
                break;
        }
    }

    private static void PrintHelp(Terminal? ctx)
    {
        Say(ctx, "[Disguise] Available commands:");
        Say(ctx, "  disguise status           current state (prefab, marker, save protection)");
        Say(
            ctx,
            "  disguise list [filter]    every prefab you can become (the window shows it too)"
        );
        Say(ctx, "  disguise set <prefab>     change the prefab and enable the disguise");
        Say(ctx, "                            (a click on a row of the window does this too)");
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
            "  disguise fly [on|off]     fly: the creature is raised for the others (visual only)"
        );
        Say(ctx, "  disguise gui [on|off]     open / close the window (same as the GuiKey chord)");
        Say(
            ctx,
            "  disguise setFakePosition  fake the position the others see on their map ([x z] | off)"
        );
        Say(ctx, "  disguise perf             frame time of the last few seconds (resets it)");
        Say(ctx, "  disguise help             show this help");
        Say(ctx, "  Config file: BepInEx\\config\\orax.Disguise.cfg");
        Say(ctx, "  Install on the dedicated server AND on your PC only, never for other players.");
    }

    /// <summary>
    /// The one place that changes the creature: what "disguise set" runs, and what a click on
    /// a row of the window runs (GuiCreatureList). It writes the config (which invalidates
    /// the resolved hash through SettingChanged) and re-applies at once - and it prints
    /// nothing: the console and the window report their own way, on top of it.
    ///
    /// Returns false when there is no local player (main menu): the change is stored all the
    /// same and lands on the next spawn, which is exactly what ApplyNow announces.
    /// </summary>
    internal static bool ApplyCreature(string prefabName, string reason)
    {
        DisguiseConfig.ConfigCreature.Value = prefabName;
        DisguiseConfig.ConfigEnabled.Value = true;
        return ApplyNowCore(reason);
    }

    /// <summary>
    /// Re-applies the disguise on the local player right now. Returns false in the main menu
    /// (no local player), where nothing can be applied yet.
    /// </summary>
    private static bool ApplyNowCore(string reason)
    {
        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        if (zdo == null || !zdo.IsValid())
        {
            return false;
        }
        DisguiseApplier.ApplyTo(zdo, reason);
        return true;
    }

    private static void ApplyNow(Terminal? ctx, string reason)
    {
        if (!ApplyNowCore(reason))
        {
            Say(
                ctx,
                "[Disguise] No local player (main menu): the change will be applied on the next spawn."
            );
        }
    }

    /// <summary>
    /// "disguise refresh": re-applies the disguise and immediately asks the server for an
    /// observer refresh - the manual fallback when a vanilla client still shows the old look.
    /// </summary>
    private static void RefreshObservers(Terminal? ctx)
    {
        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        if (zdo == null || !zdo.IsValid())
        {
            Say(ctx, "[Disguise] No local player (main menu): nothing to refresh.");
            return;
        }
        DisguiseApplier.ApplyTo(zdo, "refresh");
        ObserverRefresh.StartObserverRefresh(zdo, force: true);
        // Headline first: the console reads it first, and the window's status line, which
        // keeps one line per action, would otherwise only ever show the note.
        Say(
            ctx,
            $"[Disguise] Observer refresh requested: other players re-create your object "
                + $"in ~{Mathf.Clamp(DisguiseConfig.ConfigRefreshHold.Value, 0.2f, 3f):0.0} s (this mod is required on the dedicated server)."
        );
        if (!DisguiseConfig.ConfigForceRefresh.Value)
        {
            Say(ctx, "Note: ForceObserverRefresh is false, so this refresh was forced manually.");
        }
    }

    /// <summary>
    /// "disguise view [on|off]": shows or hides your creature on your OWN screen. Purely
    /// local rendering (SelfView): it never changes what the other machines receive.
    /// </summary>
    private static void SetOwnView(Terminal? ctx, string? argument)
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
                Say(ctx, "Warning: the configured prefab does not exist here (see disguise list).");
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
    private static void SetLogs(Terminal? ctx, string? argument)
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
    /// "disguise fly [on|off]": raises the creature above you for the other machines (and on
    /// your own screen with SeeYourDisguise). Your player never moves: the position the
    /// server keeps, saves and runs the game logic with stays on the ground - only what is
    /// drawn and what goes out on the wire is raised (FlightControl).
    /// </summary>
    private static void SetFly(Terminal? ctx, string? argument)
    {
        bool flying = FlightControl.IsFlying;
        if (argument == null)
        {
            flying = !flying;
        }
        else if (!TryParseOnOff(argument, out flying))
        {
            Say(ctx, "Usage: disguise fly [on|off]");
            return;
        }

        if (flying)
        {
            FlightControl.Enable();
        }
        else
        {
            FlightControl.Disable();
        }

        if (FlightControl.IsFlying)
        {
            Say(
                ctx,
                $"[Disguise] Flight ON: the creature is drawn +{FlightControl.Height:0.0} m above you, "
                    + "your own player stays on the ground."
            );
            if (ZNet.instance != null && !ZNet.instance.IsServer())
            {
                Say(
                    ctx,
                    "Note: the others see it only while the dedicated server runs this DLL (section 1.b)."
                );
            }
        }
        else
        {
            Say(ctx, "[Disguise] Flight OFF: the creature is back on the ground.");
        }
    }

    /// <summary>
    /// "disguise gui [on|off]": opens or closes the window - the same thing as the GuiKey
    /// chord (F8 + LeftAlt by default), for whoever prefers the console. The window is an
    /// overlay: the game keeps running, only the frames with the pointer on the window or the
    /// filter field focused are handed to the UI (no click reaches the character, the camera
    /// waits, typing never walks it away).
    /// </summary>
    private static void SetGui(Terminal? ctx, string? argument)
    {
        bool open = DisguiseGui.IsOpen;
        if (argument == null)
        {
            open = !open;
        }
        else if (!TryParseOnOff(argument, out open))
        {
            Say(ctx, "Usage: disguise gui [on|off]");
            return;
        }

        if (open)
        {
            string? failure = DisguiseGui.Show();
            if (failure != null)
            {
                Say(ctx, $"[Disguise] Window not opened: {failure}");
                return;
            }
            Say(
                ctx,
                "[Disguise] Window opened: drag it anywhere, filter or untick a category, click a row to pick a prefab; Close (or the key chord) dismisses it."
            );
        }
        else
        {
            DisguiseGui.Hide();
            Say(ctx, "[Disguise] Window closed.");
        }
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
    private static void SetFakePosition(Terminal? ctx, string[] words)
    {
        if (words.Length >= 3 && IsOffArgument(words[2]))
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
        if (words.Length >= 4)
        {
            if (
                !TryParseMapCoordinate(words[2], out float x)
                || !TryParseMapCoordinate(words[3], out float z)
            )
            {
                PrintFakePositionUsage(ctx);
                return;
            }
            position = new Vector3(x, CurrentHeight(), z);
        }
        else if (words.Length == 2)
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

    private static void PrintFakePositionUsage(Terminal? ctx)
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

    private static void PublishFakePosition(Terminal? ctx)
    {
        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        if (zdo == null || !zdo.IsValid())
        {
            Say(ctx, "Note: no local player right now, it is published when you spawn.");
            return;
        }
        FakePosition.Publish(zdo);
    }

    private static void PrintStatus(Terminal? ctx)
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
            $"  Configured prefab: \"{DisguiseConfig.ConfigCreature.Value}\" -> {PrefabResolver.PrefabName(PrefabResolver.CreaturePrefabHash)}"
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
            $"  Flight: {(FlightControl.IsFlying ? $"on, +{FlightControl.Height:0.0} m" : "off")} (disguise fly, keys in [5 - Flight])"
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

    /// <summary>
    /// "disguise list [filter]": every registered prefab, in the six categories of
    /// PrefabCatalog - the complete catalogue, whatever the window's checkboxes have on (they
    /// only hide rows there). Counts per category first, four names per line after that.
    /// Empty without a ZNetScene (main menu), which is where the notice above comes from.
    /// </summary>
    private static void ListPrefabs(Terminal? ctx, string? filter)
    {
        if (ZNetScene.instance == null)
        {
            Say(
                ctx,
                "[Disguise] ZNetScene unavailable (main menu): the list is available in game."
            );
            return;
        }
        List<string> names = PrefabCatalog.Names(filter, null);
        Say(
            ctx,
            $"[Disguise] {names.Count} prefab(s)"
                + (filter != null ? $" containing \"{filter}\"" : "")
                + ":"
        );
        int[] counts = PrefabCatalog.Counts(filter);
        string kinds = string.Empty;
        for (int i = 0; i < counts.Length; i++)
        {
            kinds +=
                (i > 0 ? ", " : string.Empty)
                + PrefabCatalog.Label((PrefabKind)i)
                + " "
                + counts[i];
        }
        Say(ctx, "  kinds: " + kinds);
        for (int i = 0; i < names.Count; i += 4)
        {
            int count = Math.Min(4, names.Count - i);
            Say(ctx, "  " + string.Join(", ", names.GetRange(i, count)));
        }
    }

    /// <summary>
    /// One line of output, to whichever of the two places has the command in front of it: the
    /// console when ctx is the terminal the command was typed in, the window's own status
    /// line when a button ran it (ctx = null). The text is identical, only where it is read
    /// changes - the console and the window report their own way.
    /// </summary>
    private static void Say(Terminal? ctx, string text)
    {
        if (ctx == null)
        {
            GuiStatus.Show(text);
            return;
        }

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
