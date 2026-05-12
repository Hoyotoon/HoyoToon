# HoyoToon 0.3.6

## Simulator
- Updated the Character Row to use bounded elastic scrolling instead of a hard-clamped edge.
- Limited Character Row overscroll by the amount of character visibility at the row edge, matching the in-game behavior more closely.
- Reduced runtime UI churn by avoiding redundant row, slot, FPS, and version text updates.
- Cached simulator character icon path resolution and generated runtime icon sprites to avoid repeated asset scans and texture work.

## Manager
- Fixed manager-wide UI refresh timing that could make controls feel like they needed to be clicked twice.
- Fixed Add Model, dropdown, toggle, and button actions so discrete UI changes register immediately instead of waiting for a second interaction.
- Kept batch Auto Setup placement sync in lockstep so newly added models update the active character as well as the managed model roster.
- Prevented active module content from rebuilding when reselecting the already-open tab.
- Ensured modules enter their selected state before their UI content is created.
- Reduced unnecessary Assets tab UI rebuilds during its polling loop.
- Cached manager header character splash lookups to avoid repeated detection and icon-cache work for the same active model.
- Persisted `Sync With Scene View` through manager rebuilds, layout changes, and Game View fullscreen transitions.
- Applied the same sync persistence to the standalone Screenshot Tool.
