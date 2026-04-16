# Prototype: Cockpit Controls (飞船驾驶舱操控)

**Question**: Does dual virtual joystick spaceship control feel responsive and
fun enough on Android touch to support the "commanding a warship" player fantasy?

**Date**: 2026-04-14
**Status**: Ready to test

---

## Setup (5 minutes)

1. Create a new Unity scene: `prototypes/cockpit-controls/Scenes/CockpitProto.unity`
2. Add a 2D Sprite (or capsule) as the ship. Add `Rigidbody2D` component.
3. Create an empty GameObject named `[InputManager]`. Add `DualJoystickInput.cs`.
4. Add `ShipController.cs` to the ship GameObject.
   - Drag the `[InputManager]` GameObject into the `JoystickInput` field.
5. Create two UI Canvas → Image circles for joystick visuals (optional).
   - Add `JoystickVisual.cs` to each, assign to Left/Right slot in `DualJoystickInput`.
6. Add a Particle System to the ship (child object, pointing backward) for engine trail.
   - Assign it to `ShipController.ThrustParticles`.
7. Set `Rigidbody2D.gravityScale = 0`.
8. Build to Android or test in Editor with mouse (simulated touch).

## Files

| File | Purpose |
|------|---------|
| `Scripts/DualJoystickInput.cs` | Touch input — two-finger joystick tracking |
| `Scripts/ShipController.cs` | Ship movement via Rigidbody2D.AddForce |
| `Scripts/JoystickVisual.cs` | UI circle that follows finger position |

## Tuning Values (all hardcoded — easy to tweak during testing)

| Value | Default | Located in |
|-------|---------|-----------|
| Thrust power | 8f | `ShipController.ThrustPower` |
| Rotation speed | 180f | `ShipController.RotationSpeed` |
| Linear damping | 1.5f | `ShipController.LinearDamping` |
| Dead zone | 0.08f | `DualJoystickInput.DeadZone` |
| Joystick radius | 80px | `DualJoystickInput.JoystickRadius` |

## What to Observe

- Does the ship feel like it has weight? (AddForce should feel floaty-but-responsive)
- Is 0.08f dead zone right? (finger resting on screen shouldn't drift)
- Can you control thrust + aim simultaneously with two thumbs?
- Does the visual joystick knob track naturally?

## DO NOT

- Import any scripts from `src/` — this is isolated throwaway code
- Use this code in production — if PROCEED, rewrite from scratch against ADR-0003
