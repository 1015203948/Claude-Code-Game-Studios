# Accessibility Requirements — 星链霸权 (Starchain Hegemony)

> **Tier**: Standard
> **Platform**: Android（手机 / 平板）
> **Last Updated**: 2026-04-14
> **Reference**: This document is referenced by all UX specs (`design/ux/`) and
> the Control Manifest (`docs/architecture/control-manifest.md`).

Standard tier covers the baseline requirements for a commercial mobile game:
colorblind support, motion reduction, touch target sizing, and basic cognitive
load controls. Features beyond this tier are deferred to post-launch.

---

## Tier Definition

| Tier | This Project |
|------|-------------|
| Basic | ✅ Included |
| **Standard** | ✅ **Selected — this tier** |
| Comprehensive | ⬜ Deferred (post-launch) |
| Exemplary | ⬜ Out of scope |

---

## Feature Matrix

### 1. Visual — Color & Contrast

| Feature | Requirement | Priority | Status |
|---------|-------------|----------|--------|
| UI text contrast ratio | ≥ 4.5:1 (WCAG AA small text) | REQUIRED | Not started |
| Large text / icon contrast | ≥ 3:1 (WCAG AA large text) | REQUIRED | Not started |
| Colorblind mode — Protanopia / Deuteranopia | Star map node ownership colors must use shape + pattern in addition to color | REQUIRED | Not started |
| Colorblind mode — Tritanopia | HUD alert colors (energy deficit warning) must not rely on color alone | REQUIRED | Not started |
| Color-independent state indicators | All critical states (node ownership, ship state, resource deficit) must have a non-color indicator (icon, shape, label) | REQUIRED | Not started |

> **Why this matters for 星链霸权**: The star map uses color-coded nodes to convey
> ownership state (player / enemy / neutral / unexplored). This is core gameplay
> information — colorblind players must be able to read it without relying on hue.

### 2. Motion & Animation

| Feature | Requirement | Priority | Status |
|---------|-------------|----------|--------|
| Reduce motion option | Toggle in Settings to disable / reduce scene transition animations (dual-perspective switch), node pulse animations, and fleet movement trails | REQUIRED | Not started |
| No mandatory motion for progress | Game must be completable with all animations disabled | REQUIRED | Not started |
| Flicker / strobe | No flashing content > 3 Hz that covers > 25% of the screen | REQUIRED | Not started |

> **Implementation note**: The dual-perspective switching transition (ADR-0001)
> must respect the reduce-motion toggle. When enabled, switch instantly rather
> than playing a camera blend / fade animation.

### 3. Touch & Input

| Feature | Requirement | Priority | Status |
|---------|-------------|----------|--------|
| Minimum touch target size | All interactive elements ≥ 48 × 48 dp (Android Material guidance) | REQUIRED | Not started |
| Touch target spacing | Adjacent targets separated by ≥ 8 dp to prevent mis-taps | REQUIRED | Not started |
| No time-limited inputs | No game mechanic requires a tap within a mandatory time window that cannot be adjusted | ADVISORY | Not started |
| Joystick dead zone configurable | `JOYSTICK_DEAD_ZONE` exposed in Settings (default 0.08, range 0.04–0.20) | REQUIRED | Not started |

> **Implementation note**: The virtual joystick dead zone is already defined as a
> configurable constant in ADR-0003 (`JOYSTICK_DEAD_ZONE = 0.08f`). The Settings
> screen must expose this value.

### 4. Text & Readability

| Feature | Requirement | Priority | Status |
|---------|-------------|----------|--------|
| Minimum font size | Body text ≥ 14sp; labels ≥ 12sp | REQUIRED | Not started |
| System font scale support | UI layout must not break at Android font scale 1.3× | REQUIRED | Not started |
| No text conveyed by style alone | Important information must not rely on bold / italic / color alone | REQUIRED | Not started |
| Language: Simplified Chinese | Primary UI language; all strings must be externalized (no hardcoded Chinese) | REQUIRED | Not started |

### 5. Audio

| Feature | Requirement | Priority | Status |
|---------|-------------|----------|--------|
| Separate volume controls | Music volume and SFX volume independently adjustable | REQUIRED | Not started |
| No audio-only information | Critical game information (combat start, resource warning) must have a visual indicator in addition to audio | REQUIRED | Not started |

### 6. Cognitive Load

| Feature | Requirement | Priority | Status |
|---------|-------------|----------|--------|
| Pause available at any time | Game must be pausable from both star map and cockpit views | REQUIRED | Not started |
| No real-time penalties for pausing | Economy and fleet timers may pause when game is paused (configurable via `Time.timeScale`) | ADVISORY | Not started |
| Confirmations for destructive actions | Scrapping a ship or abandoning a colony requires a confirmation dialog | REQUIRED | Not started |
| Tutorial skip | All tutorial prompts must be skippable | REQUIRED | Not started |

---

## Out of Scope (Standard Tier)

The following features are explicitly deferred to post-launch or a future
Comprehensive tier upgrade:

- Screen reader / TalkBack support (Android Accessibility Service)
- Full controller / keyboard remapping
- Subtitles / captions (no voiced dialogue in MVP)
- Motor accessibility (dwell selection, switch access)
- Cognitive difficulty scaling (enemy speed reduction, extended timers)

---

## Implementation Notes for Engineers

1. **Colorblind mode**: Implement as a global `AccessibilitySettings.ColorblindMode` enum (`None`, `Protanopia`, `Deuteranopia`, `Tritanopia`). The StarMap node renderer and HUD alert system must read this value.
2. **Reduce motion**: Implement as `AccessibilitySettings.ReduceMotion` bool. The `ViewLayerManager` (ADR-0001) transition sequence checks this flag — if true, skip the blend animation.
3. **Settings persistence**: All accessibility settings persist to `Application.persistentDataPath` independently of save files.
4. **Touch targets**: The 48dp minimum is already required by TR-starmap-ui-002. This document extends the requirement to all interactive elements across all screens.
5. **Dead zone exposure**: ADR-0003 defines `JOYSTICK_DEAD_ZONE = 0.08f` as a configurable constant. The Settings screen must bind to this value with a slider (range 0.04–0.20).

---

## Acceptance Criteria

- **AC-A11Y-01**: In Protanopia colorblind mode, all star map nodes are distinguishable without relying on color (shape/pattern/label present).
- **AC-A11Y-02**: With Reduce Motion enabled, dual-perspective switching completes instantly with no animation.
- **AC-A11Y-03**: All interactive elements measure ≥ 48 × 48 dp on a 1080p Android device.
- **AC-A11Y-04**: UI remains usable at Android system font scale 1.3× (no text clipping or layout overflow).
- **AC-A11Y-05**: Joystick dead zone slider in Settings correctly adjusts `JOYSTICK_DEAD_ZONE` and persists across sessions.
- **AC-A11Y-06**: Energy deficit warning displays a visual icon in addition to playing the audio alert.
- **AC-A11Y-07**: Game can be paused from both star map view and cockpit view at any time.
