#nullable enable
using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The prefab list of the Disguise window: a filter field (Jotunn input) with a Clear button
/// at its right, and a scroll view (Jotunn) holding one row per vanilla prefab - exactly the
/// names "disguise list" prints, through PrefabCatalog.Names, so the console and the window
/// can never disagree about a name. What the window adds are two filters of its own on top
/// of that shared source: the text field, and the checkbox row (GuiCategories) - the six
/// categories of PrefabKind, then Favorites, which while it is on shows the marked prefabs
/// only.
///
/// The opening only builds the rows the boxes already OWED: a row is a Jotunn button, and
/// hundreds of them are what makes an opening cost something. A row owed later is paid for
/// THEN - once, by EnsureShownRows through Refilter - and no
/// row is ever destroyed, so switching a box off and back on is again only a SetActive.
/// The three lists run parallel at the same index - the name (what gets applied), the kind
/// (which checkbox owns it) and the body (what is on screen, null while its category has
/// never been shown) - and the text filter only shows and hides, which is what keeps typing
/// free on a list of hundreds of prefabs.
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
    /// The Clear button beside the filter: its width and the air between it and the field.
    /// The two of them are exactly as wide as the list below (Build gives the field what is
    /// left of the width it is handed), so the block still ends where the rows end.
    /// </summary>
    private const float ClearWidth = 70f;

    private const float ClearGap = 6f;

    /// <summary>
    /// The scroll view narrows its own content by the vertical scrollbar (Jotunn's rule), so
    /// the rows are built that width to fill it exactly.
    /// </summary>
    private const float HandleSize = 10f;

    private const float HandleDistanceToBorder = 2f;

    private static readonly List<string> s_names = new();

    private static readonly List<PrefabKind> s_kinds = new();

    /// <summary>
    /// The body of each row, null while its category has never been shown: the opening only
    /// builds the categories the boxes have on, and EnsureShownRows fills the rest in.
    /// </summary>
    private static readonly List<GameObject?> s_rows = new();

    /// <summary>
    /// The content the rows are children of, and the width they are built with: kept because
    /// a category switched on later is built long after Build returned.
    /// </summary>
    private static Transform? s_content;

    private static float s_rowWidth;

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
    /// How tall the whole block is for a list of that height: the scroll view is only its
    /// lower part, and the filter field and the gap above it belong to the block as well.
    /// GuiWindow needs it to place whatever sits UNDER the list (GuiPose), which would
    /// otherwise be built over the last rows.
    /// </summary>
    internal static float BlockHeight(float listHeight)
    {
        return FilterHeight + Spacing + listHeight;
    }

    /// <summary>
    /// Builds the whole block inside the window: the filter field on top with the Clear
    /// button at its right, the scroll view under it, every row, and the notice shown when
    /// nothing matches. GuiWindow owns the layout and passes the top-left corner of the
    /// block, its width and the height of the list itself (the filter, the button and the
    /// gap are taken from there: field + gap + button = the width passed in).
    /// </summary>
    internal static void Build(Transform parent, float x, float y, float width, float listHeight)
    {
        Forget();

        GUIManager gui = GUIManager.Instance;

        // The Clear button takes its width out of the block, never out of the list below:
        // the rows keep the full width they were given.
        float fieldWidth = width - ClearGap - ClearWidth;
        GameObject field = gui.CreateInputField(
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(x, y),
            InputField.ContentType.Standard,
            "filter (e.g. troll)",
            18,
            fieldWidth,
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

        // The Clear button, at the right of the field and as tall as it: Jotunn's own button
        // (the Valheim style the command row uses), its position the CENTRE of the box - the
        // pivot Unity leaves in the middle - so x is the middle of the button and y the
        // middle of the field (its top corner minus half its height, GuiWindow's own rule
        // for the command row).
        GameObject clear = gui.CreateButton(
            "Clear",
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(x + fieldWidth + ClearGap + (ClearWidth * 0.5f), y - (FilterHeight * 0.5f)),
            ClearWidth,
            FilterHeight
        );
        Button? clearButton = clear.GetComponent<Button>();
        if (clearButton != null)
        {
            clearButton.onClick.AddListener(ClearFilter);
        }

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
            "no prefab matches the filter",
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
        s_content = content;
        s_rowWidth = rowWidth;

        // The catalogue first: every name, then the kind of each - the component tests that
        // say which checkbox owns the row, and the only pass of them at this opening
        // (PrefabCatalog skips them when all it was asked for is names).
        float catalogueStartedAt = Time.realtimeSinceStartup;
        List<string> names = PrefabCatalog.Names(null, null);
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            s_names.Add(name);
            s_kinds.Add(PrefabCatalog.KindOf(name));
        }
        float catalogueMs = (Time.realtimeSinceStartup - catalogueStartedAt) * 1000f;

        // The rows then - only the rows the boxes already OWE. A row is four objects
        // built by Jotunn, and a row built only to be hidden again is waste: an opening now
        // costs what it is going to SHOW. A row owed later (a category switched on, or the
        // Favorites box ticked with marks whose category is off) is paid for once, in order,
        // by EnsureShownRows; nothing here is ever destroyed, so switching one box off again
        // stays the SetActive it has always been.
        float rowsStartedAt = Time.realtimeSinceStartup;
        int built = 0;
        for (int i = 0; i < s_names.Count; i++)
        {
            GameObject? row = GuiCategories.ShowRow(s_names[i], s_kinds[i])
                ? CreateRow(s_names[i], content, rowWidth)
                : null;
            s_rows.Add(row);
            if (row != null)
            {
                built++;
            }
        }

        // One line when Logs is on, so an opening that feels slow can be read off the log:
        // the walk that tags every prefab, and the rows actually built for it.
        Disguise.LogDebug(
            $"GUI: {s_names.Count} name(s) in {catalogueMs:0.0} ms, "
                + $"{built} of {s_rows.Count} row(s) in "
                + $"{(Time.realtimeSinceStartup - rowsStartedAt) * 1000f:0.0} ms."
        );

        if (s_rows.Count == 0)
        {
            // No ZNetScene yet (main menu): same situation as the console's list.
            Text? notice = noMatch.GetComponent<Text>();
            if (notice != null)
            {
                notice.text = "the prefab list is available in game";
            }
            noMatch.SetActive(true);
        }

        SetFilter(string.Empty);
    }

    /// <summary>
    /// One row of the list: a Jotunn button of the list's own width and RowHeight, its text
    /// beige (Jotunn paints every button orange - the configured creature needs that colour
    /// to stand out), a click routed to Select and the pointer routed to the preview.
    /// content and rowWidth are what Build measured: a row can also be owed to a category
    /// switched on long after Build returned.
    /// </summary>
    private static GameObject CreateRow(string name, Transform content, float rowWidth)
    {
        GUIManager gui = GUIManager.Instance;
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
            // off any row puts the configured one back) - GuiRowHover is that listener,
            // and GuiPreview the rule it feeds.
            GuiPreview.WatchRow(body, creature);
        }
        return body;
    }

    /// <summary>
    /// Builds the rows a category is OWING: the opening only made the ones the boxes had on,
    /// so a category switched on since then has no body yet - Refilter is the one place that
    /// asks, and both ways of switching a category on (the box, or a Show* value typed in
    /// ConfigurationManager) go through it, so a category is paid for exactly once. The rows
    /// keep the order of the sorted names, which is what the sibling index of the content
    /// says: a new row goes at the number of rows already built in front of it. The rows
    /// that exist are never touched - switching a category off stays a SetActive.
    /// </summary>
    private static void EnsureShownRows()
    {
        Transform? content = s_content;
        if (content == null)
        {
            return; // no window (or a build that lost its scroll view): nothing can be owed
        }

        int sibling = 0;
        for (int i = 0; i < s_rows.Count; i++)
        {
            GameObject? body = s_rows[i];
            if (body != null)
            {
                sibling++;
                continue;
            }
            if (!GuiCategories.ShowRow(s_names[i], s_kinds[i]))
            {
                continue;
            }
            GameObject row = CreateRow(s_names[i], content, s_rowWidth);
            row.transform.SetSiblingIndex(sibling);
            s_rows[i] = row;
            sibling++;
        }
    }

    /// <summary>Drops every reference to the window's list (closed, or destroyed by a scene change).</summary>
    internal static void Forget()
    {
        s_names.Clear();
        s_kinds.Clear();
        s_rows.Clear();
        s_filter = null;
        s_scroll = null;
        s_noMatch = null;
        s_content = null;
        s_rowWidth = 0f;
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
    /// Shows the rows the boxes want ON (a category the window has switched on, or - with
    /// the Favorites box ticked - a marked prefab, whatever its category) AND that contain
    /// the text (case-insensitive, like the console's list filter), hides the others. Nothing
    /// is created nor destroyed here: only SetActive, plus the scroll back to the top so the
    /// first match is where the eye already is. Typing a letter and ticking a box both end
    /// up here, which is why the two can never disagree about what is on screen. A null body
    /// is a row the boxes still owe (Refilter builds it one call above) and can never match
    /// - the boxes want it off, so it is not on screen either way.
    /// </summary>
    private static void SetFilter(string text)
    {
        string needle = (text ?? string.Empty).Trim();
        int matches = 0;
        for (int i = 0; i < s_rows.Count; i++)
        {
            GameObject? body = s_rows[i];
            if (body == null)
            {
                continue;
            }
            bool match =
                GuiCategories.ShowRow(s_names[i], s_kinds[i])
                && (
                    needle.Length == 0
                    || s_names[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                );
            if (body.activeSelf != match)
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
    /// Re-runs the filter with the text already in the field: what a category box asks for
    /// (GuiCategories), and what a Show* value changed in ConfigurationManager asks for while
    /// the window is open - the only two things that can change which rows the list OWES.
    /// The rows a category just switched on has never had are built first, once
    /// (EnsureShownRows), and the rest of the work is SetActive, exactly as typing a letter.
    /// </summary>
    internal static void Refilter()
    {
        EnsureShownRows();
        InputField? field = s_filter;
        SetFilter(field != null ? field.text : string.Empty);
    }

    /// <summary>
    /// The Clear button at the right of the filter: empties the field and re-runs the filter
    /// exactly the way a category box does (Refilter - build what a category owes, then
    /// SetActive, no row ever dropped, then back to the top), so every row is on screen again
    /// whatever was typed. The text is written WITHOUT the field's own notification and the
    /// refilter is asked for right here: one path, taken once, instead of depending on
    /// whether a write of `.text` notifies - two filters on screen must never disagree. A
    /// window is the only thing that can carry the button, so without one there is nothing
    /// to clear.
    /// </summary>
    private static void ClearFilter()
    {
        InputField? field = s_filter;
        if (field == null)
        {
            return;
        }
        field.SetTextWithoutNotify(string.Empty);
        Refilter();
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
            GameObject? body = s_rows[i];
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
