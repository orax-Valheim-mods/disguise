#nullable enable

namespace Disguise;

/// <summary>
/// The categories the prefab list is split into, one checkbox per kind in the window
/// (GuiCategories). Every prefab belongs to exactly one of them: PrefabCatalog.KindOf tests
/// them in this order and stops at the first match, so a piece that is also destructible
/// stays in Building and can never appear under two checkboxes.
/// </summary>
internal enum PrefabKind
{
    /// <summary>A prefab carrying a Character: the list the mod shipped with, plus Player.</summary>
    Creature = 0,

    /// <summary>An ItemDrop: materials, weapons, food, trophies.</summary>
    Item = 1,

    /// <summary>A Piece: walls, floors, doors, furniture, crafting stations.</summary>
    Building = 2,

    /// <summary>The world itself: trees, plants, pickables, rocks and ore veins.</summary>
    Nature = 3,

    /// <summary>A Ship.</summary>
    Ship = 4,

    /// <summary>Whatever none of the five above claimed: the list never leaves a prefab out.</summary>
    Other = 5,
}
