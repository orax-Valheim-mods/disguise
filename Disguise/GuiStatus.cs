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
/// controls (GuiWindow) stands over it, which is the reason the panel is 220 px taller than
/// it once was - this box stands at -722..-766 on the 850, riding the very edge it is
/// anchored to (StatusY is one sum - the border, the help line, the air between them and
/// half of this box - so only ever the HELP LINE moves it: +40 when that line arrived,
/// +100 when it was given its seven lines, the option rows of the band standing over the
/// box never entering it - those pushed the box down with the edge instead; then the
/// flight settings added two rows to the band - the second one a whole row of fields -
/// while the help line gave 70 of those seven lines back for three bigger ones, which is
/// how 176 became 106: the box travelled DOWN 78 with the band above it, while the help
/// line's 70 came out of the distance between the box and the edge it hangs on - the edge
/// itself growing by only the 8 that are left).
/// </summary>
internal static class GuiStatus
{
    /// <summary>What the line says while no command button has been pressed yet.</summary>
    private const string Hint =
        "Click a row: the creature is set and applied at once - the same as \"disguise set <name>\".";

    /// <summary>
    /// The centre of the box as a height from the BOTTOM edge of the panel, which is what it
    /// is anchored to: 44 px are two lines of 16 px, so a long answer wraps instead of being
    /// cut in the middle of its first one. 106 puts the box at -722..-766 on a panel 850
    /// tall - the 176 that stood it at -644..-688 on the 842 while the help line below
    /// still held SEVEN lines (36 on the 630 before that line existed, 76 from the 670 it
    /// arrived on - unchanged across the third option row of the 706 - and 176 from the
    /// 806 it was given them on): this number is only
    /// ever the border, the help line, the air and half of this box, so the flight's 78 px
    /// of new band never entered it - that band moved the BOX (it stands 20 px over the
    /// band's last row) while the help line's 70 came out here, leaving the edge itself
    /// 8 taller - 106 = 8 of border + 70 of help + 6 of air + the half of this box the
    /// number centres.
    /// </summary>
    private const float StatusY = 106f;

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
