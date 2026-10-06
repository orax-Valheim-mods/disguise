#nullable enable
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// Creates what goes INSIDE the Disguise window, with Jotunn's Valheim-styled controls (wood
/// panel, AveriaSerif fonts, Valheim buttons). DisguiseGui owns the lifecycle: this class
/// only builds, it never keeps the window alive and it never talks to the console.
///
/// Step 3 of the window: title, the creature that is configured right now, the creature list
/// with its filter (GuiCreatureList builds that block, this class only tells it where to sit)
/// and, on the right, the 3D preview (GuiPreview builds the picture, CreaturePreview films
/// into it). The command buttons are built here in the last step of the branch, which is why
/// the size is already the final one.
/// </summary>
internal static class GuiWindow
{
    /// <summary>Final size of the window: it must not jump when the last step arrives.</summary>
    internal const float Width = 860f;

    internal const float Height = 540f;

    // Left half of the window: top-left corner of the filter + list block, and its size.
    private const float ListX = 24f;

    private const float ListY = -132f;

    private const float ListWidth = 400f;

    private const float ListHeight = 300f;

    // Right half: upper left corner of the 3D preview, given to GuiPreview as the list
    // block's corner is given to GuiCreatureList: it starts at the height of the filter, is
    // as tall as the list, and the line under it fills the space down to the hint.
    private const float PreviewX = 25f;

    private const float PreviewY = -132f;

    /// <summary>Gap between the bottom of the picture and the line under it.</summary>
    private const float PreviewGap = 6f;

    /// <summary>
    /// The line under the title, kept here so it can be rewritten after a click on a row or a
    /// change made by the console while the window is open. Unity-null once the window is
    /// gone, which is the whole safety of RefreshState.
    /// </summary>
    private static Text? s_state;

    internal static GameObject Build(Transform parent)
    {
        GUIManager gui = GUIManager.Instance;
        s_state = null;

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

        GameObject state = Label(
            gui,
            DescribeState(),
            window.transform,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -92f),
            gui.AveriaSerif,
            20,
            gui.ValheimBeige,
            800f,
            40f
        );
        s_state = state.GetComponent<Text>();

        // The filter field and the scrollable list, built by GuiCreatureList.
        GuiCreatureList.Build(window.transform, ListX, ListY, ListWidth, ListHeight);

        // The 3D preview on the right: the picture here, the stage it shows in CreaturePreview.
        GuiPreview.Build(window.transform, PreviewX, PreviewY);

        _ = Label(
            gui,
            "The command buttons arrive next on this branch.",
            window.transform,
            new Vector2(0.5f, 1f),
            new Vector2(
                PreviewX + (CreaturePreview.PictureWidth * 0.5f),
                PreviewY - CreaturePreview.PictureHeight - PreviewGap - 20f
            ),
            gui.AveriaSerif,
            16,
            gui.ValheimBeige,
            CreaturePreview.PictureWidth,
            40f
        );

        _ = Label(
            gui,
            "Click a row: the creature is set and applied at once - the same as \"disguise set <name>\".",
            window.transform,
            new Vector2(0.5f, 0f),
            new Vector2(0f, 36f),
            gui.AveriaSerif,
            16,
            gui.ValheimBeige,
            800f,
            36f
        );

        return window;
    }

    /// <summary>
    /// Rewrites the line under the title from the config: called by GuiCreatureList after a
    /// row was clicked (or after a change the console made while the window is open). Does
    /// nothing without a window, which is every frame it is closed and every frame after a
    /// scene change destroyed it.
    /// </summary>
    internal static void RefreshState()
    {
        Text? label = s_state;
        if (label != null)
        {
            label.text = DescribeState();
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

    /// <summary>The line under the title: what is configured right now, and whether it is on.</summary>
    private static string DescribeState()
    {
        string creature = PrefabResolver.PrefabName(PrefabResolver.CreaturePrefabHash);
        string state = DisguiseConfig.ConfigEnabled.Value ? "disguise on" : "disguise off";
        return $"Creature: {creature}   -   {state}";
    }
}
