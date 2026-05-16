namespace Aoyon.FaceTune.Gui;

using Components;

[CustomPropertyDrawer(typeof(ParameterDriverOperation))]
internal class ParameterDriverOperationDrawer : PropertyDrawer
{
    private const float HeightMargin = 2;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var typeProp = property.FindPropertyRelative(ParameterDriverOperation.TypePropName);
        var destinationProp = property.FindPropertyRelative(ParameterDriverOperation.DestinationPropName);
        var type = (ParameterDriverChangeType)typeProp.enumValueIndex;

        var current = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var typeRect = new Rect(current.x, current.y, current.width * 0.35f, current.height);
        var destinationRect = new Rect(typeRect.xMax + 4, current.y, current.width - typeRect.width - 4, current.height);
        EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);
        PlaceholderTextField.TextField(destinationRect, destinationProp, "ParameterDriverOperation:prop:Destination:placeholder".LS());

        current.y += EditorGUIUtility.singleLineHeight + HeightMargin;
        switch (type)
        {
            case ParameterDriverChangeType.Set:
            case ParameterDriverChangeType.Add:
                DrawProperty(current, property, ParameterDriverOperation.ValuePropName, "ParameterDriverOperation:prop:Value");
                break;
            case ParameterDriverChangeType.Random:
                DrawRandomFields(current, property);
                break;
            case ParameterDriverChangeType.Copy:
                DrawCopyFields(current, property);
                break;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var typeProp = property.FindPropertyRelative(ParameterDriverOperation.TypePropName);
        var type = (ParameterDriverChangeType)typeProp.enumValueIndex;
        var lines = type switch
        {
            ParameterDriverChangeType.Copy when property.FindPropertyRelative(ParameterDriverOperation.ConvertRangePropName).boolValue => 4,
            _ => 2
        };
        return EditorGUIUtility.singleLineHeight * lines + HeightMargin * (lines - 1);
    }

    private static void DrawRandomFields(Rect rect, SerializedProperty property)
    {
        var minProp = property.FindPropertyRelative(ParameterDriverOperation.ValueMinPropName);
        var maxProp = property.FindPropertyRelative(ParameterDriverOperation.ValueMaxPropName);
        var chanceProp = property.FindPropertyRelative(ParameterDriverOperation.ChancePropName);
        var preventRepeatsProp = property.FindPropertyRelative(ParameterDriverOperation.PreventRepeatsPropName);

        var width = (rect.width - 12) / 4;
        var minRect = new Rect(rect.x, rect.y, width, rect.height);
        var maxRect = new Rect(minRect.xMax + 4, rect.y, width, rect.height);
        var chanceRect = new Rect(maxRect.xMax + 4, rect.y, width, rect.height);
        var repeatRect = new Rect(chanceRect.xMax + 4, rect.y, width, rect.height);

        EditorGUI.PropertyField(minRect, minProp, "ParameterDriverOperation:prop:ValueMin".LG());
        EditorGUI.PropertyField(maxRect, maxProp, "ParameterDriverOperation:prop:ValueMax".LG());
        EditorGUI.PropertyField(chanceRect, chanceProp, "ParameterDriverOperation:prop:Chance".LG());
        EditorGUI.PropertyField(repeatRect, preventRepeatsProp, "ParameterDriverOperation:prop:PreventRepeats".LG());
    }

    private static void DrawCopyFields(Rect rect, SerializedProperty property)
    {
        var sourceProp = property.FindPropertyRelative(ParameterDriverOperation.SourcePropName);
        var convertRangeProp = property.FindPropertyRelative(ParameterDriverOperation.ConvertRangePropName);

        var sourceRect = new Rect(rect.x, rect.y, rect.width * 0.65f - 2, rect.height);
        var convertRect = new Rect(sourceRect.xMax + 4, rect.y, rect.width - sourceRect.width - 4, rect.height);
        PlaceholderTextField.TextField(sourceRect, sourceProp, "ParameterDriverOperation:prop:Source:placeholder".LS());
        EditorGUI.PropertyField(convertRect, convertRangeProp, "ParameterDriverOperation:prop:ConvertRange".LG());

        if (!convertRangeProp.boolValue) return;

        rect.y += EditorGUIUtility.singleLineHeight + HeightMargin;
        DrawRangePair(
            rect,
            property.FindPropertyRelative(ParameterDriverOperation.SourceMinPropName),
            property.FindPropertyRelative(ParameterDriverOperation.SourceMaxPropName),
            "ParameterDriverOperation:label:SourceRange");

        rect.y += EditorGUIUtility.singleLineHeight + HeightMargin;
        DrawRangePair(
            rect,
            property.FindPropertyRelative(ParameterDriverOperation.DestinationMinPropName),
            property.FindPropertyRelative(ParameterDriverOperation.DestinationMaxPropName),
            "ParameterDriverOperation:label:DestinationRange");
    }

    private static void DrawRangePair(Rect rect, SerializedProperty minProp, SerializedProperty maxProp, string labelKey)
    {
        var labelWidth = rect.width * 0.3f;
        var fieldWidth = (rect.width - labelWidth - 8) / 2;
        var labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
        var minRect = new Rect(labelRect.xMax + 4, rect.y, fieldWidth, rect.height);
        var maxRect = new Rect(minRect.xMax + 4, rect.y, fieldWidth, rect.height);

        EditorGUI.LabelField(labelRect, labelKey.LS());
        EditorGUI.PropertyField(minRect, minProp, GUIContent.none);
        EditorGUI.PropertyField(maxRect, maxProp, GUIContent.none);
    }

    private static void DrawProperty(Rect rect, SerializedProperty property, string propertyName, string labelKey)
    {
        var prop = property.FindPropertyRelative(propertyName);
        EditorGUI.PropertyField(rect, prop, labelKey.LG());
    }
}
