
# THIS IS THE HSR TEST

# Hoyo2VRC
- HoyoToon now has a C++ developed converter tool to convert Hoyoverse Models to be ready to use with HoyoToon in Unity. This fully replaces the Blender version of the tool.
- This tool is called Hoyo2VRC and is included with HoyoToon as a standalone executable.
- Converter is non-destructive and will not modify the original model it's structure, rigging or anything else. It will create a new model with non-destructive changes.
- Hoyo2VRC is based on the API and will do the following changes to the model:
  - Clear Poses
  - Adjust model scale to be 1 unit = 1 meter (scale factor of 100).
  - For Honkai Star Rail models, it will also:
    - Convert animations to shapekeys for the facial expressions.
  - It can do more but those are disabled for now.

# Scene
- HoyoToon will come with it's own scene template that has the proper setup for lighting and post processing to best showcase the shader.
- The Scene will come with the new HoyoToon Manager prefab already in the scene.


# HoyoToon Manager
- The Hoyotoon Manager is a new central component that will manage HoyoToon from a single place.
- The manager will handle things such as:
  - Auto setting up models.
  - Generating Materials
  - Assigning proper Texture settings
  - Converting Models if needed
  - Setting Model Import settings
  - Adding them to the scene properly with the needed components.
  - Generating Tangents
  - Adding and managing the Lights in your scene.
  - Managing global shader/script settings.
  - Managing per-model shader/script settings.
  - Managing Post Processing settings.
- The manager will be going through a lot of changes as a lot of old code is still present.

# Scripts
- Created the new HoyoToon Manager
- Removed unneccasery scripts related to the shader GUI
- Added an automated check to detect if VRChat SDK is installed and which version.



# Post Processing
- HoyoToon now uses Unity's Post Processing Stack v2 to handle effects such as Bloom, Color Grading, and more.
- This will all be setup automatically by the HoyoToon Manager when using the HoyoToon Scene
- Profiles will be made for each game and can be swapped by the HoyoToon Manager.
- Custom profiles can also be made.
- This does allow for it to be used inside of VRChat.
- Current post processing profiles included are:
  - Genshin Impact
  - Honkai Impact 3rd (work in progress)
  - Honkai Star Rail
  - Zenless Zone Zero (work in progress)


# Honkai Star Rail
- Added the full version of the Honkai Star Rail shader based on the latest version of the game.

# Genshin Impact
- Added 20+ shaders 
- Current disabled for auto setup.
