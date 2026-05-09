# HoyoToon 0.3.4

## Scripts
- Fixed render watermark compositing so screenshots add the watermark after capture without darkening it on transparent outputs.
- Restored play-mode Q/E model swapping by auto-adding a simulator placement input controller when scenes only contain the placement controller.
- Routed placement keyboard shortcuts through an editor shortcut-focus check so they work from Game View or the HoyoToon Manager without firing while editing text.
- New Emoji Controller
    - Characters with anims will now have their default face loaded.
    - Characters with anims will now auto blink.
    - Configuration is stored as a local only ScriptableObject in `Scriptables > (Game) > Config > EmojiControllerProfile`
- New Look At Controller
    - Characters can now track the active camera with configurable per-game constraints and eye follow settings.
    - Honkai Star Rail auto setup now assigns the look-at profile automatically.
    - Pressing `F` in play mode toggles character look-at on or off through `HoyoToon.inputactions`.
    - Runtime look-at logic was cleaned up to remove debug-only work and keep per-frame tracking performant.
- Added a HoyoToon input manager so camera, character switching, look-at toggles, and future runtime inputs are driven from one shared input owner.
