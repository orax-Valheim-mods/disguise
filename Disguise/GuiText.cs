#nullable enable
using System;
using TMPro;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// One thing every label of the GUI does over and over: show a word, and write it only when
/// the word really changed. uGUI lays a text out again every time its `text` is ASSIGNED -
/// even with the same characters - so a label written every frame re-measures, re-wraps and
/// re-generates its mesh for nothing. The comparison is Ordinal on purpose: whether two
/// words differ must not be answered by the culture of the machine.
///
/// The Func overloads exist because the line of the window is built from values that are
/// themselves computed (two of them move on their own - the flight and its height), and the
/// work must only be done when there is somebody to show it to: the builder is called once
/// the label is found, and the string once, never twice.
///
/// A null label is a label a scene change destroyed - nothing to write there, and no error
/// either. Both component kinds are covered because the game has both: uGUI `Text` for most
/// of the window, TextMeshPro for the pause menu and the newest screens. They share no base
/// carrying `text`, so there is one overload per kind - three in all, and only the three
/// that are used.
/// </summary>
internal static class GuiText
{
    /// <summary>Writes `value` when the label really shows something else.</summary>
    internal static void Set(Text? label, string value)
    {
        if (label == null || string.Equals(label.text, value, StringComparison.Ordinal))
        {
            return;
        }

        label.text = value;
    }

    /// <summary>Same, for a TextMeshPro label.</summary>
    internal static void Set(TMP_Text? label, string value)
    {
        if (label == null || string.Equals(label.text, value, StringComparison.Ordinal))
        {
            return;
        }

        label.text = value;
    }

    /// <summary>Same, with the text built on the spot - and only if it is going to be read.</summary>
    internal static void Set(Text? label, Func<string> value)
    {
        if (label == null)
        {
            return;
        }

        Set(label, value());
    }
}
