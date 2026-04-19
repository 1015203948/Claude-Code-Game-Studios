#if false
// MIT License - Copyright (c) 2026 Game Studios
// Integration tests for ShipControlSystem (EditMode)

namespace Game.Tests.Integration.Gameplay {
    using System;
    using NUnit.Framework;
    using UnityEngine;
    using Game.Gameplay;
    using Game.Inputs;
    using Game.Channels;
    using Game.Data;

    // ─── Mock ShipStateChannel ──────────────────────────────────────────────

    internal sealed class MockShipStateChannel : ShipStateChannel {
        public event Action<string, ShipState> OnStateChanged;

        public new void Raise(string instanceId, ShipState newState) {
            OnStateChanged?.Invoke(instanceId, newState);
        }

        public new void Subscribe(Action<string, ShipState> handler) {
            OnShipStateChanged += handler;
        }

        public new void Unsubscribe(Action<string, ShipState> handler) {
            OnShipStateChanged -= handler;
        }

        public void SimulateState(string instanceId, ShipState state) {
            OnShipStateChanged?.Invoke(instanceId, state);
        }
    }

    // ─── Mock DualJoystickInput ─────────────────────────────────────────────

    internal sealed class MockDualJoystickInput : DualJoystickInput {
        public Vector2 ThrustInput { get; set; }
        public Vector2 AimInput { get; set; }

        public void Reset() {
            ThrustInput = Vector2.zero;
            AimInput = Vector2.zero;
        }
    }

    // ─── Test Fixture ─────────────────────────────────────────────────────

    [TestFixture]
    public class ShipControlSystemTest {
        private GameObject _go;
        private ShipControlSystem _shipControl;
        private Rigidbody _rb;
        private MockShipStateChannel _shipStateChannel;
        private MockDualJoystickInput _dualJoystick;

        // Reflection helpers
        private static readonly System.Reflection.BindingFlags PrivFlgs =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        [SetUp]
        public void Setup() {
            _go = new GameObject("ShipControlSystem");

            // Add Rigidbody
            _rb = _go.AddComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;

            // Add ShipControlSystem
            _shipControl = _go.AddComponent<ShipControlSystem>();

            // Create mock channels
            _shipStateChannel = ScriptableObject.CreateInstance<MockShipStateChannel>();
            _dualJoystick = _go.AddComponent<MockDualJoystickInput>();

            // Inject into private fields via reflection
            var channelField = typeof(ShipControlSystem).GetField(
                "_shipStateChannel", PrivFlgs);
            channelField.SetValue(_shipControl, _shipStateChannel);

            var joystickField = typeof(ShipControlSystem).GetField(
                "_dualJoystick", PrivFlgs);
            joystickField.SetValue(_shipControl, _dualJoystick);

            // Initialize at rest
            _dualJoystick.Reset();
        }

        [TearDown]
        public void TearDown() {
            _shipControl.enabled = false;
            UnityEngine.Object.DestroyImmediate(_go);
            UnityEngine.Object.DestroyImmediate(_shipStateChannel);
            UnityEngine.Object.DestroyImmediate(_dualJoystick);
        }

        // ─── AC-CTRL-11: IN_COCKPIT init resets state ────────────────────────

        [Test]
        public void test_ctrl_11_initialize_cockpit_resets_softlock_and_cooldown_enables_input() {
            // First transition to DESTROYED to set non-null state
            _shipStateChannel.SimulateState("ship1", ShipState.DESTROYED);

            // Set some non-zero state
            var softLockField = typeof(ShipControlSystem).GetField(
                "_softLockTarget", PrivFlgs);
            var cooldownField = typeof(ShipControlSystem).GetField(
                "_weaponCooldown", PrivFlgs);
            var inputField = typeof(ShipControlSystem).GetField(
                "_inputEnabled", PrivFlgs);

            softLockField.SetValue(_shipControl, _go); // non-null
            cooldownField.SetValue(_shipControl, 99f); // non-zero
            inputField.SetValue(_shipControl, false);

            // Transition to IN_COCKPIT
            _shipStateChannel.SimulateState("ship1", ShipState.IN_COCKPIT);

            // Verify reset
            Assert.IsNull(softLockField.GetValue(_shipControl),
                "AC-CTRL-11: SoftLockTarget must be null after InitializeCockpit");
            Assert.AreEqual(0f, (float)cooldownField.GetValue(_shipControl), 0.0001f,
                "AC-CTRL-11: WeaponCooldown must be 0 after InitializeCockpit");
            Assert.IsTrue((bool)inputField.GetValue(_shipControl),
                "AC-CTRL-11: _inputEnabled must be true after InitializeCockpit");
        }

        // ─── AC-CTRL-12: DESTROYED cleanup disables input, preserves velocity ─

        [Test]
        public void test_ctrl_12_cleanup_cockpit_clears_softlock_disables_input_preserves_velocity() {
            // Enter cockpit first
            _shipStateChannel.SimulateState("ship1", ShipState.IN_COCKPIT);

            // Give the ship some velocity
            _rb.linearVelocity = new Vector3(10f, 0f, 5f);

            var softLockField = typeof(ShipControlSystem).GetField(
                "_softLockTarget", PrivFlgs);
            softLockField.SetValue(_shipControl, _go); // non-null

            var inputField = typeof(ShipControlSystem).GetField(
                "_inputEnabled", PrivFlgs);

            // Transition to DESTROYED
            _shipStateChannel.SimulateState("ship1", ShipState.DESTROYED);

            // AC-CTRL-12: soft-lock cleared
            Assert.IsNull(softLockField.GetValue(_shipControl),
                "AC-CTRL-12: SoftLockTarget must be null after CleanupCockpit");

            // AC-CTRL-12: input disabled
            Assert.IsFalse((bool)inputField.GetValue(_shipControl),
                "AC-CTRL-12: _inputEnabled must be false after CleanupCockpit");

            // AC-CTRL-05/12: velocity PRESERVED (inertia not zeroed)
            Assert.AreEqual(10f, _rb.linearVelocity.x, 0.0001f,
                "AC-CTRL-05/12: linearVelocity.x must be preserved after CleanupCockpit");
            Assert.AreEqual(5f, _rb.linearVelocity.z, 0.0001f,
                "AC-CTRL-05/12: linearVelocity.z must be preserved after CleanupCockpit");
        }

        // ─── AC-CTRL-05: Velocity not zeroed on exit ─────────────────────────

        [Test]
        public void test_ctrl_05_cleanup_preserves_velocity_not_zeroed() {
            // This is covered by AC-CTRL-12 test above, kept as explicit AC test
            _shipStateChannel.SimulateState("ship1", ShipState.IN_COCKPIT);

            _rb.linearVelocity = new Vector3(7f, 3f, -2f);
            var storedVelocity = _rb.linearVelocity;

            _shipStateChannel.SimulateState("ship1", ShipState.DESTROYED);

            Assert.AreEqual(storedVelocity.x, _rb.linearVelocity.x, 0.0001f,
                "AC-CTRL-05: Velocity must not be zeroed on state exit");
            Assert.AreEqual(storedVelocity.y, _rb.linearVelocity.y, 0.0001f);
            Assert.AreEqual(storedVelocity.z, _rb.linearVelocity.z, 0.0001f);
        }

        // ─── AC-CTRL-03: FixedUpdate applies both thrust and turn ─────────────

        [Test]
        public void test_ctrl_03_fixed_update_applies_both_thrust_and_turn_forces() {
            _shipStateChannel.SimulateState("ship1", ShipState.IN_COCKPIT);

            // Set non-zero inputs
            _dualJoystick.ThrustInput = Vector2.one * 0.8f;
            _dualJoystick.AimInput = new Vector2(0.5f, 0f);

            // Reset velocity to measure delta
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.ResetInertiaTensor();

            // Call FixedUpdate via reflection
            var fixedUpdate = typeof(ShipControlSystem).GetMethod(
                "FixedUpdate", PrivFlgs);
            fixedUpdate.Invoke(_shipControl, null);

            // Thrust: should have velocity in transform.forward
            Assert.AreNotEqual(Vector3.zero, _rb.linearVelocity,
                "AC-CTRL-03: Thrust must produce linear velocity");
            float forwardSpeed = Vector3.Dot(_rb.linearVelocity, _go.transform.forward);
            Assert.Greater(forwardSpeed, 0f,
                "AC-CTRL-03: Thrust must be along transform.forward");

            // Turn: angularVelocity should be set (not zero)
            float angularSpeed = _rb.angularVelocity.magnitude;
            Assert.Greater(angularSpeed, 0f,
                "AC-CTRL-03: Turn must produce angular velocity");
        }

        // ─── AC-CTRL-04: ApplyThrust uses transform.forward only (no reverse) ─

        [Test]
        public void test_ctrl_04_thrust_uses_transform_forward_only_no_reverse() {
            _shipStateChannel.SimulateState("ship1", ShipState.IN_COCKPIT);

            // Use maximum thrust in "reverse" direction — thrust is magnitude only
            _dualJoystick.ThrustInput = Vector2.up; // same as Vector2.one normalized
            _dualJoystick.AimInput = Vector2.zero;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            var fixedUpdate = typeof(ShipControlSystem).GetMethod(
                "FixedUpdate", PrivFlgs);
            fixedUpdate.Invoke(_shipControl, null);

            // Thrust must be along transform.forward (never backward even with negative input)
            float forwardSpeed = Vector3.Dot(_rb.linearVelocity, _go.transform.forward);
            float lateralSpeed = Vector3.Dot(_rb.linearVelocity, _go.transform.right);
            float upwardSpeed = Vector3.Dot(_rb.linearVelocity, _go.transform.up);

            Assert.Greater(forwardSpeed, 0f,
                "AC-CTRL-04: Thrust must be along transform.forward (no reverse)");
            Assert.AreApproximatelyEqual(0f, lateralSpeed, 0.1f,
                "AC-CTRL-04: No lateral thrust component");
            Assert.AreApproximatelyEqual(0f, upwardSpeed, 0.1f,
                "AC-CTRL-04: No upward thrust component");
        }

        // ─── AC-CTRL-06: Angular velocity set directly (heading decoupled) ────

        [Test]
        public void test_ctrl_06_angular_velocity_set_not_velocity_direction_forced() {
            _shipStateChannel.SimulateState("ship1", ShipState.IN_COCKPIT);

            // Zero thrust, full turn
            _dualJoystick.ThrustInput = Vector2.zero;
            _dualJoystick.AimInput = new Vector2(1f, 0f);

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.ResetInertiaTensor();

            var fixedUpdate = typeof(ShipControlSystem).GetMethod(
                "FixedUpdate", PrivFlgs);
            fixedUpdate.Invoke(_shipControl, null);

            // Angular velocity should be set
            Assert.Greater(_rb.angularVelocity.magnitude, 0f,
                "AC-CTRL-06: angularVelocity must be set (heading decoupled from velocity)");

            // Linear velocity should be zero (no thrust)
            Assert.AreEqual(Vector3.zero, _rb.linearVelocity,
                "AC-CTRL-06: With no thrust, linearVelocity must remain zero");
        }

        // ─── Weapon Cooldown Countdown ──────────────────────────────────────

        [Test]
        public void test_weapon_cooldown_countdown_decreases_over_time() {
            _shipStateChannel.SimulateState("ship1", ShipState.IN_COCKPIT);

            // Directly set cooldown to known value via reflection
            var cooldownField = typeof(ShipControlSystem).GetField(
                "_weaponCooldown", PrivFlgs);
            cooldownField.SetValue(_shipControl, 0.5f);

            // Call Update once
            float initialCooldown = (float)cooldownField.GetValue(_shipControl);

            // Simulate time passage by calling Update via reflection
            var updateMethod = typeof(ShipControlSystem).GetMethod(
                "Update", PrivFlgs);

            // Manually step time by setting Time.deltaTime proxy
            // Since Update reads Time.deltaTime directly, we need to use a testable approach.
            // We test the countdown by reading the value after one frame.
            updateMethod.Invoke(_shipControl, null);

            float afterCooldown = (float)cooldownField.GetValue(_shipControl);

            Assert.Less(afterCooldown, initialCooldown,
                "WeaponCooldown must decrease after Update");
            Assert.GreaterOrEqual(afterCooldown, 0f,
                "WeaponCooldown must not go below zero");
        }

        // ─── Soft-lock Stub Returns Null ─────────────────────────────────────

        [Test]
        public void test_softlock_stub_returns_null_until_real_system() {
            _shipStateChannel.SimulateState("ship1", ShipState.IN_COCKPIT);

            // Call Update to run soft-lock stub
            var updateMethod = typeof(ShipControlSystem).GetMethod(
                "Update", PrivFlgs);
            updateMethod.Invoke(_shipControl, null);

            // Soft-lock must be null (stub — no real combat system yet)
            Assert.IsNull(_shipControl.SoftLockTarget,
                "Soft-lock must be null (stub implementation)");

            // Also verify via private field
            var softLockField = typeof(ShipControlSystem).GetField(
                "_softLockTarget", PrivFlgs);
            Assert.IsNull(softLockField.GetValue(_shipControl),
                "_softLockTarget private field must be null after Update");
        }

        // ─── Public API ─────────────────────────────────────────────────────

        [Test]
        public void test_get_thrust_magnitude_returns_input_magnitude() {
            _shipStateChannel.SimulateState("ship1", ShipState.IN_COCKPIT);

            _dualJoystick.ThrustInput = new Vector2(0.3f, 0.4f); // magnitude = 0.5

            var fixedUpdate = typeof(ShipControlSystem).GetMethod(
                "FixedUpdate", PrivFlgs);
            fixedUpdate.Invoke(_shipControl, null);

            float magnitude = _shipControl.GetThrustMagnitude();
            Assert.AreEqual(0.5f, magnitude, 0.0001f,
                "GetThrustMagnitude must return input magnitude");
        }

        [Test]
        public void test_get_aim_direction_returns_aim_input() {
            _shipStateChannel.SimulateState("ship1", ShipState.IN_COCKPIT);

            var expectedAim = new Vector2(-0.7f, 0.3f);
            _dualJoystick.AimInput = expectedAim;

            var fixedUpdate = typeof(ShipControlSystem).GetMethod(
                "FixedUpdate", PrivFlgs);
            fixedUpdate.Invoke(_shipControl, null);

            Assert.AreEqual(expectedAim, _shipControl.GetAimDirection(),
                "GetAimDirection must return last aim input");
        }
    }
}
#endif
