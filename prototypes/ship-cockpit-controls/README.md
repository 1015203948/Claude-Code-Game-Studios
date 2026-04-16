# Prototype: Ship Cockpit Controls
**Question**: Which touch control scheme makes ship flight feel most natural on Android?
**Date**: 2026-04-12
**Engine**: Unity 6.3 LTS

---

## Scene Setup (5 minutes)

### 1. Create a new scene
File → New Scene → Basic (Built-in) → Save as `ShipControlsTest`

### 2. Ship GameObject
- Create → 3D Object → Capsule → rename to `Ship`
- Add Component → Rigidbody
  - Use Gravity: OFF
  - Constraints: Freeze Rotation Z (optional — prevents roll)
- Add Component → `ShipController` (from Scripts/)
- Add Component → `InputBridge` (from Scripts/)

### 3. Camera
- Select Main Camera
- Add Component → `CameraFollow`
- Set Target = Ship
- Offset: (0, 4, -10)

### 4. Environment
- Create empty GameObject → rename `Environment`
- Add Component → `EnvironmentSetup`
- Obstacle Count: 5, Ring Radius: 20

### 5. Canvas (UI)
- Create → UI → Canvas
  - Render Mode: Screen Space — Overlay
  - Canvas Scaler: Scale With Screen Size, Reference 1920×1080

#### 5a. Dual Stick Panel (Scheme A)
- Create empty UI Panel → name `DualStickUI`
- Inside: create two Image objects named `LeftBackground` and `RightBackground`
  - Size: 150×150, position left/right sides of screen
  - Add child Image named `Handle` (size 60×60) to each
- Add `VirtualJoystick` component to each background
  - Background = self, Handle = child Handle image
  - Handle Range: 60

#### 5b. Single Stick Panel (Scheme B)
- Duplicate `LeftBackground` → parent to new Panel `SingleStickUI`
- Add `VirtualJoystick` component (same settings)

#### 5c. Tap-To-Move Panel (Scheme C)
- Create Panel `TapMoveUI`
- Add TextMeshPro label: "点击屏幕设定目标点"

#### 5d. Scheme Toggle Button
- Create Button → label "切换方案"
- Add `SchemeToggle` component to any GameObject
  - Wire: Ship, DualStickUI, SingleStickUI, TapMoveUI, scheme label
- Button OnClick → SchemeToggle.NextScheme()

### 6. Wire InputBridge
- Select Ship → InputBridge component
  - Left Joystick = LeftBackground (DualStickUI)
  - Right Joystick = RightBackground (DualStickUI)
  - Ship = Ship
  - Main Camera = Main Camera
  - Tap Raycast Layers = Default

### 7. Build & Deploy to Android
- File → Build Settings → Android
- Player Settings → Minimum API Level: 25 (Unity 6.3 requirement)
- Build and Run

---

## What to Test

For each scheme, answer these questions:

| Question | Scheme A | Scheme B | Scheme C |
|----------|----------|----------|----------|
| Can you navigate between all 5 obstacles without frustration? | | | |
| Does the ship feel like YOU are controlling it? | | | |
| Do you feel "in the cockpit" or "moving a cursor"? | | | |
| Would you want to play a 5-minute session with this? | | | |
| Thumb fatigue after 2 minutes? | | | |

## Tuning Knobs (adjust in Inspector without recompiling)

| Knob | Default | Try |
|------|---------|-----|
| `thrustForce` | 20 | 10–40 |
| `rotationSpeed` | 120 | 60–200 |
| `maxSpeed` | 15 | 8–25 |
| `handleRange` (joystick) | 60px | 40–100px |
| `deadZone` | 0.1 | 0.05–0.2 |
| `linearDamping` (Rigidbody) | 1.5 | 0.5–4.0 |

---

## Files

| File | Purpose |
|------|---------|
| `ShipController.cs` | Movement logic for all 3 schemes |
| `VirtualJoystick.cs` | Floating touch joystick UI |
| `InputBridge.cs` | Routes touch input to ShipController |
| `CameraFollow.cs` | Smooth third-person follow camera |
| `SchemeToggle.cs` | Runtime scheme switcher |
| `EnvironmentSetup.cs` | Procedural obstacle ring |
| `REPORT.md` | Prototype findings |
