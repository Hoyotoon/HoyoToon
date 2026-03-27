# HoyoToon 0.0.8

## UI
- Brought back some of mihoyos custom features as well as a couple new ones for improving the UI for the shaders. 

## Shaders 
- Added the proper Normal Mapping support for new beta characters.
- Created the Shader GUI for all shaders that are currently available (minus post processing). This really needs to be tested and scrutinized
- Created a ComputeShader that one can toggle the usage of in the character tab of the manager. 

## HoyoToon Manager
- Refactored Character and Scene tabs in the manager GUI and got rid of some things that werent necessary. 
- In the Character tab, you can now apply special effect mappings. This will allow you to use the AuraOutline shader that is used on characters like Fat Fuck and Sparxie.

## ScreenShotter
- Refactored it so it uses the camera clear color as the transparency.

## Post Processing
- Change alpha outputs on almost all passes to make the transparency in the screenshots work.


## Onboarding
- Screenshotter functions have been restored so onboarding should work as normal again.