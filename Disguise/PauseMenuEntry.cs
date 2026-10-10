#nullable enable
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The Disguise line of the pause menu: one more entry in the list Escape draws, opening
/// the same window the GuiKey chord opens and "disguise gui" opens.
///
/// The entry is a CLONE of the game's own Settings button - same look, same layout, same
/// place in the list (right after it) - relabelled, and given the click it opens with. That
/// click is not added to the one the clone was born with: a Button carries the listeners its
/// PREFAB wired into it, and UnityEventBase.RemoveAllListeners leaves those alone, so the
/// whole UnityEvent is thrown away and a new one put in its place. Without that, the entry
/// would open Settings behind the window it just opened.
///
/// Building it is done the first time the pause menu opens and costs one Instantiate for the
/// whole session: the Menu of a world is built with that world, and a button built here dies
/// with it - which the null test answers for, being Unity's own, the one that is also true
/// of a destroyed object.
///
/// Every game member this needs is PUBLIC, and all are still reached through GameAccess: the
/// visibility audit lists every member reference the DLL makes, public or not, and this adds
/// none to that list.
/// </summary>
internal static class PauseMenuEntry
{
    /// <summary>What the line says, in English like every label of this mod.</summary>
    private const string EntryLabel = "Disguise";

    /// <summary>What the clone is called, so it is never mistaken for a line of the game.</summary>
    private const string EntryName = "DisguiseEntry";

    private static Button? s_button;

    private static Menu? s_menu;

    /// <summary>True once something went wrong building the entry: the log is then silent.</summary>
    private static bool s_failed;

    /// <summary>
    /// Makes sure the entry stands in the pause menu that was just opened, and that the
    /// gamepad's up / down chain runs through it. Called from the postfix of Menu.Show, which
    /// is also the case where the prefix stopped the menu from opening (the window was on
    /// screen): the entry is then built or rewired inside a menu that is not shown, which
    /// costs nothing and shows nothing.
    /// </summary>
    internal static void Add(Menu menu)
    {
        try
        {
            if (s_button == null)
            {
                s_button = Build(menu);
            }
            WireNavigation();
        }
        catch (Exception ex)
        {
            if (!s_failed)
            {
                s_failed = true;
                Disguise.LogError($"Pause menu entry: {ex}");
            }
        }
    }

    /// <summary>
    /// Puts the entry into the gamepad's chain, right after Settings - the place it has in
    /// the list. The menu rewires its own buttons every time it opens (Menu.UpdateNavigation,
    /// which knows nothing of this one) and again whenever the manual-save cooldown changes
    /// one of them, so the two links it breaks are set again rather than set once: after
    /// every Show (this call) and after that method too (Patch_MenuNavigation). Rewiring is
    /// idempotent - the chain that already holds the entry is left as it is.
    /// </summary>
    internal static void WireNavigation()
    {
        Button? button = s_button;
        if (button == null)
        {
            return; // the world changed and took the entry with it: the next Show rebuilds it
        }
        Button? settings = GameAccess.GetMenuSettingsButton(s_menu);
        if (settings == null || settings.navigation.selectOnDown == button)
        {
            return; // no Settings to stand after, or the chain already holds the entry
        }

        Navigation here = button.navigation;
        here.mode = Navigation.Mode.Explicit;
        here.selectOnUp = settings;
        here.selectOnDown = settings.navigation.selectOnDown;
        button.navigation = here;

        Navigation after = settings.navigation;
        after.selectOnDown = button;
        settings.navigation = after;
    }

    /// <summary>The click: the pause menu goes, then the window opens as the chord opens it.</summary>
    private static void OnClicked()
    {
        GameAccess.HideMenu(s_menu);
        DisguiseGui.Toggle();
    }

    /// <summary>One clone of the game's own Settings button, or null (and one log line).</summary>
    private static Button? Build(Menu menu)
    {
        Button? settings = GameAccess.GetMenuSettingsButton(menu);
        Transform? parent = settings != null ? settings.transform.parent : null;
        if (settings == null || parent == null)
        {
            return Fail(menu, "Menu carries no Settings button to take the shape of.");
        }

        GameObject entry = UnityEngine.Object.Instantiate(settings.gameObject, parent);
        entry.name = EntryName;
        entry.SetActive(true); // Settings is hidden in some modes: the entry is not Settings

        Button? button = entry.GetComponent<Button>();
        if (button == null)
        {
            UnityEngine.Object.Destroy(entry);
            return Fail(menu, "The button taken as a shape carries no Button.");
        }

        button.onClick = new Button.ButtonClickedEvent(); // the prefab's own clicks go with it
        button.onClick.AddListener(OnClicked);
        button.interactable = true;
        SetLabel(entry);

        // Right after Settings, which is the place this entry is wired for as well: the list
        // is laid out by sibling index, and the chain above is written with the same shape.
        entry.transform.SetSiblingIndex(settings.transform.GetSiblingIndex() + 1);

        s_menu = menu;
        Disguise.LogInfo($"Pause menu: entry added ({EntryLabel}).");
        return button;
    }

    /// <summary>
    /// The word on the entry. Valheim's own localizer walks BOTH kinds of text it has
    /// (Localization.ReLocalizeVisible reads Text and TMP_Text alike, every thirty frames
    /// while the menu is on screen), so both are answered here: whichever the button really
    /// carries takes the word, and the one it does not carry is simply not found. The word
    /// itself has no "$" in it, so the localizer leaves it alone - it only ever translates
    /// what it is given as a key.
    /// </summary>
    private static void SetLabel(GameObject entry)
    {
        TMP_Text? mesh = entry.GetComponentInChildren<TMP_Text>(true);
        if (mesh != null && !string.Equals(mesh.text, EntryLabel, StringComparison.Ordinal))
        {
            mesh.text = EntryLabel;
        }
        Text? text = entry.GetComponentInChildren<Text>(true);
        if (text != null && !string.Equals(text.text, EntryLabel, StringComparison.Ordinal))
        {
            text.text = EntryLabel;
        }
    }

    /// <summary>One line in the log, and no entry at all.</summary>
    private static Button? Fail(Menu menu, string reason)
    {
        if (!s_failed)
        {
            s_failed = true;
            Disguise.LogWarning($"Pause menu: no Disguise entry - {reason}");
        }
        s_menu = menu;
        return null;
    }
}
