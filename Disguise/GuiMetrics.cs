#nullable enable

namespace Disguise;

/// <summary>
/// The two measures the window shares with the lines under it. The width is the 860 of the
/// window minus the margin it keeps on both sides: the help line, the status line and the
/// state line are all cut to it, so they line up with the columns of the window instead of
/// each drifting a few pixels. The height is one row: a command button, the state line, a
/// line of the list - the help line is the one line of the window that is NOT one of these
/// anymore, its band being three lines tall of its own (GuiHelp.HelpHeight).
///
/// Both live here and nowhere else, so a measure that used to be three private constants
/// (and one raw literal) cannot drift apart. Everything else keeps its own constant in its
/// own file: the status line is a notch taller (44f) because it sits under the window's
/// frame, the title row sits above all of this (TitleRowY, in GuiWindow), and the font
/// sizes are one-offs that read best where they are written.
/// </summary>
internal static class GuiMetrics
{
    /// <summary>The measure every line spanning the window is cut to.</summary>
    internal const float LineWidth = 800f;

    /// <summary>The height of one row of the window.</summary>
    internal const float RowHeight = 40f;
}
