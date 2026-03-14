# HoyoToon 0.0.0

**Follow the [guidelines](https://discord.com/channels/1129811149416824934/1474289094384418816), and test all the changelog entries before posting.**

First beta release of the new HoyoToon. This will focussed on the the Core Rendering Pipeline, Honkai Star Rail, and the Post Processing for Honkai Star Rail.
Since this is the first release, you'll require to install this as if it's the first time, `Clean install the package into a dedicated project` and follow the onboarding process. If you have any issues, please report them in the forum post.
**Further releases will be shipped through the updater, so you won't need to do a clean install every time, unless mentioned otherwise.**

## Core
- Created the new Custom Rendering Pipeline (Honkai Star Rail only for now)
    - Completely replaces the default Unity Built in Rendering Pipeline.
    - Built ontop of URP.
    - Since our focus is for Mihoyo only some basic Unity features may not work as intended or are missing. Report any issues you find.
- HoyoToon currently only supports `Unity 6 (6.3 LTS)`.
- Won't go into too many technical details for the Rendering Pipeline, as this all happens under the hood, but if you want to know more about it, feel free to ask in the forum post.
- If you are an older HoyoToon user, please take benchmarks of the old version vs the new version and report any performance differences you find.
- **Entire Codebase has been refactored and rewritten from the ground up.**
- Removed `Wuthering Waves` Big I know right!
- There's a dedicated `HoyoToon` Scene now. Which is the preferred way to use HoyoToon all it's required components are setup and ready to go. It'll also be setup to be the same as the Mihoyo games.
- Due to now properly handling things we are now also utilizing Anti-Aliasing and Render scale (`MSAA 8x` and `2 Render Scale` by default) which should make the renders look a lot better than before. This will only show in Game View and in renders.
- All downloaded or generated assets will now be stored in a `HoyoToon` folder inside the Assets folder, and will be organized by game and character. This is to keep things organized and easy to find.


## Hoyo2VRC
- Hoyo2VRC is now built-in to HoyoToon. It's no longer an adddon that requires blender or preprocessing to work.
- When running Auto Setup, Hoyo2VRC will run in the background and automatically process your model for you.
- It is completely non-destructive, and will not modify your original model in any way. It will create a processed model, which will then be used further.
- Hoyo2VRC will do the following:
    - Non-destructive conversion
    - Rig remains the same including it's original armature and bone names.
    - Driven by the API so finding and fixing issues with models will be easier and faster to implement.
    - Modular feature calling so in the future if I expose the settings in the UI, you can choose what to run on the model and what not to run.
    - Remove Empties
    - Generate Shapekeys for Honkai Star Rail `_With_Anims Models`
    - Rename exported FBX to it's stripped character name.
    - Tags the processed model with a custom tag so you can easily find it in your project.
    - Can do more but it's disabled atm since it's not needed currently.

## Onboarding
- Added a new onboarding process for first time users, which will guide them through the basics of HoyoToon and how to use it. This is a very basic onboarding process, but it will be improved in future releases. 
- Currently it'll guide you through the following
  - Prerequisites
  - Resources
  - Downloading your first model
  - Setting up a model with HoyoToon
  - A simple show and tell of the UI
- If you have any suggestions for the onboarding process, please let us know in the forum post.


# Honkai Star Rail
- Added Honkai Star Rail as a supported game. This includes the following:
  - Shaders
  - Materials
  - Textures
  - Model setup with Hoyo2VRC
  - Post Processing profiles
- Feel free to make renders and try as many characters as you want, and report any issues you find in the forum post. Since this is the first release, there might be some issues with the shaders and materials, so please report them if you find any.

## Materials
- Material Generation/Detection has been completely rewritten and is now smarter. 
  - It can now automatically find and detect the jsons regardless of where they are as long as it's in the root folder of the character.
  - Generation has also been made a lot smoother as batching is now implemented, so it'll generate all materials at once instead of one by one. 
  - UI will no longer spam you during material generation due to the batching, and it'll be faster than before.

## Textures
- Similar to materials, texture generation/detection has also been rewritten and is now smarter. 
  - It can now automatically find and detect the textures regardless of where they are as long as it's in the root folder of the character.
  - Generation has also been made a lot smoother as batching is now implemented, so it'll generate all textures at once instead of one by one. 
  - UI will no longer spam you during texture generation due to the batching, and it'll be faster than before.

## Models
- Due to Hoyo2VRC being part of HoyoToon now, the model setup process has been made a lot smoother and easier.
- When you run the Auto Setup, it'll automatically process your model with Hoyo2VRC and set it up for you.


## Scene 
- You might notice new scripts on models that are added to the scene, this is because of the new rendering pipeline, and it's required for the rendering pipeline to work.
- You can ignore the ones on the models themselves, as they're exposed for inside of the HoyoToon Manager.
- Models will also have Twist bone constraints added to them.


## Resources
- Resource download and management is slighltly faster and handles batching better.


## HoyoToon Manager
- The HoyoToon Manager has been completely redesigned and rewritten.
- The Manager will now be part of the base `HoyoToon` Scene, and will be setup with the required components and references to work out of the box.
- The Onboarding process will guide you through the basics of the Manager and how to use it, but if you have any questions, feel free to ask in the forum post.
- Current Manager Features:
  - **Setup**
    - Auto Setup (Runs Hoyo2VRC and sets up the model for you)
    - Supports FBX selection in project
    - Supports folder selection
    - Supports batch processing
    - Supports Drag and Drop
    - Select active model in scene

  - **Main**
    - Model Info (Name, Materials, Textures, FBX Settings)

  - **Models**
    - Built in model downloader from the CDN
    - Game, Character, and Variant selection
    - Automatically downloads and with `Auto Setup after download` option enabled will automatically process the model after download.

  - **Character**
    - Character scriptables. It's pretty advanced so feel free to ask in the forum post if you have any questions about it.

  - **Scenes**
    - Scene scriptables. Similar to the Character scriptables, it's pretty advanced so feel free to ask in the forum post if you have any questions about it.

  - **Lighting**
    - Lighting related controls
    - Create lights
    - Manage lights
    - Reposition lights
    - Auto Rotate lights

  - **Post Processing**
    - Post Processing controls.

  - **Renders**
    - Create Renders
    - Toggle Transparency
    - Syncronize the Main Camera with the Scene Camera. This allows you to move around in the scene and have the main camera mirror your movements.
    - Custom Resolution and Scale
    - Watermark toggle. This adds a small HoyoToon watermark to the corner of your renders, similar to the one in the Mihoyo games. It's not intrusive and is only there to show off that the renders were made with HoyoToon.

  - **Footer**
    - Global Features
        - Create Prefab
        - Regenrate Materials 

- If there's any features you'd like to see in the Manager, please let us know in the forum post.

## Updater
- A completely new updater system has been implemented, which will allow for easier updates in the future.
- The updater is fully connected to Github and will automatically check for updates when we push a new release.
- The updater no longer relies on Unity Packages and can patch and update individual files, which allows for smaller and faster updates in the future.

## Simulator
- If some of ya'll remember an old abandoned project I had called HoyoToon Simulator. It's now minimally integrated into the new HoyoToon as a testing ground. 
- Currently it recreates the Genshin Character Screen camera system and will be automatically enabled when you enter play mode in the HoyoToon Scene.
- It's not fully fleshed out yet, but it'll be improved in future releases and will be a great way to check out your character and make cool renders.
- Currently it supports:
    - Camera controls (Orbit, Rotate, Zoom) (`Right mouse to activate camera controls, Middle mouse to zoom`)
    - Focus zoom on a specific point (`Alt + Middle mouse`)
    - Auto Rotate (`R`)
    - Projection switching (Perspective and Orthographic) (`Tab`)
    - Swapping views (Front, Back, Left, Right, Top, Bottom) (`Numpad keys 1, 3 7 and Ctrl + those for the opposite view`)
    - Automatic transitioning between the character and face upon zooming in close enough to the character's face, similar to the Mihoyo games.


## UI 
- Removed renders of characters from the UI to make it uniform.