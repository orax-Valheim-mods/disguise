#nullable enable
using System;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Every mod setting (BepInEx config file) plus their binding. The other classes read
/// the entries through DisguiseConfig.ConfigCreature.Value and friends.
/// </summary>
internal static class DisguiseConfig
{
    /// <summary>
    /// The span a costume's level is kept inside: the very one the vanilla spawn command
    /// gives a creature - Terminal.cs clamps its [level] argument with Mathf.Min(level, 9)
    /// BEFORE calling SetLevel (4 for an item), so "spawn Troll 1 100" hands out a level 9.
    /// The costume follows that same span wherever a level can be set: this entry (BepInEx
    /// clamps it on load and on every write), the two commands (TryParseLevel) and the
    /// window's + (SyncLevelButtons). The game itself never clamps - SetLevel asks only for
    /// >= 1 and Awake reads the ZDO as it stands - so the ceiling is a choice here: staying
    /// inside what vanilla hands out. A level outside the skins a prefab carries only keeps
    /// raising the health the costume is drawn with.
    /// </summary>
    internal const int LevelMin = 1;

    internal const int LevelMax = 9;

    internal static ConfigEntry<bool> ConfigEnabled = null!;

    internal static ConfigEntry<string> ConfigCreature = null!;

    /// <summary>Level the costume appears as (LevelMin..LevelMax): see BindGeneral.</summary>
    internal static ConfigEntry<int> ConfigCreatureLevel = null!;

    internal static ConfigEntry<bool> ConfigProtectSave = null!;

    internal static ConfigEntry<float> ConfigReassert = null!;

    internal static ConfigEntry<bool> ConfigLogs = null!;

    internal static ConfigEntry<bool> ConfigForceRefresh = null!;

    internal static ConfigEntry<float> ConfigRefreshHold = null!;

    /// <summary>
    /// Leave THIS player's entry out of the player list the server broadcasts (OffList):
    /// every other game then shows it nowhere on that list - the "Players" line and count
    /// of its F2 overlay, the session player list of its map menu and the pins of its map.
    /// The choice travels in the player ZDO (cd_offlist), like the fake map position.
    /// </summary>
    internal static ConfigEntry<bool> ConfigHideFromPlayerList = null!;

    // -- 3 - Display: what the OTHER machines see while you are disguised ---------------
    // What the costume is DRAWN with, in the two halves every piece of gear belongs to: its
    // armor, and the weapons - whether there are any at all (ShowWeapons), and whose they
    // are when there are (WhoseWeapons). The four armour rows (helmet, chest, legs,
    // cape) have no entry any more: a humanoid costume - every vanilla creature - wears ITS
    // OWN gear in those rows, whatever the mask says (CostumeGear rewrites them either way),
    // so a setting could only ever be read for a costume without a humanoid - a case
    // EquipmentMask simply keeps covered for good (BuildMask). Held items are the same
    // story now: those two weapons options decide that pair alone, which is why
    // HideHeldItems is gone (its bit is still the mask's, only read another way). What is
    // left besides them: the health-bar relation.
    internal static ConfigEntry<bool> ConfigShowAsAlly = null!;

    internal static ConfigEntry<bool> ConfigShowArmor = null!;

    /// <summary>The switch of the pair: whether the costume is drawn with weapons at all.</summary>
    internal static ConfigEntry<bool> ConfigShowWeapons = null!;

    internal static ConfigEntry<string> ConfigWhoseWeapons = null!;

    /// <summary>
    /// The two WORDS of WhoseWeapons, spelled the way the .cfg holds them - the answers it
    /// has always taken, and the two no item name may ever be told from (FixedWeapon).
    /// </summary>
    internal const string WeaponsCreature = "Creature";

    internal const string WeaponsPlayer = "Player";

    /// <summary>
    /// Whether the costume is drawn with the weapons YOU hold rather than with the ones the
    /// creature is born with - the second half of the pair, read on top of the switch above
    /// (while ShowWeapons is off there are no weapons at all, and this says nothing a screen
    /// can show). Compared ignoring case, so a value typed into the .cfg by hand ("player")
    /// lands the same way as one of the two names above - which is what the entry's
    /// acceptable values hold, and what the window and the console only ever write.
    /// </summary>
    internal static bool PlayerWeapons() =>
        string.Equals(ConfigWhoseWeapons.Value, WeaponsPlayer, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The ONE item of the creature's own pool WhoseWeapons fixes the costume's hands on,
    /// read straight out of the entry: null while it still holds one of the two words above
    /// - "Creature", the seeded roll, and "Player", yours - or nothing at all, which is where
    /// a fresh install starts. Anything else is an item NAME, trimmed and compared like those
    /// two (no case), so "club" typed into the .cfg lands the same way as the "Club" the
    /// window writes.
    ///
    /// Not a check of any pool: the entry may name an item no longer held by the creature
    /// configured at this very moment (another creature was configured under it, or the file
    /// was edited by hand), and every reader therefore asks GearSkin.WeaponHash for the pool
    /// it is about to draw - an answer the pool does not hold is the roll, never an item of
    /// another creature. ReconcileWhoseWeapons is what writes such a stale name back out of
    /// the entry itself, so the .cfg, the window and the console cannot end up disagreeing.
    /// </summary>
    internal static string? FixedWeapon()
    {
        string value = ConfigWhoseWeapons.Value.Trim();
        if (value.Length == 0)
        {
            return null;
        }
        if (
            string.Equals(value, WeaponsCreature, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, WeaponsPlayer, StringComparison.OrdinalIgnoreCase)
        )
        {
            return null;
        }
        return value;
    }

    /// <summary>
    /// Puts WhoseWeapons back on "Creature" while it names an item the CONFIGURED creature
    /// cannot hold - a choice made for the last creature, or a name typed into the .cfg by
    /// hand. The roll is the only one of the three answers that fits every creature, and
    /// keeping the name instead would leave the entry saying one thing while every reader
    /// (LocalWeaponHash) answers another.
    ///
    /// Nothing is written while the creature cannot be resolved yet: no ZNetScene (the
    /// config is bound at startup, before any world is loaded) or no such prefab, both of
    /// which are "no answer" rather than "no such weapon" - the readers ask the very same
    /// pool and answer the roll for as long as it stays unreadable.
    /// </summary>
    internal static void ReconcileWhoseWeapons()
    {
        string? fixedWeapon = FixedWeapon();
        if (fixedWeapon == null)
        {
            return;
        }
        GameObject? prefab = PrefabResolver.CreaturePrefab();
        if (prefab != null && GearSkin.WeaponHash(prefab, fixedWeapon) == 0)
        {
            ConfigWhoseWeapons.Value = WeaponsCreature;
        }
    }

    // -- 4 - Own view: what YOU see on your own screen ----------------------------------
    internal static ConfigEntry<bool> ConfigSeeYourDisguise = null!;

    internal static ConfigEntry<bool> ConfigHideItemSparkle = null!;

    internal static ConfigEntry<KeyboardShortcut> ConfigFlyEnableKey = null!;

    internal static ConfigEntry<KeyboardShortcut> ConfigFlyDisableKey = null!;

    internal static ConfigEntry<KeyboardShortcut> ConfigFlyUpKey = null!;

    internal static ConfigEntry<KeyboardShortcut> ConfigFlyDownKey = null!;

    internal static ConfigEntry<float> ConfigFlySpeed = null!;

    internal static ConfigEntry<bool> ConfigFlyScrollWheel = null!;

    internal static ConfigEntry<float> ConfigFlyScrollStep = null!;

    internal static ConfigEntry<KeyboardShortcut> ConfigGuiKey = null!;

    internal static ConfigEntry<int> ConfigGuiScrollStep = null!;

    // -- 6 - GUI: the OTHER way to the window - the Disguise line of the pause menu. Read
    //    when that menu opens (PauseMenuEntry.Add) and applied at once when the value moves
    //    (the handler bound below), which builds the line or takes it out again.
    internal static ConfigEntry<bool> ConfigShowMenuEntry = null!;

    // -- 6 - GUI, the window itself: where it SITS. An offset from the middle of the screen
    //    in pixels - 0/0 is the middle, which is what a fresh install gets and what the two
    //    entries say until a window has been dragged. DisguiseGui reads the place every
    //    frame the window is open and writes it back once, when the window closes;
    //    GuiWindow puts it on the panel when the window is built.
    internal static ConfigEntry<float> ConfigWindowX = null!;

    internal static ConfigEntry<float> ConfigWindowY = null!;

    /// <summary>
    /// Where the window sits, as one place: the two entries above, an offset from the middle
    /// of the screen in pixels - 0/0 being that middle, which is why a window nobody has
    /// dragged opens centred. Read by GuiWindow.Place when the window is built and by
    /// DisguiseGui every frame, so a value typed in ConfigurationManager lands at once.
    /// </summary>
    internal static Vector2 WindowPlace => new Vector2(ConfigWindowX.Value, ConfigWindowY.Value);

    // -- 6 - GUI, continued: which categories the window's list shows (the six checkboxes -
    //    one per kind, read there at every opening and at every frame, so a value typed in
    //    ConfigurationManager lands without reopening. The console's "disguise list" always
    //    prints every kind: these hide rows in the window, never names from the catalogue.
    //    The first box is Favorites - not a kind, but a mark kept in its own entry below,
    //    and while it is on the six Show* values are simply ignored (GuiCategories.ShowRow);
    //    the second, All, has no entry either - it is those six written at once, so nothing
    //    of it lands in this file (GuiCategories).
    internal static ConfigEntry<bool> ConfigShowStatic = null!;

    internal static ConfigEntry<bool> ConfigShowCreatures = null!;

    internal static ConfigEntry<bool> ConfigShowItems = null!;

    internal static ConfigEntry<bool> ConfigShowBuildings = null!;

    internal static ConfigEntry<bool> ConfigShowNature = null!;

    internal static ConfigEntry<bool> ConfigShowOther = null!;

    internal static ConfigEntry<bool> ConfigShowFavorites = null!;

    internal static ConfigEntry<string> ConfigFavorites = null!;

    /// <summary>
    /// Binds every setting to the config file. Called once, from Disguise.Awake. The work
    /// itself sits in one private method per section - the six sections of the .cfg, in the
    /// order the file shows them - so no method grows past the screen it is read on, and a
    /// section can be read or changed on its own. Each of those methods binds its settings
    /// AND registers the handlers of those settings: what belongs together stays together.
    /// </summary>
    internal static void Bind(ConfigFile config)
    {
        BindGeneral(config);
        BindMultiplayer(config);
        BindDisplay(config);
        BindOwnView(config);
        BindFlight(config);
        BindGui(config);
    }

    /// <summary>Section `1 - General`: the mod itself, and what the log writes.</summary>
    private static void BindGeneral(ConfigFile config)
    {
        ConfigEnabled = config.Bind(
            "1 - General",
            "Enabled",
            true,
            "Disguise enabled. To become a visible viking again: \"disguise disable\" in the console (F5), "
                + "then restart the server. NEVER delete the DLL without disabling this option first (see ProtectSaveFile)."
        );

        ConfigCreature = config.Bind(
            "1 - General",
            "CreaturePrefab",
            "Greydwarf",
            "Exact name of the creature prefab (e.g. Troll, Draugr, Wolf, Greydwarf, Blob, Skeleton, Neck, Boar, Eikthyr...). "
                + "Console command: disguise list to see the available names."
        );

        ConfigCreature.SettingChanged += (_, _) =>
        {
            PrefabResolver.InvalidateCreatureHash();
            // The pool WhoseWeapons is read against just moved with it: a weapon chosen for
            // the last creature is not one this creature can hold, and the roll is the only
            // answer that always fits (ReconcileWhoseWeapons).
            ReconcileWhoseWeapons();
        };

        ConfigCreatureLevel = config.Bind(
            "1 - General",
            "CreatureLevel",
            1,
            new ConfigDescription(
                "The level you appear as: 1 is the plain creature, 2 and 3 the starred ones - "
                    + "another skin where the prefab has one, a star over the costume, and the "
                    + "health of a real creature of that level (Character multiplies it by the "
                    + "level). Beyond the skins a prefab carries, only that health keeps "
                    + "growing. Kept inside 1 to 9, the span the vanilla spawn command gives "
                    + "a creature. Console: disguise level <1-9>, or disguise set <name> [level].",
                new AcceptableValueRange<int>(LevelMin, LevelMax)
            )
        );

        ConfigProtectSave = config.Bind(
            "1 - General",
            "ProtectSaveFile",
            true,
            "Writes \"Player\" (not the creature prefab) to the world save. "
                + "The disguise is re-applied automatically on load. Keeps the world usable if the mod is removed."
        );

        ConfigReassert = config.Bind(
            "1 - General",
            "ReassertSeconds",
            2f,
            "Period (seconds) between checks / re-application of the disguise on your own player."
        );

        ConfigLogs = config.Bind(
            "1 - General",
            "Logs",
            false,
            "Write the mod's own informational messages (Info / Debug) to the BepInEx log "
                + "(BepInEx\\LogOutput.log). Off by default: a working install keeps its log "
                + "quiet, and \"disguise logs on\" switches these lines on - the command "
                + "writes the value back, so it stays for the next start. "
                + "Warnings and errors are always written, whatever this value is: they are "
                + "what tells a broken install from a working one. "
                + "Console command: disguise logs [on|off]."
        );
    }

    /// <summary>Section `2 - Multiplayer`: what the other machines are told, and when.</summary>
    private static void BindMultiplayer(ConfigFile config)
    {
        ConfigForceRefresh = config.Bind(
            "2 - Multiplayer",
            "ForceObserverRefresh",
            true,
            "Automatically makes other players re-create your object right after a disguise change, "
                + "so vanilla clients that already saw you switch immediately (requires this DLL on the dedicated server). "
                + "Without it, they only refresh when you leave and re-enter their zone (~100 m) or after a reconnection."
        );

        ConfigRefreshHold = config.Bind(
            "2 - Multiplayer",
            "RefreshHoldSeconds",
            1.5f,
            "How long (seconds) the server relays your player ZDO with a fake far-away position during an "
                + "observer refresh. The change becomes visible to other players after roughly this delay. "
                + "Increase it (e.g. 2.5) on a laggy connection if clients do not refresh."
        );

        ConfigHideFromPlayerList = config.Bind(
            "2 - Multiplayer",
            "HideFromPlayerList",
            false,
            "Leave YOUR entry out of the player list the server broadcasts: every other game "
                + "then shows you nowhere on that list - the \"Players\" line and count of its "
                + "F2 overlay, the session player list of its map menu, the pins of its map. "
                + "The choice travels in your player ZDO (cd_offlist), exactly like the fake "
                + "map position: only the machine playing can decide, and a server running no "
                + "DLL of this mod has no patch to leave anything out - while vanilla clients "
                + "need none of it, they only ever show what the server sends them. The server "
                + "writes one list for every joining game: while this is on it holds every name "
                + "but yours, your own F2 too while you play as a client (the host's own F2 "
                + "reads the server's list, whole again after every write). Nothing of the "
                + "game itself moves - the server keeps the full list for its own logic, its "
                + "console and its save. Off, the default, is the plain list. Window: the "
                + "'Hide from player list' box under the command buttons. Console command: "
                + "disguise offlist [on|off]."
        );

        // Published at once, like the other options deciding what the others see: the list
        // goes out on its own rhythm (SendPlayerList, a few seconds) and the reassert keeps
        // it current anyway, but a click or a command here should reach the server without
        // waiting for either. No local player (main menu, dedicated server): the handler is
        // a no-op and the next spawn publishes it.
        ConfigHideFromPlayerList.SettingChanged += (_, _) =>
            OffList.Publish(GameAccess.GetLocalPlayerZDO());
    }

    /// <summary>Section `3 - Display`: what the OTHER machines see while you are disguised.</summary>
    private static void BindDisplay(ConfigFile config)
    {
        // -- 3 - Display: the equipment hashes live in your ZDO, and VisEquipment attaches
        // those items to whatever prefab carries it - the creature, on a vanilla client.
        // Hiding them removes the misplaced gear, your own view keeps it (see EquipmentMask).
        // The four rows the ARMOR sits in have no entry any more (HideHelmet / HideChest /
        // HideLegs / HideCape): a humanoid costume - every vanilla creature - dresses those
        // very rows with its own gear (CostumeGear), which reads them whether the mask touched
        // them or not and lands on the very same packet either way, so an entry there could
        // only ever be read by a costume carrying no Humanoid at all. They are gone, and
        // EquipmentMask hides those rows for good (BuildMask). The same reasoning took
        // HideHeldItems away: what the hands hold is decided by the two weapons options now -
        // whether the costume is drawn with weapons at all, and whose they are while it is -
        // and everything but "the player's own" is what hides your items from the other
        // machines (BuildMask again, on the very bit HideHeldItems used to gate). What is
        // left of the section: these three and ShowAsAlly, the other half of that display -
        // the health bar EnemyHud draws over the very same creature (see AllyView).
        ConfigShowArmor = config.Bind(
            "3 - Display",
            "ShowArmor",
            true,
            "Draw the costume with the ARMOR a real creature of that prefab is born with - the "
                + "gear Humanoid.Start gives a creature at spawn (its default items and one pick "
                + "of each random pool). On, the default, is how a Fuling in the world stands: "
                + "armor and all. Off draws the costume without it (the weapon is ShowWeapons' "
                + "half, and the two are read together). It reaches every screen drawing that "
                + "costume: your own here, the window's picture, and the machines running no DLL "
                + "of this mod - your ZDO publishes that gear itself while you are disguised, "
                + "rolled the way the game rolls it so every machine agrees on the same pick. The "
                + "machines running this DLL are never affected: they keep drawing YOU, with "
                + "your own equipment. Window: the 'Show armor' box under the command buttons. "
                + "Console command: disguise armor [on|off]."
        );

        // Read BEFORE the switch below binds its own ShowWeapons: the one entry this build
        // splits in two used to hold the very word WhoseWeapons holds now, and BepInEx drops
        // a line whose type no longer matches (it logs a warning and keeps the default) -
        // this hands the choice over instead of letting it fall on the floor, once, on the
        // first start of a build that holds both entries (LegacyWhoseWeapons).
        string whose = LegacyWhoseWeapons(config) ?? WeaponsCreature;

        ConfigShowWeapons = config.Bind(
            "3 - Display",
            "ShowWeapons",
            true,
            "Draw the costume with weapons at all. On, the default, is how a Fuling in the "
                + "world stands - spear and all: its hands hold what WhoseWeapons picks. Off "
                + "draws it with EMPTY hands, like a creature that spawns unarmed, and your "
                + "own held items stay hidden from the machines running no DLL of this mod "
                + "then as well (there would be nothing to glue them to). Like ShowArmor it "
                + "reaches every screen drawing that costume: your own here, the window's "
                + "picture, and the machines running no DLL - your ZDO publishes it itself "
                + "while you are disguised. The machines running this DLL are never "
                + "affected: they keep drawing YOU, with your own equipment. Window: the "
                + "'Show weapons' box under the command buttons. Console command: "
                + "disguise weapons [on|off]."
        );

        ConfigWhoseWeapons = config.Bind(
            "3 - Display",
            "WhoseWeapons",
            whose,
            new ConfigDescription(
                "Whose weapons a humanoid costume is held with, while ShowWeapons is on: "
                    + "\"Creature\" (the default) is the one a real creature of that prefab "
                    + "takes into a hand at spawn (MonsterAI takes the best weapon of its "
                    + "inventory), which is how a Fuling in the world stands - spear and all; "
                    + "\"Player\" is the ones YOU hold in both hands, an empty hand drawing "
                    + "nothing; ANY OTHER VALUE is the name of one item out of that "
                    + "creature's own pool (its default items and every random pool), and "
                    + "the hands are then that item ALONE - the other one empty, whatever "
                    + "the roll answers. The window's list holds them all, and a name the "
                    + "configured creature cannot hold is put back to \"Creature\" the moment "
                    + "another creature is configured. It reaches every screen drawing that "
                    + "costume, and it decides what YOUR hands carry there too: hidden from "
                    + "the machines running no DLL of this mod while the costume wears its "
                    + "own, exactly what it shows while it wears yours. Read only while "
                    + "ShowWeapons is on - a costume drawn with empty hands has no weapon for "
                    + "anyone to own. Window: the list at the right of the 'Show weapons' box. "
                    + "Console command: disguise weapons <creature|player|weapon>."
            )
        );

        ConfigShowAsAlly = config.Bind(
            "3 - Display",
            "ShowAsAlly",
            false,
            "Show the creature to the other machines as an ALLY (green health bar) instead of an "
                + "enemy (red). Only the display changes: EnemyHud asks BaseAI whether your ZDO is "
                + "an enemy, and this answers no. Their game rules, your own view and your "
                + "equipment stay exactly as they are."
        );

        // Any of the three can move both halves of what goes out: the armor half of the
        // costume gear (CostumeGear), and the weapon half - which decides the mask as well,
        // since YOUR held items are hidden while the costume does not wear them (EquipmentMask).
        // The publishers only bump the DataRevision when their value really changes, so the
        // steady state is free.
        ConfigShowArmor.SettingChanged += DisplaySettingChanged;
        ConfigShowWeapons.SettingChanged += DisplaySettingChanged;
        ConfigWhoseWeapons.SettingChanged += DisplaySettingChanged;
        ConfigShowAsAlly.SettingChanged += (_, _) => AllyView.OnSettingChanged();
    }

    /// <summary>
    /// The value the entry this build split in two used to hold - "Creature" or "Player",
    /// read straight out of the .cfg file the plugin was just bound to.
    ///
    /// Why a file read rather than a binding: ShowWeapons is a SWITCH in this build, so the
    /// old line ("ShowWeapons = Player") no longer matches its type and BepInEx drops it -
    /// it logs a warning and keeps the default, and the choice is gone before anybody could
    /// move it into WhoseWeapons. The line is still on disk at this point (the switch below
    /// is what makes it disappear), so this reads it first and hands the word over as the
    /// default of WhoseWeapons - once, on the first start of this build; every later start
    /// finds the rewritten file and answers null, which is the plain default.
    ///
    /// No file, no permission, a file written by a build that has already split the entry:
    /// all three answer null rather than throwing, and a fresh install has nothing to carry
    /// over anyway.
    /// </summary>
    private static string? LegacyWhoseWeapons(ConfigFile config)
    {
        try
        {
            foreach (string raw in File.ReadAllLines(config.ConfigFilePath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue; // a comment, not the entry
                }
                if (!line.StartsWith("ShowWeapons", StringComparison.Ordinal))
                {
                    continue;
                }
                int split = line.IndexOf('=');
                if (split < 0)
                {
                    continue; // not a config line at all
                }
                string value = line.Substring(split + 1).Trim().Trim('"');
                if (string.Equals(value, WeaponsPlayer, StringComparison.OrdinalIgnoreCase))
                {
                    return WeaponsPlayer;
                }
                if (string.Equals(value, WeaponsCreature, StringComparison.OrdinalIgnoreCase))
                {
                    return WeaponsCreature;
                }
            }
        }
        catch (Exception)
        {
            // No file to read: the default below is the answer, exactly like a fresh install.
        }
        return null;
    }

    /// <summary>
    /// Section `4 - Own view`: what this screen draws. Everything of it is local - the two
    /// settings that decide what the COSTUME wears (armor, weapons) sit in [3 - Display],
    /// where the wire they are published on belongs (they are read here too, at every build
    /// of a clone).
    /// </summary>
    private static void BindOwnView(ConfigFile config)
    {
        // -- 4 - Own view: rendering on this screen, polled every frame by SelfView.Tick()
        // and (while the window is open) by GuiPreview.Tick, so the two settings need no
        // SettingChanged handler: a clone already standing rebuilds itself when one moves.
        ConfigSeeYourDisguise = config.Bind(
            "4 - Own view",
            "SeeYourDisguise",
            true,
            "Show your disguise on your own screen: your viking is hidden and the creature is drawn in "
                + "your place, exactly like the other clients see you. Local visuals only - it never "
                + "changes what the other machines receive. Console command: disguise view [on|off]."
        );

        ConfigHideItemSparkle = config.Bind(
            "4 - Own view",
            "HideItemSparkle",
            false,
            "Hide the shimmer the game draws beside a dropped item: the looping particle system every "
                + "Item prefab carries on its root (Wood, Stone, a coin - the sparkle that makes a "
                + "dropped item findable). Local visuals only, like SeeYourDisguise: it changes what "
                + "THIS machine draws - your own costume and the window's picture - and never what "
                + "another machine receives. A machine without this DLL keeps the vanilla sparkle: it "
                + "builds the real item prefab from the network and has no setting to read. Window: "
                + "the 'Hide item sparkle' box under the command buttons."
        );
    }

    /// <summary>Section `5 - Flight`: the keys, the wheel and the speed of the climb.</summary>
    private static void BindFlight(ConfigFile config)
    {
        // -- 5 - Flight: purely visual, the player never leaves the ground (FlightControl).
        // The keys are BepInEx KeyboardShortcut values: the .cfg stores them as
        // "main key + modifiers" (e.g. Home + LeftAlt), and ConfigurationManager (Alt+F10 by
        // default) records a new one by simply pressing it - no manual text to type. They
        // ship UNBOUND, which is a KeyboardShortcut with no main key at all: every chord test
        // answers false to one, so nothing here can fire by accident, and the flight is
        // driven by the mouse wheel and by the console until a shortcut is recorded. A chord
        // recorded by hand should keep away from AltGr, the RightAlt of a French keyboard,
        // which types characters all day.
        ConfigFlyEnableKey = config.Bind(
            "5 - Flight",
            "FlyEnableKey",
            new KeyboardShortcut(KeyCode.None),
            "Start the flight: the creature is raised for the other machines (your player "
                + "stays on the ground). Same as \"disguise fly on\". Unbound by default: "
                + "no key can fire by accident, and the console starts the flight. "
                + "To change it: ConfigurationManager (Alt+F10) -> 5 - Flight -> click this "
                + "value, then press the wanted keys."
        );

        ConfigFlyDisableKey = config.Bind(
            "5 - Flight",
            "FlyDisableKey",
            new KeyboardShortcut(KeyCode.None),
            "End the flight: the creature is back on the ground. Same as \"disguise fly off\". "
                + "Unbound by default, like the three other flight keys. "
                + "To change it: ConfigurationManager (Alt+F10) -> 5 - Flight -> click this "
                + "value, then press the wanted keys."
        );

        ConfigFlyUpKey = config.Bind(
            "5 - Flight",
            "FlyUpKey",
            new KeyboardShortcut(KeyCode.None),
            "Raise the creature while the flight is on (hold it: the creature climbs "
                + "continuously at FlySpeed metres per second, up to 100 m). Unbound by "
                + "default: the mouse wheel climbs instead (FlyScrollWheel). "
                + "To change it: ConfigurationManager (Alt+F10) -> 5 - Flight -> click this "
                + "value, then press the wanted keys."
        );

        ConfigFlyDownKey = config.Bind(
            "5 - Flight",
            "FlyDownKey",
            new KeyboardShortcut(KeyCode.None),
            "Lower the creature while the flight is on (hold it: down to the ground, "
                + "never below it). Unbound by default: the mouse wheel descends instead "
                + "(FlyScrollWheel). "
                + "To change it: ConfigurationManager (Alt+F10) -> 5 - Flight -> click this "
                + "value, then press the wanted keys."
        );

        ConfigFlySpeed = config.Bind(
            "5 - Flight",
            "FlySpeed",
            5f,
            "How fast (metres per second) the creature climbs and descends while the "
                + "corresponding key is held."
        );

        ConfigFlyScrollWheel = config.Bind(
            "5 - Flight",
            "FlyScrollWheel",
            true,
            "While the flight is on, the mouse wheel raises / lowers the creature instead of "
                + "zooming the camera (the zoom comes back the moment the flight ends). The "
                + "minimap and the build rotation keep their own wheel. Off: the wheel only "
                + "zooms, and the flight is driven by the keys alone."
        );

        ConfigFlyScrollStep = config.Bind(
            "5 - Flight",
            "FlyScrollStep",
            0.2f,
            "Metres the creature moves per scroll notch while the flight is on (used when "
                + "FlyScrollWheel is on). A smooth wheel or a trackpad moves proportionally; "
                + "a negative value reverses climb and descent."
        );
    }

    /// <summary>Section `6 - GUI`: the window - its chord, its place, its list.</summary>
    private static void BindGui(ConfigFile config)
    {
        ConfigGuiKey = config.Bind(
            "6 - GUI",
            "GuiKey",
            new KeyboardShortcut(KeyCode.None),
            "Open / close the Disguise window: prefab list, preview and command buttons. "
                + "Unbound by default: the pause menu's Disguise line (Escape, then that "
                + "line) and \"disguise gui\" open it, and Escape closes it. "
                + "To change it: ConfigurationManager (Alt+F10) -> 6 - GUI -> click this "
                + "value, then press the wanted keys."
        );

        ConfigShowMenuEntry = config.Bind(
            "6 - GUI",
            "ShowMenuEntry",
            true,
            "Show the \"Disguise\" line in the game's pause menu (Escape), right under the "
                + "game's own Settings line: it opens the Disguise window, exactly like "
                + "GuiKey and \"disguise gui\". On by default, so a player who has bound no "
                + "shortcut at all still has a way to reach the window from the game itself. "
                + "Off, the pause menu only holds the game's own lines. Read every time that "
                + "menu opens, and applied at once when this changes."
        );

        ConfigGuiScrollStep = config.Bind(
            "6 - GUI",
            "GuiScrollStep",
            3000,
            "Pixels the prefab list of the Disguise window travels per mouse wheel notch. "
                + "Valheim's UI canvas is 1 unit = 1 screen pixel, so this really is pixels. "
                + "Read every frame while the window is open, so a change takes effect "
                + "immediately. The list stops at both ends: it grows by 34 pixels per row "
                + "and once every category is shown it is tens of thousands of pixels tall "
                + "behind the 300 visible, so the 3000 default crosses ten screens of list "
                + "per notch. 0 holds the list still, and a negative value inverts the wheel."
        );

        ConfigWindowX = config.Bind(
            "6 - GUI",
            "WindowX",
            0f,
            "Where the Disguise window sits, left to right: pixels away from the CENTRE of "
                + "the screen. 0 is the centre, and it is the default - a window nobody has "
                + "dragged opens in the middle. The place is written back here when the "
                + "window closes and put on the panel when it opens, so a window dragged to "
                + "a corner comes back in that corner. A value typed here by hand works as "
                + "well, and is kept on screen when read, the way the drag keeps it: the "
                + "window can never end up outside the monitor."
        );

        ConfigWindowY = config.Bind(
            "6 - GUI",
            "WindowY",
            0f,
            "Where the Disguise window sits, bottom to top: pixels ABOVE the centre of the "
                + "screen (a negative value puts it lower). 0 is the centre, and it is the "
                + "default. Written back when the window closes and read when it opens, "
                + "exactly like WindowX - the two together are the place of the window, and "
                + "the window opens centred until a drag says otherwise."
        );

        ConfigShowStatic = config.Bind(
            "6 - GUI",
            "ShowStatic",
            false,
            "Show the static prefabs in the window's list: every prefab without ZSyncTransform "
                + "(walls, floors, furniture, trees, rocks - most of what a world is made of). "
                + "A machine running no DLL of ours - a vanilla client, which is the machine a "
                + "costume is for - draws them once where their object was created and never "
                + "moves them again, so a disguise of this kind stays behind while you walk. "
                + "A category turned off only hides rows in the window, and \"disguise list\" "
                + "keeps printing every prefab of every category."
        );
        ConfigShowCreatures = config.Bind(
            "6 - GUI",
            "ShowCreatures",
            true,
            "Show the creature prefabs (one carrying a Character) in the window's list. One "
                + "checkbox per category, one category per prefab: a category turned off only "
                + "hides rows in the window, and \"disguise list\" keeps printing every "
                + "prefab of every category."
        );
        ConfigShowItems = config.Bind(
            "6 - GUI",
            "ShowItems",
            false,
            "Show the dropped-item prefabs (an ItemDrop: materials, weapons, food, trophies) "
                + "in the window's list."
        );
        ConfigShowBuildings = config.Bind(
            "6 - GUI",
            "ShowBuildings",
            false,
            "Show the buildable prefabs (a Piece: walls, floors, doors, furniture, crafting "
                + "stations) in the window's list."
        );
        ConfigShowNature = config.Bind(
            "6 - GUI",
            "ShowNature",
            false,
            "Show the natural prefabs (trees, plants, pickables, rocks, ore veins) in the "
                + "window's list."
        );
        ConfigShowOther = config.Bind(
            "6 - GUI",
            "ShowOther",
            false,
            "Show the prefabs no other category claims (spawners, markers, whatever a world "
                + "mod adds) in the window's list. Off, the list only holds what the five "
                + "other checkboxes show - nothing is ever dropped from the catalogue itself."
        );
        ConfigShowFavorites = config.Bind(
            "6 - GUI",
            "ShowFavorites",
            false,
            "Tick the \"Favorites\" box of the window: the list then shows ONLY the prefabs "
                + "listed in Favorites, and the six category boxes are ignored while it is "
                + "ticked. Untick it to go back to the categories. Rows are only hidden in "
                + "the window - \"disguise list\" keeps printing every prefab of every "
                + "category."
        );
        ConfigFavorites = config.Bind(
            "6 - GUI",
            "Favorites",
            string.Empty,
            "Comma-separated names of the prefabs marked as favorites (e.g. "
                + "\"Troll,Draugr,Wolf\"). Written by the Favorite button of the window - "
                + "which marks the SELECTED prefab, the very name on the state line - and by "
                + "\"disguise favorite\", and read by its Favorites box. The names are "
                + "the ones \"disguise list\" prints; a name no catalogue holds simply never "
                + "matches a row, and nothing here is ever dropped from the catalogue itself."
        );

        ConfigShowMenuEntry.SettingChanged += (_, _) => PauseMenuEntry.OnSettingChanged();
    }

    /// <summary>
    /// A display option changed: the other machines still hold the state of the last packet,
    /// so the new one goes out right away - the mask (EquipmentMask), the costume's gear
    /// (CostumeGear), or both of them, which is what these two options decide together.
    /// </summary>
    private static void DisplaySettingChanged(object sender, EventArgs e)
    {
        EquipmentMask.OnSettingChanged();
        CostumeGear.OnSettingChanged();
    }
}
