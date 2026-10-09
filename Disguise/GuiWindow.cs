#nullable enable
using System;
using BepInEx.Configuration;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// Creates what goes INSIDE the Disguise window, with Jotunn's Valheim-styled controls (wood
/// panel, AveriaSerif fonts, Valheim buttons). DisguiseGui owns the lifecycle: this class
/// only builds, it never keeps the window alive and it never talks to the console.
///
/// The window as the branch built it, in four steps: title and, directly under it, the line
/// that says what is configured (with the GuiKey chord on the title row, next to Close), the
/// six category checkboxes that gate the list (GuiCategories builds them, this class only
/// says where they sit - under that line), the
/// prefab list with its filter (GuiCreatureList builds that block, this class only tells it
/// where to sit), on the right the 3D preview (GuiPreview builds the picture, CreaturePreview
/// films into it), under it the four command buttons, and at the bottom the status line
/// (GuiStatus), which is the hint while no button has been pressed and the answer of the
/// last one after that.
/// </summary>
internal static class GuiWindow
{
    /// <summary>Final size of the window: it must not jump when the last step arrives.</summary>
    internal const float Width = 860f;

    /// <summary>
    /// Height of the panel. The list block takes 338 of them (filter 36 + gap 2 + 300 of
    /// list, GuiCreatureList.BlockHeight) under the -132 it starts at, so it ends at -470;
    /// the two pose rows (GuiPose) take 78 more with their gaps, -476 down to -554; and the
    /// status line - anchored to the bottom edge, its box ending 58 px above it - follows
    /// this down, so nothing above ever moves when it changes. 630 is what leaves those two
    /// rows their band above that line: 540 left 50 px under the list, which fit neither of
    /// them, and 600 put the Offset row back over the last rows of the list.
    /// </summary>
    internal const float Height = 630f;

    // Left half of the window: top-left corner of the filter + list block, and its size.
    private const float ListX = 24f;

    private const float ListY = -132f;

    private const float ListWidth = 400f;

    private const float ListHeight = 300f;

    /// <summary>
    /// Gap between the bottom of the list BLOCK (filter + gap + list) and the first pose row
    /// (GuiPose).
    /// </summary>
    private const float PoseGap = 6f;

    /// <summary>
    /// Centre of the state line (the prefab, the two toggles and now the size of the
    /// costume): a 40 px box, so -56 to -96 - the title's own box ends at -56 and this one
    /// starts where that one stops. Jotunn never sets a pivot (it stays in the middle), so
    /// this position IS the centre of the box, exactly like CategoryY below.
    /// </summary>
    private const float StateY = -76f;

    /// <summary>
    /// Centre of the checkbox row (GuiCategories), UNDER the state line and over the list
    /// block: the band -96 to -132 is 36 px, and the row takes the middle of it - its 20 px
    /// box lands at -104..-124 and its 24 px labels at -102..-126, six pixels clear of the
    /// state line above and six of the filter below. The row is left-aligned with the list it
    /// filters, and neither of them moves for the other.
    /// </summary>
    private const float CategoryY = -114f;

    // Right half: upper left corner of the 3D preview, given to GuiPreview as the list
    // block's corner is given to GuiCreatureList: it starts at the height of the filter, is
    // as tall as the list, and the command row plus the status line fill the space down to
    // the bottom edge of the window.
    private const float PreviewX = 25f;

    private const float PreviewY = -132f;

    /// <summary>Gap between the bottom of the picture and the row of buttons under it.</summary>
    private const float PreviewGap = 6f;

    // The command row, as wide as the picture above it: four buttons of equal width.
    private const float ButtonHeight = 40f;

    private const float ButtonGap = 6f;

    private const float ButtonTop = PreviewY - CreaturePreview.PictureHeight - PreviewGap;

    /// <summary>Centre of the row, given to CreateButton as a position of the window.</summary>
    private const float ButtonY = ButtonTop - (ButtonHeight * 0.5f);

    /// <summary>
    /// The label of the first command button, kept here because it is a toggle: RefreshState
    /// rewrites it after every change, the same way it rewrites the state line. Unity-null
    /// once the window is gone, which is the whole safety of both.
    /// </summary>
    private static Text? s_power;

    /// <summary>
    /// The line under the title, kept here so it can be rewritten after a click on a row or a
    /// change made by the console while the window is open - and once per frame while it is
    /// open, because the flight and its offset move on their own (DisguiseGui.Tick).
    /// Unity-null once the window is gone, which is the whole safety of RefreshState.
    /// </summary>
    private static Text? s_state;

    internal static GameObject Build(Transform parent)
    {
        GUIManager gui = GUIManager.Instance;
        s_state = null;
        s_power = null;

        // Draggable: Jotunn puts its DragWindowCntrl on the panel, and the whole background
        // forwards the gesture, so there is no dedicated title bar to grab.
        GameObject window = gui.CreateWoodpanel(
            parent,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Width,
            Height,
            draggable: true
        );

        _ = Label(
            gui,
            "Disguise",
            window.transform,
            new Vector2(0.5f, 1f),
            new Vector2(-70f, -34f),
            gui.AveriaSerifBold,
            30,
            gui.ValheimOrange,
            460f,
            44f
        );

        GameObject closeButton = gui.CreateButton(
            "Close",
            window.transform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-72f, -34f),
            110f,
            42f
        );
        Button? button = closeButton.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(DisguiseGui.Hide);
        }

        // The chord on the title row, between the title and Close: the window shows the keys
        // that call it back after it is closed, read from the config at every open (the same
        // value ConfigurationManager records), so it is never a stale guess. Anchored to the
        // top-right corner like Close, whose centre it keeps: x -240 is 190 right of the
        // middle of the window, 13 px left of the button.
        GameObject chord = Label(
            gui,
            "Open / close\n" + DescribeChord(),
            window.transform,
            new Vector2(1f, 1f),
            new Vector2(-240f, -34f),
            gui.AveriaSerif,
            16,
            gui.ValheimBeige,
            200f,
            44f
        );
        Text? chordText = chord.GetComponent<Text>();
        if (chordText != null)
        {
            // A chord of three modifiers is wider than this box: shrink the whole label
            // instead of clipping its second line (Unity shrinks only when it must).
            chordText.resizeTextForBestFit = true;
            chordText.resizeTextMaxSize = 16;
            chordText.resizeTextMinSize = 10;
        }

        GameObject state = Label(
            gui,
            DescribeState(),
            window.transform,
            new Vector2(0.5f, 1f),
            new Vector2(0f, StateY),
            gui.AveriaSerif,
            20,
            gui.ValheimBeige,
            800f,
            40f
        );
        Text? stateText = state.GetComponent<Text>();
        if (stateText != null)
        {
            // Five values on the line now (prefab, the toggle, the flight, the height, the
            // size): a long prefab name must make the text smaller rather than run past the
            // edges of the window - the rule the chord label above already follows, at its
            // own sizes.
            stateText.resizeTextForBestFit = true;
            stateText.resizeTextMaxSize = 20;
            stateText.resizeTextMinSize = 14;
        }
        s_state = stateText;

        // The six category checkboxes, under the state line and over the block they filter.
        GuiCategories.Build(window.transform, ListX, CategoryY);

        // The filter field and the scrollable list, built by GuiCreatureList.
        GuiCreatureList.Build(window.transform, ListX, ListY, ListWidth, ListHeight);

        // The two rows that move and turn the costume, under the block the list makes (the
        // list itself is only its scroll view: the filter field and its gap are part of the
        // block too, which is why the corner is measured through BlockHeight - GuiWindow
        // owns the corner, GuiPose owns the rhythm of its own two rows).
        GuiPose.Build(
            window.transform,
            ListX,
            ListY - GuiCreatureList.BlockHeight(ListHeight) - PoseGap
        );

        // The 3D preview on the right: the picture here, the stage it shows in CreaturePreview.
        GuiPreview.Build(window.transform, PreviewX, PreviewY);

        // The four command buttons under the picture, then the line they answer on (which
        // still holds the hint about clicking a row while no button has been pressed).
        CommandButtons(gui, window.transform);
        GuiStatus.Build(window.transform);

        return window;
    }

    /// <summary>
    /// The row under the picture: four buttons, each one running its sub-command through
    /// DisguiseCommand.Run, which has no console here and answers on the status line below.
    /// The first one is a toggle, so what it says and what it runs both follow the config.
    /// </summary>
    private static void CommandButtons(GUIManager gui, Transform parent)
    {
        float width = (CreaturePreview.PictureWidth - (3f * ButtonGap)) / 4f;
        float centre = PreviewX + (width * 0.5f);

        s_power = Command(gui, parent, DescribePower(), centre, width, TogglePower);

        centre += width + ButtonGap;
        _ = Command(gui, parent, "Refresh", centre, width, () => RunCommand("refresh"));

        centre += width + ButtonGap;
        _ = Command(gui, parent, "Own view", centre, width, () => RunCommand("view"));

        centre += width + ButtonGap;
        _ = Command(gui, parent, "Flight", centre, width, () => RunCommand("fly"));
    }

    /// <summary>One button of the command row; its label Text is returned for the toggle.</summary>
    private static Text? Command(
        GUIManager gui,
        Transform parent,
        string label,
        float centreX,
        float width,
        UnityAction onClick
    )
    {
        GameObject body = gui.CreateButton(
            label,
            parent,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(centreX, ButtonY),
            width,
            ButtonHeight
        );
        Button? button = body.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(onClick);
        }
        return body.GetComponentInChildren<Text>();
    }

    /// <summary>What every command button does: run its sub-command, then rewrite the state.</summary>
    private static void RunCommand(string sub)
    {
        DisguiseCommand.Run(sub);
        RefreshState();
    }

    /// <summary>The toggle: enable when it is off, disable when it is on - the label says which.</summary>
    private static void TogglePower() =>
        RunCommand(DisguiseConfig.ConfigEnabled.Value ? "disable" : "enable");

    /// <summary>
    /// Rewrites the line under the title, the label of the toggle and the six pose fields,
    /// writing NONE of them when it is unchanged: called once per frame while the window is
    /// open (DisguiseGui.Tick), by GuiCreatureList after a row was clicked or after a change
    /// the console made while the window was open, and by the command buttons after they ran
    /// their sub-command. Two of the four values on the line move on their own - the flight
    /// and its height - so the ordinary frame costs the line being built and compared, never a
    /// write to the Text (which would lay it out again for the same words); the pose fields
    /// cost six more string comparisons, and skip the one being typed in (GuiPose). Does
    /// nothing without a window, which is every frame it is closed and every frame after a
    /// scene change destroyed it.
    /// </summary>
    internal static void RefreshState()
    {
        Text? label = s_state;
        if (label != null)
        {
            string line = DescribeState();
            if (!string.Equals(label.text, line, StringComparison.Ordinal))
            {
                label.text = line;
            }
        }

        Text? power = s_power;
        if (power != null)
        {
            string toggle = DescribePower();
            if (!string.Equals(power.text, toggle, StringComparison.Ordinal))
            {
                power.text = toggle;
            }
        }

        GuiPose.Sync();
    }

    /// <summary>
    /// One line of text, centred: Jotunn builds the object, the alignment is then set here
    /// because its helper leaves the Unity default (upper left), which would push every
    /// centred line into the left half of its box. Shared with GuiCreatureList, which puts
    /// the "nothing matches" notice in the middle of the list.
    /// </summary>
    internal static GameObject Label(
        GUIManager gui,
        string text,
        Transform parent,
        Vector2 anchor,
        Vector2 position,
        Font font,
        int fontSize,
        Color color,
        float width,
        float height
    )
    {
        GameObject label = gui.CreateText(
            text,
            parent,
            anchor,
            anchor,
            position,
            font,
            fontSize,
            color,
            outline: true,
            outlineColor: Color.black,
            width,
            height,
            addContentSizeFitter: false
        );
        Text? text1 = label.GetComponent<Text>();
        if (text1 != null)
        {
            text1.alignment = TextAnchor.MiddleCenter;
        }
        return label;
    }

    /// <summary>
    /// The line under the title: the prefab configured right now, whether the disguise is on,
    /// whether the flight is, how far the costume is raised above the player - the flight's
    /// own height, in metres, with one decimal - and how big that prefab is (the size of one
    /// box around its meshes, width x height x depth in metres, left out while it has no mesh
    /// to measure).
    ///
    /// It is called a height rather than an offset because the Offset row below (GuiPose) is a
    /// different thing: this one is the flight's own raise, an offset and not a position - the
    /// player never moves and neither does its Y, only what the machines draw is lifted
    /// (FlightControl.ViewOffset on this screen, cd_fly in the packets of every other one).
    /// </summary>
    private static string DescribeState()
    {
        string creature = PrefabResolver.PrefabName(PrefabResolver.CreaturePrefabHash);
        string disguise = DisguiseConfig.ConfigEnabled.Value ? "on" : "off";
        string flight = FlightControl.IsFlying ? "on" : "off";
        string line =
            $"Prefab: {creature}   -   disguise {disguise}   -   "
            + $"Flight {flight}   height: {FlightHeight()}";
        Vector3 size = PrefabResolver.CreatureSize();
        if (size.sqrMagnitude > 0f)
        {
            line += $"   -   size {size.x:0.0} x {size.y:0.0} x {size.z:0.0} m";
        }
        return line;
    }

    /// <summary>
    /// How far the costume stands above the player (metres, one decimal): "0.0" while the
    /// flight is off - Disable puts the height back to 0 there - and the height the flight
    /// holds it at otherwise. FlightControl clamps that height to 0..100, so it can only ever
    /// read 0 or more: a negative raise has no meaning (nothing sinks under the ground the
    /// player already stands on). The pose of the costume itself is written by the two rows
    /// below and never reaches this line, which keeps it about the flight alone.
    /// </summary>
    private static string FlightHeight()
    {
        float raise = FlightControl.IsFlying ? FlightControl.Height : 0f;
        return $"{raise:0.0}";
    }

    /// <summary>
    /// The label of the toggle button: what pressing it does right now, so the same button
    /// reads "Disable" while the disguise is on and "Enable" while it is off.
    /// </summary>
    private static string DescribePower() =>
        DisguiseConfig.ConfigEnabled.Value ? "Disable" : "Enable";

    /// <summary>
    /// The GuiKey chord as the config records it - main key first, then the modifiers, which
    /// is what the .cfg holds ("F9 + LeftAlt") - or "Not set", the word ConfigurationManager
    /// shows for an empty one and the reason the chord never fires then.
    /// </summary>
    private static string DescribeChord()
    {
        KeyboardShortcut shortcut = DisguiseConfig.ConfigGuiKey.Value;
        if (shortcut.MainKey == KeyCode.None)
        {
            return "Not set";
        }
        string chord = shortcut.MainKey.ToString();
        foreach (KeyCode modifier in shortcut.Modifiers)
        {
            if (modifier != KeyCode.None)
            {
                chord += " + " + modifier.ToString();
            }
        }
        return chord;
    }
}
