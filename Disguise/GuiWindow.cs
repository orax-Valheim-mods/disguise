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
/// Step 1 of the window: title, the creature that is configured right now, a hint and the
/// Close button. The creature list, its 3D preview and the command buttons are built here in
/// the next steps of the branch, which is why the size is already the final one.
/// </summary>
internal static class GuiWindow
{
    /// <summary>Final size of the window: it must not jump when the next steps arrive.</summary>
    internal const float Width = 860f;

    internal const float Height = 540f;

    internal static GameObject Build(Transform parent)
    {
        GUIManager gui = GUIManager.Instance;

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

        _ = Label(
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

        _ = Label(
            gui,
            "The creature list, its 3D preview and the command buttons arrive next on this branch.",
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
    /// One line of text, centred: Jotunn builds the object, the alignment is then set here
    /// because its helper leaves the Unity default (upper left), which would push every
    /// centred line into the left half of its box.
    /// </summary>
    private static GameObject Label(
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
