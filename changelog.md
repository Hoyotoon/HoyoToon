# HoyoToon 0.1.9

Memory leak season is here. 

## Rendering
- Fixed an issue where the Reflection Plane Render Feature would still run when there's no condition to trigger it, which could cause performance issues.
- Fixed an issue where the self casting shadow atlas would be allocated even when the controller has no usable self-shadow caster pass, which could cause performance issues and memory waste.
- Fixed an issue where the LightingGBuffer would apply to that aren't the Scene/Game view, which could cause performance issues.
- Added Cached repeated global lighting sync per frame.
- Added Cached Camera Look at Target discovery.
- Fixed post-process/bloom render pass recreation leaks.
- Added generated LUT cleanup.
- Pruned/cleared hair shadow material pass caches.

## Manager
- Added a toggle to display the Weapon of the current active character.
- Fix the invisible window popping up when running auto setup.