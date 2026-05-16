namespace Aoyon.FaceTune.Gui;

internal class ExpressionManagerWindow : EditorWindow
{
    [SerializeField] private GameObject? _avatarRoot;
    [SerializeField] private string _searchText = string.Empty;
    private readonly Dictionary<int, bool> _foldouts = new();
    private ExpressionManagerThumbnailCache _thumbnailCache = null!;
    private IReadOnlyList<IExpressionManagerDisplayItem> _displayItems = Array.Empty<IExpressionManagerDisplayItem>();
    private IReadOnlyList<IExpressionManagerDisplayItem> _filteredDisplayItems = Array.Empty<IExpressionManagerDisplayItem>();
    private IReadOnlyList<ExpressionManagerExpressionItem> _items = Array.Empty<ExpressionManagerExpressionItem>();
    private IReadOnlyList<ExpressionManagerExpressionItem> _filteredItems = Array.Empty<ExpressionManagerExpressionItem>();
    private IReadOnlyList<ExpressionManagerUnlinkedSourceItem> _unlinkedSources = Array.Empty<ExpressionManagerUnlinkedSourceItem>();
    private IReadOnlyList<ExpressionManagerUnlinkedSourceItem> _filteredUnlinkedSources = Array.Empty<ExpressionManagerUnlinkedSourceItem>();
    private AvatarContext? _avatarContext;
    private string? _lastSearchText;
    private string _rendererState = string.Empty;
    private bool _needsRebuildItems = true;
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
        _thumbnailCache ??= new ExpressionManagerThumbnailCache();
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        _thumbnailCache?.Dispose();
        _thumbnailCache = null!;
    }

    private void SetAvatarRoot(GameObject avatarRoot)
    {
        _avatarRoot = avatarRoot;
        _foldouts.Clear();
        RebuildItems();
    }

    private void OnGUI()
    {
        DrawHeader();

        if (_avatarRoot == null)
        {
            EditorGUILayout.HelpBox("Avatar root is not set.", MessageType.Info);
            return;
        }

        EnsureItems();
        EnsureFilteredItems();

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label($"Expressions: {_filteredItems.Count}/{_items.Count}", GUILayout.Width(130));
            GUILayout.Label($"Unlinked: {_filteredUnlinkedSources.Count}/{_unlinkedSources.Count}", GUILayout.Width(110));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                GUI.FocusControl(null);
                RebuildItems();
                Repaint();
            }
        }

        if (_items.Count == 0 && _unlinkedSources.Count == 0)
        {
            EditorGUILayout.HelpBox("No FaceTune expressions were found under this avatar.", MessageType.Info);
            return;
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        if (_items.Count == 0)
        {
            EditorGUILayout.HelpBox("No FaceTune expressions were found under this avatar.", MessageType.Info);
        }

        foreach (var item in _filteredDisplayItems)
        {
            DrawDisplayItem(item);
        }

        DrawUnlinkedSourcesSection();
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

    private void DrawDisplayItem(IExpressionManagerDisplayItem item)
    {
        switch (item)
        {
            case ExpressionManagerPresetGroup presetGroup:
                DrawPresetGroup(presetGroup);
                break;
            case ExpressionManagerPatternGroup patternGroup:
                DrawPatternGroup(patternGroup);
                break;
            case ExpressionManagerExpressionItem expressionItem:
                DrawExpressionItem(expressionItem);
                break;
        }
    }

    private void DrawPresetGroup(ExpressionManagerPresetGroup group)
    {
        var instanceId = group.Preset.GetInstanceID();
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
                    group.Preset.name,
                    true,
                    EditorStyles.foldoutHeader);

                GUILayout.FlexibleSpace();
                GUILayout.Label($"Expressions: {group.ExpressionCount}", GUILayout.Width(96));
                if (GUILayout.Button("Select", GUILayout.Width(58)))
                {
                    SelectObject(group.Preset);
                }
            }

            if (!_foldouts[instanceId]) return;

            EditorGUILayout.LabelField("Path", group.HierarchyPath);
            EditorGUILayout.Space(2);
            foreach (var pattern in group.Patterns)
            {
                DrawPatternGroup(pattern);
            }
        }
    }

    private void DrawPatternGroup(ExpressionManagerPatternGroup group)
    {
        var instanceId = group.Pattern.GetInstanceID();
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
                    group.Pattern.name,
                    true,
                    EditorStyles.foldoutHeader);

                GUILayout.FlexibleSpace();
                GUILayout.Label($"Expressions: {group.ExpressionCount}", GUILayout.Width(96));
                if (GUILayout.Button("Select", GUILayout.Width(58)))
                {
                    SelectObject(group.Pattern);
                }
            }

            if (!_foldouts[instanceId]) return;

            EditorGUILayout.LabelField("Path", group.HierarchyPath);
            EditorGUILayout.Space(2);
            foreach (var expression in group.Expressions)
            {
                DrawExpressionItem(expression);
            }
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

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawThumbnail(item);

                using (new EditorGUILayout.VerticalScope())
                {
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
        }
    }

    private void DrawThumbnail(ExpressionManagerExpressionItem item)
    {
        var size = ExpressionManagerThumbnailCache.ThumbnailSize;
        var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        if (_avatarContext == null)
        {
            GUI.Box(rect, "No Preview");
            return;
        }

        var thumbnail = _thumbnailCache.GetOrRequest(item, _avatarContext, _rendererState, Repaint);
        var texture = thumbnail.Texture;
        if (texture == null)
        {
            GUI.Box(rect, thumbnail.Status == ExpressionManagerThumbnailStatus.Failed ? "No Preview" : "Loading");
            return;
        }

        GUI.Box(rect, GUIContent.none);
        GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
    }

    private void DrawUnlinkedSourcesSection()
    {
        if (_unlinkedSources.Count == 0) return;

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                $"Unlinked Expression Data Sources: {_filteredUnlinkedSources.Count}/{_unlinkedSources.Count}",
                EditorStyles.boldLabel);

            if (_filteredUnlinkedSources.Count == 0)
            {
                EditorGUILayout.HelpBox("No unlinked Expression Data Sources match the current search.", MessageType.Info);
                return;
            }

            foreach (var item in _filteredUnlinkedSources)
            {
                DrawUnlinkedSourceItem(item);
            }
        }
    }

    private void DrawUnlinkedSourceItem(ExpressionManagerUnlinkedSourceItem item)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            DrawThumbnail(item);

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Path", item.HierarchyPath);
                foreach (var component in item.Components)
                {
                    DrawEditableTarget(component);
                }
            }
        }
    }

    private void DrawThumbnail(ExpressionManagerUnlinkedSourceItem item)
    {
        var size = ExpressionManagerThumbnailCache.ThumbnailSize;
        var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        if (_avatarContext == null)
        {
            GUI.Box(rect, "No Preview");
            return;
        }

        var thumbnail = _thumbnailCache.GetOrRequest(item, _avatarContext, _rendererState, Repaint);
        var texture = thumbnail.Texture;
        if (texture == null)
        {
            GUI.Box(rect, thumbnail.Status == ExpressionManagerThumbnailStatus.Failed ? "No Preview" : "Loading");
            return;
        }

        GUI.Box(rect, GUIContent.none);
        GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
    }

    private void OnHierarchyChanged()
    {
        _needsRebuildItems = true;
        _thumbnailCache?.Clear();
        Repaint();
    }

    private void RebuildItems()
    {
        _needsRebuildItems = true;
        _thumbnailCache?.Clear();
        EnsureItems();
    }

    private void EnsureItems()
    {
        if (!_needsRebuildItems || _avatarRoot == null) return;

        _displayItems = ExpressionManagerItemCollector.CollectDisplayItems(_avatarRoot);
        _items = _displayItems
            .SelectMany(item => item.Expressions)
            .ToArray();
        _unlinkedSources = ExpressionManagerItemCollector.CollectUnlinkedSources(_avatarRoot, _items);
        _avatarContext = null;
        _rendererState = string.Empty;
        if (AvatarContextBuilder.TryBuild(_avatarRoot, out var avatarContext, out _))
        {
            _avatarContext = avatarContext;
            _rendererState = ExpressionManagerThumbnailCache.CreateRendererState(avatarContext.Root);
        }

        _lastSearchText = null;
        _needsRebuildItems = false;
        EnsureFilteredItems();
    }

    private void EnsureFilteredItems()
    {
        if (_lastSearchText == _searchText) return;

        _filteredDisplayItems = ExpressionManagerItemCollector.FilterDisplayItems(_displayItems, _searchText).ToArray();
        _filteredItems = _filteredDisplayItems
            .SelectMany(item => item.Expressions)
            .ToArray();
        _filteredUnlinkedSources = ExpressionManagerItemCollector.FilterUnlinkedSources(_unlinkedSources, _searchText).ToArray();
        _lastSearchText = _searchText;
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
