namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(HandGestureCondition))]
internal class HandGestureConditionDrawer : PropertyDrawer
{
    private readonly LocalizedPopup _handPopup;

    public HandGestureConditionDrawer()
    {
        _handPopup = new LocalizedPopup(null, typeof(Hand).GetEnumNames().Select(k => $"Hand:enum:{k}"));
    }

    private const float RowSpacing = 4f;
    private const float FieldSpacing = 4f;
    private const float HandPopupWidth = 90f;
    private const float GestureButtonHeight = 28f;
    private static GUIStyle? _gestureImageButtonStyle;
    private static GUIStyle? _gestureTextButtonStyle;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var handProp = property.FindPropertyRelative(HandGestureCondition.HandPropName);
        var handGestureProp = property.FindPropertyRelative(HandGestureCondition.HandGesturePropName);
        var equalityComparisonProp = property.FindPropertyRelative(HandGestureCondition.EqualityComparisonPropName);

        var lineHeight = EditorGUIUtility.singleLineHeight;
        var handRect = new Rect(position.x, position.y, Math.Min(HandPopupWidth, position.width), lineHeight);
        var gestureRect = new Rect(position.x, handRect.yMax + RowSpacing, position.width, GestureButtonHeight);
        var comparisonRect = new Rect(position.x, gestureRect.yMax + RowSpacing, position.width, lineHeight);

        _handPopup.Field(handRect, handProp);
        DrawGestureButtons(gestureRect, handGestureProp);
        DrawComparisonToolbar(comparisonRect, equalityComparisonProp);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 2f + GestureButtonHeight + RowSpacing * 2f;
    }

    private static void DrawGestureButtons(Rect position, SerializedProperty handGestureProp)
    {
        var buttonCount = HandGestureIconSet.Icons.Length;
        var spacingTotal = FieldSpacing * (buttonCount - 1);
        var buttonWidth = Math.Max(1f, (position.width - spacingTotal) / buttonCount);
        var buttonHeight = position.height;
        var x = position.x;
        var textColor = EditorStyles.label.normal.textColor;

        for (var i = 0; i < buttonCount; i++)
        {
            var buttonRect = new Rect(x, position.y, buttonWidth, buttonHeight);
            var selected = handGestureProp.enumValueIndex == i;
            var style = HandGestureIconSet.Icons[i].TextureName == null ? GestureTextButtonStyle : GestureImageButtonStyle;
            if (GUI.Toggle(buttonRect, selected, HandGestureIconSet.ContentFor(i, textColor), style) && !selected)
            {
                handGestureProp.enumValueIndex = i;
            }
            x += buttonWidth + FieldSpacing;
        }
    }

    private static void DrawComparisonToolbar(Rect position, SerializedProperty equalityComparisonProp)
    {
        var contents = new[]
        {
            "EqualityComparison:enum:Equal".LG(),
            "EqualityComparison:enum:NotEqual".LG()
        };
        equalityComparisonProp.enumValueIndex = GUI.Toolbar(position, equalityComparisonProp.enumValueIndex, contents);
    }

    private static GUIStyle GestureImageButtonStyle => _gestureImageButtonStyle ??= new GUIStyle(EditorStyles.toolbarButton)
    {
        alignment = TextAnchor.MiddleCenter,
        imagePosition = ImagePosition.ImageOnly,
        padding = new RectOffset(2, 2, 2, 2)
    };

    private static GUIStyle GestureTextButtonStyle => _gestureTextButtonStyle ??= new GUIStyle(EditorStyles.toolbarButton)
    {
        alignment = TextAnchor.MiddleCenter,
        imagePosition = ImagePosition.TextOnly
    };
}
