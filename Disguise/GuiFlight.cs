#nullable enable
using System;
using System.Globalization;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The window's side of the flight, in two places that have room for it: the four chords it
/// answers to (FlyEnableKey, FlyDisableKey, FlyUpKey, FlyDownKey) on a line of their own in
/// the empty half under the pose rows - the title row only ever had space for the one chord
/// that opens the window - and the three settings it flies with, as the band's FIFTH and
/// SIXTH rows under the off-list box (GuiWindow passes every coordinate; this class only
/// builds). The switch is the very control the other boxes of the band are made of
/// (GuiCategories.BuildBox, FlyScrollWheel), the two numbers are fields the way the pose
/// rows' six are (GuiPose) - one row of them, Speed beside Step - and the chords are spelled
/// the way the .cfg holds them (GuiWindow.DescribeChord, read when the window opens, the
/// same as the chord on the title row).
///
/// A change of a number applies as it is typed, with the parser the console and the pose
/// rows share (DisguiseCommand.TryParseCoordinate, so "1.5" and "1,5" both land) and a box
/// that is not a number yet keeps the value it had. What the player is typing is never
/// rewritten either: Sync repairs the fields from the outside only while they are not
/// focused, and while one of the two HAS the focus the frame belongs to the UI (Focused,
/// read by DisguiseGui.BlocksInput) exactly as the pose fields and the filter do - typing a
/// speed must not walk the character away. Nothing of this ever reaches the console or the
/// status line: a box writes its entry, and the answer a click gives is the other rows'
/// business - the switch and the two numbers change their entries and nothing else (the
/// flight itself still answers its four keys and "disguise fly" for whoever prefers
/// either).
/// </summary>
internal static class GuiFlight
{
    // The chord line: four lines of 16 px in the air under the pose rows (GuiWindow passes
    // its centre), best-fit like the chord on the title row for a chord written long.
    private const float KeysWidth = 400f;

    private const float KeysHeight = 90f;

    private const int KeysFontSize = 16;

    // The number row: one caption and one field per setting, the rhythm GuiPose keeps for
    // its own rows - a centre, then a row tall enough for a 16 px word in a 36 px box.
    private const float RowHeight = 36f;

    private const float SpeedLabelWidth = 104f;

    private const float StepLabelWidth = 84f;

    private const float FieldWidth = 64f;

    private const float ItemGap = 6f;

    /// <summary>What the chord line is for (GuiHelp.Watch - the line and its four chords).</summary>
    private const string KeysHelp =
        "The four flight chords, read from the config when the window opens: on and off start the flight and land it, up and down climb and descend while held. \"Not set\" means that chord never fires.";

    /// <summary>What the wheel switch is for (GuiHelp.Watch - box and label, BuildBox).</summary>
    private const string WheelHelp =
        "Raise or lower the costume with the mouse wheel while the flight is on - one notch is the step below, and the camera zoom waits while the wheel flies. On, the default; off gives the wheel back to the camera (FlyScrollWheel).";

    private const string SpeedHelp =
        "How fast a held climb or descent key moves, in metres per second - 5 by default (FlySpeed). A number, and a half-typed one keeps the value the field had.";

    private const string StepHelp =
        "Metres one notch of the wheel moves the costume while the box above is on - 0.2 by default (FlyScrollStep). 0 gives the wheel back to the camera, a negative value reverses climb and descent.";

    /// <summary>The chord line, its switch and its two fields - Unity-null once the window is gone.</summary>
    private static Text? s_keys;

    private static Toggle? s_wheel;

    private static bool s_wheelApplied;

    private static InputField? s_speed;

    private static InputField? s_step;

    /// <summary>True while Sync writes a field, so its own listener has nothing to apply.</summary>
    private static bool s_syncing;

    /// <summary>
    /// True while the player types in one of the two fields: the frame then belongs to the
    /// UI whether the pointer is on the window or not - read by DisguiseGui.BlocksInput,
    /// exactly as GuiPose.Focused and GuiCreatureList.FilterFocused are.
    /// </summary>
    internal static bool Focused => IsFocused(s_speed) || IsFocused(s_step);

    // ------------------------------------------------------------------ build

    /// <summary>
    /// Builds the chord line and the two rows of the band. GuiWindow owns the layout and
    /// passes everything: the centre of the line (keysX, keysY), the band's own left edge
    /// (bandX, where every option row starts) and the CENTRES of the two rows (wheelY,
    /// fieldsY) - the second one being a row of fields rather than a switch line, which is
    /// why it is given the same 36 the boxes of the band stand at.
    /// </summary>
    internal static void Build(
        Transform parent,
        float keysX,
        float keysY,
        float bandX,
        float wheelY,
        float fieldsY
    )
    {
        Forget();

        GUIManager gui = GUIManager.Instance;

        // The four chords, read from the config at this very open (the same rule as the
        // chord on the title row): one line of the window that is not an option at all,
        // standing where the list and the pose rows leave the left half empty.
        GameObject keys = GuiWindow.Label(
            gui,
            DescribeKeys(),
            parent,
            new Vector2(0.5f, 1f),
            new Vector2(keysX, keysY),
            gui.AveriaSerif,
            KeysFontSize,
            gui.ValheimBeige,
            KeysWidth,
            KeysHeight
        );
        Text? keysText = keys.GetComponent<Text>();
        if (keysText != null)
        {
            // Four lines at most, and a chord longer than the line it was written for makes
            // the whole text smaller rather than pushing its last line past the box - the
            // rule the chord and the state line of this window follow.
            keysText.resizeTextForBestFit = true;
            keysText.resizeTextMaxSize = KeysFontSize;
            keysText.resizeTextMinSize = 10;
        }
        s_keys = keysText;
        GuiHelp.Watch(keys, KeysHelp);

        // The switch: the band's own control (GuiCategories.BuildBox, the same one the
        // sparkle, armor, weapons and off-list boxes are), writing its entry itself.
        float cursor = bandX;
        s_wheelApplied = DisguiseConfig.ConfigFlyScrollWheel.Value;
        s_wheel = GuiCategories.BuildBox(
            parent,
            ref cursor,
            wheelY,
            "Fly with wheel",
            WheelHelp,
            s_wheelApplied,
            on =>
            {
                s_wheelApplied = on;
                DisguiseConfig.ConfigFlyScrollWheel.Value = on;
            }
        );

        // The number row under it: caption, field, caption, field - centres, from the
        // band's own left edge. The listener comes after the text, so the initial value
        // has nothing to apply (GuiPose's own rule).
        float rowTop = fieldsY + (RowHeight * 0.5f);
        float labelX = bandX;
        BuildCaption(gui, parent, labelX, fieldsY, "Speed (m/s)", SpeedLabelWidth, SpeedHelp);
        float fieldX = labelX + SpeedLabelWidth + ItemGap;
        s_speed = BuildNumber(gui, parent, fieldX, rowTop, "m/s", SpeedHelp);
        if (s_speed != null)
        {
            s_speed.text = Number(DisguiseConfig.ConfigFlySpeed.Value);
            s_speed.onValueChanged.AddListener(text => OnNumber(text, true));
        }

        labelX = fieldX + FieldWidth + ItemGap;
        BuildCaption(gui, parent, labelX, fieldsY, "Step (m)", StepLabelWidth, StepHelp);
        fieldX = labelX + StepLabelWidth + ItemGap;
        s_step = BuildNumber(gui, parent, fieldX, rowTop, "m", StepHelp);
        if (s_step != null)
        {
            s_step.text = Number(DisguiseConfig.ConfigFlyScrollStep.Value);
            s_step.onValueChanged.AddListener(text => OnNumber(text, false));
        }
    }

    /// <summary>
    /// One caption of the number row - a centred label at the row's own centre, watched for
    /// the help the field beside it carries as well: the pointer lands on whichever of the
    /// two it really is over, and the event system stops at the first object handling it.
    /// </summary>
    private static void BuildCaption(
        GUIManager gui,
        Transform parent,
        float x,
        float centreY,
        string text,
        float width,
        string help
    )
    {
        GameObject body = GuiWindow.Label(
            gui,
            text,
            parent,
            new Vector2(0f, 1f),
            new Vector2(x + (width * 0.5f), centreY),
            gui.AveriaSerif,
            16,
            gui.ValheimBeige,
            width,
            RowHeight
        );
        GuiHelp.Watch(body, help);
    }

    /// <summary>
    /// One field of the number row: Jotunn's Valheim-styled input field with the pivot
    /// correction every field of this window needs (Jotunn sets the position BEFORE the
    /// anchors and never touches the pivot, which stays in the middle - so the corner asked
    /// for would be the centre of the field), the same two lines as GuiPose's own.
    /// </summary>
    private static InputField? BuildNumber(
        GUIManager gui,
        Transform parent,
        float x,
        float rowTop,
        string placeholder,
        string help
    )
    {
        GameObject body = gui.CreateInputField(
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(x, rowTop),
            InputField.ContentType.Standard,
            placeholder,
            18,
            FieldWidth,
            RowHeight
        );
        RectTransform rect = body.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, rowTop);
        }
        GuiHelp.Watch(body, help);
        return body.GetComponent<InputField>();
    }

    /// <summary>Drops the controls (the window is gone: Unity-null is the whole safety here).</summary>
    internal static void Forget()
    {
        s_keys = null;
        s_wheel = null;
        s_speed = null;
        s_step = null;
        s_syncing = false;
    }

    // ------------------------------------------------------------------ per frame

    /// <summary>
    /// Brings the line and the two fields up to date, one string comparison each: called
    /// once per frame while the window is open (GuiWindow.RefreshState), so a value typed
    /// in ConfigurationManager or the .cfg lands without reopening - and what the player is
    /// typing stays where it is, focus being the only thing that stops a write. The switch
    /// follows the same rule as every box of the band: only a real change is pushed back,
    /// and a click on it writes that cache itself.
    /// </summary>
    internal static void Sync()
    {
        GuiText.Set(s_keys, DescribeKeys);

        Toggle? wheel = s_wheel;
        if (wheel != null)
        {
            bool on = DisguiseConfig.ConfigFlyScrollWheel.Value;
            if (on != s_wheelApplied)
            {
                s_wheelApplied = on;
                wheel.SetIsOnWithoutNotify(on);
            }
        }

        SyncField(s_speed, Number(DisguiseConfig.ConfigFlySpeed.Value));
        SyncField(s_step, Number(DisguiseConfig.ConfigFlyScrollStep.Value));
    }

    // ------------------------------------------------------------------ listeners

    /// <summary>
    /// One of the two fields changed (speed = true, step = false): parsed with the parser
    /// the console and the pose rows share, and written straight into the entry - a box
    /// that is not a number yet keeps the value it had, the same rule as GuiPose.
    /// </summary>
    private static void OnNumber(string text, bool speed)
    {
        if (s_syncing || !DisguiseCommand.TryParseCoordinate(text, out float value))
        {
            return;
        }
        if (speed)
        {
            DisguiseConfig.ConfigFlySpeed.Value = value;
        }
        else
        {
            DisguiseConfig.ConfigFlyScrollStep.Value = value;
        }
    }

    // ------------------------------------------------------------------ helpers

    private static bool IsFocused(InputField? field) => field != null && field.isFocused;

    private static void SyncField(InputField? field, string text)
    {
        if (field == null || field.isFocused)
        {
            return;
        }
        if (string.Equals(field.text, text, StringComparison.Ordinal))
        {
            return;
        }
        s_syncing = true;
        field.text = text;
        s_syncing = false;
    }

    /// <summary>
    /// What a field shows: two decimals - the precision the window's other numbers keep - in
    /// the current culture, so a French keyboard reads back exactly what it typed ("1,5"
    /// stays "1,5" and parses again).
    /// </summary>
    private static string Number(float value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>
    /// The four chords on their line, one per line and in the order the .cfg lists them
    /// (enable, disable, up, down), each spelled the way the config records it.
    /// </summary>
    private static string DescribeKeys() =>
        $"Flight on: {GuiWindow.DescribeChord(DisguiseConfig.ConfigFlyEnableKey.Value)}\n"
        + $"Flight off: {GuiWindow.DescribeChord(DisguiseConfig.ConfigFlyDisableKey.Value)}\n"
        + $"Flight up: {GuiWindow.DescribeChord(DisguiseConfig.ConfigFlyUpKey.Value)}\n"
        + $"Flight down: {GuiWindow.DescribeChord(DisguiseConfig.ConfigFlyDownKey.Value)}";
}
