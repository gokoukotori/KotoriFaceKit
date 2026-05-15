using Aoyon.FaceTune.Gui;
using NUnit.Framework;
using System.Linq;
using UnityEngine;

namespace Aoyon.FaceTune.Tests
{
    public class ExpressionManagerItemCollectorTests
    {
        private GameObject _avatarRoot = null!;

        [SetUp]
        public void SetUp()
        {
            _avatarRoot = new GameObject("Avatar");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_avatarRoot);
        }

        [Test]
        public void CollectFindsAllExpressionsUnderAvatarRoot()
        {
            var leftBranch = CreateChild(_avatarRoot, "LeftBranch");
            var rightBranch = CreateChild(_avatarRoot, "RightBranch");
            var smile = CreateExpression(leftBranch, "Smile");
            var angry = CreateExpression(rightBranch, "Angry");

            var items = ExpressionManagerItemCollector.Collect(_avatarRoot).ToArray();

            Assert.That(items.Select(item => item.Expression).ToArray(), Is.EqualTo(new[] { smile, angry }));
            Assert.That(items.Select(item => item.HierarchyPath).ToArray(), Is.EqualTo(new[] { "LeftBranch/Smile", "RightBranch/Angry" }));
        }

        [Test]
        public void CollectIncludesExpressionDataAndReferencedSourcesAsEditableTargets()
        {
            var expression = CreateExpression(_avatarRoot, "Smile");
            var expressionData = expression.gameObject.AddComponent<ExpressionDataComponent>();
            var baseData = expression.gameObject.AddComponent<BaseExpressionDataComponent>();
            var expressionOverride = expression.gameObject.AddComponent<ExpressionOverrideComponent>();
            expressionData.Mode = ExpressionDataMode.Reference;
            expressionData.DataReferences.Add(baseData);
            expressionData.DataReferences.Add(expressionOverride);

            var item = ExpressionManagerItemCollector.Collect(_avatarRoot).Single();

            Assert.That(item.ExpressionDataComponents, Is.EqualTo(new[] { expressionData }));
            Assert.That(item.EditableTargets, Is.EqualTo(new Component[] { expressionData, baseData, expressionOverride }));
        }

        [Test]
        public void CollectExpandsReferencedBaseStackAsEditableTargets()
        {
            var expression = CreateExpression(_avatarRoot, "Smile");
            var expressionData = expression.gameObject.AddComponent<ExpressionDataComponent>();
            var library = CreateChild(_avatarRoot, "Library");
            var baseData = library.AddComponent<BaseExpressionDataComponent>();
            var expressionOverride = library.AddComponent<ExpressionOverrideComponent>();
            expressionData.Mode = ExpressionDataMode.Reference;
            expressionData.DataReferences.Add(baseData);

            var item = ExpressionManagerItemCollector.Collect(_avatarRoot).Single();
            var unlinkedSources = ExpressionManagerItemCollector
                .CollectUnlinkedSources(_avatarRoot, new[] { item })
                .ToArray();

            Assert.That(item.EditableTargets, Is.EqualTo(new Component[] { expressionData, baseData, expressionOverride }));
            Assert.That(unlinkedSources.Select(source => source.Component).ToArray(), Has.No.Member(expressionOverride));
        }

        [Test]
        public void CollectUnlinkedSourcesFindsUnreferencedBaseAndOverrideSources()
        {
            var library = CreateChild(_avatarRoot, "Library");
            var baseData = library.AddComponent<BaseExpressionDataComponent>();
            var expressionOverride = library.AddComponent<ExpressionOverrideComponent>();
            CreateExpression(_avatarRoot, "Smile");
            var expressionItems = ExpressionManagerItemCollector.Collect(_avatarRoot);

            var unlinkedSources = ExpressionManagerItemCollector
                .CollectUnlinkedSources(_avatarRoot, expressionItems)
                .ToArray();

            Assert.That(unlinkedSources.Select(item => item.Component).ToArray(), Is.EqualTo(new Component[] { expressionOverride }));
            Assert.That(unlinkedSources.Select(item => item.HierarchyPath).ToArray(), Is.EqualTo(new[] { "Library" }));
            Assert.That(unlinkedSources.Single().Components.ToArray(), Is.EqualTo(new Component[] { baseData, expressionOverride }));
            Assert.That(unlinkedSources.Select(item => item.Component).ToArray(), Has.No.Member(baseData));
        }

        [Test]
        public void CollectUnlinkedSourcesGroupsSameGameObjectOverrideOnlyStack()
        {
            var overrideObject = CreateChild(_avatarRoot, "Override Stack");
            var firstOverride = overrideObject.AddComponent<ExpressionOverrideComponent>();
            var secondOverride = overrideObject.AddComponent<ExpressionOverrideComponent>();

            var unlinkedSources = ExpressionManagerItemCollector
                .CollectUnlinkedSources(_avatarRoot, Enumerable.Empty<ExpressionManagerExpressionItem>())
                .ToArray();

            Assert.That(unlinkedSources.Select(item => item.Component).ToArray(), Is.EqualTo(new Component[] { secondOverride }));
            Assert.That(unlinkedSources.Single().Components.ToArray(), Is.EqualTo(new Component[] { firstOverride, secondOverride }));
            Assert.That(unlinkedSources.Select(item => item.HierarchyPath).ToArray(), Is.EqualTo(new[] { "Override Stack" }));
            Assert.That(unlinkedSources.Select(item => item.Component).ToArray(), Has.No.Member(firstOverride));
        }

        [Test]
        public void CollectUnlinkedSourcesIncludesExternalOverrideStackTargetingLinkedBase()
        {
            var expression = CreateExpression(_avatarRoot, "Expression");
            var expressionData = expression.gameObject.AddComponent<ExpressionDataComponent>();
            var baseObject = CreateChild(_avatarRoot, "Base Source");
            var baseData = baseObject.AddComponent<BaseExpressionDataComponent>();
            var linkedLocalOverride = baseObject.AddComponent<ExpressionOverrideComponent>();
            var overrideObject = CreateChild(_avatarRoot, "External Override Stack");
            var firstOverride = overrideObject.AddComponent<ExpressionOverrideComponent>();
            var secondOverride = overrideObject.AddComponent<ExpressionOverrideComponent>();
            expressionData.Mode = ExpressionDataMode.Reference;
            expressionData.DataReferences.Add(baseData);
            firstOverride.TargetBase = baseData;
            var expressionItems = ExpressionManagerItemCollector.Collect(_avatarRoot);

            var unlinkedSources = ExpressionManagerItemCollector
                .CollectUnlinkedSources(_avatarRoot, expressionItems)
                .ToArray();

            Assert.That(expressionItems.Single().EditableTargets, Does.Contain(linkedLocalOverride));
            Assert.That(unlinkedSources.Select(item => item.Component).ToArray(), Is.EqualTo(new Component[] { secondOverride }));
            Assert.That(unlinkedSources.Single().Components.ToArray(), Is.EqualTo(new Component[] { firstOverride, secondOverride }));
            Assert.That(unlinkedSources.Select(item => item.Component).ToArray(), Has.No.Member(firstOverride));
            Assert.That(unlinkedSources.Select(item => item.HierarchyPath).ToArray(), Is.EqualTo(new[] { "External Override Stack" }));
        }

        [Test]
        public void CollectUnlinkedSourcesExcludesSourcesAlreadyShownInExpressionTargets()
        {
            var expression = CreateExpression(_avatarRoot, "Smile");
            var expressionData = expression.gameObject.AddComponent<ExpressionDataComponent>();
            var referencedBase = expression.gameObject.AddComponent<BaseExpressionDataComponent>();
            var childSource = expression.gameObject.AddComponent<ExpressionOverrideComponent>();
            var unlinkedBase = CreateChild(_avatarRoot, "Library").AddComponent<BaseExpressionDataComponent>();
            expressionData.DataReferences.Add(referencedBase);
            var expressionItems = ExpressionManagerItemCollector.Collect(_avatarRoot);

            var unlinkedSources = ExpressionManagerItemCollector
                .CollectUnlinkedSources(_avatarRoot, expressionItems)
                .ToArray();

            Assert.That(unlinkedSources.Select(item => item.Component).ToArray(), Is.EqualTo(new Component[] { unlinkedBase }));
            Assert.That(expressionItems.Single().EditableTargets, Does.Contain(referencedBase));
            Assert.That(expressionItems.Single().EditableTargets, Does.Contain(childSource));
        }

        [Test]
        public void FilterUnlinkedSourcesMatchesNamePathOrTypeName()
        {
            var library = CreateChild(_avatarRoot, "Library");
            var baseObject = CreateChild(library, "Happy Base");
            var overrideObject = CreateChild(library, "Override Source");
            baseObject.AddComponent<BaseExpressionDataComponent>();
            overrideObject.AddComponent<ExpressionOverrideComponent>();
            var unlinkedSources = ExpressionManagerItemCollector
                .CollectUnlinkedSources(_avatarRoot, Enumerable.Empty<ExpressionManagerExpressionItem>())
                .ToArray();

            var nameMatches = ExpressionManagerItemCollector.FilterUnlinkedSources(unlinkedSources, "happy").ToArray();
            var pathMatches = ExpressionManagerItemCollector.FilterUnlinkedSources(unlinkedSources, "library/override").ToArray();
            var typeMatches = ExpressionManagerItemCollector.FilterUnlinkedSources(unlinkedSources, "BaseExpression").ToArray();

            Assert.That(nameMatches.Select(item => item.Component.name).ToArray(), Is.EqualTo(new[] { "Happy Base" }));
            Assert.That(pathMatches.Select(item => item.Component.name).ToArray(), Is.EqualTo(new[] { "Override Source" }));
            Assert.That(typeMatches.Select(item => item.Component.name).ToArray(), Is.EqualTo(new[] { "Happy Base" }));
        }

        [Test]
        public void CollectCountsConditionsFromExpressionAncestors()
        {
            var conditionObject = CreateChild(_avatarRoot, "Condition");
            var condition = conditionObject.AddComponent<ConditionComponent>();
            condition.HandGestureConditions.Add(new HandGestureCondition());
            condition.ParameterConditions.Add(ParameterCondition.Bool("Face", true));
            CreateExpression(conditionObject, "Smile");

            var item = ExpressionManagerItemCollector.Collect(_avatarRoot).Single();

            Assert.That(item.ConditionCount, Is.EqualTo(2));
        }

        [Test]
        public void FilterMatchesExpressionNameOrHierarchyPath()
        {
            CreateExpression(CreateChild(_avatarRoot, "Menu"), "Smile");
            CreateExpression(CreateChild(_avatarRoot, "Gesture"), "Angry");
            var items = ExpressionManagerItemCollector.Collect(_avatarRoot).ToArray();

            var nameMatches = ExpressionManagerItemCollector.Filter(items, "smile").ToArray();
            var pathMatches = ExpressionManagerItemCollector.Filter(items, "gesture").ToArray();

            Assert.That(nameMatches.Select(item => item.Expression.name).ToArray(), Is.EqualTo(new[] { "Smile" }));
            Assert.That(pathMatches.Select(item => item.Expression.name).ToArray(), Is.EqualTo(new[] { "Angry" }));
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            return child;
        }

        private static ExpressionComponent CreateExpression(GameObject parent, string name)
        {
            var expressionObject = CreateChild(parent, name);
            return expressionObject.AddComponent<ExpressionComponent>();
        }
    }
}
