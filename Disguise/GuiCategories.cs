#nullable enable
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The row of checkboxes of the Disguise window: six boxes, one per PrefabKind, which decide
/// the categories the list shows (the console's "disguise list" always prints every kind),
/// and two which are not kinds at all: Favorites - a mark beside them (a prefab keeps the one
/// kind PrefabCatalog gave it) - and All, which stands for the six at once: it is on while
/// all six are, one click on it writes the six Show* entries together, and one click on any
/// of the six moves it too. It has no entry of its own, being those six values in one box.
/// Favorites is drawn FIRST, All right after it, then the six kinds in alphabetical order of
/// their label (s_kinds); while Favorites is on, the six are ignored and the list shows the
/// marked prefabs only: one click to leaf through a favorites list, one more to be back on
/// the categories.
///
/// The state lives in the config ([6 - GUI], the six Show* = true by default, ShowFavorites
/// = false, the marks themselves in Favorites as one comma-separated line), so a choice
/// survives a restart; Sync pushes it back into the boxes every frame while the window is
/// open, which is how a value typed in ConfigurationManager - or a mark written by the
/// Favorite button - shows up without reopening, the same rule as GuiScrollStep. A toggle
/// re-runs the filter (Refilter), which first builds the rows the list is OWING - the
/// opening only builds what the boxes already show - and then hides or shows with the same
/// SetActive as typing a letter: a row is never destroyed, so switching a box back and forth
/// costs nothing a second time. A category and a mark differ in one thing only, and it is
/// the scroll: a category is another list to look at, so the list goes back to its first row
/// (Refilter), while a mark is one row more or less behind the pointer, so the list keeps
/// the place it is standing in (RefilterInPlace).
///
/// The row sits UNDER the state line and above the list it filters, left-aligned with it;
/// GuiWindow owns those coordinates and passes them. Box and label are ONE checkbox - the
/// label carries a transitionless Button that flips the
/// box, so the hit target is the whole line and there is only one path to the config.
/// </summary>
internal static class GuiCategories
{
    /// <summary>Size of the box, height of its label, and the air inside one item.</summary>
    private const float BoxSize = 20f;

    private const float LabelHeight = 24f;

    private const int LabelFontSize = 16;

    /// <summary>Box to its label, and one checkbox (box + label) to the next one.</summary>
    private const float InnerGap = 8f;

    private const float ItemGap = 18f;

    /// <summary>Kept beside the measured text so a rounded pixel can never wrap its last letter.</summary>
    private const float LabelPadding = 4f;

    /// <summary>
    /// The eight boxes of the row: Favorites, All, then the six kinds - eight boxes, seven
    /// entries of config (All is not one of them: it writes the six Show* entries at once).
    /// </summary>
    private const int BoxCount = PrefabCatalog.KindCount + 2;

    /// <summary>
    /// The six kinds in the order their boxes are DRAWN in, after Favorites and All: in
    /// alphabetical order of their label (Buildings, Creatures, Items,
    /// Nature, Other, Static). The enum keeps its own order, which is the order "disguise
    /// list" prints the categories in; no box reads a kind by its number any more, so a kind
    /// added later has to find its place in this array.
    /// </summary>
    private static readonly PrefabKind[] s_kinds =
    {
        PrefabKind.Building,
        PrefabKind.Creature,
        PrefabKind.Item,
        PrefabKind.Nature,
        PrefabKind.Other,
        PrefabKind.Static,
    };

    /// <summary>The row is placed from the top-left corner of the window, like the list.</summary>
    private static readonly Vector2 s_anchor = new Vector2(0f, 1f);

    /// <summary>Jotunn never sets a pivot: it stays in the middle, so a position IS a centre.</summary>
    private static readonly Vector2 s_centre = new Vector2(0.5f, 0.5f);

    private static readonly List<Toggle?> s_toggles = new();

    /// <summary>What the boxes showed last, so Sync only reacts to a real change.</summary>
    private static readonly bool[] s_applied = new bool[BoxCount];

    /// <summary>The Favorites line Sync saw last, so a mark added or taken off is also a change.</summary>
    private static string s_appliedFavorites = string.Empty;

    // ------------------------------------------------------------------ build

    /// <summary>
    /// Builds the eight checkboxes at (x, y) with y the CENTRE of the row: box, then its
    /// label sized to the text it really has (measured through Text.preferredWidth, so the
    /// gap before the next box is the same whatever the word is). The row grows right from
    /// the list's x and stops well inside the window: eight short labels never reach
    /// GuiWindow.Width - 24 of margin (the widest of them, "Creatures" or "Favorites", is
    /// the one to watch). One box per entry is built by BuildBox, which this row shares with
    /// GuiWindow's sparkle box.
    ///
    /// The index a box is built with IS its place in the row: 0 is Favorites, 1 is All, and
    /// the six kinds run from 2 (BoxCount boxes either way) - the same index Sync, ShownAt
    /// and OnToggled speak in, so the three can never be pointing at two different boxes.
    /// </summary>
    internal static void Build(Transform parent, float x, float y)
    {
        Forget();
        s_appliedFavorites = DisguiseConfig.ConfigFavorites.Value ?? string.Empty;

        float cursor = x;
        for (int i = 0; i < BoxCount; i++)
        {
            bool shown = ShownAt(i);
            s_applied[i] = shown;
            int captured = i;
            s_toggles.Add(
                BuildBox(
                    parent,
                    ref cursor,
                    y,
                    TitleAt(i),
                    HelpAt(i),
                    shown,
                    on => OnToggled(captured, on)
                )
            );
        }
    }

    /// <summary>
    /// What a box says on the help line (GuiHelp): the mark, then All, then one category.
    /// </summary>
    private static string HelpAt(int index)
    {
        if (index == 0)
        {
            return "Show the marked prefabs only - the six boxes are ignored while this one is on, so the list becomes a favorites list.";
        }
        if (index == 1)
        {
            return "All six categories at once: tick it to switch every one of them on, untick it to switch them all off - it stands ticked only while all six are.";
        }
        return $"Filter the list to {TitleAt(index)}: every box can be on at once, and \"disguise list\" in the console prints every category whatever the boxes say.";
    }

    /// <summary>
    /// Builds ONE checkbox - its box, then its label sized to the text it really has
    /// (measured through Text.preferredWidth, so the gap before the next box is the same
    /// whatever the word is) - starting at the cursor given, with y the CENTRE of the row,
    /// and returns the cursor after it: the rhythm the row above walks, one call per box.
    /// Box and label are ONE checkbox - the label carries a transitionless Button that flips
    /// the box, so the hit target is the whole line and there is only one path to what the
    /// box writes. The listener comes from the caller: this is also how GuiWindow builds the
    /// sparkle box, the same control standing on its own with its own entry to write and no
    /// list to re-filter. Both halves of the line are given the help they describe (GuiHelp):
    /// they are two objects, and the event system stops at the first one that handles the
    /// pointer - the label, which carries the Button. Returns null when Jotunn's box carries
    /// no Toggle, which is a build that could never be clicked and is therefore worth no
    /// entry in the row either.
    /// </summary>
    internal static Toggle? BuildBox(
        Transform parent,
        ref float cursor,
        float y,
        string title,
        string help,
        bool value,
        Action<bool> onToggled
    )
    {
        GUIManager gui = GUIManager.Instance;

        GameObject box = gui.CreateToggle(parent, BoxSize, BoxSize);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        if (boxRect != null)
        {
            // CreateToggle parents with worldPositionStays, which leaves the box wherever
            // the window happens to be: anchors and position are written afterwards, the
            // way GuiCreatureList corrects its own field.
            boxRect.anchorMin = s_anchor;
            boxRect.anchorMax = s_anchor;
            boxRect.pivot = s_centre;
            boxRect.anchoredPosition = new Vector2(cursor + (BoxSize * 0.5f), y);
        }
        cursor += BoxSize + InnerGap;

        GameObject label = GuiWindow.Label(
            gui,
            title,
            parent,
            s_anchor,
            new Vector2(cursor, y),
            gui.AveriaSerif,
            LabelFontSize,
            gui.ValheimBeige,
            40f,
            LabelHeight
        );
        Text? text = label.GetComponent<Text>();
        float labelWidth = 40f;
        if (text != null)
        {
            // Measured, so the air before the next box is the same whatever the word is;
            // and a hit target again (Jotunn switches raycasting off on every label), so
            // the whole line - the word too - is the checkbox, not only the little box.
            text.raycastTarget = true;
            labelWidth = Mathf.Max(text.preferredWidth, 8f) + LabelPadding;
            RectTransform labelRect = label.GetComponent<RectTransform>();
            if (labelRect != null)
            {
                labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, labelWidth);
                labelRect.anchoredPosition = new Vector2(cursor + (labelWidth * 0.5f), y);
            }
        }
        cursor += labelWidth + ItemGap;

        // The help line, on both halves of the checkbox: the pointer lands on whichever of
        // the two it really is over, and the event system stops at the first object that
        // handles it (the label, which carries the Button).
        GuiHelp.Watch(box, help);
        GuiHelp.Watch(label, help);

        Toggle? toggle = box.GetComponent<Toggle>();
        if (toggle == null)
        {
            return null;
        }

        // isOn first, the listener after: writing the value must not fire the toggle.
        toggle.isOn = value;
        toggle.onValueChanged.AddListener(on => onToggled(on));

        // The label flips the box instead of carrying a second path of its own: the
        // box's listener then does what it always does (whatever the caller wrote).
        // No transition of any kind - the word must not look like a button.
        Button labelButton = label.AddComponent<Button>();
        labelButton.transition = Selectable.Transition.None;
        labelButton.onClick.AddListener(() =>
        {
            toggle.isOn = !toggle.isOn;
        });
        return toggle;
    }

    /// <summary>Drops the boxes (the window is gone: Unity-null is the whole safety here).</summary>
    internal static void Forget()
    {
        s_toggles.Clear();
    }

    // ------------------------------------------------------------------ per frame

    /// <summary>
    /// What DisguiseGui.Tick runs once per frame while the window is on screen: a Show* value
    /// changed behind the window (ConfigurationManager) is pushed into its box and the list
    /// is re-filtered once - and so is a mark added or taken off, since the Favorites line is
    /// a value of the config like the others (written by the Favorite button, by a console
    /// "disguise favorite", or by hand in the .cfg). Only a real difference does either, and
    /// the two never share a refilter: a category is a list to look at from its first row,
    /// a mark only one row more or less behind the pointer (Refilter / RefilterInPlace).
    /// </summary>
    internal static void Sync()
    {
        int count = s_toggles.Count;
        if (count != BoxCount)
        {
            return; // no window, or a partial build: never risk an index that means another box
        }
        bool categories = false;
        for (int i = 0; i < count; i++)
        {
            bool want = ShownAt(i);
            if (want == s_applied[i])
            {
                continue;
            }
            s_applied[i] = want;
            Toggle? toggle = s_toggles[i];
            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(want);
            }
            categories = true;
        }

        // The Favorites line is a value like the others: a mark written by the Favorite
        // button, by "disguise favorite", or by hand in the .cfg changes which rows the list
        // owes - a favorite whose category is OFF may have no row yet, and the refilter
        // below builds it.
        string favorites = DisguiseConfig.ConfigFavorites.Value ?? string.Empty;
        bool marks = !string.Equals(favorites, s_appliedFavorites, StringComparison.Ordinal);
        if (marks)
        {
            s_appliedFavorites = favorites;

            // A mark changes what a row SAYS as well as whether it is there: every row
            // built so far is given its star (GuiCreatureList) before the refilter below
            // shows it - rows built after this carry the star themselves.
            GuiCreatureList.SyncFavoriteStars();
        }

        if (categories)
        {
            GuiCreatureList.Refilter();
        }
        else if (marks)
        {
            GuiCreatureList.RefilterInPlace();
        }
    }

    // ------------------------------------------------------------------ state

    /// <summary>Whether the window's list shows this category (the config behind its box).</summary>
    internal static bool Shown(PrefabKind kind) => KindEntry(kind).Value;

    /// <summary>Whether the first box is on: the list shows marks and nothing else then.</summary>
    internal static bool FavoritesOn() => DisguiseConfig.ConfigShowFavorites.Value;

    /// <summary>
    /// Whether the list shows this ROW: Favorites decides alone while it is on (the six
    /// kinds are ignored then - one click gets a list of marks and nothing else), and the
    /// box of the kind decides otherwise. The text filter is the other half of the row's
    /// life and lives in GuiCreatureList, which asks this once per row.
    /// </summary>
    internal static bool ShowRow(string name, PrefabKind kind)
    {
        return FavoritesOn() ? PrefabFavorites.Contains(name) : Shown(kind);
    }

    /// <summary>The value a box shows: Favorites (0), All (1), then the six kinds (BoxCount of them).</summary>
    private static bool ShownAt(int index)
    {
        if (index == 0)
        {
            return FavoritesOn();
        }
        if (index == 1)
        {
            return AllOn();
        }
        return Shown(s_kinds[index - 2]);
    }

    /// <summary>The label of a box: "Favorites", "All", then the plural of the kind.</summary>
    private static string TitleAt(int index)
    {
        if (index == 0)
        {
            return "Favorites";
        }
        return index == 1 ? "All" : Title(s_kinds[index - 2]);
    }

    /// <summary>
    /// One box was clicked: its entry takes the value, and the list is re-filtered once.
    /// All is the special case - it writes the six kind entries at once (and pushes the same
    /// value into their boxes right away, Sync would a frame later), while the six kinds move
    /// the All box with them, without a click of its own (writing a value never re-runs a
    /// listener).
    /// </summary>
    private static void OnToggled(int index, bool on)
    {
        if (index == 1)
        {
            s_applied[1] = on;
            for (int i = 0; i < s_kinds.Length; i++)
            {
                KindEntry(s_kinds[i]).Value = on;
                s_applied[2 + i] = on;
                Toggle? kind = BoxAt(2 + i);
                kind?.SetIsOnWithoutNotify(on);
            }
            GuiCreatureList.Refilter();
            return;
        }

        s_applied[index] = on;
        Entry(index).Value = on;
        if (index > 1)
        {
            bool all = AllOn();
            s_applied[1] = all;
            Toggle? box = BoxAt(1);
            box?.SetIsOnWithoutNotify(all);
        }
        GuiCreatureList.Refilter();
    }

    /// <summary>What the All box stands for: the six Show* entries, all of them on.</summary>
    private static bool AllOn()
    {
        for (int i = 0; i < s_kinds.Length; i++)
        {
            if (!KindEntry(s_kinds[i]).Value)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>One box of the row by its index: null while the window is not fully built.</summary>
    private static Toggle? BoxAt(int index)
    {
        return s_toggles.Count == BoxCount && index >= 0 && index < BoxCount
            ? s_toggles[index]
            : null;
    }

    /// <summary>The Show* entry of a box: Favorites its own, the six kinds the six - All has none.</summary>
    private static ConfigEntry<bool> Entry(int index)
    {
        return index == 0 ? DisguiseConfig.ConfigShowFavorites : KindEntry(s_kinds[index - 2]);
    }

    /// <summary>The Show* entry of a kind: the six boxes are the six entries, no copy anywhere.</summary>
    private static ConfigEntry<bool> KindEntry(PrefabKind kind)
    {
        switch (kind)
        {
            case PrefabKind.Static:
                return DisguiseConfig.ConfigShowStatic;
            case PrefabKind.Creature:
                return DisguiseConfig.ConfigShowCreatures;
            case PrefabKind.Item:
                return DisguiseConfig.ConfigShowItems;
            case PrefabKind.Building:
                return DisguiseConfig.ConfigShowBuildings;
            case PrefabKind.Nature:
                return DisguiseConfig.ConfigShowNature;
            default:
                return DisguiseConfig.ConfigShowOther;
        }
    }

    /// <summary>The label with a capital: "Creatures", "Buildings" - the plural is the catalogue's.</summary>
    private static string Title(PrefabKind kind)
    {
        string label = PrefabCatalog.Label(kind);
        if (label.Length == 0)
        {
            return label;
        }
        return char.ToUpperInvariant(label[0]) + label.Substring(1);
    }
}
