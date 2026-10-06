#nullable enable
using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The creature list of the Disguise window: a filter field (Jotunn input) and a scroll view
/// (Jotunn) holding one row per prefab that carries a Character - exactly the list
/// "disguise list" prints, through DisguiseCommand.CreatureNames, so the console and the
/// window can never disagree.
///
/// Every row is built ONCE when the window opens, and the filter then only shows and hides
/// them (SetActive): typing creates nothing and destroys nothing, which is what keeps a
/// keystroke free on a list of a hundred creatures. The rows live in two parallel lists at
/// the same index - the name (what gets applied) and the body (what is on screen).
///
/// A row click is the console's "disguise set &lt;name&gt;": DisguiseCommand.ApplyCreature writes
/// the config and re-applies at once, then the highlight and the state line under the title
/// are brought back in sync (GuiWindow.RefreshState). Sync does the same the other way
/// around, for a creature changed by the console while the window is open.
///
/// While the filter has focus, FilterFocused stays true whatever the pointer does:
/// DisguiseGui.BlocksInput then hands the frame to the UI (Patch_TakeInput and
/// Patch_SetMouseLook read it), which is the rule Valheim itself uses for the search field of
/// the build menu - typing must never walk the character away nor turn the camera.
/// </summary>
internal static class GuiCreatureList
{
    /// <summary>Height of a row, of the filter field, and the air between two rows.</summary>
    private const float RowHeight = 32f;

    private const float FilterHeight = 36f;
    private const float Spacing = 2f;

    /// <summary>
    /// The scroll view narrows its own content by the vertical scrollbar (Jotunn's rule), so
    /// the rows are built that width to fill it exactly.
    /// </summary>
    private const float HandleSize = 10f;

    private const float HandleDistanceToBorder = 2f;

    private static readonly List<string> s_names = new();
    private static readonly List<GameObject> s_rows = new();

    private static InputField? s_filter;
    private static ScrollRect? s_scroll;
    private static GameObject? s_noMatch;

    private static string s_syncedCreature = string.Empty;

    /// <summary>Last GuiScrollStep pushed to the scroll view, so the write is skipped when unchanged.</summary>
    private static int? s_lastGuiScrollStep;

    /// <summary>
    /// True while the player types in the filter: the frame then belongs to the UI whether
    /// the pointer is on the window or not - read by DisguiseGui.BlocksInput.
    /// </summary>
    internal static bool FilterFocused
    {
        get
        {
            InputField? field = s_filter;
            return field != null && field.isFocused;
        }
    }

    // ------------------------------------------------------------------ build

    /// <summary>
    /// Builds the whole block inside the window: the filter field on top, the scroll view
    /// under it, every row, and the notice shown when nothing matches. GuiWindow owns the
    /// layout and passes the top-left corner of the block, its width and the height of the
    /// list itself (the filter and the gap are taken from there).
    /// </summary>
    internal static void Build(Transform parent, float x, float y, float width, float listHeight)
    {
        Forget();

        GUIManager gui = GUIManager.Instance;

        GameObject field = gui.CreateInputField(
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(x, y),
            InputField.ContentType.Standard,
            "filter (e.g. troll)",
            18,
            width,
            FilterHeight
        );
        s_filter = field.GetComponent<InputField>();
        if (s_filter != null)
        {
            s_filter.onValueChanged.AddListener(SetFilter);
        }

        // Jotunn sets the position BEFORE the anchors and never touches the pivot, which stays
        // in the middle: the corner asked for above would be the CENTRE of the field, and a
        // 400 px field centred 24 px from the left edge would hang out of the window. The
        // pivot is moved to the upper left corner and the position applied again - the same
        // correction the scroll view box gets below.
        RectTransform fieldRect = field.GetComponent<RectTransform>();
        fieldRect.pivot = new Vector2(0f, 1f);
        fieldRect.anchoredPosition = new Vector2(x, y);

        float listY = y - FilterHeight - Spacing;
        GameObject scroll = gui.CreateScrollView(
            parent,
            showHorizontalScrollbar: false,
            showVerticalScrollbar: true,
            handleSize: HandleSize,
            handleDistanceToBorder: HandleDistanceToBorder,
            handleColors: gui.ValheimScrollbarHandleColorBlock,
            slidingAreaBackgroundColor: new Color(0f, 0f, 0f, 0.4f),
            width: width,
            height: listHeight
        );

        // Jotunn builds its scroll view anchored to the upper right of a centred box: the box
        // is moved to the corner GuiWindow asked for. Anchors first and position last, so the
        // placement never depends on how Unity reacts to an anchor change.
        RectTransform box = scroll.GetComponent<RectTransform>();
        box.anchorMin = new Vector2(0f, 1f);
        box.anchorMax = new Vector2(0f, 1f);
        box.pivot = new Vector2(0f, 1f);
        box.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        box.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, listHeight);
        box.anchoredPosition = new Vector2(x, listY);
        s_scroll = scroll.GetComponentInChildren<ScrollRect>();

        // Clamped instead of Unity's default Elastic: with Elastic, one notch past the top
        // puts the content OUT of the valid range (the list dips and a gap opens above the
        // first row) and LateUpdate then springs it back - the "it goes down and comes right
        // back up" flash at both ends. Clamped corrects the position inside the very scroll
        // that asked for too much, so an impossible move simply does not happen. It is also
        // what XPortalNetworks achieves by forcing the normalised position and killing the
        // velocity after every scroll of its own dropdown.
        ScrollRect? view = s_scroll;
        if (view != null)
        {
            view.movementType = ScrollRect.MovementType.Clamped;
        }

        // The wheel speed is an option (6 - GUI / GuiScrollStep), so it is pushed into the
        // scroll view right away and again every frame while the window is open: uGUI moves
        // the list by scrollDelta * scrollSensitivity pixels, and its default of 1 would move
        // it a single pixel per notch - what made the wheel feel stuck. The same property is
        // what XPortalNetworks raises to a floor of 520 pixels per notch.
        ApplyScrollStep();

        GameObject noMatch = GuiWindow.Label(
            gui,
            "no creature matches the filter",
            parent,
            new Vector2(0f, 1f),
            new Vector2(x + (width * 0.5f), listY - (listHeight * 0.5f)),
            gui.AveriaSerif,
            18,
            gui.ValheimBeige,
            width,
            40f
        );
        s_noMatch = noMatch;
        noMatch.SetActive(false);

        Transform? content = scroll.transform.Find("Scroll View/Viewport/Content");
        if (content == null)
        {
            // The window stays usable (title, state, Close) and only the list is missing.
            Disguise.LogWarning("GUI: Jotunn's scroll view has no content, list skipped.");
            return;
        }

        VerticalLayoutGroup? group = content.GetComponent<VerticalLayoutGroup>();
        if (group != null)
        {
            // A row keeps exactly the size it was built with: no LayoutElement needed, and
            // hiding a row takes it out of the layout and out of the scrollable height.
            group.childControlWidth = false;
            group.childForceExpandWidth = false;
            group.childControlHeight = false;
            group.childForceExpandHeight = false;
            group.spacing = Spacing;
        }

        float rowWidth = width - (2f * HandleDistanceToBorder) - HandleSize;
        List<string> names = DisguiseCommand.CreatureNames(null);
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            GameObject body = gui.CreateButton(
                name,
                content,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                rowWidth,
                RowHeight
            );
            Text? label = body.GetComponentInChildren<Text>();
            if (label != null)
            {
                // Jotunn already aligns the text of a button to the left, and paints it
                // ValheimOrange (ApplyButtonStyle): "the selected row turns orange" showed
                // nothing at first because every row was orange. The list now reads beige,
                // with the configured creature in orange - the colours the title and the
                // state line already use.
                label.color = gui.ValheimBeige;
            }

            Button? button = body.GetComponent<Button>();
            if (button != null)
            {
                string creature = name;
                button.onClick.AddListener(() => Select(creature));

                // Hovering the row shows its creature in the preview of step 3 (and stepping
                // off any row puts the configured one back) - GuiPreview owns that rule.
                GuiPreview.WatchRow(body, creature);
            }

            s_names.Add(name);
            s_rows.Add(body);
        }

        if (s_rows.Count == 0)
        {
            // No ZNetScene yet (main menu): same situation as the console's list.
            Text? notice = noMatch.GetComponent<Text>();
            if (notice != null)
            {
                notice.text = "the creature list is available in game";
            }
            noMatch.SetActive(true);
        }

        SetFilter(string.Empty);
    }

    /// <summary>Drops every reference to the window's list (closed, or destroyed by a scene change).</summary>
    internal static void Forget()
    {
        s_names.Clear();
        s_rows.Clear();
        s_filter = null;
        s_scroll = null;
        s_noMatch = null;
        s_syncedCreature = string.Empty;

        // The next window brings a brand new ScrollRect, which starts on Unity's own
        // sensitivity of 1: the option has to be written to it again whatever the cache says.
        s_lastGuiScrollStep = null;
    }

    // ------------------------------------------------------------------ per frame

    /// <summary>
    /// What DisguiseGui.Tick runs once per frame while the window is on screen: the wheel
    /// speed option is pushed to the scroll view (a change in ConfigurationManager then needs
    /// no reopening), and the highlight + the state line are brought back to what the config
    /// says - the console can change the creature with the window open, and the two views
    /// must agree.
    /// </summary>
    internal static void Sync()
    {
        ApplyScrollStep();

        string current = DisguiseConfig.ConfigCreature.Value ?? string.Empty;
        if (string.Equals(current, s_syncedCreature, StringComparison.Ordinal))
        {
            return;
        }
        s_syncedCreature = current;
        RefreshHighlight();
        GuiWindow.RefreshState();
    }

    /// <summary>
    /// GuiScrollStep (6 - GUI), in pixels per wheel notch: uGUI multiplies the wheel delta by
    /// this value, and its default of 1 moved the list one pixel at a time. 0 holds the list,
    /// a negative value inverts the wheel - both are read as written, no clamping. Only the
    /// changes are written: this runs every frame while the window is open.
    /// </summary>
    private static void ApplyScrollStep()
    {
        ScrollRect? scroll = s_scroll;
        int step = DisguiseConfig.ConfigGuiScrollStep.Value;
        if (scroll != null && step != s_lastGuiScrollStep)
        {
            scroll.scrollSensitivity = step;
            s_lastGuiScrollStep = step;
        }
    }

    // ------------------------------------------------------------------ filter and selection

    /// <summary>
    /// Shows the rows containing the text (case-insensitive, like the console's list filter)
    /// and hides the others. Nothing is created nor destroyed here: only SetActive, plus the
    /// scroll back to the top so the first match is where the eye already is.
    /// </summary>
    private static void SetFilter(string text)
    {
        string needle = (text ?? string.Empty).Trim();
        int matches = 0;
        for (int i = 0; i < s_rows.Count; i++)
        {
            bool match =
                needle.Length == 0
                || s_names[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            GameObject body = s_rows[i];
            if (body != null && body.activeSelf != match)
            {
                body.SetActive(match);
            }
            if (match)
            {
                matches++;
            }
        }

        if (s_noMatch != null)
        {
            s_noMatch.SetActive(matches == 0);
        }

        ScrollRect? scroll = s_scroll;
        if (scroll != null)
        {
            scroll.verticalNormalizedPosition = 1f;
        }

        RefreshHighlight();
    }

    /// <summary>
    /// A click on a row: the same thing the console's "disguise set &lt;name&gt;" does. No message
    /// is printed - the window shows the result itself, through the state line and the
    /// highlight of the row that is now the configured creature.
    /// </summary>
    private static void Select(string creature)
    {
        _ = DisguiseCommand.ApplyCreature(creature, "gui");
        s_syncedCreature = DisguiseConfig.ConfigCreature.Value ?? string.Empty;
        RefreshHighlight();
        GuiWindow.RefreshState();
    }

    /// <summary>
    /// Colours the row of the configured creature ValheimOrange and every other visible one
    /// ValheimBeige - Jotunn paints all of its button texts orange, so the base colour cannot
    /// be the selected one. Hidden rows are skipped on purpose: they are recoloured by the
    /// next filter change, which is the moment they can be seen.
    /// </summary>
    private static void RefreshHighlight()
    {
        string current = DisguiseConfig.ConfigCreature.Value ?? string.Empty;
        GUIManager gui = GUIManager.Instance;
        for (int i = 0; i < s_rows.Count; i++)
        {
            GameObject body = s_rows[i];
            if (body == null || !body.activeSelf)
            {
                continue;
            }
            Text? label = body.GetComponentInChildren<Text>();
            if (label == null)
            {
                continue;
            }
            label.color = string.Equals(s_names[i], current, StringComparison.OrdinalIgnoreCase)
                ? gui.ValheimOrange
                : gui.ValheimBeige;
        }
    }
}
