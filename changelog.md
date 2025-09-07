# HoyoToon 0.0.6

## Scripts:

- Early version of `MaterialDetection` utility added for standalone detection of game and shader from JSON data.
- Updated `IJsonParsingService` and `Utf8JsonParsingService` to use vendored Utf8Json for high-performance JSON parsing.
- Updated `JsonConfigService` to read `Resources` key in `HoyoToonAPIConfig.json` for game configurations.
- Menu action added under `HoyoToon/Detect Game Shader` for testing material detection from selected JSON or multiple.
- Removed Newtonsoft.Json dependency. Utf8Json is now vendored in `Scripts/Editor/Utf8Json.dll`.
