#nullable enable
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The line at the very bottom of the window, under the status line (GuiStatus): what the
/// element the pointer is ON is for. Nothing else asks it anything - the status line keeps
/// its own job (the hint, then the answer of the last command button), the two never write
/// to each other, and this one is only ever written by an element ENTERing or LEAVING the
/// pointer (GuiHover, one component per element, GuiRowHover for the rows of the list).
///
/// What it says is decided by the element that shows it, and it goes back to the idle line
/// when the pointer rests on nothing that has anything to say - which is the whole reason
/// for the two handlers: leaving one element for another must never leave the first one's
/// words on screen. The write is therefore owned (Show takes the component asking, Clear
/// only obeys the one the line belongs to), so the order Unity sends the two events in
/// cannot put a stale line up: whoever wrote it last is the only one who can take it down,
/// and the words themselves are compared before they are written (the same rule as
/// GuiWindow.RefreshState - a Text written for the same words would lay itself out again).
///
/// The line is built by GuiWindow with everything else and dropped with the window
/// (Forget, next to GuiStatus's), which is every frame it is closed and every frame after
/// a scene change destroyed it: Show and Clear then do nothing at all.
/// </summary>
internal static class GuiHelp
{
    /// <summary>
    /// What the line says while the pointer rests on nothing that describes itself: the one
    /// thing worth knowing about the panel as a whole, and a promise about the line itself.
    /// </summary>
    private const string Idle =
        "Drag the panel to move the window - this line describes whatever the pointer rests on.";

    /// <summary>
    /// Centre of the box, from the bottom edge of the panel: the 44 px of the status line
    /// above it (GuiStatus, which keeps the -608..-652 it has had since the option controls
    /// joined the band over it - six pixels clear of this box) leave this line a band of its
    /// own - HelpHeight of it, from 8 to 148 above the bottom edge, six clear of that line
    /// and eight of the panel's border. The band was 40 px, two lines of this font, and that
    /// was the one band of the window the long helps did not fit: squeezed to the minimum
    /// size they still lost their tails, the armor box alone spelling around nine hundred
    /// characters. It is 140 px - seven lines - now, and the PANEL grew by those 100 with
    /// it (GuiWindow.Height, which is why GuiStatus.StatusY grew by the same 100:
    /// bottom-anchored, it would have moved down with the edge otherwise). 78 is the 8 px
    /// clear of the border plus half of HelpHeight: this box sits on the very edge the
    /// height change does not touch, and it was only its own growth that moved it - the
    /// earlier growths of the panel took the status line up and left it where it stood.
    /// </summary>
    private const float HelpY = 78f;

    /// <summary>
    /// Height of the band - seven lines of the 20 px one line of this font takes at its own
    /// size, the same two lines' 40 px the band had grown seven times over. Every help but
    /// the two monsters of this window now draws at the full size (HelpFontSize), and those
    /// two settle a little under it rather than at the minimum and clipped.
    /// </summary>
    private const float HelpHeight = 140f;

    private const int HelpFontSize = 15;

    /// <summary>Unity-null once the window is gone, which is the whole safety of Show.</summary>
    private static Text? s_line;

    /// <summary>
    /// The component the line belongs to - the one whose words are on it right now, so a
    /// Clear from any other element is refused (an element that never wrote the line cannot
    /// take down someone else's words, whatever order the two events arrive in).
    /// </summary>
    private static object? s_owner;

    // ------------------------------------------------------------------ build

    /// <summary>
    /// Adds the line to the window, anchored to its bottom edge like the status line above
    /// it, with the idle text on it. Called once per window, from GuiWindow.
    /// </summary>
    internal static void Build(Transform parent)
    {
        GUIManager gui = GUIManager.Instance;
        s_owner = null;
        GameObject label = GuiWindow.Label(
            gui,
            Idle,
            parent,
            new Vector2(0.5f, 0f),
            new Vector2(0f, HelpY),
            gui.AveriaSerif,
            HelpFontSize,
            gui.ValheimBeige,
            GuiMetrics.LineWidth,
            HelpHeight
        );
        Text? text = label.GetComponent<Text>();
        if (text != null)
        {
            // Seven lines at most, and a help longer than the line it was written for makes
            // the whole text smaller rather than losing its tail: the rule the chord and
            // the state line of this window already follow. At two lines the long helps
            // were past this rule - shrunk to the minimum AND clipped - which is why the
            // band grew with the panel (HelpHeight, HelpY).
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = HelpFontSize;
            text.resizeTextMinSize = 10;
        }
        s_line = text;
    }

    /// <summary>
    /// Describes an element of the window: the component that does the telling is put on the
    /// element's own object, and the object is made a hit target as well - Jotunn switches
    /// raycasting off on every label, and an object the pointer cannot reach is an object
    /// whose help can never be shown. Nothing else is touched: the element keeps every
    /// handler it has (a Button keeps its own Enter and Exit, they are called next to these
    /// ones, all the components of one object being called), and an event the element does
    /// not handle itself still walks up to the panel, which owns the drag.
    /// </summary>
    internal static void Watch(GameObject? body, string help)
    {
        if (body == null || help.Length == 0)
        {
            return;
        }

        GuiHover? hover = body.GetComponent<GuiHover>();
        if (hover == null)
        {
            hover = body.AddComponent<GuiHover>();
        }
        hover.Help = help;

        Graphic? graphic = body.GetComponent<Graphic>();
        if (graphic != null && !graphic.raycastTarget)
        {
            graphic.raycastTarget = true;
        }
    }

    // ------------------------------------------------------------------ listeners

    /// <summary>
    /// The pointer stepped on an element (GuiHover or GuiRowHover): its words go on the
    /// line. Does nothing without a window, and nothing at all when the line already
    /// belongs to this element - two objects of one control (a box and its label) carry the
    /// same help, and the second one must not rewrite it.
    /// </summary>
    internal static void Show(string help, object owner)
    {
        Text? label = s_line;
        if (label == null || help.Length == 0 || ReferenceEquals(owner, s_owner))
        {
            return;
        }
        s_owner = owner;
        GuiText.Set(label, help);
    }

    /// <summary>
    /// The pointer left an element: the idle line comes back - but only from the element the
    /// line belongs to. Unity sends the exit of the element left and the entry of the one
    /// entered in the same frame, and refusing the other elements' exits is what makes the
    /// order they arrive in irrelevant (the newest owner keeps the line).
    /// </summary>
    internal static void Clear(object owner)
    {
        Text? label = s_line;
        if (label == null || !ReferenceEquals(owner, s_owner))
        {
            return;
        }
        s_owner = null;
        GuiText.Set(label, Idle);
    }

    /// <summary>Drops the line (the window is gone: Unity-null is the whole safety here).</summary>
    internal static void Forget()
    {
        s_line = null;
        s_owner = null;
    }

    // ------------------------------------------------------------------ helpers
}
