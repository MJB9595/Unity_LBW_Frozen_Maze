# Plan: Lakitu-Style Camera Upgrade (SM64 Style)

This plan outlines the steps to transform the `NormalCam` into a smooth, natural-following camera inspired by Super Mario 64's Lakitu camera, while ensuring compatibility with the `WallMerge` system.

## 1. Goal
- **Natural Tracking**: Smooth "slack" follow behavior (not glued to the player).
- **Correct Perspective**: Higher viewpoint looking down, player framed in the lower third of the screen.
- **Smooth Obstacle Handling**: Avoid sharp snapping when hitting walls.
- **System Integrity**: Must not break the `WallMerge` 3D/2D transition.

## 2. Implementation Steps

### Step 1: Component Re-selection & Positioning
- **Component**: Re-evaluate `CinemachineOrbitalFollow` vs `CinemachineThirdPersonFollow`. 
  - *Decision*: Use `CinemachineOrbitalFollow` for its natural rotation lag and orbital feel.
- **Radius & Height**: Set Radius to ~9.0.
- **Framing**: Adjust `CinemachineRotationComposer`'s Screen Y to ~0.6-0.7 to push the player down in the viewport.

### Step 2: Smoothing (The "Lakitu" Slack)
- **Position Damping**: Set damping values to ~1.0-1.5 on all axes.
- **Rotation Damping**: Set damping to ~0.8 to make the look-at behavior smooth.
- **Recentering**: Enable horizontal and vertical recentering so the camera eventually settles behind the player.

### Step 3: Refined Collision (Deocclusion)
- **Component**: `CinemachineDeoccluder`.
- **Smoothing**: Increase `SmoothingTime` (if available in this version) or use a strategy that pulls forward smoothly.
- **Radius**: Keep a small `CameraRadius` (0.2-0.3) to prevent clipping.

### Step 4: Integration & Validation
- **WallMerge Hook**: Ensure `NormalCam` is correctly referenced in the `WallMerge` script.
- **Visual Validation**: Use `Capture2DScene` or `CaptureMultiAngleSceneView` to verify the new framing.
- **Console Check**: Verify no errors or warnings.

## 3. Success Criteria
- Player is visible in the lower third of the screen.
- Camera follows with a noticeable but pleasant lag.
- Transitioning to `WallCam` (2D) still works perfectly.
- Camera doesn't "snap" violently when the player runs into a corner.
