namespace Aoyon.FaceTune.Gui;

internal class ExpressionManagerWindow : EditorWindow
{
    [SerializeField] private GameObject? _avatarRoot;
    [SerializeField] private string _searchText = string.Empty;
    private readonly Dictionary<int, bool> _foldouts = new();
    private Vector2 _scrollPosition;

    public static void Open(GameObject avatarRoot)
    {
        var window = GetWindow<ExpressionManagerWindow>();
        window.titleContent = new GUIContent("Expression Manager");
        window.SetAvatarRoot(avatarRoot);
        window.Show();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Expression Manager");
        minSize = new Vector2(420, 320);
    }

    private void SetAvatarRoot(GameObject avatarRoot)
    {
        _avatarRoot = avatarRoot;
        _foldouts.Clear();
    }

    private void OnGUI()
    {
        DrawHeader();

        if (_avatarRoot == null)
        {
            EditorGUILayout.HelpBox("Avatar root is not set.", MessageType.Info);
            return;
        }

        var items = ExpressionManagerItemCollector.Collect(_avatarRoot);
        var filteredItems = ExpressionManagerItemCollector.Filter(items, _searchText).ToArray();

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label($"Expressions: {filteredItems.Length}/{items.Count}", GUILayout.Width(130));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                GUI.FocusControl(null);
                Repaint();
            }
        }

        if (items.Count == 0)
        {
            EditorGUILayout.HelpBox("No FaceTune expressions were found under this avatar.", MessageType.Info);
            return;
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        foreach (var item in filteredItems)
        {
            DrawExpressionItem(item);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Avatar", _avatarRoot, typeof(GameObject), true);
                }

                if (_avatarRoot != null && GUILayout.Button("Select", GUILayout.Width(64)))
                {
                    SelectObject(_avatarRoot);
                }
            }

            _searchText = EditorGUILayout.TextField("Search", _searchText);
        }
    }

    private void DrawExpressionItem(ExpressionManagerExpressionItem item)
    {
        var instanceId = item.Expression.GetInstanceID();
        if (!_foldouts.TryGetValue(instanceId, out var isExpanded))
        {
            isExpanded = true;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _foldouts[instanceId] = EditorGUILayout.Foldout(
                    isExpanded,
                    item.Expression.name,
                    true,
                    EditorStyles.foldoutHeader);

                GUILayout.FlexibleSpace();
                GUILayout.Label($"Conditions: {item.ConditionCount}", GUILayout.Width(92));
                if (GUILayout.Button("Select", GUILayout.Width(58)))
                {
                    SelectObject(item.Expression);
                }
            }

            if (!_foldouts[instanceId]) return;

            EditorGUILayout.LabelField("Path", item.HierarchyPath);
            EditorGUILayout.LabelField("Expression Data", item.ExpressionDataComponents.Count.ToString());

            if (item.EditableTargets.Count == 0)
            {
                EditorGUILayout.HelpBox("No editable Expression Data was found for this expression.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(2);
            foreach (var target in item.EditableTargets)
            {
                DrawEditableTarget(target);
            }
        }
    }

    private void DrawEditableTarget(Component target)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField(target, target.GetType(), true);
            if (GUILayout.Button("Select", GUILayout.Width(58)))
            {
                SelectObject(target);
            }

            using (new EditorGUI.DisabledScope(!CanEdit(target)))
            {
                if (GUILayout.Button("Edit", GUILayout.Width(48)))
                {
                    OpenEditor(target);
                }
            }
        }
    }

    private static bool CanEdit(Component target)
    {
        return target is ExpressionDataComponent or BaseExpressionDataComponent or ExpressionOverrideComponent;
    }

    private static void OpenEditor(Component target)
    {
        switch (target)
        {
            case ExpressionDataComponent expressionData:
                ExpressionShapesEditorLauncher.Open(expressionData);
                break;
            case ExpressionDataSourceComponent dataSource:
                ExpressionShapesEditorLauncher.Open(dataSource);
                break;
        }
    }

    private static void SelectObject(Object target)
    {
        Selection.activeObject = target;
        EditorGUIUtility.PingObject(target);
    }
}
