# HoyoToon 0.1.2

## HoyoConverter

- HoyoConverter will attempt to fix models where the variant number is missing from the embedded materials fixing issues such as Huohuo_Mat_Face not having it 00 causing it to not be slotted in by the auto setup.

## Manager
- Fixed an issue where you can duplicate the same model using auto setup.
- Made it so manual copies of the same model will have a number appended to the Active model name to allow them to actually be uniquely selectable in the manager. This is only for manual copies, auto setup will still attempt to reuse the same model if it detects it.