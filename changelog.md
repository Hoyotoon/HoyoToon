# API

- Add support for Constraint data

# Scripts

- Removed Validation from HoyoToon Manager
- Updated UI for HoyoToon Manager to look better
- Materials Module has been removed and replaced with Scriptables Module
  - Original script has been emptied and now the scriptable module shows those settings
- Added Renders module
  - Now properly makes transparent images with Post Processing and shadows.
  - Utilizes a custom shader to fix past issues with transparency and shadows
- HoyoToon Manager will now apply bone constraints if available in game metadata during setup
  - Needs to be populated with more findings
- Fixed the mismatch between self casting shadows in Scene View vs Game View
