using System;
using System.Reflection;
using MagnetPanic.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MagnetPanic.Combat.Tests
{
    public sealed class PlayerMovementCameraContractTests
    {
        static readonly Type MotorType = RequireType("MagnetPanic.Combat.ArkhamPlayerMotor, Assembly-CSharp");

        [Test]
        public void GameInputProvider_SprintPressedReflectsHeldState()
        {
            GameObject owner = new GameObject("Sprint Input Contract");
            try
            {
                GameInputProvider provider = owner.AddComponent<GameInputProvider>();
                provider.SetState(GameInputState.Gameplay);

                provider.SetSprintPressed(true);
                Assert.That(provider.SprintPressed, Is.True);

                provider.SetSprintPressed(false);
                Assert.That(provider.SprintPressed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PlayerMotor_SprintAndAccelerationCombineIntoEffectiveSpeed()
        {
            GameObject cameraObject = CreateCamera("Movement Camera", new Vector3(0f, 10f, -8f));
            GameObject player = CreatePlayer("Movement Player", cameraObject.GetComponent<Camera>());
            try
            {
                Component motor = player.GetComponent(MotorType);
                GameInputProvider provider = player.GetComponent<GameInputProvider>();

                SetMoveAxis(provider, Vector2.up);
                provider.SetSprintPressed(true);
                MotorType.GetProperty("Acceleration")?.SetValue(motor, 0.8f);

                float effectiveSpeed = (float)MotorType.GetProperty("EffectiveSpeed")?.GetValue(motor);

                Assert.That(effectiveSpeed, Is.EqualTo(5f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void PlayerMotor_LockSuppressesInputButKeepsProgrammaticDisplacement()
        {
            GameObject cameraObject = CreateCamera("Lock Camera", new Vector3(0f, 10f, -8f));
            GameObject player = CreatePlayer("Lock Player", cameraObject.GetComponent<Camera>());
            try
            {
                Component motor = player.GetComponent(MotorType);
                GameInputProvider provider = player.GetComponent<GameInputProvider>();

                SetMoveAxis(provider, Vector2.up);
                MotorType.GetMethod("SetMovementLocked")?.Invoke(motor, new object[] { true, false });
                SendUpdate(motor);

                Vector3 afterInput = player.transform.position;
                Assert.That(new Vector2(afterInput.x, afterInput.z).magnitude, Is.EqualTo(0f).Within(0.001f));

                Vector3 worldMoveDirection = (Vector3)MotorType.GetProperty("WorldMoveDirection")?.GetValue(motor);
                Assert.That(worldMoveDirection.sqrMagnitude, Is.GreaterThan(0.01f));

                MotorType.GetMethod("MoveController")?.Invoke(motor, new object[] { Vector3.right });
                Assert.That(player.transform.position.x, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        static GameObject CreateCamera(string name, Vector3 position)
        {
            GameObject cameraObject = new GameObject(name);
            cameraObject.transform.position = position;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.aspect = 16f / 9f;
            return cameraObject;
        }

        static GameObject CreatePlayer(string name, Camera sceneCamera)
        {
            GameObject player = new GameObject(name);
            player.transform.position = Vector3.zero;
            player.AddComponent<CharacterController>();
            GameInputProvider inputProvider = player.AddComponent<GameInputProvider>();
            Component motor = player.AddComponent(MotorType);
            inputProvider.Configure(sceneCamera);
            MotorType
                .GetMethod("Configure", new[] { typeof(Camera), typeof(Animator), typeof(float) })
                ?.Invoke(motor, new object[] { sceneCamera, null, 5f });
            return player;
        }

        static void SetMoveAxis(GameInputProvider provider, Vector2 axis)
        {
            typeof(GameInputProvider)
                .GetField("moveAxis", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(provider, axis);
        }

        static void SendUpdate(Component behaviour)
        {
            behaviour.SendMessage("Update", SendMessageOptions.RequireReceiver);
        }

        static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName);
            Assert.That(type, Is.Not.Null, $"Missing type {typeName}");
            return type;
        }
    }
}
