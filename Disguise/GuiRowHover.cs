#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;

namespace Disguise;

/// <summary>
/// Tells GuiPreview what the row under the pointer holds, and GuiHelp what one row of the
/// list is for: the pointer entering a row shows its creature in the picture and writes its
/// help on the line at the bottom of the window, leaving it puts the idle line back - the
/// picture itself keeps the last row it was told about until another comes along (Current),
/// so stepping off a row changes nothing THERE and something only HERE.
///
/// This small component exists because Unity's EventTrigger - which does the same job with
/// two entries - also implements scrolling, dragging and the start of both. The event system
/// only walks up to the FIRST object of a hierarchy that handles an event (and only calls the
/// handlers of that one object): an EventTrigger on the row swallowed the wheel and the
/// dragging of the list, the ScrollRect above the row never heard about either, and only the
/// scrollbar kept working - no row stands over it. An object without the interface is passed
/// over, so the walk reaches the ScrollRect again. The two interfaces below cost nothing of
/// the kind: the row keeps its own Button handlers (they are components of this very object,
/// and Unity calls all of them), and a wheel over a row still walks up to the ScrollRect.
/// </summary>
public sealed class GuiRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>
    /// What one row of the list is for - the help line's own words about it, the same for
    /// every row (GuiHelp writes them, and only ever if nothing else is being described).
    /// </summary>
    private const string RowHelp =
        "Click a row: the prefab is set and applied at once - the same as \"disguise set <name>\" - and it is what the picture on the right shows.";

    /// <summary>The creature this row holds: set once, by GuiPreview.WatchRow.</summary>
    internal string Creature { get; set; } = string.Empty;

    public void OnPointerEnter(PointerEventData eventData)
    {
        GuiPreview.Hover(Creature);
        GuiHelp.Show(RowHelp, this);
    }

    public void OnPointerExit(PointerEventData eventData) => GuiHelp.Clear(this);
}
