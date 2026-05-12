# HoyoToon 0.3.7

## Simulator
- Changed runtime UI shortcuts to a non-modifier scheme: `F1` toggles the help bar and `F2` hides all UI.
- Updated the HoyoToon input action asset with the new `F1` and `F2` bindings and removed legacy `UnityEngine.Input` hotkey polling.
- Updated help-bar shortcut labels so the row now shows separate `F1` Hide Help and `F2` Hide UI hints.
- Added separate `Key F1` and `Key F2` textures based on the existing key sprite style and switched the help bar prefab to those authored icons.
- Kept help-bar shortcut text authored in the prefab instead of rewriting shortcut labels at runtime.
- Kept UI hint rows prefab-only (no runtime-generated hint GameObjects) and made help-bar visibility resolution resilient to nested or inactive instances.
- Ensured the all-UI shortcut explicitly deactivates the `HelpBar` GameObject as part of the full UI visibility toggle.
- Added an Input System keyboard fallback for `F1`/`F2` and component-based HelpBar resolution so `F1` reliably toggles the actual `HelpBar` GameObject.
- Updated help-bar detection to resolve the `HelpBar` `GameObject` through its UI component first, then scene/name fallbacks.
- Split input and simulator responsibilities: `InputManager` now only emits input events, while a new simulator-side
  shortcut controller handles the actual UI visibility state and HelpBar toggling behavior.
- Cleaned up redundant `HoyoToon` prefixes from runtime script names and types (and updated serialized Unity references accordingly), including:
  `InputManager`, `PlanarReflection`, `CharacterRowUI`, `CharacterSlotUI`, `HelpBarUI`, `ResourcesSO`, and associated runtime enum/type references.