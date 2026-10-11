#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The right half of the Disguise window: the picture the 3D preview is drawn into (a
/// RawImage over the RenderTexture CreaturePreview films), and which creature it shows -
/// the last row the pointer was on, the configured one while no row has been pointed at in
/// this opening. What the pointer has been on comes from GuiRowHover, one small component per
/// row, and it is KEPT: moving off the list (onto the picture, the buttons, the pose rows, out
/// of the window) does not put the configured creature back, so a row just looked at stays on
/// screen to be looked at.
///
/// Showing a creature builds an inert copy of it (GhostFactory), which is not free: a
/// pointer sweeping the list would ask for one copy per row crossed. The change is therefore
/// held back HoverDelay seconds - the preview follows a pointer that has stopped, not one
/// that is crossing the list - and the copy is only rebuilt when the name really changed.
///
/// One more reason to build a copy again: the sparkle option (HideItemSparkle), which the
/// factory reads at build time - the picture then shows the costume as the screen draws it.
/// </summary>
internal static class GuiPreview
{
    /// <summary>How long a hover has to stay before its creature is built.</summary>
    private const float HoverDelay = 0.15f;

    private static RawImage? s_image;

    /// <summary>The last row the pointer was on (empty while no row has been pointed at).</summary>
    private static string s_hovered = string.Empty;

    private static string s_pending = string.Empty;

    private static string s_shown = string.Empty;

    private static float s_dueAt;

    /// <summary>
    /// What the stage was built WITH (the answer of HideSparkle for its creature) and the
    /// option itself at that moment: a value of the option that moves while the picture
    /// stands - typed into ConfigurationManager, written by the window's box - is what makes
    /// the creature be shown again, and only when it can matter (an item). The test runs on
    /// the move of the option, never per frame; the pair is dropped with the window.
    /// </summary>
    private static bool s_shownSparkle;

    private static bool s_shownOption;

    /// <summary>
    /// The level the stage was dressed for (LevelSkin, at its build): a level that moves
    /// while the picture stands makes the creature be shown again, through the same delay
    /// and the same guard as any other change of creature - one int compared per frame, and
    /// a new stage only on the move.
    /// </summary>
    private static int s_shownLevel;

    // ------------------------------------------------------------------ build

    /// <summary>
    /// Adds the picture to the window, at the corner GuiWindow asks for (its upper left
    /// corner, like the list on the other side). Called once per window, from GuiWindow.
    /// </summary>
    internal static void Build(Transform parent, float x, float y)
    {
        Forget();

        GameObject plate = new GameObject(
            "Disguise preview",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage)
        );
        RectTransform rect = plate.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        // Anchors and pivot first, position last - the rule of this window: the corner asked
        // for is the upper left corner of the picture, not its centre (Jotunn never touches
        // the pivot, which Unity leaves in the middle, and the same correction is applied to
        // the filter field and the list).
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CreaturePreview.PictureWidth);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CreaturePreview.PictureHeight);
        rect.anchoredPosition = new Vector2(x, y);

        RawImage image = plate.GetComponent<RawImage>();
        // A hit target for the help line and for nothing else (GuiHelp.Watch below): the
        // picture handles Enter and Exit alone, and an event it does not handle walks up to
        // the panel, which owns the drag - so nothing that was ever clicked on the right
        // half is eaten by it. The window measures the pointer on its own rectangle
        // (DisguiseGui.PointerOnWindow), never on this. It starts hidden, because a
        // RawImage with no texture draws a white plate - and a hidden Graphic is not
        // raycast either, so the picture only answers the pointer once it is drawn.
        image.raycastTarget = true;
        image.enabled = false;
        image.color = Color.white;
        GuiHelp.Watch(
            plate,
            "What the costume looks like now: the last row the pointer was on, else the configured one - it turns while the pointer leaves it alone."
        );
        s_image = image;
    }

    /// <summary>Watches a row: on it, its creature - and that one stays until another row is.</summary>
    internal static void WatchRow(GameObject row, string creature)
    {
        GuiRowHover? hover = row.GetComponent<GuiRowHover>();
        if (hover == null)
        {
            hover = row.AddComponent<GuiRowHover>();
        }
        hover.Creature = creature;
    }

    /// <summary>
    /// The pointer stepped on a row (GuiRowHover): show the creature it holds, and keep it
    /// for as long as no other row comes along - stepping off a row changes nothing, which is
    /// the whole point of the rule (Current).
    /// </summary>
    internal static void Hover(string creature) => s_hovered = creature;

    /// <summary>
    /// The prefab the picture shows: the last row the pointer was on, and the configured
    /// creature while no row has been pointed at in this opening.
    ///
    /// Keeping the last row instead of going back to the configured one when the pointer
    /// leaves the list is what makes the list browsable: the pointer has to cross the window
    /// to reach the buttons, and a rule that reset on every exit would put the configured
    /// creature back between the eye and the row it just left. The memory is dropped with
    /// the window: Build forgets it, once per opening.
    /// </summary>
    internal static string Current()
    {
        return s_hovered.Length > 0
            ? s_hovered
            : DisguiseConfig.ConfigCreature.Value ?? string.Empty;
    }

    // ------------------------------------------------------------------ per frame

    /// <summary>
    /// Decides what the picture shows: DisguiseGui.Tick, once per frame while the window is
    /// open. What it asks for is Current - the last row the pointer was on, the configured
    /// creature before any row was pointed at - and the texture is put on the picture the
    /// moment the stage exists (it is built by the first show, a fifth of a second after the
    /// window opens).
    /// </summary>
    internal static void Tick()
    {
        string wanted = Current();
        if (!string.Equals(wanted, s_pending, System.StringComparison.Ordinal))
        {
            s_pending = wanted;
            s_dueAt = Time.unscaledTime + HoverDelay;
        }

        // The sparkle option moved under a picture that is already standing: the stage was
        // built the old way, so the creature is shown AGAIN - which only costs anything when
        // the difference can show, i.e. for an item (HideSparkle). s_shown is emptied rather
        // than the show called straight away, so the rebuild goes through the same delay and
        // the same guard as any other change of creature; a creature costume changes
        // nothing, and the stage is left exactly where it is.
        bool sparkleOption = DisguiseConfig.ConfigHideItemSparkle.Value;
        if (sparkleOption != s_shownOption)
        {
            s_shownOption = sparkleOption;
            if (
                string.Equals(s_pending, s_shown, System.StringComparison.Ordinal)
                && s_shownSparkle != GhostFactory.HideSparkle(s_pending)
            )
            {
                s_shown = string.Empty;
            }
        }

        // The level moved under a picture that is already standing: the stage was dressed
        // for the old one, so the creature is shown AGAIN - through the same delay and the
        // same guard as any other change of creature (s_shown emptied rather than the show
        // called straight away, exactly like the sparkle above).
        int level = DisguiseConfig.ConfigCreatureLevel.Value;
        if (level != s_shownLevel)
        {
            s_shownLevel = level;
            if (string.Equals(s_pending, s_shown, System.StringComparison.Ordinal))
            {
                s_shown = string.Empty;
            }
        }

        if (
            Time.unscaledTime >= s_dueAt
            && !string.Equals(s_pending, s_shown, System.StringComparison.Ordinal)
        )
        {
            s_shown = s_pending;
            s_shownSparkle = GhostFactory.HideSparkle(s_shown);
            CreaturePreview.Show(s_pending);
        }

        RawImage? image = s_image;
        if (image != null)
        {
            RenderTexture? texture = CreaturePreview.Texture;
            if (texture == null)
            {
                image.enabled = false;
            }
            else if (image.texture != texture)
            {
                image.texture = texture;
                image.enabled = true;
            }
        }
    }

    /// <summary>Forgets the picture and the last row: the window is closing (DisguiseGui).</summary>
    internal static void Forget()
    {
        s_image = null;
        s_hovered = string.Empty;
        s_pending = string.Empty;
        s_shown = string.Empty;
        s_dueAt = 0f;
        s_shownSparkle = false;
        s_shownOption = false;
        s_shownLevel = 0;
    }
}
