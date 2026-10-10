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
/// only builds, it never keeps the window alive and it never talks to the console. It does
/// say where the panel SITS, though - the place the config holds, an offset from the middle
/// of the screen (Place), which is a new window's centre.
///
/// The window as the branch built it, in four steps: title and, directly under it, the line
/// that says what is configured (with the GuiKey chord on the title row, next to Close), the
/// row of checkboxes that gate the list (Favorites first, then the six categories
/// alphabetically - GuiCategories builds
/// them, this class only says where they sit - under that line), the
/// prefab list with its filter (GuiCreatureList builds that block, this class only tells it
/// where to sit), on the right the 3D preview (GuiPreview builds the picture, CreaturePreview
/// films into it), under it the five command buttons and, under those, the sparkle box
/// (HideItemSparkle - the one checkbox that is not in the row above, because it gates
/// nothing: it is the costume itself that is drawn differently), then the status line
/// (GuiStatus), which is the hint while no button has been pressed and the answer of the
/// last one after that - and, closing the window under it, the help line (GuiHelp), which
/// holds what the element the pointer is on is for and the idle line while it is on
/// nothing.
/// </summary>
internal static class GuiWindow
{
    /// <summary>Final size of the window: it must not jump when the last step arrives.</summary>
    internal const float Width = 860f;

    /// <summary>
    /// Height of the panel. The list block takes 338 of them (filter 36 + gap 2 + 300 of
    /// list, GuiCreatureList.BlockHeight) under the -132 it starts at, so it ends at -470;
    /// the two pose rows (GuiPose) take 78 more with their gaps, -476 down to -554; the
    /// status line keeps the band it has always had under them (-572..-616); and the help
    /// line (GuiHelp) closes the window below that band. 630 is what left those two rows
    /// their band above the status line: 540 left 50 px under the list, which fit neither of
    /// them, and 600 put the Offset row back over the last rows of the list. The help line
    /// then asked for 40 px at the BOTTOM, and the panel grew downwards for them - so
    /// nothing above moved a pixel, the status line alone following its own edge (it is
    /// anchored to the bottom one, which is why GuiStatus.StatusY grew by the same 40 and
    /// its box stands at -572..-616 exactly as before).
    /// </summary>
    internal const float Height = 670f;

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

    // The command row, as wide as the picture above it: CommandCount buttons of equal width.
    private const int CommandCount = 5;

    private const float ButtonHeight = 40f;

    private const float ButtonGap = 6f;

    private const float ButtonTop = PreviewY - CreaturePreview.PictureHeight - PreviewGap;

    /// <summary>Centre of the row, given to CreateButton as a position of the window.</summary>
    private const float ButtonY = ButtonTop - (ButtonHeight * 0.5f);

    /// <summary>
    /// Centre of the sparkle box (HideItemSparkle): the right half of the window has 94 px
    /// between the bottom of the command row (-478) and the band the status line takes
    /// (-572), and the box takes the upper part of them - 14 px under the buttons, 56 above
    /// that line - starting at the picture's own left edge, which is where the button row
    /// above starts as well. The same control as the checkbox row (GuiCategories builds it),
    /// standing alone here with its own entry to write and no list to re-filter.
    /// </summary>
    private const float SparkleY = -504f;

    /// <summary>
    /// The label of the first command button, kept here because it is a toggle: RefreshState
    /// rewrites it after every change, the same way it rewrites the state line. Unity-null
    /// once the window is gone, which is the whole safety of both.
    /// </summary>
    private static Text? s_power;

    /// <summary>
    /// The label of the Favorite button, kept here with the toggle's because it follows
    /// something that moves on its own as well: the configured prefab - the name on the state
    /// line, which the console can change while the window is open - so it can flip between
    /// "Add favorite" and "Remove favorite" without a click. Unity-null once the window is
    /// gone, which is the whole safety of RefreshState.
    /// </summary>
    private static Text? s_favorite;

    /// <summary>
    /// The line under the title, kept here so it can be rewritten after a click on a row or a
    /// change made by the console while the window is open - and once per frame while it is
    /// open, because the flight and its offset move on their own (DisguiseGui.Tick).
    /// Unity-null once the window is gone, which is the whole safety of RefreshState.
    /// </summary>
    private static Text? s_state;

    /// <summary>
    /// The sparkle box under the command row, and what it showed last: the entry can also be
    /// written by ConfigurationManager while the window is open, and RefreshState pushes that
    /// back into the box the same way it rewrites the labels above (a click writes the cache
    /// itself, so it is never pushed back). Unity-null once the window is gone, which is the
    /// whole safety of it.
    /// </summary>
    private static Toggle? s_sparkle;

    private static bool s_sparkleApplied;

    internal static GameObject Build(Transform parent)
    {
        GUIManager gui = GUIManager.Instance;
        s_state = null;
        s_power = null;
        s_favorite = null;
        s_sparkle = null;

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

        // Where it sits: the place the config holds, which is an offset from the MIDDLE of
        // the screen - 0/0, what both entries start at, IS the middle, so a window nobody
        // has dragged opens centred and one that was moved comes back where it was left
        // (DisguiseGui remembers the place every frame and writes it back on close).
        RectTransform? panel = window.GetComponent<RectTransform>();
        if (panel != null)
        {
            Place(panel);
        }

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
        GuiHelp.Watch(
            closeButton,
            "Close the window - the chord written on the title row opens it again."
        );

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
        GuiHelp.Watch(
            chord,
            "The keys that call this window back and send it away - read from the config at every open, \"Not set\" while the chord is empty."
        );

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
        GuiHelp.Watch(
            state,
            "What is configured right now: the prefab, the disguise on or off, the flight with the height it lifts the costume by, and the size of that prefab in metres (left out while it has no mesh to measure)."
        );

        // The checkbox row - Favorites first, then the six categories alphabetically - under
        // the state line and over
        // the block it filters.
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

        // The five command buttons under the picture, the sparkle box under them, the
        // line they answer on (which still holds the hint about clicking a row while no
        // button has been pressed), and the help line closing the window under that one.
        CommandButtons(gui, window.transform);
        SparkleBox(window.transform);
        GuiStatus.Build(window.transform);
        GuiHelp.Build(window.transform);

        return window;
    }

    /// <summary>
    /// The box under the command row: HideItemSparkle, the option that takes the shimmer of
    /// a dropped item off an Item costume (GhostFactory reads it at every build of a clone).
    /// The control itself comes from GuiCategories.BuildBox - the very one the checkbox row
    /// is made of - and the listener here writes the entry and the cache, and nothing else:
    /// there is no list to re-filter, and the clones standing rebuild themselves when the
    /// value moves (SelfView, GuiPreview). Placed at the picture's own left edge, where the
    /// row of buttons above starts, so the right half keeps one edge all the way down.
    /// </summary>
    private static void SparkleBox(Transform parent)
    {
        s_sparkleApplied = DisguiseConfig.ConfigHideItemSparkle.Value;

        // The preview anchors on the MIDDLE of the window and starts PreviewX right of it
        // (GuiPreview), while the box anchors on its top-left corner (BuildBox): the two
        // corners are the same x once this much is added to it.
        float cursor = (Width * 0.5f) + PreviewX;
        s_sparkle = GuiCategories.BuildBox(
            parent,
            ref cursor,
            SparkleY,
            "Hide item sparkle",
            "Takes the shimmer off an item costume on THIS machine - your own costume and the picture above; what another machine draws is never touched.",
            s_sparkleApplied,
            on =>
            {
                s_sparkleApplied = on;
                DisguiseConfig.ConfigHideItemSparkle.Value = on;
            }
        );
    }

    /// <summary>
    /// Puts the window where the config says, then keeps it on screen with the very rule the
    /// drag follows (Jotunn's DragWindowCntrl clamps the panel inside the monitor on every
    /// move): a place can also come from a hand typing into the .cfg, and from another
    /// resolution - it is a number of pixels - and neither may be able to leave the window
    /// where no hand could reach it. Screen coordinates, because that is what transform
    /// position is on the overlay canvas the window lives on, and what the drag works in.
    /// Called when the window is built, and once more for every place written into the
    /// config while it is on screen (DisguiseGui), so a value typed in ConfigurationManager
    /// lands without closing the window.
    /// </summary>
    internal static void Place(RectTransform panel)
    {
        panel.anchoredPosition = DisguiseConfig.WindowPlace;

        Vector2 at = panel.position;
        Rect size = panel.rect;
        Vector2 scale = panel.lossyScale;
        float halfWidth = (size.width * 0.5f) * scale.x;
        float halfHeight = (size.height * 0.5f) * scale.y;
        Vector2 keep = new Vector2(
            Mathf.Clamp(at.x, halfWidth, Screen.width - halfWidth),
            Mathf.Clamp(at.y, halfHeight, Screen.height - halfHeight)
        );
        if (keep != at)
        {
            panel.position = keep;
        }
    }

    /// <summary>
    /// The row under the picture: CommandCount buttons of equal width, each one running its
    /// sub-command through DisguiseCommand.Run, which has no console here and answers on the
    /// status line below. The first is a toggle, so what it says and what it runs both follow
    /// the config; the last follows the configured prefab and its mark (DescribeFavorite),
    /// which is why RefreshState rewrites it every frame like the state line above.
    /// </summary>
    private static void CommandButtons(GUIManager gui, Transform parent)
    {
        float width =
            (CreaturePreview.PictureWidth - ((CommandCount - 1) * ButtonGap)) / CommandCount;
        float centre = PreviewX + (width * 0.5f);

        s_power = Command(
            gui,
            parent,
            DescribePower(),
            "Turn the disguise on for everyone - your ZDO then carries the configured prefab instead of your viking - or stop it (disguise enable / disable).",
            centre,
            width,
            TogglePower
        );

        centre += width + ButtonGap;
        _ = Command(
            gui,
            parent,
            "Refresh",
            "Read the costume and the list again from the network (disguise refresh).",
            centre,
            width,
            () => RunCommand("refresh")
        );

        centre += width + ButtonGap;
        _ = Command(
            gui,
            parent,
            "Own view",
            "Draw the costume on YOUR screen instead of your viking - purely local, no machine is told anything (SeeYourDisguise, disguise view).",
            centre,
            width,
            () => RunCommand("view")
        );

        centre += width + ButtonGap;
        _ = Command(
            gui,
            parent,
            "Flight",
            "Raise the costume for everyone who draws you, up to 100 m - your own body stays on the ground (disguise fly).",
            centre,
            width,
            () => RunCommand("fly")
        );

        centre += width + ButtonGap;
        s_favorite = Command(
            gui,
            parent,
            DescribeFavorite(),
            "Mark the prefab named on the line above, or take the mark off - favorites are what the Favorites box shows (disguise favorite).",
            centre,
            width,
            ToggleFavorite
        );
    }

    /// <summary>One button of the command row; its label Text is returned for the toggle.</summary>
    private static Text? Command(
        GUIManager gui,
        Transform parent,
        string label,
        string help,
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
        GuiHelp.Watch(body, help);
        Button? button = body.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(onClick);
        }
        Text? text = body.GetComponentInChildren<Text>();
        if (text != null)
        {
            // Five buttons in the width of the picture: "Remove favorite" is the widest label
            // of the row, and Unity's Text CLIPS what does not fit unless it is told to shrink
            // instead - the rule the chord and the state line of this window already follow.
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = 10;
        }
        return text;
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
    /// The Favorite button: mark the SELECTED prefab - the configured one, the name on the
    /// state line above - or take the mark off, which is exactly what its label announced
    /// (DescribeFavorite). It runs the sub-command with NO name at all, and that is the
    /// console's own way of naming the configured creature ("disguise favorite" with no
    /// argument): button and console are then one single call, and the two can never end up
    /// marking two different prefabs.
    /// </summary>
    private static void ToggleFavorite() => RunCommand("favorite");

    /// <summary>
    /// Rewrites the line under the title, the labels of the toggle and of the Favorite button,
    /// the sparkle box and the six pose fields, writing NONE of them when it is unchanged:
    /// called once per frame while the window is open (DisguiseGui.Tick), by GuiCreatureList
    /// after a row was clicked or after a change the console made while the window was open,
    /// and by the command buttons after they ran their sub-command. Two of the four values
    /// on the line move on their own - the flight and its height - so the ordinary frame
    /// costs the line being built and compared, never a write to the Text (which would lay it
    /// out again for the same words); the Favorite label follows the configured prefab, which
    /// costs a look at the config and one at the marks (both a string compare and a set
    /// lookup); the pose fields cost six more string comparisons, and skip the one being typed
    /// in (GuiPose); the sparkle box costs one bool - a click on it writes that cache itself,
    /// so only a value from outside (ConfigurationManager) is ever pushed back. Does nothing
    /// without a window, which is every frame it is closed and every frame after a scene
    /// change destroyed it.
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

        Text? favorite = s_favorite;
        if (favorite != null)
        {
            string mark = DescribeFavorite();
            if (!string.Equals(favorite.text, mark, StringComparison.Ordinal))
            {
                favorite.text = mark;
            }
        }

        Toggle? sparkle = s_sparkle;
        if (sparkle != null)
        {
            bool hide = DisguiseConfig.ConfigHideItemSparkle.Value;
            if (hide != s_sparkleApplied)
            {
                s_sparkleApplied = hide;
                sparkle.SetIsOnWithoutNotify(hide);
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
    /// The label of the Favorite button: what pressing it does to the prefab SELECTED in the
    /// window - the configured one, the very name on the state line above it (so the label
    /// follows a change the console makes with the window open too). Rewritten by RefreshState
    /// like that line: the button announces its target before anything is pressed. The name
    /// itself is never printed here (it is one button wide): the answer does that, on the
    /// status line below.
    /// </summary>
    private static string DescribeFavorite()
    {
        string target = DisguiseConfig.ConfigCreature.Value ?? string.Empty;
        return target.Length > 0 && PrefabFavorites.Contains(target)
            ? "Remove favorite"
            : "Add favorite";
    }

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
