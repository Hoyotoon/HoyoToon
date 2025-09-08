# HoyoToon 0.1.0

## Scripts:

- Added Material Import Settings configuration to `HoyoToonAPIConfig.json` for setting default model import options.
- Added Material Remap configuration to `HoyoToonAPIConfig.json` for remapping materials during model import.
- Added Support for Tangent Generation Mode:
  - `No Tangents`
  - `Tangent Generation`
  - `Vertex Colors`
- Tangents has this new function where if ran on the FBX it'll modify the FBX itself to link to the tangent meshes. Meaning you can Generate Tangents once on the FBX and any future uses of the FBX in the scene will have the tangents already set up.
