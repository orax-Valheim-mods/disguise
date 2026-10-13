#nullable enable
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The line under the pose rows: the hint about clicking a row while nothing has
/// happened yet, and the answer of the last command button after that.
///
/// One command can print several lines to the console - "disguise disable" goes on with two
/// notes - and this line holds one message: DisguiseCommand.Run clears it before running, and
/// only the first line it then gets is written, which is the headline. The console keeps
/// every line when the same command is typed there (Say), and that is where the notes are
/// read.
///
/// The answer stays until the next button, and the hint comes back with the next window, so
/// there is nothing to do per frame: this class only holds the label. The help line
/// (GuiHelp) closes the window under it and the band of the level row and the option
/// controls (GuiWindow) stands over it, which is the reason the panel is 76 px taller than
/// it once was - this box sits at the very same -608..-652 it settled at when the help line
/// arrived (StatusY grew by 40 then and by 36 more for the third row of that band, the box
/// being anchored to the bottom edge).
/// </summary>
internal static class GuiStatus
{
    /// <summary>What the line says while no command button has been pressed yet.</summary>
    private const string Hint =
        "Click a row: the creature is set and applied at once - the same as \"disguise set <name>\".";

    /// <summary>
    /// The centre of the box as a height from the BOTTOM edge of the panel, which is what it
    /// is anchored to: 44 px are two lines of 16 px, so a long answer wraps instead of being
    /// cut in the middle of its first one. 76 puts the box at -608..-652 on a panel 706 tall
    /// - the very place 40 gave it on the 670 it was when the help line (GuiHelp) arrived,
    /// and 36 gave it on the 630 it grew from: anchored to the edge, the box would otherwise
    /// have moved down with the panel and stood in the band those two changes needed above
    /// it (the help line under it, the level row and the option controls over it).
    /// </summary>
    private const float StatusY = 76f;

    private const float StatusHeight = 44f;

    /// <summary>Unity-null once the window is gone, which is the whole safety of Show.</summary>
    private static Text? s_line;

    /// <summary>True once the answer of the current action has been written.</summary>
    private static bool s_written;

    internal static void Build(Transform parent)
    {
        GUIManager gui = GUIManager.Instance;
        s_written = false;
        GameObject label = GuiWindow.Label(
            gui,
            Hint,
            parent,
            new Vector2(0.5f, 0f),
            new Vector2(0f, StatusY),
            gui.AveriaSerif,
            16,
            gui.ValheimBeige,
            GuiMetrics.LineWidth,
            StatusHeight
        );
        s_line = label.GetComponent<Text>();
        GuiHelp.Watch(
            label,
            "The answer of the last command button - the \"click a row\" hint while none has been pressed yet, and the notes a command goes on printing after its headline stay in the console."
        );
    }

    /// <summary>
    /// Starts an action: the next line Show gets is the answer of that action, and every
    /// line after it belongs to the same action and stays out.
    /// </summary>
    internal static void Clear()
    {
        s_written = false;
    }

    /// <summary>
    /// One line of output with no console behind it. Does nothing without a window (the
    /// console command has its own Terminal) or when the answer is already written.
    /// </summary>
    internal static void Show(string text)
    {
        Text? label = s_line;
        if (label == null || s_written)
        {
            return;
        }
        label.text = text;
        s_written = true;
    }

    internal static void Forget()
    {
        s_line = null;
        s_written = false;
    }
}
