#nullable enable
using System;
using System.Globalization;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Disguise;

/// <summary>
/// The two rows under the prefab list - Offset [x] [y] [z] and Rotation [x] [y] [z] - and the
/// six editable fields they are made of (Jotunn's Valheim-styled input field, the same helper
/// the filter uses). They are the window's side of PoseControl: nothing else reads or writes
/// them, and the window only ever shows what the pose really is.
///
/// A change applies as it is typed. Each field parses its own text with the parser the
/// console already shares (DisguiseCommand.TryParseCoordinate, so "1.5" and "1,5" both land)
/// and keeps the value it had when the text is not a number yet - an empty box, a lone "-",
/// half a typed number - so the costume never jumps somewhere the player did not ask for.
/// What the player is typing is never rewritten either: Sync repairs the fields from the
/// outside only while they are not focused, which is what lets a value set from the F5
/// console land in the window without disturbing a half-typed one.
///
/// While one of the six has the focus, the frame belongs to the UI (Focused, read by
/// DisguiseGui.BlocksInput) exactly as the filter field does: typing a number must not walk
/// the character away.
/// </summary>
internal static class GuiPose
{
    private const float RowHeight = 36f;

    /// <summary>Gap between the two rows - the rhythm GuiWindow uses between its blocks.</summary>
    private const float RowGap = 6f;

    // One label column, then three columns of one field each, all four the same width apart
    // so that X, Y and Z line up between the two rows (LabelWidth + the gap in front of it).
    private const float LabelWidth = 110f;

    private const float FieldWidth = 82f;

    private const float FieldGap = 6f;

    private static readonly InputField?[] s_offsetFields = new InputField?[3];

    private static readonly InputField?[] s_rotationFields = new InputField?[3];

    /// <summary>True while Sync writes a field, so its own listener has nothing to apply.</summary>
    private static bool s_syncing;

    /// <summary>
    /// True while the player types in one of the six fields: the frame then belongs to the UI
    /// whether the pointer is on the window or not - read by DisguiseGui.BlocksInput, exactly
    /// as GuiCreatureList.FilterFocused does for the filter.
    /// </summary>
    internal static bool Focused => IsFocused(s_offsetFields) || IsFocused(s_rotationFields);

    // ------------------------------------------------------------------ build

    /// <summary>
    /// Builds the two rows inside the window. GuiWindow owns the layout and passes the
    /// top-left corner of the FIRST row; the second sits directly under it, and the three
    /// columns line up between the two - X, Y, Z, the order the console takes them in.
    /// </summary>
    internal static void Build(Transform parent, float x, float y)
    {
        Forget();

        GUIManager gui = GUIManager.Instance;
        BuildRow(gui, parent, x, y, "Offset (m)", s_offsetFields, PoseControl.Offset, OnOffset);
        BuildRow(
            gui,
            parent,
            x,
            y - RowHeight - RowGap,
            "Rotation (deg)",
            s_rotationFields,
            PoseControl.Euler,
            OnRotation
        );
    }

    /// <summary>
    /// One row: its label, then the three fields, each one reading the value it starts with
    /// and applying whatever it is given from then on. The listener is added AFTER the text:
    /// the initial value has nothing to apply, and the placeholder (the axis letter) is what
    /// the box shows for as long as it is empty.
    /// </summary>
    private static void BuildRow(
        GUIManager gui,
        Transform parent,
        float x,
        float y,
        string label,
        InputField?[] fields,
        Vector3 value,
        Action<int, string> onText
    )
    {
        _ = GuiWindow.Label(
            gui,
            label,
            parent,
            // Top-LEFT anchor, like the fields beside it: an anchor of (0.5, 1) would read x
            // as an offset from the middle of the window, and the label would land on the
            // preview half (GuiWindow positions its own centred labels there, and the frame
            // this row shares with the list block is the one starting at the left edge).
            new Vector2(0f, 1f),
            new Vector2(x + (LabelWidth * 0.5f), y - (RowHeight * 0.5f)),
            gui.AveriaSerif,
            16,
            gui.ValheimBeige,
            LabelWidth,
            RowHeight
        );

        for (int axis = 0; axis < 3; axis++)
        {
            int column = axis;
            float fieldX = x + LabelWidth + FieldGap + ((FieldWidth + FieldGap) * column);
            GameObject body = gui.CreateInputField(
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(fieldX, y),
                InputField.ContentType.Standard,
                AxisName(column),
                18,
                FieldWidth,
                RowHeight
            );

            // Jotunn sets the position BEFORE the anchors and never touches the pivot, which
            // stays in the middle: the corner asked for above would be the CENTRE of the
            // field, and a 82 px field centred there would hang out of its column. The pivot
            // is moved to the upper left corner and the position applied again - the same
            // correction the filter field gets (GuiCreatureList).
            RectTransform rect = body.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(fieldX, y);
            }

            InputField? field = body.GetComponent<InputField>();
            if (field == null)
            {
                continue;
            }
            field.text = Show(value, column);
            field.onValueChanged.AddListener(text => onText(column, text));
            fields[column] = field;
        }
    }

    // ------------------------------------------------------------------ per frame

    /// <summary>
    /// Rewrites the six fields from the pose, one string comparison each: called once per
    /// frame while the window is open (GuiWindow.RefreshState), so a value set from the
    /// console while the window is up lands in the fields - and what the player is typing
    /// stays where it is, focus being the only thing that stops a write.
    /// </summary>
    internal static void Sync()
    {
        Vector3 offset = PoseControl.Offset;
        SyncField(s_offsetFields[0], offset, 0);
        SyncField(s_offsetFields[1], offset, 1);
        SyncField(s_offsetFields[2], offset, 2);

        Vector3 turn = PoseControl.Euler;
        SyncField(s_rotationFields[0], turn, 0);
        SyncField(s_rotationFields[1], turn, 1);
        SyncField(s_rotationFields[2], turn, 2);
    }

    /// <summary>Unity-null once the window is gone, which is the whole safety of Sync.</summary>
    internal static void Forget()
    {
        s_syncing = false;
        for (int axis = 0; axis < 3; axis++)
        {
            s_offsetFields[axis] = null;
            s_rotationFields[axis] = null;
        }
    }

    // ------------------------------------------------------------------ listeners

    /// <summary>One of the three Offset fields changed (axis 0 = X, 1 = Y, 2 = Z).</summary>
    private static void OnOffset(int axis, string text)
    {
        if (s_syncing || !DisguiseCommand.TryParseCoordinate(text, out float value))
        {
            return; // a box that is not a number yet keeps the value it had
        }
        Vector3 current = PoseControl.Offset;
        PoseControl.SetOffset(
            axis == 0 ? value : current.x,
            axis == 1 ? value : current.y,
            axis == 2 ? value : current.z
        );
    }

    /// <summary>One of the three Rotation fields changed, in degrees.</summary>
    private static void OnRotation(int axis, string text)
    {
        if (s_syncing || !DisguiseCommand.TryParseCoordinate(text, out float value))
        {
            return;
        }
        Vector3 current = PoseControl.Euler;
        PoseControl.SetRotation(
            axis == 0 ? value : current.x,
            axis == 1 ? value : current.y,
            axis == 2 ? value : current.z
        );
    }

    // ------------------------------------------------------------------ helpers

    private static bool IsFocused(InputField?[] fields)
    {
        foreach (InputField? field in fields)
        {
            if (field != null && field.isFocused)
            {
                return true;
            }
        }
        return false;
    }

    private static void SyncField(InputField? field, Vector3 value, int axis)
    {
        if (field == null || field.isFocused)
        {
            return;
        }
        string text = Show(value, axis);
        if (string.Equals(field.text, text, StringComparison.Ordinal))
        {
            return;
        }
        s_syncing = true;
        field.text = text;
        s_syncing = false;
    }

    /// <summary>
    /// What a field shows: two decimals, the same precision the keys publish with (a
    /// hundredth of a unit), in the current culture so that a French keyboard reads back
    /// exactly what it typed ("1,5" stays "1,5" and parses again).
    /// </summary>
    private static string Show(Vector3 value, int axis)
    {
        float number =
            axis == 0 ? value.x
            : axis == 1 ? value.y
            : value.z;
        return number.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static string AxisName(int axis)
    {
        return axis == 0 ? "X"
            : axis == 1 ? "Y"
            : "Z";
    }
}
