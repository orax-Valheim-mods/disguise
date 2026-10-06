#nullable enable
using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The Disguise window: the GuiKey chord, "disguise gui", the single window that exists at
/// a time, and the handover of the mouse that makes it clickable.
///
/// The window is an OVERLAY: the game keeps running while it is on screen, nothing is
/// paused. Only the frames where the pointer sits on the window belong to the UI, which is
/// decided once per frame in BlocksInput and read by Patch_TakeInput and Patch_SetMouseLook
/// - the same rule Valheim applies to its own panels: a click on a button never turns into
/// a swing, and the mouse never turns the camera while it points at a window.
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

    /// <summary>True while the window is on screen (a scene change can destroy it silently).</summary>
    internal static bool IsOpen => s_window != null;

    /// <summary>
    /// True while the pointer is on the window: for that frame the player's input and the
    /// mouse look belong to the UI. Computed once per frame inside Tick's try/catch, so the
    /// two patches only ever read a plain boolean and can never break the game with a throw.
    /// </summary>
    internal static bool BlocksInput { get; private set; }

    // ------------------------------------------------------------------ per frame

    /// <summary>Reads the key chord and measures the pointer: Disguise.Update, once per frame.</summary>
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
            BlocksInput = IsOpen && PointerOnWindow();
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
        s_rect = null;
        s_canvas = null;
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
        s_window = null;
        s_rect = null;
        s_canvas = null;
        BlocksInput = false;
        ReleaseMouse();
    }
}
