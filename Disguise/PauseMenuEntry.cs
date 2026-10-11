#nullable enable
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The Disguise line of the pause menu: one more entry in the list Escape draws, opening
/// the same window the GuiKey chord opens and "disguise gui" opens. `ShowMenuEntry` (6 - GUI,
/// on by default) decides whether the line is there at all.
///
/// The entry is a CLONE of the game's own Settings button - same look, same layout, same
/// place in the list (right after it) - relabelled, and given the click it opens with. That
/// click is not added to the one the clone was born with: a Button carries the listeners its
/// PREFAB wired into it, and UnityEventBase.RemoveAllListeners leaves those alone, so the
/// whole UnityEvent is thrown away and a new one put in its place. Without that, the entry
/// would open Settings behind the window it just opened.
///
/// The option is read every time the pause menu opens, and applied at once when it changes
/// (the handler DisguiseConfig binds) - which is a build, or a removal with the two links of
/// the gamepad's chain put back. An entry the option takes away is DESTROYED rather than
/// switched off, and the chain is rewound first: Unity follows an explicit
/// selectOnDown whether the button it points at still exists or not, so a D-pad sent to a
/// hidden entry would simply go nowhere.
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

    /// <summary>
    /// What Settings pointed down to while the entry is not in the gamepad's chain: the link
    /// the game itself wrote, kept so that taking the entry out can put it back.
    /// </summary>
    private static Selectable? s_belowSettings;

    /// <summary>True once something went wrong building the entry: the log is then silent.</summary>
    private static bool s_failed;

    /// <summary>
    /// Makes sure the pause menu that was just opened holds the entry the option asks for,
    /// and that the gamepad's up / down chain runs through it. Called from the postfix of
    /// Menu.Show, which is also the case where the prefix stopped the menu from opening (the
    /// window was on screen): the entry is then built or rewired inside a menu that is not
    /// shown, which costs nothing and shows nothing.
    /// </summary>
    internal static void Add(Menu menu)
    {
        try
        {
            s_menu = menu;
            if (!DisguiseConfig.ConfigShowMenuEntry.Value)
            {
                Forget("ShowMenuEntry is off");
                return;
            }
            if (s_button == null)
            {
                s_button = Build(menu);
            }
            if (s_button != null)
            {
                WireNavigation();
            }
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
    /// The option moved (ConfigurationManager, the .cfg by hand): the line follows it at
    /// once, which is one build or one removal. Nothing to do before the first Show - it
    /// reads the option itself, and there is no menu to draw in yet.
    /// </summary>
    internal static void OnSettingChanged()
    {
        Menu? menu = s_menu;
        if (menu != null)
        {
            Add(menu);
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
        s_belowSettings = settings.navigation.selectOnDown;

        Navigation here = button.navigation;
        here.mode = Navigation.Mode.Explicit;
        here.selectOnUp = settings;
        here.selectOnDown = s_belowSettings;
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
            return Fail("Menu carries no Settings button to take the shape of.");
        }

        GameObject entry = UnityEngine.Object.Instantiate(settings.gameObject, parent);
        entry.name = EntryName;
        entry.SetActive(true); // Settings is hidden in some modes: the entry is not Settings

        Button? button = entry.GetComponent<Button>();
        if (button == null)
        {
            UnityEngine.Object.Destroy(entry);
            return Fail("The button taken as a shape carries no Button.");
        }

        button.onClick = new Button.ButtonClickedEvent(); // the prefab's own clicks go with it
        button.onClick.AddListener(OnClicked);
        button.interactable = true;
        SetLabel(entry);

        // Right after Settings, which is the place this entry is wired for as well: the list
        // is laid out by sibling index, and the chain above is written with the same shape.
        entry.transform.SetSiblingIndex(settings.transform.GetSiblingIndex() + 1);

        Disguise.LogInfo($"Pause menu: entry added ({EntryLabel}).");
        return button;
    }

    /// <summary>
    /// Takes the line OUT of the pause menu: its two links leave the gamepad's chain first (a
    /// link to a destroyed button is still followed, and a D-pad then goes nowhere), then the
    /// clone itself. `why` only says in the log which of the two callers brought it here.
    /// </summary>
    private static void Forget(string why)
    {
        Button? button = s_button;
        s_button = null;
        if (button == null)
        {
            return; // nothing on screen: the option and this copy have nothing to undo
        }

        Button? settings = GameAccess.GetMenuSettingsButton(s_menu);
        if (settings != null && settings.navigation.selectOnDown == button)
        {
            Navigation after = settings.navigation;
            after.selectOnDown = s_belowSettings;
            settings.navigation = after;
        }
        Navigation here = button.navigation;
        here.selectOnUp = null;
        here.selectOnDown = null;
        button.navigation = here;

        Disguise.LogInfo($"Pause menu: entry removed ({why}).");
        UnityEngine.Object.Destroy(button.gameObject);
        s_belowSettings = null;
    }

    /// <summary>
    /// The plugin is unloading (ScriptEngine reloads it into a NEW assembly, statics and
    /// all): the line has to go with this copy, or the game's own menu would keep a button
    /// nobody owns - nothing in the game ever destroys it - and the copy loaded next would
    /// draw a second one right beside it, because its own s_button is null and it knows
    /// nothing of the one already standing there. Called from Disguise.OnDestroy, before the
    /// logger goes with the plugin instance.
    /// </summary>
    internal static void Cleanup()
    {
        try
        {
            Forget("plugin unloaded");
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Pause menu cleanup: {ex}");
        }
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
        GuiText.Set(entry.GetComponentInChildren<TMP_Text>(true), EntryLabel);
        GuiText.Set(entry.GetComponentInChildren<Text>(true), EntryLabel);
    }

    /// <summary>One line in the log, and no entry at all.</summary>
    private static Button? Fail(string reason)
    {
        if (!s_failed)
        {
            s_failed = true;
            Disguise.LogWarning($"Pause menu: no Disguise entry - {reason}");
        }
        return null;
    }
}
