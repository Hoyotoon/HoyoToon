# HoyoToon 0.3.8

## Setup
- Removed the model conversion step from Auto Setup and the onboarding flow.
- Removed tangent generation from the Auto Setup onboarding copy; tangents are now handled through FBX import settings when configured.
- Added Auto Setup model renaming so detected character FBXs are renamed to the Game Entity Catalog display name before later setup steps run.
- Updated character model name detection to match Game Entity Catalog art names and ignore the `_WithAnims` suffix.
- Kept Auto Setup focused on material generation, FBX import settings, texture import settings, scene placement, and required HSR components.

## Detection
- Switched queued model display names to Game Entity Catalog display names instead of problem-list or JSON filename guesses.
- Fixed HSR character detection for local model folders such as Cerydra by resolving the character from catalog art names and parent folder context.
- Updated queued setup counts to read JSON and material assets from the FBX sibling `Materials` folder.
- Simplified character icon resolution to read icon URLs directly from the Game Entity Catalog and cache those images locally.
- Updated the character icon debug menu to use the catalog icon resolver.

## API
- Added Game Entity Catalog sync support for display names, art name mappings, entity IDs, and icon URLs.
- Added the entity catalog fallback fetch path through the public character, monster, and weapon routes.
- Added model import settings support for API-provided tangent import values.

## Cleanup
- Removed the old character ID icon lookup path in favor of Game Entity Catalog entries.
- Removed stale model converter and HoyoToon converter setup references from the active Auto Setup flow.
- Updated onboarding text so it no longer describes disabled conversion or tangent generation steps.
