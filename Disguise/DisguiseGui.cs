#nullable enable
using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The Disguise window: the GuiKey chord, "disguise gui", the single window that exists at
/// a time, and the handover of the mouse that makes it clickable. It closes four ways - the
/// chord, the command, the Close button and Escape - the last one taking the press for
/// itself (Patch_MenuShow), or the game would pause behind it.
///
/// The window is an OVERLAY: the game keeps running while it is on screen, nothing is
/// paused. The frames that belong to the UI are the ones where the pointer sits on the
/// window OR where the filter field is being typed in, decided once per frame in
/// BlocksInput and read by Patch_TakeInput and Patch_SetMouseLook - the same rule Valheim
/// applies to its own panels: a click on a button never turns into a swing, the mouse never
/// turns the camera while it points at a window, and typing a filter never walks the
/// character away (this is what SearchFieldFocused does for the build menu).
///
/// While the window is open, Jotunn's BlockInput takes the mouse away from the camera
/// (GameCamera.m_mouseCapture): without it, GameCamera.UpdateMouseCapture would lock the
/// cursor again on the very next frame and nothing could be clicked. It is called balanced
/// (one true per false) and only inside the "main" scene, exactly like Jotunn does.
/// </summary>
internal static class DisguiseGui
{
    private static GameObject? s_window;
    private static RectTransform? s_rect;
    private static Canvas? s_canvas;
    private static bool s_mouseHandedOver;

    /// <summary>
    /// Where the window sat the last frame it existed, where the config said it should sit
    /// the last time that was looked at, and whether the two disagree. The place is read
    /// every frame (Jotunn's drag writes straight into the panel, so looking at it is the
    /// only way to keep it) and written back once, when the window goes - one write costs
    /// the whole file (BepInEx writes the config on every set), so it never happens while
    /// the panel is being dragged.
    /// </summary>
    private static Vector2 s_at;

    private static Vector2 s_config;

    private static bool s_atDirty;

    /// <summary>True while the window is on screen (a scene change can destroy it silently).</summary>
    internal static bool IsOpen => s_window != null;

    /// <summary>
    /// The frame in which Escape closed the window, -1 when it did not. Patch_MenuShow reads
    /// it to keep the pause menu off that same press: both Updates run in the same frame, in
    /// an order nothing pins down, and the patch has to be right either way.
    /// </summary>
    private static int s_escapedFrame = -1;

    /// <summary>True for that frame only: the next Escape opens the pause menu as usual.</summary>
    internal static bool EscapedThisFrame => s_escapedFrame == Time.frameCount;

    /// <summary>
    /// True while the frame belongs to the UI: the pointer is on the window, or the filter
    /// field (GuiCreatureList.FilterFocused) or one of the six pose fields (GuiPose.Focused)
    /// is. Computed once per frame inside Tick's try/catch, so the two patches only ever read
    /// a plain boolean and can never break the game with a throw.
    /// </summary>
    internal static bool BlocksInput { get; private set; }

    // ------------------------------------------------------------------ per frame

    /// <summary>Reads the key chord and Escape, and measures the pointer: Disguise.Update, once per frame.</summary>
    internal static void Tick()
    {
        try
        {
            BlocksInput = false;
            Heal();
            if (IsUnavailable())
            {
                return;
            }
            if (FlightControl.ChordPressed(DisguiseConfig.ConfigGuiKey))
            {
                Toggle();
            }
            if (IsOpen)
            {
                // Where the window sits, read before anything else touches it: the drag
                // moves the panel on its own, and this is the value that goes back into
                // the config when the window closes (SavePosition).
                RememberPosition();

                // The console can change the creature while the window is on screen: the two
                // views must agree (highlight of the row + line under the title). The same
                // for a Show* value typed in ConfigurationManager, which decides with the
                // category boxes what the list is allowed to show.
                GuiCreatureList.Sync();
                GuiCategories.Sync();

                // The line under the title now holds values that move on their own - the
                // flight and the offset it lifts the costume by - so it is brought up to
                // date here too, and not only when something clicked or the console changed
                // the creature. RefreshState writes only what really changed, so the frame
                // where nothing moved costs the line being built and compared, never a write.
                GuiWindow.RefreshState();

                // What the preview shows (the last row the pointer was on, else the
                // configured creature) and the film of it - step 3 of the window.
                GuiPreview.Tick();
                CreaturePreview.Render();

                // Two halves, both read by the patches as one plain boolean: the pointer on
                // the window, or the filter field being typed in (the pointer may well be
                // somewhere else while typing - Valheim's own build menu search does the
                // same). The game keeps running otherwise: this window is an overlay.
                BlocksInput = PointerOnWindow() || GuiCreatureList.FilterFocused || GuiPose.Focused;

                // Escape closes the window exactly like the Close button, last in the block
                // because Hide() destroys what the lines above work on. The press is taken
                // here, or Menu.Update would open the pause menu (and pause the game) on the
                // same key in the same frame - Valheim's own build menu search closes on
                // Escape for the same reason. Patch_MenuShow reads EscapedThisFrame.
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    s_escapedFrame = Time.frameCount;
                    Hide();
                }
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"GUI: {ex}");
        }
    }

    /// <summary>
    /// A window destroyed by a scene change (back to the menu, new world) cannot hand the
    /// mouse back by itself: Jotunn only counts requests, so the next frame does it here.
    /// Without this, one exit to the menu would leave the cursor free for every later world.
    /// </summary>
    private static void Heal()
    {
        if (s_window != null)
        {
            return;
        }
        SavePosition(); // the panel is gone: the place it had was remembered every frame
        s_rect = null;
        s_canvas = null;
        GuiCreatureList.Forget();
        GuiCategories.Forget();
        GuiPreview.Forget();
        GuiPose.Forget();
        GuiStatus.Forget();
        CreaturePreview.Forget();
        ReleaseMouse();
    }

    /// <summary>Headless server: no GUI, no input hooks, and no keys to read either.</summary>
    private static bool IsUnavailable() => GUIManager.IsHeadless();

    private static bool PointerOnWindow()
    {
        RectTransform? rect = s_rect;
        if (rect == null)
        {
            return false;
        }

        // The window sits under Valheim's UI canvas: only a camera-space canvas needs its
        // camera to convert the pointer, an overlay canvas (the normal case) takes null.
        Canvas? canvas = s_canvas;
        Camera? uiCamera =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

        return RectTransformUtility.RectangleContainsScreenPoint(
            rect,
            Input.mousePosition,
            uiCamera
        );
    }

    /// <summary>
    /// Remembers where the window sits - Jotunn's drag moves the panel by itself, so the
    /// only way to keep the place is to look at it, once per frame the window is on screen.
    /// Two numbers, no write: the config is only told when the window closes, and what is
    /// remembered here is also what survives a window destroyed by a scene change, there
    /// being no panel left to read by then.
    /// </summary>
    private static void RememberPosition()
    {
        RectTransform? rect = s_rect;
        if (rect == null)
        {
            return;
        }

        // The config can be written behind the window's back (ConfigurationManager, the .cfg
        // by hand): the panel follows it at once, the way a Show* value lands without
        // reopening, and follows it through the same helper that placed it when it was
        // built - which is also what keeps it on screen (GuiWindow.Place).
        Vector2 place = DisguiseConfig.WindowPlace;
        if (place != s_config)
        {
            s_config = place;
            GuiWindow.Place(rect);
        }

        s_at = rect.anchoredPosition;
        s_atDirty = s_at != s_config; // the panel is somewhere the config does not know
    }

    /// <summary>
    /// Puts the place back into the config - once, and only if it has changed, so closing a
    /// window nobody has moved costs nothing. Called when the window goes, every way it
    /// goes: closed, unloaded, or destroyed behind our back by a scene change (Heal).
    /// </summary>
    private static void SavePosition()
    {
        if (!s_atDirty)
        {
            return;
        }
        s_atDirty = false;
        DisguiseConfig.ConfigWindowX.Value = s_at.x;
        DisguiseConfig.ConfigWindowY.Value = s_at.y;
        s_config = s_at; // the config now says what the panel said: nothing left to write
    }

    // ------------------------------------------------------------------ open / close

    /// <summary>
    /// Opens the window. Returns null on success, else the reason nothing was opened: the
    /// command prints it, the key chord only writes it to the BepInEx log.
    /// </summary>
    internal static string? Show()
    {
        if (IsOpen)
        {
            return null;
        }
        try
        {
            if (IsUnavailable())
            {
                return "there is no GUI on a dedicated server.";
            }
            GameObject? root = GUIManager.CustomGUIFront;
            if (root == null)
            {
                return "the game GUI is not ready yet (main menu).";
            }

            GameObject window = GuiWindow.Build(root.transform);
            s_window = window;
            s_rect = window.GetComponent<RectTransform>();
            s_canvas = window.GetComponentInParent<Canvas>();
            HandMouseOver();
            Disguise.LogInfo("Window: opened.");
            return null;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"GUI: {ex}");
            DestroyWindow();
            return "the window could not be created (see the BepInEx log).";
        }
    }

    /// <summary>Closes the window and gives the mouse back to the camera.</summary>
    internal static void Hide()
    {
        GameObject? window = s_window;
        DestroyWindow();
        if (window != null)
        {
            Disguise.LogInfo("Window: closed.");
            UnityEngine.Object.Destroy(window);
        }
    }

    /// <summary>The chord: opens the window, or closes it when it is already on screen.</summary>
    internal static void Toggle()
    {
        if (IsOpen)
        {
            Hide();
            return;
        }
        string? failure = Show();
        if (failure != null)
        {
            Disguise.LogInfo($"Window: {failure}");
        }
    }

    /// <summary>The plugin is unloading: never keep the mouse captured behind us.</summary>
    internal static void Shutdown()
    {
        try
        {
            DestroyWindow();
        }
        catch (Exception ex)
        {
            Disguise.LogError($"GUI shutdown: {ex}");
        }
    }

    // ------------------------------------------------------------------ mouse handover

    private static void HandMouseOver()
    {
        // Jotunn only acts inside the "main" scene (and never on a headless server); the
        // flag keeps this one true matched with its one false, whatever happened between.
        if (s_mouseHandedOver || !InGameScene())
        {
            return;
        }
        GUIManager.BlockInput(true);
        s_mouseHandedOver = true;
    }

    private static void ReleaseMouse()
    {
        if (!s_mouseHandedOver)
        {
            return;
        }
        s_mouseHandedOver = false;
        GUIManager.BlockInput(false);
    }

    private static bool InGameScene() => SceneManager.GetActiveScene().name == "main";

    /// <summary>Forgets everything about the current window object (scene change, failure).</summary>
    private static void DestroyWindow()
    {
        SavePosition();
        s_window = null;
        s_rect = null;
        s_canvas = null;
        BlocksInput = false;
        GuiCreatureList.Forget();
        GuiCategories.Forget();
        GuiPreview.Forget();
        GuiPose.Forget();
        GuiStatus.Forget();
        CreaturePreview.Forget();
        ReleaseMouse();
    }
}
