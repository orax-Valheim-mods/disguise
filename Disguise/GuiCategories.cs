#nullable enable
using System.Collections.Generic;
using BepInEx.Configuration;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The row of category checkboxes of the Disguise window: one box per PrefabKind, deciding
/// which categories the list shows (six of them, six boxes - the console's "disguise
/// list" always prints every kind).
///
/// The state lives in the config ([6 - GUI], Show* = true by default), so a choice survives
/// a restart; Sync pushes it back into the boxes every frame while the window is open, which
/// is how a value typed in ConfigurationManager shows up without reopening - the same rule
/// as GuiScrollStep. A toggle re-runs the filter (Refilter), which first builds the rows a
/// category switched ON has never had - the opening only builds the categories already on -
/// and then hides or shows with the same SetActive as typing a letter: a row is never
/// destroyed, so switching a category back and forth costs nothing a second time.
///
/// The row sits in the band the layout leaves free between the title and the state line,
/// left-aligned with the list it filters; GuiWindow owns those coordinates and passes them.
/// Box and label are ONE checkbox - the label carries a transitionless Button that flips the
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

    /// <summary>The row is placed from the top-left corner of the window, like the list.</summary>
    private static readonly Vector2 Anchor = new Vector2(0f, 1f);

    /// <summary>Jotunn never sets a pivot: it stays in the middle, so a position IS a centre.</summary>
    private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

    private static readonly List<Toggle> s_toggles = new();

    /// <summary>What the boxes showed last, so Sync only reacts to a real change.</summary>
    private static readonly bool[] s_applied = new bool[PrefabCatalog.KindCount];

    // ------------------------------------------------------------------ build

    /// <summary>
    /// Builds the six checkboxes at (x, y) with y the CENTRE of the row: box, then its
    /// label sized to the text it really has (measured through Text.preferredWidth, so the
    /// gap before the next box is the same whatever the word is). The row grows right from
    /// the list's x and stops well inside the window: six short labels never reach
    /// GuiWindow.Width - 24 of margin (the widest of them, "Creatures", is the one to watch).
    /// </summary>
    internal static void Build(Transform parent, float x, float y)
    {
        Forget();
        GUIManager gui = GUIManager.Instance;

        float cursor = x;
        for (int i = 0; i < PrefabCatalog.KindCount; i++)
        {
            PrefabKind kind = (PrefabKind)i;
            bool shown = Shown(kind);
            s_applied[i] = shown;

            GameObject box = gui.CreateToggle(parent, BoxSize, BoxSize);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            if (boxRect != null)
            {
                // CreateToggle parents with worldPositionStays, which leaves the box wherever
                // the window happens to be: anchors and position are written afterwards, the
                // way GuiCreatureList corrects its own field.
                boxRect.anchorMin = Anchor;
                boxRect.anchorMax = Anchor;
                boxRect.pivot = Centre;
                boxRect.anchoredPosition = new Vector2(cursor + (BoxSize * 0.5f), y);
            }
            cursor += BoxSize + InnerGap;

            GameObject label = GuiWindow.Label(
                gui,
                Title(kind),
                parent,
                Anchor,
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

            Toggle? maybeToggle = box.GetComponent<Toggle>();
            if (maybeToggle != null)
            {
                Toggle toggle = maybeToggle;

                // isOn first, the listener after: writing the value must not fire the toggle.
                toggle.isOn = shown;
                PrefabKind captured = kind;
                toggle.onValueChanged.AddListener(on => OnToggled(captured, on));
                s_toggles.Add(toggle);

                // The label flips the box instead of carrying a second path of its own: the
                // box's listener then does what it always does (config, list, one Refilter).
                // No transition of any kind - the word must not look like a button.
                Button labelButton = label.AddComponent<Button>();
                labelButton.transition = Selectable.Transition.None;
                labelButton.onClick.AddListener(() =>
                {
                    toggle.isOn = !toggle.isOn;
                });
            }
        }
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
    /// is re-filtered once - only a real difference does either.
    /// </summary>
    internal static void Sync()
    {
        int count = s_toggles.Count;
        if (count != PrefabCatalog.KindCount)
        {
            return; // no window, or a partial build: never risk an index that means another kind
        }
        bool changed = false;
        for (int i = 0; i < count; i++)
        {
            bool want = Shown((PrefabKind)i);
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
            changed = true;
        }
        if (changed)
        {
            GuiCreatureList.Refilter();
        }
    }

    // ------------------------------------------------------------------ state

    /// <summary>Whether the window's list shows this category (the config behind its box).</summary>
    internal static bool Shown(PrefabKind kind) => Entry(kind).Value;

    private static void OnToggled(PrefabKind kind, bool on)
    {
        s_applied[(int)kind] = on;
        Entry(kind).Value = on;
        GuiCreatureList.Refilter();
    }

    /// <summary>The Show* entry of a kind: the six boxes are the six entries, no copy anywhere.</summary>
    private static ConfigEntry<bool> Entry(PrefabKind kind)
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
