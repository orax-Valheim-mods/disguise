#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The right half of the Disguise window: the picture the 3D preview is drawn into (a
/// RawImage over the RenderTexture CreaturePreview films), and which creature it shows -
/// the one the row under the pointer is, or the configured one while the pointer is on no
/// row at all. What the pointer is on comes from GuiRowHover, one small component per row.
///
/// Showing a creature builds an inert copy of it (GhostFactory), which is not free: a
/// pointer sweeping the list would ask for one copy per row crossed. The change is therefore
/// held back HoverDelay seconds - the preview follows a pointer that has stopped, not one
/// that is crossing the list - and the copy is only rebuilt when the name really changed.
/// </summary>
internal static class GuiPreview
{
    /// <summary>How long a hover has to stay before its creature is built.</summary>
    private const float HoverDelay = 0.15f;

    private static RawImage? s_image;

    private static string s_hovered = string.Empty;

    private static string s_pending = string.Empty;

    private static string s_shown = string.Empty;

    private static float s_dueAt;

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
        // Never a hit target: the window measures the pointer on its own rectangle, and a
        // picture over the right half must not eat what is clicked there later. It also starts
        // hidden, because a RawImage with no texture draws a white plate.
        image.raycastTarget = false;
        image.enabled = false;
        image.color = Color.white;
        s_image = image;
    }

    /// <summary>Watches a row: on it, its creature; off it, back to the configured one.</summary>
    internal static void WatchRow(GameObject row, string creature)
    {
        GuiRowHover? hover = row.GetComponent<GuiRowHover>();
        if (hover == null)
        {
            hover = row.AddComponent<GuiRowHover>();
        }
        hover.Creature = creature;
    }

    /// <summary>The pointer stepped on a row (GuiRowHover): show the creature it holds.</summary>
    internal static void Hover(string creature) => s_hovered = creature;

    /// <summary>The pointer stepped off that row: back to the creature configured right now.</summary>
    internal static void Leave() => s_hovered = string.Empty;

    // ------------------------------------------------------------------ per frame

    /// <summary>
    /// Decides what the picture shows: DisguiseGui.Tick, once per frame while the window is
    /// open. The hover is what the pointer asks for, the configured creature the fallback -
    /// and the texture is put on the picture the moment the stage exists (it is built by the
    /// first show, a fifth of a second after the window opens).
    /// </summary>
    internal static void Tick()
    {
        string hovered = s_hovered;
        string wanted =
            hovered.Length > 0 ? hovered : DisguiseConfig.ConfigCreature.Value ?? string.Empty;
        if (!string.Equals(wanted, s_pending, System.StringComparison.Ordinal))
        {
            s_pending = wanted;
            s_dueAt = Time.unscaledTime + HoverDelay;
        }

        if (
            Time.unscaledTime >= s_dueAt
            && !string.Equals(s_pending, s_shown, System.StringComparison.Ordinal)
        )
        {
            s_shown = s_pending;
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

    /// <summary>Forgets the picture and the hover: the window is closing (DisguiseGui).</summary>
    internal static void Forget()
    {
        s_image = null;
        s_hovered = string.Empty;
        s_pending = string.Empty;
        s_shown = string.Empty;
        s_dueAt = 0f;
    }
}
