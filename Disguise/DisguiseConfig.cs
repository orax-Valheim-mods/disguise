#nullable enable
using System;
using BepInEx.Configuration;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Every mod setting (BepInEx config file) plus their binding. The other classes read
/// the entries through DisguiseConfig.ConfigCreature.Value and friends.
/// </summary>
internal static class DisguiseConfig
{
    internal static ConfigEntry<bool> ConfigEnabled = null!;

    internal static ConfigEntry<string> ConfigCreature = null!;

    internal static ConfigEntry<bool> ConfigProtectSave = null!;

    internal static ConfigEntry<float> ConfigReassert = null!;

    internal static ConfigEntry<bool> ConfigLogs = null!;

    internal static ConfigEntry<bool> ConfigForceRefresh = null!;

    internal static ConfigEntry<float> ConfigRefreshHold = null!;

    // -- 3 - Display: what the OTHER machines see while you are disguised ---------------
    internal static ConfigEntry<bool> ConfigHideHelmet = null!;
    internal static ConfigEntry<bool> ConfigShowAsAlly = null!;

    internal static ConfigEntry<bool> ConfigHideChest = null!;

    internal static ConfigEntry<bool> ConfigHideLegs = null!;

    internal static ConfigEntry<bool> ConfigHideCape = null!;

    internal static ConfigEntry<bool> ConfigHideHeldItems = null!;

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
    //    and while it is on the six Show* values are simply ignored (GuiCategories.ShowRow).
    internal static ConfigEntry<bool> ConfigShowStatic = null!;

    internal static ConfigEntry<bool> ConfigShowCreatures = null!;

    internal static ConfigEntry<bool> ConfigShowItems = null!;

    internal static ConfigEntry<bool> ConfigShowBuildings = null!;

    internal static ConfigEntry<bool> ConfigShowNature = null!;

    internal static ConfigEntry<bool> ConfigShowOther = null!;

    internal static ConfigEntry<bool> ConfigShowFavorites = null!;

    internal static ConfigEntry<string> ConfigFavorites = null!;

    /// <summary>Binds every setting to the config file. Called once, from Disguise.Awake.</summary>
    internal static void Bind(ConfigFile config)
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
            "Troll",
            "Exact name of the creature prefab (e.g. Troll, Draugr, Wolf, Greydwarf, Blob, Skeleton, Neck, Boar, Eikthyr...). "
                + "Console command: disguise list to see the available names."
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
            true,
            "Write the mod's own informational messages (Info / Debug) to the BepInEx log "
                + "(BepInEx\\LogOutput.log). Warnings and errors are always written, whatever this "
                + "value is: they are what tells a broken install from a working one. "
                + "Console command: disguise logs [on|off]."
        );

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

        // -- 3 - Display: the equipment hashes live in your ZDO, and VisEquipment attaches
        // those items to whatever prefab carries it - the creature, on a vanilla client.
        // Hiding them removes the misplaced gear, your own view keeps it (see EquipmentMask).
        // ShowAsAlly is the other half of that display: the health bar EnemyHud draws over
        // the very same creature (see AllyView).
        ConfigHideHelmet = config.Bind(
            "3 - Display",
            "HideHelmet",
            true,
            "Hide the helmet from the other machines while you are disguised (fixes a helmet glued "
                + "at the wrong position on humanoid creatures). Your own view keeps it."
        );

        ConfigHideChest = config.Bind(
            "3 - Display",
            "HideChest",
            true,
            "Hide the chest armour from the other machines while you are disguised. "
                + "Your own view keeps it."
        );

        ConfigHideLegs = config.Bind(
            "3 - Display",
            "HideLegs",
            true,
            "Hide the leggings from the other machines while you are disguised. "
                + "Your own view keeps them."
        );

        ConfigHideCape = config.Bind(
            "3 - Display",
            "HideCape",
            true,
            "Hide the cape from the other machines while you are disguised. Your own view keeps it."
        );

        ConfigHideHeldItems = config.Bind(
            "3 - Display",
            "HideHeldItems",
            false,
            "Hide the items held in your hands and carried on your back from the other machines "
                + "while you are disguised. Your own view keeps them."
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

        ConfigHideHelmet.SettingChanged += HideSettingChanged;
        ConfigHideChest.SettingChanged += HideSettingChanged;
        ConfigHideLegs.SettingChanged += HideSettingChanged;
        ConfigHideCape.SettingChanged += HideSettingChanged;
        ConfigHideHeldItems.SettingChanged += HideSettingChanged;
        ConfigShowAsAlly.SettingChanged += (_, _) => AllyView.OnSettingChanged();

        // -- 4 - Own view: purely local rendering, polled every frame by SelfView.Tick() and
        // (while the window is open) by GuiPreview.Tick, so no SettingChanged handler is
        // needed here: a clone already standing rebuilds itself when the setting moves.
        ConfigSeeYourDisguise = config.Bind(
            "4 - Own view",
            "SeeYourDisguise",
            false,
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

        // -- 5 - Flight: purely visual, the player never leaves the ground (FlightControl).
        // The keys are BepInEx KeyboardShortcut values: the .cfg stores them as
        // "main key + modifiers" (e.g. Home + LeftAlt), and ConfigurationManager (Alt+F10 by
        // default) records a new one by simply pressing it - no manual text to type. They
        // default to LeftAlt, which never fires while text is entered (AltGr is the RightAlt
        // of a French keyboard).
        ConfigFlyEnableKey = config.Bind(
            "5 - Flight",
            "FlyEnableKey",
            new KeyboardShortcut(KeyCode.Home, KeyCode.LeftAlt),
            "Start the flight: the creature is raised for the other machines (your player "
                + "stays on the ground). Same as \"disguise fly on\". "
                + "To change it: ConfigurationManager (Alt+F10) -> 5 - Flight -> click this "
                + "value, then press the wanted keys."
        );

        ConfigFlyDisableKey = config.Bind(
            "5 - Flight",
            "FlyDisableKey",
            new KeyboardShortcut(KeyCode.End, KeyCode.LeftAlt),
            "End the flight: the creature is back on the ground. Same as \"disguise fly off\". "
                + "To change it: ConfigurationManager (Alt+F10) -> 5 - Flight -> click this "
                + "value, then press the wanted keys."
        );

        ConfigFlyUpKey = config.Bind(
            "5 - Flight",
            "FlyUpKey",
            new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftAlt),
            "Raise the creature while the flight is on (hold it: the creature climbs "
                + "continuously at FlySpeed metres per second, up to 100 m). "
                + "To change it: ConfigurationManager (Alt+F10) -> 5 - Flight -> click this "
                + "value, then press the wanted keys."
        );

        ConfigFlyDownKey = config.Bind(
            "5 - Flight",
            "FlyDownKey",
            new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftAlt),
            "Lower the creature while the flight is on (hold it: down to the ground, "
                + "never below it). "
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

        ConfigGuiKey = config.Bind(
            "6 - GUI",
            "GuiKey",
            new KeyboardShortcut(KeyCode.F8, KeyCode.LeftAlt),
            "Open / close the Disguise window: prefab list, preview and command buttons. "
                + "To change it: ConfigurationManager (Alt+F10) -> 6 - GUI -> click this "
                + "value, then press the wanted keys."
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
            true,
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
            true,
            "Show the dropped-item prefabs (an ItemDrop: materials, weapons, food, trophies) "
                + "in the window's list."
        );
        ConfigShowBuildings = config.Bind(
            "6 - GUI",
            "ShowBuildings",
            true,
            "Show the buildable prefabs (a Piece: walls, floors, doors, furniture, crafting "
                + "stations) in the window's list."
        );
        ConfigShowNature = config.Bind(
            "6 - GUI",
            "ShowNature",
            true,
            "Show the natural prefabs (trees, plants, pickables, rocks, ore veins) in the "
                + "window's list."
        );
        ConfigShowOther = config.Bind(
            "6 - GUI",
            "ShowOther",
            true,
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

        ConfigCreature.SettingChanged += (_, _) => PrefabResolver.InvalidateCreatureHash();
    }

    /// <summary>
    /// A "hide" option changed: the other machines still hold the state of the last packet,
    /// EquipmentMask re-sends it right away.
    /// </summary>
    private static void HideSettingChanged(object sender, EventArgs e)
    {
        EquipmentMask.OnSettingChanged();
    }
}
