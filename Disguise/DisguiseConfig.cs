#nullable enable
using System;
using BepInEx.Configuration;

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

    internal static ConfigEntry<bool> ConfigForceRefresh = null!;

    internal static ConfigEntry<float> ConfigRefreshHold = null!;

    // -- 3 - Display: what the OTHER machines see while you are disguised ---------------
    internal static ConfigEntry<bool> ConfigHideHelmet = null!;

    internal static ConfigEntry<bool> ConfigHideChest = null!;

    internal static ConfigEntry<bool> ConfigHideLegs = null!;

    internal static ConfigEntry<bool> ConfigHideCape = null!;

    internal static ConfigEntry<bool> ConfigHideHeldItems = null!;

    // -- 4 - Own view: what YOU see on your own screen ----------------------------------
    internal static ConfigEntry<bool> ConfigSeeYourDisguise = null!;

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

        ConfigHideHelmet.SettingChanged += HideSettingChanged;
        ConfigHideChest.SettingChanged += HideSettingChanged;
        ConfigHideLegs.SettingChanged += HideSettingChanged;
        ConfigHideCape.SettingChanged += HideSettingChanged;
        ConfigHideHeldItems.SettingChanged += HideSettingChanged;

        // -- 4 - Own view: purely local rendering, polled every frame by SelfView.Tick(),
        // so no SettingChanged handler is needed here.
        ConfigSeeYourDisguise = config.Bind(
            "4 - Own view",
            "SeeYourDisguise",
            false,
            "Show your disguise on your own screen: your viking is hidden and the creature is drawn in "
                + "your place, exactly like the other clients see you. Local visuals only - it never "
                + "changes what the other machines receive. Console command: disguise view [on|off]."
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
