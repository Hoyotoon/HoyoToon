# HoyoToon 0.0.5

**Follow the [guidelines](https://discord.com/channels/1129811149416824934/1474289094384418816), and test all the changelog entries before posting.**


## Scene
- Updated Camera to use Skybox instead of Solid Color as the default background.


## HoyoToon Manager
- Removed the `Apply` button from the Post Processing module since changes are now applied immediately.


## Honkai Star Rail
- Set Self shadow to be enabled by default in the face shader.


## Updater
- Update failure dialog now informs users that downloads are staged locally and the updater will try to resume on the next editor startup, improving clarity on the update process after a failure.
- Updates files are staged first > suppress refreshing and reloading the editor until the final install pass, which should reduce the chance of partial local changes if a failure occurs during the update process.