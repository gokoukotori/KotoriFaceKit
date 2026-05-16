namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ParameterDriverComponent))]
internal class ParameterDriverEditor : FaceTuneIMGUIEditorBase<ParameterDriverComponent>
{
    protected override void OnInnerInspectorGUI()
    {
        DrawDefaultInspector(true);

        EditorGUILayout.HelpBox(
            "ParameterDriverComponent:help:AddUsesNumericParameters".LS(),
            MessageType.Info);
    }
}
