#nullable enable
using System;
using System.Collections.Generic;
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
/// films into it), under it the five command buttons and, under those, the band the level row
/// holds at the picture's own left edge - the sparkle box (HideItemSparkle) to its right -
/// and the two option rows standing under the picture's own left edge, the armor box
/// (ShowArmor) over the weapons row (ShowWeapons as a box like it, with the WhoseWeapons
/// list at its right), the
/// three of them gating nothing in the row above: it is the costume itself that is drawn
/// differently - then the status line
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
    /// status line keeps the band it has always had under them (-608..-652 since the third
    /// option row arrived); and the help line (GuiHelp) closes the window below that band.
    /// 630 is what left those two rows their band above the status line: 540 left 50 px
    /// under the list, which fit neither of them, and 600 put the Offset row back over the
    /// last rows of the list. The help line then asked for 40 px at the BOTTOM, and the
    /// panel grew downwards for them - so nothing above moved a pixel, the status line alone
    /// following its own edge (it is anchored to the bottom one, which is why
    /// GuiStatus.StatusY grew by the same 40). And 706 is that very story one more time: the
    /// band under the command buttons grew a THIRD option row (WeaponsY, under the armor
    /// box), which the 94 px left to the two-row band could not hold - the panel grew by the
    /// 36 px of that row, again downwards, so every top-anchored corner above stands where
    /// it stood; GuiStatus.StatusY alone grew by those 36 too (bottom-anchored like the help
    /// line, it would otherwise have travelled down with the edge and stood IN the new row),
    /// while GuiHelp.HelpY keeps closing the window against the very edge it grew from.
    /// </summary>
    internal const float Height = 706f;

    // The band at the very top: the title, Close and the chord all sit on it, 34 px under
    // the top edge - one Y for the three, so the row stays a row when one of them moves.
    private const float TitleRowY = -34f;

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

    private const float ButtonGap = 6f;

    private const float ButtonTop = PreviewY - CreaturePreview.PictureHeight - PreviewGap;

    /// <summary>Centre of the row, given to CreateButton as a position of the window.</summary>
    private const float ButtonY = ButtonTop - (GuiMetrics.RowHeight * 0.5f);

    /// <summary>
    /// Centre of the sparkle box (HideItemSparkle): the right half of the window used to
    /// have 94 px between the bottom of the command row (-478) and the band the status line
    /// took (-572), split into two rows - this one 14 px under the buttons, the box under it
    /// 20 px above that line, both starting at the picture's own left edge's right, after
    /// the level row. The panel is 36 px taller since the band holds three rows (Height), so
    /// the band ends at -608 now. The same control as the checkbox row (GuiCategories builds
    /// it), standing alone here with its own entry to write and no list to re-filter.
    /// </summary>
    private const float SparkleY = -504f;

    /// <summary>
    /// Centre of the armor box (ShowArmor), on the second row of the band: 36 px under the
    /// sparkle box, and at the picture's OWN left edge (PreviewX) rather than to the right
    /// of the level row - that row only ever holds the first row of the band, so the two
    /// rows under it start where the picture starts.
    /// </summary>
    private const float ArmorY = -540f;

    /// <summary>
    /// Centre of the weapons row (ShowWeapons): its box with the label, then the word that
    /// names the list and the list itself - which says whose weapons the costume is held
    /// with - at the right of them: the third and last row of the
    /// band, 36 px under the armor box and 12 px over the edge of the status line's own
    /// band. The list opens DOWNWARDS from there, over that line and the help line under it,
    /// which is why the row is built last of everything (Build).
    /// </summary>
    private const float WeaponsY = -576f;

    /// <summary>
    /// The list of the weapons row: one row tall CLOSED, which is the height Jotunn gives
    /// the field (CreateDropDown) and the height its own rows stand at when they are open.
    /// OPEN it is as tall as the entries the configured creature really gives it - one row
    /// each, up to WeaponsListMaxRows of them - and the template is cut to that very height
    /// (FillWhose -> ShapeTemplate), which IS the height Unity opens the list at:
    /// Dropdown.Show sizes the open list from the template and never grows past it, so a
    /// creature whose pool holds several weapons shows every one of them, and one that holds
    /// a great many shows the first cap of them and scrolls the rest (what Jotunn's
    /// template is built with is a ScrollRect over a Masked viewport, so the overflow is
    /// what lands in it). The cap is also what keeps that list on the screen: it opens
    /// downwards from the last row of the window, six of its rows already reaching past the
    /// edge of the panel - and Unity flips a list whose corners leave the canvas over its
    /// field instead, so nothing of it is ever lost either way.
    ///
    /// The width is 50 more than the word is given: Jotunn keeps that much of a list for
    /// its arrow and for the inset of its label ("width - 50", CreateDropDown), so the
    /// caption and every row really have WeaponsListWidth - 50 to write in - enough for
    /// "Creature", "Player" and the names of the weapons between them to stand on one line,
    /// and ShapeTemplate tells every one of them not to wrap anyway, so a word that ever
    /// outgrows that draws past it instead of breaking in two.
    /// </summary>
    private const float WeaponsListWidth = 150f;

    private const float WeaponsListHeight = 30f;

    private const int WeaponsListMaxRows = 6;

    /// <summary>
    /// The word in front of the weapons list ("Whose"): it is MEASURED once the window is
    /// built, exactly the
    /// way GuiCategories measures a checkbox label (Text.preferredWidth), so the list
    /// stands at the word's own right whatever the font answers - the first number is only
    /// the box the word is created in before that measure arrives, and the padding is what
    /// keeps a rounded pixel from wrapping its last letter.
    /// </summary>
    private const float WeaponsWordStart = 40f;

    private const float WeaponsWordPadding = 4f;

    /// <summary>The word of that row to its list, the checkbox box's own inner gap (GuiCategories).</summary>
    private const float WeaponsItemGap = 8f;

    /// <summary>
    /// The band's split, in pixels of the picture above it: the level row stands at the
    /// picture's own left edge and takes LevelRowWidth of the band (LevelRow), then
    /// LevelBandGap of air, and the sparkle box takes what is left of it (SparkleBox) -
    /// one shared number, because the row is built in the picture's coordinates and the
    /// box in the window's, and neither may ask where the other one is.
    /// </summary>
    private const float LevelWordWidth = 60f;

    private const float LevelNumberWidth = 44f;

    private const float LevelItemGap = 4f;

    private const float LevelBandGap = 8f;

    private const float LevelRowWidth =
        LevelWordWidth
        + LevelItemGap
        + GuiMetrics.RowHeight
        + LevelItemGap
        + LevelNumberWidth
        + LevelItemGap
        + GuiMetrics.RowHeight;

    /// <summary>
    /// The label of the Enable/Disable command button - the second of the row, after the
    /// Favorite one - kept here because it is a toggle: RefreshState
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
    /// The level row's number and its two buttons, at the picture's own left edge of the
    /// band shared with the sparkle box: the number is rewritten per frame like the state
    /// line (RefreshState), and the buttons stop being clickable at the ends of the span
    /// the game itself keeps a creature inside
    /// (1 to 9) instead of running a command that could not move the level. Unity-null once
    /// the window is gone, the same safety as everything above.
    /// </summary>
    private static Text? s_level;

    private static Button? s_levelDown;

    private static Button? s_levelUp;

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

    /// <summary>
    /// The armor box on the second row of the band, and what it showed last: the same pair as
    /// the sparkle box above, for the same reason - the entry can also be written by
    /// ConfigurationManager while the window is open, and RefreshState pushes that back into
    /// the box. Unity-null once the window is gone, which is the whole safety of it.
    /// </summary>
    private static Toggle? s_armor;

    private static bool s_armorApplied;

    /// <summary>
    /// The weapons row of the band, and what it showed last: its box - the same pair as the
    /// two boxes above, for the same reason (a value from outside, ConfigurationManager or
    /// the console, is pushed back into it by RefreshState, while a click writes the cache
    /// itself) - and the list beside it, whose index is the second value of that row. The
    /// list only says anything while the box is ticked, so it is greyed out with it
    /// (SyncWhoseList). Unity-null once the window is gone, which is the whole safety of
    /// both.
    /// </summary>
    private static Toggle? s_weapons;

    private static bool s_weaponsApplied;

    private static Dropdown? s_whose;

    private static int s_whoseApplied;

    /// <summary>
    /// The entries of that list as they stand - Creature, then every weapon the configured
    /// creature can hold, then Player - and the creature they were read from. The pool moves
    /// with the configured prefab, so RefreshState fills the list again whenever a row of
    /// the window or a console command puts another creature there; and because WhoseIndex
    /// and WhoseAt are the only two readers of it, the row the pointer lands on, the row the
    /// config is on and the word the console receives can never spell an option two ways.
    /// </summary>
    private static List<string> s_whoseOptions = new();

    private static string s_whoseCreature = string.Empty;

    internal static GameObject Build(Transform parent)
    {
        GUIManager gui = GUIManager.Instance;
        s_state = null;
        s_power = null;
        s_favorite = null;
        s_sparkle = null;
        s_armor = null;
        s_weapons = null;
        s_whose = null;
        s_whoseOptions = new();
        s_whoseCreature = string.Empty;

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
            new Vector2(-70f, TitleRowY),
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
            new Vector2(-72f, TitleRowY),
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
            new Vector2(-240f, TitleRowY),
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
            GuiMetrics.LineWidth,
            GuiMetrics.RowHeight
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

        // The five command buttons under the picture, the three option rows under them, the
        // line they answer on (which still holds the hint about clicking a row while no
        // button has been pressed), and the help line closing the window under that one.
        CommandButtons(gui, window.transform);
        SparkleBox(window.transform);
        ArmorBox(window.transform);
        LevelRow(gui, window.transform);
        GuiStatus.Build(window.transform);
        GuiHelp.Build(window.transform);

        // The weapons row comes LAST of everything, after the two lines it stands over: its
        // list opens downwards from here, across the status line and the help line under it
        // (WeaponsRow).
        WeaponsRow(gui, window.transform);

        return window;
    }

    /// <summary>
    /// The box under the command row: HideItemSparkle, the option that takes the shimmer of
    /// a dropped item off an Item costume (GhostFactory reads it at every build of a clone).
    /// The control itself comes from GuiCategories.BuildBox - the very one the checkbox row
    /// is made of - and the listener here writes the entry and the cache, and nothing else:
    /// there is no list to re-filter, and the clones standing rebuild themselves when the
    /// value moves (SelfView, GuiPreview). Standing to the RIGHT of the level row, which
    /// holds the picture's own left edge (LevelRow): the two controls share the band the
    /// buttons above end on, and both stay inside the picture's own two edges.
    /// </summary>
    private static void SparkleBox(Transform parent)
    {
        s_sparkleApplied = DisguiseConfig.ConfigHideItemSparkle.Value;

        // The preview anchors on the MIDDLE of the window and starts PreviewX right of it
        // (GuiPreview), while the box anchors on its top-left corner (BuildBox): the two
        // corners are the same x once this much is added to it - and the level row, built
        // in the preview's own coordinates, has already taken LevelRowWidth of the band.
        float cursor = (Width * 0.5f) + PreviewX + LevelRowWidth + LevelBandGap;
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
    /// The box on the second row of the band: ShowArmor, the option that takes the armor a
    /// humanoid costume is born with off that costume (GearSkin reads it at every build of a
    /// copy, CostumeGear at every packet leaving this machine). Same control and same rules
    /// as the sparkle box above it - GuiCategories.BuildBox again - and the same life: a copy
    /// already standing rebuilds itself when the value moves (SelfView, GuiPreview), and a
    /// value written from outside while the window is open is pushed back into the box
    /// (RefreshState). It stands at the picture's OWN left edge rather than to the right of
    /// the level row: that row only ever holds the first row of the band (LevelRow), so both
    /// rows under it start where the picture starts. The console spells the same switch
    /// "disguise armor [on|off]", said the way a player says it: ON is the armor drawn.
    /// </summary>
    private static void ArmorBox(Transform parent)
    {
        s_armorApplied = DisguiseConfig.ConfigShowArmor.Value;

        float cursor = (Width * 0.5f) + PreviewX;
        s_armor = GuiCategories.BuildBox(
            parent,
            ref cursor,
            ArmorY,
            "Show armor",
            "Draw the costume with the ARMOR a real creature of that prefab is born with - the gear the game gives a creature of it at spawn: its default items and one pick of each random pool. On, the default, is how a Fuling in the world stands - helmet and all; off draws the costume without helmet, chest, legs or cape (whose WEAPON it holds is the row under this one). It reaches every screen drawing that costume: yours here, the picture above, and the machines running no DLL of this mod, whose packets your ZDO carries that gear in - rolled the way the game rolls it, so every machine agrees on the same pick. The machines running this DLL keep drawing YOU with your own equipment, whatever this says. Console: disguise armor [on|off].",
            s_armorApplied,
            on =>
            {
                s_armorApplied = on;
                DisguiseConfig.ConfigShowArmor.Value = on;
            }
        );
    }

    /// <summary>What the box of the weapons row describes (GuiHelp.Watch).</summary>
    private const string WeaponsBoxHelp =
        "Draw the costume with weapons at all: ticked, its hands hold what the list to the right picks (WhoseWeapons); unticked, both hands are empty - and YOUR held items stay hidden from the machines running no DLL of this mod then as well, there being nothing to glue them to. It reaches every screen drawing that costume: yours here, the picture above, and the machines running no DLL, whose packets your ZDO carries that gear in - rolled the way the game rolls it, so every machine agrees on the same pick. The machines running this DLL keep drawing YOU with your own equipment, whatever this says. Console: disguise weapons [on|off].";

    /// <summary>What the word and the list of the weapons row both describe (GuiHelp.Watch).</summary>
    private const string WhoseHelp =
        "Whose weapons the costume is held with while the box to the left is ticked: \"Creature\", the default, is the one a real creature of that prefab takes into a hand at spawn - MonsterAI takes the best weapon of its inventory, so a Fuling stands with its spear; the names between the two are every weapon that very creature can hold, and picking one FIXES the costume's hands on it (the other hand empty) whatever the roll answers; \"Player\" is the ones YOU hold in both hands, an empty hand drawing nothing. It reaches every screen drawing that costume, and it decides what YOUR hands carry there too: hidden from the machines running no DLL of this mod while the costume wears its own, exactly what it shows while it wears yours. Greyed out while that box is unticked - a costume drawn with empty hands gives nobody's weapons to name - and put back on \"Creature\" whenever another creature is configured, one that holds no such weapon. Console: disguise weapons <creature|player|weapon>.";

    /// <summary>
    /// The third and last row of the band, at the picture's own left edge like the armor box
    /// over it: the BOX (ShowWeapons), then the word naming the list and the Jotunn
    /// Valheim-styled list of every answer WhoseWeapons can take - its two words, with every
    /// weapon the configured creature can hold between them - a LIST beside a box because
    /// the two are one pair of options (the switch decides whether there are any weapons,
    /// the list which ones while there are), which is also why the console takes both a
    /// switch and an argument (disguise weapons [on|off|creature|player|weapon]).
    ///
    /// The box is the very control the armor box and the sparkle box are built with
    /// (GuiCategories.BuildBox), so the three of them are one control three times - and
    /// like them it writes its entry itself, while a click on the LIST runs its sub-command
    /// (RunCommand) so the answer lands on the status line and the console and the window
    /// stay one single call. RefreshState then pushes both values back - into the box and
    /// into the list - which is how either written by the console or by ConfigurationManager
    /// while the window is open lands without closing it: a bool and an int, the same life
    /// as the two boxes above. The entries themselves are not constant, though: they come
    /// out of the configured creature's own pool (FillWhose), so RefreshState fills the list
    /// again the moment a row of this window or a console command puts another creature
    /// there - and that same fill is what tells the open list how tall it may open.
    ///
    /// The row is built LAST of everything (Build), after the status and the help lines it
    /// stands over, because the list opens DOWNWARDS across those two lines - and Unity gives
    /// the open list a canvas of its own (Dropdown.SetupTemplate adds one at sorting order
    /// 30000), so nothing else of the window can draw over it once it is open either.
    ///
    /// Two things of Jotunn's template are corrected here, both in what Unity built before
    /// the options arrived (ShapeTemplate): the height of the open list, cut to the entries
    /// the row really holds, and the row's background Image, which ApplyDropdownStyle
    /// switches off - a Graphic that is off is not raycast, so without it the pointer would
    /// land on the word alone (or on nothing at all) and a row could not be clicked.
    /// </summary>
    private static void WeaponsRow(GUIManager gui, Transform parent)
    {
        const int fontSize = 16;
        float cursor = (Width * 0.5f) + PreviewX;

        // The switch first, at the picture's own left edge like the armor box over it: box
        // and label are ONE checkbox (GuiCategories.BuildBox), and the list below only names
        // whose weapons a costume drawn WITH weapons is held with - which is why a click on
        // it also greys it back in or out (SyncWhoseList).
        s_weaponsApplied = DisguiseConfig.ConfigShowWeapons.Value;
        s_weapons = GuiCategories.BuildBox(
            parent,
            ref cursor,
            WeaponsY,
            "Show weapons",
            WeaponsBoxHelp,
            s_weaponsApplied,
            on =>
            {
                s_weaponsApplied = on;
                DisguiseConfig.ConfigShowWeapons.Value = on;
                SyncWhoseList();
            }
        );

        GameObject word = Label(
            gui,
            "Whose",
            parent,
            new Vector2(0f, 1f),
            new Vector2(cursor, WeaponsY),
            gui.AveriaSerif,
            fontSize,
            gui.ValheimBeige,
            WeaponsWordStart,
            WeaponsListHeight
        );
        GuiHelp.Watch(word, WhoseHelp);

        float wordWidth = WeaponsWordStart;
        Text? text = word.GetComponent<Text>();
        if (text != null)
        {
            // Measured, so the air before the list is the same whatever the word is - the
            // rule of every label of this window that has something standing at its right
            // (GuiCategories measures its checkbox labels the very same way).
            wordWidth = Mathf.Max(text.preferredWidth, 8f) + WeaponsWordPadding;
            RectTransform wordRect = word.GetComponent<RectTransform>();
            if (wordRect != null)
            {
                wordRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, wordWidth);
                wordRect.anchoredPosition = new Vector2(cursor + (wordWidth * 0.5f), WeaponsY);
            }
        }
        cursor += wordWidth + WeaponsItemGap;

        // Jotunn never sets the pivot (it stays in the middle), so the position IS the centre
        // of the list - the same coordinates the word above was just given, only anchored on
        // the top-left corner of the window like every other box of this band (BuildBox).
        GameObject body = gui.CreateDropDown(
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(cursor + (WeaponsListWidth * 0.5f), WeaponsY),
            fontSize,
            WeaponsListWidth,
            WeaponsListHeight
        );
        GuiHelp.Watch(body, WhoseHelp);

        Dropdown? list = body.GetComponent<Dropdown>();
        if (list != null)
        {
            // A click on a row runs its sub-command (RunCommand), so the word the console
            // receives is the very entry the pointer landed on - both of them read out of
            // the entries FillWhose writes below, which is also what puts the one the config
            // is on in the field, without notifying (or filling it would run the click it is
            // only standing in for).
            list.onValueChanged.AddListener(index => RunCommand($"weapons {WhoseAt(index)}"));
            s_whose = list;
            FillWhose(list);
        }
        SyncWhoseList();

        // Last of everything (see above): the open list draws over the lines under this row.
        body.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Greys the list of the weapons row with the switch beside it, or brings it back: the
    /// list only says something while the costume is drawn with weapons at all, and leaving
    /// it clickable there would answer a question the costume cannot show (WhoseWeapons is
    /// read only while ShowWeapons is on - DisguiseConfig). Called when the box is built,
    /// when it is clicked and when RefreshState pushes an outside value into it; without a
    /// window there is no list to grey, which is the whole safety of it.
    /// </summary>
    private static void SyncWhoseList()
    {
        Dropdown? list = s_whose;
        if (list != null)
        {
            list.interactable = DisguiseConfig.ConfigShowWeapons.Value;
        }
    }

    /// <summary>
    /// The entries of the weapons row as the configured creature gives them: Creature, then
    /// one name per weapon that very creature can hold (GearSkin.Weapons - the same pool the
    /// console prints and the config is read against), then Player. The two words stand at
    /// both ends whatever happens, so a costume that can hold nothing at all still answers a
    /// list of two - and every name in between is one the pool really holds, never an item
    /// of some other creature.
    /// </summary>
    private static List<string> WhoseOptions()
    {
        List<string> options = new() { DisguiseConfig.WeaponsCreature };
        foreach (string weapon in GearSkin.Weapons(PrefabResolver.CreaturePrefab()))
        {
            options.Add(weapon);
        }
        options.Add(DisguiseConfig.WeaponsPlayer);
        return options;
    }

    /// <summary>
    /// Fills the weapons list with the entries of the configured creature, cuts the open
    /// list to them (ShapeTemplate, BEFORE the entries go in - Unity clones the row it finds
    /// in the template the moment it opens, so the height has to stand first) and puts the
    /// field back on the answer the config is on, without notifying. Called once when the
    /// row is built, and once per creature the window is told about while it stands
    /// (RefreshState) - the pool is the only thing here that can ever move.
    /// </summary>
    private static void FillWhose(Dropdown list)
    {
        // A name the configured creature cannot hold is put back on the roll first, so the
        // entries below and the answer the config gives are one and the same thing (the
        // creature's own handler and the console do this too).
        DisguiseConfig.ReconcileWhoseWeapons();
        s_whoseCreature = DisguiseConfig.ConfigCreature.Value ?? string.Empty;
        s_whoseOptions = WhoseOptions();
        ShapeTemplate(list, s_whoseOptions.Count);
        list.ClearOptions();
        list.AddOptions(s_whoseOptions);
        s_whoseApplied = WhoseIndex();
        list.SetValueWithoutNotify(s_whoseApplied);
        list.RefreshShownValue();
    }

    /// <summary>
    /// The two corrections this row makes to Jotunn's dropdown TEMPLATE (WeaponsRow), both in
    /// what Unity built before the options arrived: the height - the template cut from the
    /// five rows Unity builds it with to the rows the list really holds (capped at
    /// WeaponsListMaxRows), with the row and its label at WeaponsListHeight so a 16 px word
    /// stands inside its line, and the content sized to match (Dropdown.Show sizes the open
    /// list from what it finds here, so the three of them have to add up or a row is cut off
    /// at one end - and it only ever SHRINKS that height, which is why the cap is what the
    /// entries beyond it scroll inside).
    ///
    /// And the row's background, switched back ON and kept invisible: ApplyDropdownStyle
    /// turns it off - it was the highlight the list no longer draws - and a Graphic that is
    /// off is not raycast, so the pointer would find no row to click. Invisible, it is a hit
    /// target and nothing else: Jotunn gave that Toggle no transition, so nothing ever tints
    /// it back. The word of the field and the word of every row are told not to wrap as well,
    /// since Jotunn gives them width - 50 to stand in (CreateDropDown).
    /// </summary>
    private static void ShapeTemplate(Dropdown list, int rows)
    {
        RectTransform? template = list.template;
        if (template == null)
        {
            return;
        }

        // The template hangs from the BOTTOM edge of the field (Unity), so its height alone
        // says how far the open list reaches under the status and help lines of this window -
        // and the height Unity opens it at, Dropdown.Show never growing it past this.
        template.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            WeaponsListHeight * Mathf.Clamp(rows, 1, WeaponsListMaxRows)
        );

        Transform? row = template.Find("Viewport/Content/Item");
        if (row != null)
        {
            RectTransform? body = row as RectTransform;
            if (body != null)
            {
                // Unity builds the row 20 px tall, which no 16 px word fits inside.
                body.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, WeaponsListHeight);
            }
        }

        Transform? content = template.Find("Viewport/Content");
        RectTransform? box = content as RectTransform;
        if (box != null)
        {
            // One row tall here only: Dropdown.Show writes the real height (one per entry)
            // into this very rect the moment the list opens, and it is that height the open
            // list is then cut to - the two must add up with the row above, which is what
            // makes the last entry stand whole instead of half past the bottom edge.
            box.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, WeaponsListHeight);
        }

        if (list.itemText != null)
        {
            // Sized to the field while the row was still Unity's 20 px (CreateDropDown), so
            // it is sized again now that the row is this tall: the word then stands inside
            // its line instead of overhanging it into the row above.
            list.itemText.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                WeaponsListHeight
            );
            list.itemText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        if (list.captionText != null)
        {
            list.captionText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        Toggle? toggle = row != null ? row.GetComponent<Toggle>() : null;
        if (toggle != null && toggle.targetGraphic is Image background)
        {
            background.enabled = true;
            background.color = Color.clear;
        }
    }

    /// <summary>
    /// Which row of the weapons list the config is on right now: the one holding its very
    /// word, compared the way the entries themselves are read (no case, so a value typed
    /// into the .cfg by hand still lights the row it was meant to) - and the first one while
    /// the entry holds something the configured creature cannot hold, which is the answer
    /// the roll gives then as well (LocalWeaponHash draws nothing else).
    /// </summary>
    private static int WhoseIndex()
    {
        string value = DisguiseConfig.ConfigWhoseWeapons.Value;
        for (int i = 0; i < s_whoseOptions.Count; i++)
        {
            if (string.Equals(s_whoseOptions[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return 0;
    }

    /// <summary>The word the console spells the option of that row with (WhoseAt, the click).</summary>
    private static string WhoseAt(int index) =>
        index >= 0 && index < s_whoseOptions.Count
            ? s_whoseOptions[index]
            : DisguiseConfig.WeaponsCreature;

    /// <summary>What the word and the number of the level row both describe (GuiHelp.Watch).</summary>
    private const string LevelHelp =
        "The level your costume is drawn at - the star levels of the creatures. It is written into your ZDO for every machine to read (and kept in the configuration, CreatureLevel).";

    /// <summary>
    /// The level row: the star levels, at the picture's own left edge of the band the
    /// sparkle box shares - the word, then - and + around the number, the box to their
    /// right. Each button runs "disguise level <1-9>" the way every button of this window
    /// runs its sub-command (Command), so the answer lands on the status line below and
    /// the console and the window stay one single call. The number is rewritten per frame
    /// like the state line (RefreshState), so a level typed into the console or into
    /// ConfigurationManager lands without closing the window; and a level at the ends of
    /// the span the game itself keeps a creature inside stops its button there, rather
    /// than running a command that could not move it.
    /// </summary>
    private static void LevelRow(GUIManager gui, Transform parent)
    {
        const int fontSize = 16;
        float height = GuiMetrics.RowHeight;

        // Everything is measured from the picture's own left edge, rightwards: this row
        // holds that edge of the band, and SparkleBox starts where LevelRowWidth says the
        // row ends - one shared number, because the two builds use different coordinates.
        float cursor = PreviewX;
        float wordX = cursor + (LevelWordWidth * 0.5f);
        cursor += LevelWordWidth + LevelItemGap;
        float minusX = cursor + (height * 0.5f);
        cursor += height + LevelItemGap;
        float numberX = cursor + (LevelNumberWidth * 0.5f);
        cursor += LevelNumberWidth + LevelItemGap;
        float plusX = cursor + (height * 0.5f);

        s_levelUp = ButtonOf(
            Command(
                gui,
                parent,
                "+",
                "One level more (disguise level <1-9>): 2 and 3 are the starred creatures - another skin where the prefab has one, a star, and the health of a real creature of that level. It stops at 9, where the vanilla spawn command stops.",
                plusX,
                height,
                SparkleY,
                () => RunCommand($"level {DisguiseConfig.ConfigCreatureLevel.Value + 1}")
            )
        );

        GameObject number = Label(
            gui,
            DescribeLevel(),
            parent,
            new Vector2(0.5f, 1f),
            new Vector2(numberX, SparkleY),
            gui.AveriaSerif,
            fontSize,
            gui.ValheimBeige,
            LevelNumberWidth,
            height
        );
        s_level = number.GetComponent<Text>();
        GuiHelp.Watch(number, LevelHelp);

        s_levelDown = ButtonOf(
            Command(
                gui,
                parent,
                "-",
                "One level less (disguise level <n>): level 1 is the plain creature, with no skin of its own above the base look.",
                minusX,
                height,
                SparkleY,
                () => RunCommand($"level {DisguiseConfig.ConfigCreatureLevel.Value - 1}")
            )
        );

        GameObject word = Label(
            gui,
            "Level",
            parent,
            new Vector2(0.5f, 1f),
            new Vector2(wordX, SparkleY),
            gui.AveriaSerif,
            fontSize,
            gui.ValheimBeige,
            LevelWordWidth,
            height
        );
        GuiHelp.Watch(word, LevelHelp);
    }

    /// <summary>The Button a command's label belongs to: the label is a child of its body.</summary>
    private static Button? ButtonOf(Text? label)
    {
        return label != null ? label.GetComponentInParent<Button>() : null;
    }

    /// <summary>The number on the level row: the level the costume is drawn at.</summary>
    private static string DescribeLevel() => DisguiseConfig.ConfigCreatureLevel.Value.ToString();

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
    /// status line below. The first follows the configured prefab and its mark
    /// (DescribeFavorite); the second is a toggle, so what it says and what it runs both
    /// follow the config - and RefreshState rewrites both every frame like the state line
    /// above.
    /// </summary>
    private static void CommandButtons(GUIManager gui, Transform parent)
    {
        float width =
            (CreaturePreview.PictureWidth - ((CommandCount - 1) * ButtonGap)) / CommandCount;
        float centre = PreviewX + (width * 0.5f);

        s_favorite = Command(
            gui,
            parent,
            DescribeFavorite(),
            "Mark the prefab named on the line above, or take the mark off - favorites are what the Favorites box shows (disguise favorite).",
            centre,
            width,
            ButtonY,
            ToggleFavorite
        );

        centre += width + ButtonGap;
        s_power = Command(
            gui,
            parent,
            DescribePower(),
            "Turn the disguise on for everyone - your ZDO then carries the configured prefab instead of your viking - or stop it (disguise enable / disable).",
            centre,
            width,
            ButtonY,
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
            ButtonY,
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
            ButtonY,
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
            ButtonY,
            () => RunCommand("fly")
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
        float centreY,
        UnityAction onClick
    )
    {
        GameObject body = gui.CreateButton(
            label,
            parent,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(centreX, centreY),
            width,
            GuiMetrics.RowHeight
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
    /// the sparkle box, the armor box, the weapons box and its list and the six pose fields,
    /// writing NONE
    /// of them when it is unchanged: called once per frame while the window is open
    /// (DisguiseGui.Tick), by GuiCreatureList after a row was clicked or after a change the
    /// console made while the window was open, and by the command buttons after they ran
    /// their sub-command. Two of the four values on the line move on their own - the flight
    /// and its height - so the ordinary frame costs the line being built and compared, never
    /// a write to the Text (which would lay it out again for the same words); the Favorite
    /// label follows the configured prefab, which costs a look at the config and one at the
    /// marks (both a string compare and a set lookup); the pose fields cost six more string
    /// comparisons, and skip the one being typed in (GuiPose); the four option controls cost
    /// one bool each and one int - a click on one writes that cache itself, so only
    /// a value from outside (ConfigurationManager, the console) is ever pushed back - and the
    /// weapons list one prefab-hash compare more, filling itself again only when the creature
    /// behind it has really moved (FillWhose).
    /// Does nothing without a window, which is every frame it is closed and every frame after
    /// a scene change destroyed it.
    /// </summary>
    internal static void RefreshState()
    {
        GuiText.Set(s_state, DescribeState);
        GuiText.Set(s_power, DescribePower);
        GuiText.Set(s_favorite, DescribeFavorite);
        GuiText.Set(s_level, DescribeLevel);
        SyncLevelButtons();

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

        Toggle? armor = s_armor;
        if (armor != null)
        {
            bool showArmor = DisguiseConfig.ConfigShowArmor.Value;
            if (showArmor != s_armorApplied)
            {
                s_armorApplied = showArmor;
                armor.SetIsOnWithoutNotify(showArmor);
            }
        }

        Toggle? weapons = s_weapons;
        if (weapons != null)
        {
            bool showWeapons = DisguiseConfig.ConfigShowWeapons.Value;
            if (showWeapons != s_weaponsApplied)
            {
                s_weaponsApplied = showWeapons;
                weapons.SetIsOnWithoutNotify(showWeapons);
                SyncWhoseList(); // the list is greyed out with the switch
            }
        }

        Dropdown? whose = s_whose;
        if (whose != null)
        {
            if (
                !string.Equals(
                    DisguiseConfig.ConfigCreature.Value,
                    s_whoseCreature,
                    StringComparison.Ordinal
                )
            )
            {
                // Another creature, therefore another pool of weapons to pick from: the
                // entries are read again and the field is put back on the answer the config
                // gives over them (FillWhose), which a name the new pool does not hold sends
                // straight to the first one - the roll, exactly what is drawn then as well.
                FillWhose(whose);
            }
            int shown = WhoseIndex();
            if (shown != s_whoseApplied)
            {
                s_whoseApplied = shown;
                whose.SetValueWithoutNotify(shown);
                whose.RefreshShownValue();
            }
        }

        GuiPose.Sync();
    }

    /// <summary>
    /// The - and + of the level row stop being clickable at the ends of the span the game
    /// itself keeps a creature inside: a button that could not move the level would only
    /// answer "set to the same value", and the greyed one says it before the click.
    /// </summary>
    private static void SyncLevelButtons()
    {
        Button? down = s_levelDown;
        Button? up = s_levelUp;
        if (down == null && up == null)
        {
            return; // no window: every frame it is closed or destroyed
        }
        int level = DisguiseConfig.ConfigCreatureLevel.Value;
        if (down != null)
        {
            down.interactable = level > DisguiseConfig.LevelMin;
        }
        if (up != null)
        {
            up.interactable = level < DisguiseConfig.LevelMax;
        }
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
