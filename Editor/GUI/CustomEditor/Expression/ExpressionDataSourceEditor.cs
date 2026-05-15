using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ExpressionDataSourceComponent), true)]
internal class ExpressionDataSourceEditor : FaceTuneIMGUIEditorBase<ExpressionDataSourceComponent>
{
    private AvatarContext? _context;

    private SerializedProperty _memoProperty = null!;
    private SerializedProperty _blendShapeAnimationsProperty = null!;
    private SerializedProperty _clipProperty = null!;
    private SerializedProperty _clipOptionProperty = null!;
    private SerializedProperty _allBlendShapeAnimationAsFacialProperty = null!;
    private SerializedProperty? _targetBaseProperty;
    private LocalizedPopup _clipOptionPopup = null!;

    private int _facialClipAnimationCount = 0;
    private int _nonFacialClipAnimationCount = 0;
    private string[] _missingBlendShapeNames = null!;

    public override void OnEnable()
    {
        base.OnEnable();
        CustomEditorUtility.TryGetContext(Component.gameObject, out _context);
        _memoProperty = serializedObject.FindProperty(nameof(ExpressionDataSourceComponent.Memo));
        _blendShapeAnimationsProperty = serializedObject.FindProperty(nameof(ExpressionDataSourceComponent.BlendShapeAnimations));
        _clipProperty = serializedObject.FindProperty(nameof(ExpressionDataSourceComponent.Clip));
        _clipOptionProperty = serializedObject.FindProperty(nameof(ExpressionDataSourceComponent.ClipOption));
        _allBlendShapeAnimationAsFacialProperty = serializedObject.FindProperty(nameof(ExpressionDataSourceComponent.AllBlendShapeAnimationAsFacial));
        _targetBaseProperty = Component is ExpressionOverrideComponent
            ? serializedObject.FindProperty(nameof(ExpressionOverrideComponent.TargetBase))
            : null;
        UpdateInfo();
        _clipOptionPopup = new LocalizedPopup(typeof(ClipImportOption));
        Undo.undoRedoPerformed += UpdateInfo;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        _clipOptionPopup.Dispose();
        Undo.undoRedoPerformed -= UpdateInfo;
    }

    protected override void OnInnerInspectorGUI()
    {
        DrawMissingBlendShapeGUI();
        EditorGUILayout.Space();
        DrawOverrideTargetBaseGUI();
        EditorGUILayout.Space();
        DrawAnimationClipGUI();
        EditorGUILayout.Space();
        DrawManualGUI();
        EditorGUILayout.Space();
        DrawAdvancedOptionsGUI();
        EditorGUILayout.Space();
        DrawMemoGUI();
    }

    private void DrawMissingBlendShapeGUI()
    {
        if (_missingBlendShapeNames.Length == 0) return;
        EditorGUILayout.HelpBox($"{"ExpressionDataSourceComponent:label:MissingBlendShapes".LS()}: {string.Join(", ", _missingBlendShapeNames)}", MessageType.Warning);
    }

    private void DrawOverrideTargetBaseGUI()
    {
        if (Component is not ExpressionOverrideComponent expressionOverride || _targetBaseProperty == null) return;

        if (UsesBaseStack(expressionOverride))
        {
            EditorGUILayout.HelpBox("ExpressionOverrideComponent:info:SameGameObjectStack".LS(), MessageType.Info);
            return;
        }

        if (UsesOverrideOnlyStackTail(expressionOverride))
        {
            EditorGUILayout.HelpBox("ExpressionOverrideComponent:info:OverrideOnlyStack".LS(), MessageType.Info);
            return;
        }

        LocalizedPropertyField(_targetBaseProperty, "ExpressionOverrideComponent:prop:TargetBase");
        var targetSource = _targetBaseProperty.objectReferenceValue as ExpressionDataSourceComponent;
        if (targetSource == null)
        {
            EditorGUILayout.HelpBox("ExpressionOverrideComponent:warning:TargetBaseMissing".LS(), MessageType.Warning);
        }
    }

    private void DrawMemoGUI()
    {
        LocalizedPropertyField(_memoProperty);
    }

    private void DrawAnimationClipGUI()
    {
        EditorGUILayout.LabelField("ExpressionDataSourceComponent:label:AnimationClipMode".LG(), EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        using (new EditorGUILayout.HorizontalScope())
        {
            LocalizedPropertyField(_clipProperty);

            var clipInfoText = $"{"ExpressionDataSourceComponent:label:ClipFacialAnimationCount".LS()}: {_facialClipAnimationCount}, {"ExpressionDataSourceComponent:label:ClipNonFacialAnimationCount".LS()}: {_nonFacialClipAnimationCount}";
            var infoContent = EditorGUIUtility.IconContent("console.infoicon.sml");
            infoContent.tooltip = clipInfoText;
            GUILayout.Label(infoContent, GUIStyleHelper.IconLabel, GUILayout.Width(16), GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        _clipOptionPopup.Field(_clipOptionProperty);

        if (EditorGUI.EndChangeCheck())
        {
            EditorApplication.delayCall += UpdateInfo;
        }
    }

    private void DrawManualGUI()
    {
        LocalizedPropertyField(_blendShapeAnimationsProperty);

        EditorGUILayout.Space();
        if (GUILayout.Button("ExpressionDataSourceComponent:button:OpenEditor".LG()))
        {
            OpenEditor();
        }
    }

    private bool _showAdvancedOptions = false;
    private void DrawAdvancedOptionsGUI()
    {
        _showAdvancedOptions = EditorGUILayout.Foldout(_showAdvancedOptions, "ExpressionDataSourceComponent:label:AdvancedOptions".LG());
        if (_showAdvancedOptions)
        {
            EditorGUI.indentLevel++;
            LocalizedPropertyField(_allBlendShapeAnimationAsFacialProperty);
            EditorGUI.indentLevel--;
        }
    }

    private void UpdateInfo()
    {
        if (_context == null)
        {
            _facialClipAnimationCount = 0;
            _nonFacialClipAnimationCount = 0;
            _missingBlendShapeNames = new string[0];
            return;
        }

        var (facialAnimations, nonFacialAnimations) = Component.ProcessClip(_context.BodyPath);

        _facialClipAnimationCount = facialAnimations.Count;
        _nonFacialClipAnimationCount = nonFacialAnimations.Count;

        var allBlendShapes = _context.ZeroBlendShapes
            .Select(x => x.Name)
            .ToHashSet();
        _missingBlendShapeNames = Component.BlendShapeAnimations.Concat(facialAnimations)
            .Distinct()
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrEmpty(x) && !allBlendShapes.Contains(x))
            .ToArray();
    }

    private void OpenEditor()
    {
        ExpressionShapesEditorLauncher.Open(Component);
    }

    private static bool UsesBaseStack(ExpressionOverrideComponent expressionOverride)
    {
        var sources = new List<ExpressionDataSourceComponent>();
        expressionOverride.gameObject.GetComponents(sources);
        var sourceArray = sources
            .SkipDestroyed()
            .ToArray();
        return sourceArray
            .Any(source => source is BaseExpressionDataComponent);
    }

    private static bool UsesOverrideOnlyStackTail(ExpressionOverrideComponent expressionOverride)
    {
        var sources = new List<ExpressionDataSourceComponent>();
        expressionOverride.gameObject.GetComponents(sources);
        var sourceArray = sources
            .SkipDestroyed()
            .ToArray();
        var expressionOverrides = sourceArray
            .OfType<ExpressionOverrideComponent>()
            .ToArray();
        var index = Array.IndexOf(expressionOverrides, expressionOverride);
        return expressionOverrides.Length > 1 && index > 0 && !sourceArray
            .Any(source => source is BaseExpressionDataComponent);
    }
}
