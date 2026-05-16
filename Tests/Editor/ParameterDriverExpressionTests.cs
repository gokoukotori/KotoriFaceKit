using System.Collections.Generic;
using System.Linq;
using Aoyon.FaceTune.Importer;
using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Aoyon.FaceTune.Tests
{
    public class ParameterDriverExpressionTests
    {
        private GameObject _avatarRoot = null!;
        private GameObject _faceObject = null!;
        private Mesh _faceMesh = null!;
        private AvatarContext _avatarContext = null!;

        [SetUp]
        public void SetUp()
        {
            _avatarRoot = new GameObject("Avatar");
            _avatarRoot.AddComponent<VRCAvatarDescriptor>();
            _faceObject = CreateChild(_avatarRoot, "Body");
            var faceRenderer = _faceObject.AddComponent<SkinnedMeshRenderer>();
            _faceMesh = CreateFaceMesh("Smile");
            faceRenderer.sharedMesh = _faceMesh;

            var zeroShapes = new BlendShapeWeightSet(new[]
            {
                new BlendShapeWeight("Smile", 0),
            });

            _avatarContext = new AvatarContext(
                _avatarRoot,
                faceRenderer,
                _faceMesh,
                "Body",
                zeroShapes,
                new HashSet<string>(),
                zeroShapes);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_faceMesh);
            Object.DestroyImmediate(_avatarRoot);
        }

        [Test]
        public void ToExpressionCollectsParameterDriversInHierarchyOrder()
        {
            var expression = CreateExpression(_avatarRoot, "SmileExpression");
            var firstDriver = expression.gameObject.AddComponent<ParameterDriverComponent>();
            firstDriver.LocalOnly = true;
            firstDriver.Operations.Add(ParameterDriverOperation.Set("Face/Mode", 1));
            firstDriver.Operations.Add(ParameterDriverOperation.Add("Face/Counter", 2));

            var child = CreateChild(expression.gameObject, "Child Driver");
            var secondDriver = child.AddComponent<ParameterDriverComponent>();
            secondDriver.Operations.Add(ParameterDriverOperation.Random("Face/Random", 0, 3, 0.5f, true));
            secondDriver.Operations.Add(ParameterDriverOperation.Copy("Face/Source", "Face/Destination", true, -1, 1, 0, 100));

            var result = expression.ToExpression(_avatarContext);

            Assert.That(result.ParameterDrivers.Select(driver => driver.LocalOnly).ToArray(), Is.EqualTo(new[] { true, false }));
            Assert.That(result.ParameterDrivers.SelectMany(driver => driver.Operations).ToArray(), Is.EqualTo(new[]
            {
                ParameterDriverOperation.Set("Face/Mode", 1),
                ParameterDriverOperation.Add("Face/Counter", 2),
                ParameterDriverOperation.Random("Face/Random", 0, 3, 0.5f, true),
                ParameterDriverOperation.Copy("Face/Source", "Face/Destination", true, -1, 1, 0, 100),
            }));
        }

        [Test]
        public void MergeExpressionAppendsParameterDrivers()
        {
            var baseExpression = new AvatarExpression(
                "Base",
                Enumerable.Empty<GenericAnimation>(),
                new ExpressionSettings(),
                FacialSettings.Keep,
                new[]
                {
                    new ParameterDriverSettings(true, new[] { ParameterDriverOperation.Set("Face/Base", 1) }),
                });

            var overlayExpression = new AvatarExpression(
                "Overlay",
                Enumerable.Empty<GenericAnimation>(),
                new ExpressionSettings(),
                FacialSettings.Keep,
                new[]
                {
                    new ParameterDriverSettings(false, new[] { ParameterDriverOperation.Set("Face/Overlay", 2) }),
                });

            baseExpression.MergeExpression(overlayExpression);

            Assert.That(baseExpression.ParameterDrivers.SelectMany(driver => driver.Operations).ToArray(), Is.EqualTo(new[]
            {
                ParameterDriverOperation.Set("Face/Base", 1),
                ParameterDriverOperation.Set("Face/Overlay", 2),
            }));
        }

        [Test]
        public void VRChatSupportCreatesParameterDriverBehaviorAndAnimatorParameters()
        {
            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var controller = VirtualAnimatorController.Create(cloneContext, "FX");
            var state = VirtualState.Create("Smile");
            var parameterDrivers = new[]
            {
                new ParameterDriverSettings(true, new[]
                {
                    ParameterDriverOperation.Set("Face/Mode", 1),
                    ParameterDriverOperation.Add("Face/Counter", 2),
                    ParameterDriverOperation.Random("Face/Random", 0, 3, 0.5f, true),
                    ParameterDriverOperation.Copy("Face/Source", "Face/Destination", true, -1, 1, 0, 100),
                }),
            };

            new VRChatSupport().ApplyParameterDrivers(controller, state, parameterDrivers);

            var driver = state.Behaviours.OfType<VRCAvatarParameterDriver>().Single();
            Assert.That(driver.localOnly, Is.True);
            Assert.That(driver.parameters.Select(parameter => parameter.type).ToArray(), Is.EqualTo(new[]
            {
                VRC_AvatarParameterDriver.ChangeType.Set,
                VRC_AvatarParameterDriver.ChangeType.Add,
                VRC_AvatarParameterDriver.ChangeType.Random,
                VRC_AvatarParameterDriver.ChangeType.Copy,
            }));
            Assert.That(driver.parameters.Select(parameter => parameter.name).ToArray(), Is.EqualTo(new[]
            {
                "Face/Mode",
                "Face/Counter",
                "Face/Random",
                "Face/Destination",
            }));
            Assert.That(driver.parameters[0].value, Is.EqualTo(1));
            Assert.That(driver.parameters[1].value, Is.EqualTo(2));
            Assert.That(driver.parameters[2].valueMin, Is.EqualTo(0));
            Assert.That(driver.parameters[2].valueMax, Is.EqualTo(3));
            Assert.That(driver.parameters[2].chance, Is.EqualTo(0.5f));
            Assert.That(driver.parameters[2].preventRepeats, Is.True);
            Assert.That(driver.parameters[3].source, Is.EqualTo("Face/Source"));
            Assert.That(driver.parameters[3].convertRange, Is.True);
            Assert.That(driver.parameters[3].sourceMin, Is.EqualTo(-1));
            Assert.That(driver.parameters[3].sourceMax, Is.EqualTo(1));
            Assert.That(driver.parameters[3].destMin, Is.EqualTo(0));
            Assert.That(driver.parameters[3].destMax, Is.EqualTo(100));
            Assert.That(controller.Parameters.Keys, Is.SupersetOf(new[]
            {
                "Face/Mode",
                "Face/Counter",
                "Face/Random",
                "Face/Source",
                "Face/Destination",
            }));
        }

        [Test]
        public void VRChatSupportImportsParameterDriversFromAnimatorState()
        {
            var state = new AnimatorState();
            var firstDriver = CreateVrcParameterDriver(
                true,
                new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = "Face/Mode",
                    value = 1,
                },
                new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Add,
                    name = "Face/Counter",
                    value = 2,
                });
            var secondDriver = CreateVrcParameterDriver(
                false,
                new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Random,
                    name = "Face/Random",
                    valueMin = 0,
                    valueMax = 3,
                    chance = 0.5f,
                    preventRepeats = true,
                },
                new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Copy,
                    name = "Face/Destination",
                    source = "Face/Source",
                    convertRange = true,
                    sourceMin = -1,
                    sourceMax = 1,
                    destMin = 0,
                    destMax = 100,
                });
            state.behaviours = new StateMachineBehaviour[] { firstDriver, secondDriver };

            var result = new VRChatSupport().GetParameterDrivers(state);

            Assert.That(result.Select(driver => driver.LocalOnly).ToArray(), Is.EqualTo(new[] { true, false }));
            Assert.That(result.SelectMany(driver => driver.Operations).ToArray(), Is.EqualTo(new[]
            {
                ParameterDriverOperation.Set("Face/Mode", 1),
                ParameterDriverOperation.Add("Face/Counter", 2),
                ParameterDriverOperation.Random("Face/Random", 0, 3, 0.5f, true),
                ParameterDriverOperation.Copy("Face/Source", "Face/Destination", true, -1, 1, 0, 100),
            }));
        }

        [Test]
        public void AnimatorControllerImporterCreatesParameterDriverComponentsForImportedExpressions()
        {
            var controller = CreateControllerWithDefaultState("Smile", CreateFacialClip("Smile", 100));
            var state = controller.layers[0].stateMachine.defaultState;
            state.behaviours = new StateMachineBehaviour[]
            {
                CreateVrcParameterDriver(
                    true,
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = "Face/Mode",
                        value = 1,
                    },
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Copy,
                        name = "Face/Destination",
                        source = "Face/Source",
                        convertRange = true,
                        sourceMin = -1,
                        sourceMax = 1,
                        destMin = 0,
                        destMax = 100,
                    }),
            };
            var importRoot = CreateChild(_avatarRoot, "Imported");

            new AnimatorControllerImporter(_avatarContext, controller).Import(importRoot);

            var expression = importRoot.GetComponentInChildren<ExpressionComponent>();
            Assert.That(expression, Is.Not.Null);
            var driver = expression!.GetComponent<ParameterDriverComponent>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(driver!.LocalOnly, Is.True);
            Assert.That(driver.Operations.ToArray(), Is.EqualTo(new[]
            {
                ParameterDriverOperation.Set("Face/Mode", 1),
                ParameterDriverOperation.Copy("Face/Source", "Face/Destination", true, -1, 1, 0, 100),
            }));
        }

        [Test]
        public void AnimatorControllerImporterSkipsParameterDriverOnlyStates()
        {
            var controller = CreateControllerWithDefaultState("DriverOnly", new AnimationClip());
            var state = controller.layers[0].stateMachine.defaultState;
            state.behaviours = new StateMachineBehaviour[]
            {
                CreateVrcParameterDriver(
                    true,
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = "Face/Mode",
                        value = 1,
                    }),
            };
            var importRoot = CreateChild(_avatarRoot, "Imported");

            new AnimatorControllerImporter(_avatarContext, controller).Import(importRoot);

            Assert.That(importRoot.GetComponentsInChildren<ExpressionComponent>().Length, Is.EqualTo(0));
            Assert.That(importRoot.GetComponentsInChildren<ParameterDriverComponent>().Length, Is.EqualTo(0));
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

        private static AnimatorController CreateControllerWithDefaultState(string stateName, AnimationClip clip)
        {
            var controller = new AnimatorController
            {
                name = "FX"
            };
            controller.AddLayer("Face");
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState(stateName);
            state.motion = clip;
            stateMachine.defaultState = state;
            return controller;
        }

        private static AnimationClip CreateFacialClip(string blendShapeName, float weight)
        {
            var clip = new AnimationClip();
            clip.AddBlendShapes("Body", new[] { new BlendShapeWeight(blendShapeName, weight) });
            return clip;
        }

        private static VRCAvatarParameterDriver CreateVrcParameterDriver(
            bool localOnly,
            params VRC_AvatarParameterDriver.Parameter[] parameters)
        {
            var driver = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
            driver.localOnly = localOnly;
            driver.parameters.AddRange(parameters);
            return driver;
        }

        private static Mesh CreateFaceMesh(params string[] blendShapeNames)
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
                normals = new[] { Vector3.back, Vector3.back, Vector3.back },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up },
            };

            var deltaVertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            var deltaNormals = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            var deltaTangents = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            foreach (var blendShapeName in blendShapeNames)
            {
                mesh.AddBlendShapeFrame(blendShapeName, 100, deltaVertices, deltaNormals, deltaTangents);
            }

            return mesh;
        }
    }
}
