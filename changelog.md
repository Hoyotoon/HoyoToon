# HoyoToon 0.3.5

## Simulator
- Added a dedicated UI for Game View.
- Character Icons will now show in the UI and allow you to select and change characters by clicking on them.
- Bottom Help bar with current controls and a FPS counter.
- Added a toggle for showing the UI in the Game View. `H`

## Scene And Lighting
- Added `Sync Light to Camera Rotation` for the HSR scene main light and manager light motion controls.
- Syncing a light to the camera now temporarily locks the camera-relative source yaw to `180` and restores the previous light rotation when sync is disabled.
- Persisted the Render tab `Sync With View` setting across Game View fullscreen and layout changes.

## Rendering
- Transparent renders now hide simulator UI during capture and restore it afterwards.
- Updated HSR post-processing, tone mapping, and manikin UI floor/shadow shader support.
