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
                "Disguise: status | set <prefab> [level] | level <n> | enable | disable | refresh | view | logs | fly | offset | rotation | setFakePosition | perf | list [filter] | favorite [prefab] | gui | help",
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
            "level",
            "refresh",
            "view",
            "logs",
            "fly",
            "offset",
            "rotation",
            "setFakePosition",
            "perf",
            "list",
            "favorite",
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
    /// The sub may carry an argument the way the console spells it ("favorite Troll"), so it
    /// is split here into the words Execute reads.
    /// </summary>
    internal static void Run(string sub)
    {
        // One action = one line on the window: the notes a command goes on printing in the
        // console after its headline are dropped there.
        GuiStatus.Clear();
        Execute(null, ("disguise " + sub).Split(' '));
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

            case "offset":
                SetOffset(ctx, words);
                break;

            case "rotation":
                SetRotation(ctx, words);
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
                    Say(
                        ctx,
                        "Usage: disguise set <prefab_name> [level]   (disguise list for the names)"
                    );
                    break;
                }
                int setLevel = 1; // set without a level starts every costume at 1
                if (words.Length > 3 && !TryParseLevel(words[3], out setLevel))
                {
                    Say(ctx, NotALevel(words[3]));
                    break;
                }
                // The level moves WITH the creature and before it, so the costume is applied
                // once, with both values already in the config.
                DisguiseConfig.ConfigCreatureLevel.Value = setLevel;
                bool applied = ApplyCreature(words[2], "set");
                Say(
                    ctx,
                    $"[Disguise] Prefab set to \"{DisguiseConfig.ConfigCreature.Value}\" at level {DisguiseConfig.ConfigCreatureLevel.Value} (resolved prefab: {PrefabResolver.PrefabName(PrefabResolver.CreaturePrefabHash)})."
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

            case "level":
                if (words.Length < 3)
                {
                    Say(
                        ctx,
                        $"[Disguise] Current level: {DisguiseConfig.ConfigCreatureLevel.Value}."
                    );
                    Say(
                        ctx,
                        "Usage: disguise level <n>   (2 and 3 are the starred creatures, no upper limit)"
                    );
                    break;
                }
                if (!TryParseLevel(words[2], out int level))
                {
                    Say(ctx, NotALevel(words[2]));
                    break;
                }
                DisguiseConfig.ConfigCreatureLevel.Value = level;
                Say(
                    ctx,
                    $"[Disguise] Level set to {DisguiseConfig.ConfigCreatureLevel.Value}: the costume is drawn as a creature of that level - another skin at 2 and 3 where the prefab has one, a star, and the health of one."
                );
                ApplyNow(ctx, "level");
                break;

            case "list":
                ListPrefabs(ctx, words.Length > 2 ? words[2] : null);
                break;

            case "favorite":
                ToggleFavorite(ctx, RestOf(words, 2));
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
            "  disguise list [filter]    every vanilla prefab you can become (the window too)"
        );
        Say(ctx, "  disguise favorite [name]  mark / unmark a favorite (the window's Favorite");
        Say(ctx, "                            button does this, on the selected prefab)");
        Say(ctx, "  disguise set <prefab>     change the prefab and enable the disguise");
        Say(ctx, "                            ([level] with it, and 1 when it is left out;");
        Say(ctx, "                            a row of the window does the same, at your level)");
        Say(ctx, "  disguise level <n>        the level the costume is drawn at: 2 and 3 are the");
        Say(ctx, "                            starred creatures (the window's -/+ row does this,");
        Say(ctx, "                            and no upper limit - the game itself has none)");
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
        Say(
            ctx,
            "  disguise offset <X Y Z>   move the costume (world units, 1 = 1 metre, visual only)"
        );
        Say(ctx, "  disguise rotation <X Y Z> turn the costume in place (degrees, visual only)");
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
    /// "disguise offset <X> <Y> <Z>": moves the costume away from you - on your own screen and,
    /// with this DLL on the dedicated server, on every other machine. Your player never moves:
    /// the position the server keeps, saves and runs the game logic with stays where you
    /// stand, only what is drawn and what goes out on the wire is moved (PoseControl). Without
    /// arguments it only reports where the costume stands right now.
    /// </summary>
    private static void SetOffset(Terminal? ctx, string[] words)
    {
        const string usage =
            "Usage: disguise offset <X> <Y> <Z>   (world units, 1 = 1 metre; 0 0 0 puts it back)";
        if (words.Length < 5)
        {
            Vector3 current = PoseControl.Offset;
            Say(ctx, $"[Disguise] Offset: ({current.x:0.0}, {current.y:0.0}, {current.z:0.0})");
            Say(ctx, usage);
            return;
        }
        if (!TryReadPose(ctx, words, usage, out float x, out float y, out float z))
        {
            return;
        }

        PoseControl.SetOffset(x, y, z);
        Vector3 pose = PoseControl.Offset;
        Say(
            ctx,
            $"[Disguise] Offset set: ({pose.x:0.0}, {pose.y:0.0}, {pose.z:0.0}) - the costume is drawn that far from you."
        );
        Say(
            ctx,
            "Your player never moves: only the position the others are handed does (needs this DLL on the dedicated server)."
        );
    }

    /// <summary>
    /// "disguise rotation <X> <Y> <Z>": turns the costume in place, in degrees, ON TOP of the
    /// way it already faces - zero therefore changes nothing at all. Same two places as the
    /// offset (PoseControl), same rule: the player itself is never turned, and what the server
    /// stores in the save keeps the rotation the game gave you.
    /// </summary>
    private static void SetRotation(Terminal? ctx, string[] words)
    {
        const string usage =
            "Usage: disguise rotation <X> <Y> <Z>   (degrees; 0 0 0 faces it the way it normally does)";
        if (words.Length < 5)
        {
            Vector3 current = PoseControl.Euler;
            Say(ctx, $"[Disguise] Rotation: ({current.x:0.0}, {current.y:0.0}, {current.z:0.0})");
            Say(ctx, usage);
            return;
        }
        if (!TryReadPose(ctx, words, usage, out float x, out float y, out float z))
        {
            return;
        }

        PoseControl.SetRotation(x, y, z);
        Vector3 turn = PoseControl.Euler;
        Say(
            ctx,
            $"[Disguise] Rotation set: ({turn.x:0.0}, {turn.y:0.0}, {turn.z:0.0}) - the costume is drawn that way."
        );
        Say(
            ctx,
            "Your player never turns: only what the others are handed does (needs this DLL on the dedicated server)."
        );
    }

    /// <summary>Reads the three numbers of a pose command, or reports what stopped it.</summary>
    private static bool TryReadPose(
        Terminal? ctx,
        string[] words,
        string usage,
        out float x,
        out float y,
        out float z
    )
    {
        x = 0f;
        y = 0f;
        z = 0f;
        if (
            !TryParseCoordinate(words[2], out x)
            || !TryParseCoordinate(words[3], out y)
            || !TryParseCoordinate(words[4], out z)
        )
        {
            Say(ctx, "[Disguise] The three values must be numbers (e.g. 0 1.5 -45).");
            Say(ctx, usage);
            return false;
        }
        return true;
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
                !TryParseCoordinate(words[2], out float x)
                || !TryParseCoordinate(words[3], out float z)
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

    /// <summary>
    /// One number as the game and both keyboards write it: InvariantCulture first (the game
    /// writes "10.5"), then the current culture, so that "10,5" is also accepted on a French
    /// keyboard. Shared with the pose (offset / rotation) and with the window, which types the
    /// same three numbers into its own fields.
    /// </summary>
    internal static bool TryParseCoordinate(string text, out float value)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }
        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    /// <summary>
    /// A level as the game takes one: a WHOLE number, from DisguiseConfig.LevelMin up and
    /// with NO ceiling - Character.SetLevel accepts every level the game itself does, and a
    /// costume of a level above the skins a prefab carries only keeps raising the health it
    /// is drawn with.
    /// </summary>
    private static bool TryParseLevel(string text, out int level)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out level))
        {
            return false;
        }
        level = Mathf.Max(DisguiseConfig.LevelMin, level);
        return true;
    }

    /// <summary>What a word that is not a level is answered with, wherever it was typed.</summary>
    private static string NotALevel(string text) =>
        $"[Disguise] \"{text}\" is not a level: a whole number, {DisguiseConfig.LevelMin} or more.";

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
            $"  Item sparkle on your screen: {(DisguiseConfig.ConfigHideItemSparkle.Value ? "hidden" : "shown")} (HideItemSparkle, 4 - Own view)"
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
            $"  Pose: offset ({PoseControl.Offset.x:0.0}, {PoseControl.Offset.y:0.0}, {PoseControl.Offset.z:0.0}), "
                + $"rotation ({PoseControl.Euler.x:0.0}, {PoseControl.Euler.y:0.0}, {PoseControl.Euler.z:0.0}) "
                + "(disguise offset, disguise rotation)"
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
    /// "disguise favorite [prefab]": marks a prefab as a favorite, or takes the mark off - one
    /// call does either and answers which. No name means the configured creature, and that is
    /// the whole of how the window's Favorite button runs it (it is this call with no name,
    /// so button and console are the same call and can never mark two different prefabs). A
    /// name is the other way to spell it - from the console or from a hotkey - and it may be
    /// any prefab at all. The window's list follows through GuiCategories.Sync, which reads
    /// the config line every frame while it is open: nothing here ever reaches into the GUI.
    /// </summary>
    private static void ToggleFavorite(Terminal? ctx, string name)
    {
        string target =
            name.Length > 0 ? name : DisguiseConfig.ConfigCreature.Value ?? string.Empty;
        if (target.Length == 0)
        {
            Say(ctx, "[Disguise] Nothing to mark: no prefab is configured.");
            return;
        }
        bool added = PrefabFavorites.Toggle(target);
        Say(
            ctx,
            added
                ? $"[Disguise] \"{target}\" added to favorites."
                : $"[Disguise] \"{target}\" taken off the favorites."
        );
    }

    /// <summary>Every word from <paramref name="from"/> on, as one value: a name may hold a space.</summary>
    private static string RestOf(string[] words, int from)
    {
        return words.Length > from
            ? string.Join(" ", words, from, words.Length - from)
            : string.Empty;
    }

    /// <summary>
    /// "disguise list [filter]": every VANILLA registered prefab, in the six categories of
    /// PrefabCatalog - the complete catalogue, whatever the window's checkboxes have on (they
    /// only hide rows there). Counts per category first, four names per line after that.
    /// Empty without a ZNetScene (main menu), which is where the notice above comes from.
    ///
    /// This is the one answer long enough to scroll away in the console, so every line also
    /// goes to the BepInEx log through SayLogged - which is gated by the Logs option, like
    /// every Info line of the mod.
    /// </summary>
    private static void ListPrefabs(Terminal? ctx, string? filter)
    {
        if (ZNetScene.instance == null)
        {
            const string notice =
                "ZNetScene unavailable (main menu): the list is available in game.";
            Say(ctx, "[Disguise] " + notice);
            Disguise.LogInfo(notice);
            return;
        }
        List<string> names = PrefabCatalog.Names(filter, null);
        string header =
            $"{names.Count} prefab(s)" + (filter != null ? $" containing \"{filter}\"" : "") + ":";
        Say(ctx, "[Disguise] " + header);
        Disguise.LogInfo(header);
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
        SayLogged(ctx, "  kinds: " + kinds);
        for (int i = 0; i < names.Count; i += 4)
        {
            int count = Math.Min(4, names.Count - i);
            SayLogged(ctx, "  " + string.Join(", ", names.GetRange(i, count)));
        }
    }

    /// <summary>
    /// One line of "disguise list": into the console like every other line, and into the
    /// BepInEx log as well. Disguise.LogInfo writes only while the Logs option is on
    /// ([1 - General]), so a log stays as quiet as that option asks - the headline and the
    /// notice above take care of their own "[Disguise] " lead, which the log does not need.
    /// </summary>
    private static void SayLogged(Terminal? ctx, string text)
    {
        Say(ctx, text);
        Disguise.LogInfo(text);
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
