# HoyoToon 0.0.9

## UI
- Fixed an issue regarding Shader parameters inside nested groups not being searchable. 

## Shader
- Fixed an issue where multiple shaders were causing the transparency to fail because they had incorrect blending states
- Outlines now receive lighting colors 
- Fixed an issue where the Shadow Color Grading highlight and shadow were reversed for the areas that were deemed skin. Also fixed the hair being incorrectly attributed to skin. 
- Fixed issue where the Hair Depth texture wasnt utilizing the showbyID

## Pipeline
- Fixed an issue where, when the graphics API is set to DX12, theres a possiblity to get an error about an uninitialized SRV.

## Updater
- Fixed issue where moved or deleted files were not properly being moved/deleted. 