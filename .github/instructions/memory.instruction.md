---
applyTo: "**"
---

# User Memory

## User Preferences

- Programming languages: C# (Unity Editor tooling)
- Code style preferences: Modular architecture, clean separation of concerns, reuse existing project UI and logger systems
- Development environment: Unity Editor on Windows, Visual Studio/VS Code
- Communication style: Concise, actionable, prefers autonomous completion

## Project Context

- Current project type: Unity Editor package (HoyoToon)
- Tech stack: Unity Editor, C#; Newtonsoft.Json used
- Architecture patterns: Utilities for logging and dialogs; Resource manager demonstrates patterns
- Key requirements: Implement modular GitHub-based updater under Scripts/Editor/Updater; no hardcoding in single file; use custom logger and dialog UI; preserve features (tree SHA compare, batch updates, retries); support branch switching (default Beta) via menu with warning; per-branch tracker cache

## Coding Patterns

- Preferred patterns and practices: Category-based logging via HoyoToonLogger/HoyoToonLogCore; dialogs via HoyoToonDialogWindow; persistent cache files in package path
- Code organization preferences: Editor code in Scripts/Editor; static managers; separate models/utils/clients
- Testing approaches: Manual via Editor UI; progress windows for long ops
- Documentation style: Inline comments; menu items under HoyoToon/

## Context7 Research History

- Libraries researched on Context7: GitHub REST API Trees and Contents endpoints
- Best practices discovered: Use GET /git/trees/{tree_sha}?recursive=1; contents returns base64; include User-Agent and optional Authorization; use raw.githubusercontent for file bytes
- Implementation patterns used: Compare tree SHA to skip batch rebuild; compute Git blob SHA1 with header; avoid .meta and package.json; support retry on downloads
- Version-specific findings: API version header optional; rate limits 60 unauthenticated, higher with token

## Conversation History

- Decision: Build modular updater: models, client, tracker, controller, window; integrate HoyoToonLogger and HoyoToonDialogWindow; compute package path via package name; add branch selection with EditorPrefs-backed persistence and menu items; default branch set to Beta; per-branch tracker files
- Notes: Use persistentDataPath for tracker JSON; set Updater log category color; avoid Unity compile conflicts by disabling AutoRefresh during apply

## Notes

- Follow existing resource manager’s progress window pattern for long tasks
- Settings are code-only immutable (no ScriptableObject)
