#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;

namespace Disguise;

/// <summary>
/// Tells GuiPreview what the row under the pointer holds, and NOTHING else: it listens to the
/// pointer entering and leaving the row, and implements no other pointer interface.
///
/// This small component exists because Unity's EventTrigger - which does the same job with
/// two entries - also implements scrolling, dragging and the start of both. The event system
/// only walks up to the FIRST object of a hierarchy that handles an event (and only calls the
/// handlers of that one object): an EventTrigger on the row swallowed the wheel and the
/// dragging of the list, the ScrollRect above the row never heard about either, and only the
/// scrollbar kept working - no row stands over it. An object without the interface is passed
/// over, so the walk reaches the ScrollRect again.
/// </summary>
public sealed class GuiRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>The creature this row holds: set once, by GuiPreview.WatchRow.</summary>
    internal string Creature { get; set; } = string.Empty;

    public void OnPointerEnter(PointerEventData eventData) => GuiPreview.Hover(Creature);

    public void OnPointerExit(PointerEventData eventData) => GuiPreview.Leave();
}
