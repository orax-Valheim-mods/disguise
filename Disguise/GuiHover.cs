#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;

namespace Disguise;

/// <summary>
/// Tells GuiHelp what the element under the pointer is for, and nothing else: the pointer
/// entering an element puts its words on the line at the bottom of the window, leaving it
/// puts the idle line back. One component per element, added by GuiHelp.Watch, which also
/// makes the element a hit target (a label the pointer cannot reach is a label whose help
/// can never be shown).
///
/// This small component exists for the reason GuiRowHover does: Unity's EventTrigger -
/// which does this job with two entries - also implements scrolling, dragging and the start
/// of both. The event system only walks up to the FIRST object of a hierarchy that handles
/// an event (and only calls the handlers of that one object): an EventTrigger on a row
/// swallowed the wheel and the dragging of the list, the ScrollRect above the row never
/// heard about either, and only the scrollbar kept working - no row stands over it. Two
/// pointer interfaces cost nothing of the kind: the element keeps every handler it already
/// has (they are all components of the same object, and Unity calls all of them - a Button
/// still gets its own Enter and Exit), and an event the element does not handle itself is
/// passed to its parent, so the panel still owns the drag wherever the pointer grabs it.
/// </summary>
public sealed class GuiHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>What this element is for: set once, by GuiHelp.Watch.</summary>
    internal string Help { get; set; } = string.Empty;

    public void OnPointerEnter(PointerEventData eventData) => GuiHelp.Show(Help, this);

    public void OnPointerExit(PointerEventData eventData) => GuiHelp.Clear(this);
}
