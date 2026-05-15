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
