# HoyoToon 0.3.1


## Manager
- Fixed an issue where the manager could no longer be scrolled if the window was too small.
- Released the active manager module before rebuilding the module list so editor update handlers cannot survive a UI rebuild.
- Disposed onboarding input blockers, paused detached overlay refresh schedules, and kept onboarding target registry callbacks from retaining UI elements.
- Added unload/quit cleanup and an LRU cap for generated remote avatar textures.
- Fixed an issue where Character Icons could fail being set to sprites.
- Fixed an issue where the Model Queue would not empty when running the auto setup.
- Deferred manager refreshes during active manager value edits so sliders and toggles are not interrupted mid-change, and prevented child toggles from overwriting foldout section state.
- Fixed the manager value-edit guard compile error by importing UnityEditor UIElements field types.
- Stopped nested checkbox changes from bubbling into manager foldout headers so toggling a property no longer collapses its section.

## Package
- Marked the dev editor assembly as Editor-only so development tooling cannot be compiled into runtime player targets.
- Preserved updater and resource-sync rollback backups when rollback itself fails, and reports the backup path for manual recovery.
- Marked the editor assembly as Editor-only and renamed the asmdef to match the editor assembly name.
- Split runtime asmdefs so optional rendering, simulator, scene, and data dependencies are isolated.
- Removed generated package-local check DLLs from Temp so Unity no longer imports duplicate HoyoToon assemblies.
- Removed dead LWGUI display-mode locals that produced editor compile warnings.

## Runtime
- Gated Lighting GBuffer enqueueing behind a cached scene activity check so cameras without registered HSR lighting work skip full-resolution GBuffer target setup.
- Fixed the Lighting GBuffer scene activity gate compile error by defining the `LightMode` shader tag used for material checks.
- Restored cached legacy receiver discovery for planar reflections and manikin floor shadows so existing scenes without marker components continue rendering while marked scenes use explicit registration.
- Confirmed the runtime compatibility fallback uses runtime-only APIs with no `UnityEditor` references or `UNITY_EDITOR` branches.
- Changed CustomRPTransparentBuffer snapshot invalidation to scene/controller dirty versions and kept cached draw items renderable through execute-time active checks.
- Switched hair, manikin, and self-shadow renderer discovery to the registered HSR controller path so runtime rendering no longer falls back to scene-root discovery.
- Reduced compute skinning rebuild allocations by reusing mesh channel scratch lists instead of pulling vertex, normal, tangent, and UV channels through fresh managed arrays.
- Removed direct UnityEditor usage from the runtime Environment Manager by routing prefab instantiation and undo registration through RuntimeEditorBridge.
- Scoped HSR scene light, character-light registry, render globals, and scene controller resolution to the owning scene for additive scene workflows, with an active-scene fallback for Edit Mode SceneView rendering.
- Guarded scene-scoped HSR and placement lookups while scenes are unloading so leaving Play Mode does not query root objects from an unloaded scene.
- Stopped the HSR scene controller from mutating shared material keyword assets; scene-driven HSR keywords are now applied through render pass command buffers.
- Fixed RenderGraph HSR keyword helpers to accept raster command buffers used by raster passes.
- Replaced render-time global renderer scans in planar reflections and HSR manikin floor shadows with scene-local marker registries, cached planar reflection material pass lookup, scoped related HSR shadow renderer collection to the render scene, and marked auto-setup characters as reflection casters.
- Restored reflection planes and manikin floor shadows in existing scenes by adding scene-local legacy receiver/caster discovery that sticks to cached positive results when marker components have not been added yet.
- Moved HSR self-shadow shadow-casting control onto the character controller so scoped renderers stay Off while self shadows are enabled and return to On when disabled.
- Scoped render-feature shader globals to render graph/command-buffer passes, with explicit per-camera fallback clears for bloom, hair shadows, self shadows, manikin shadows, planar reflections, and HSR inherited lighting globals.
- Keyed HSR inherited lighting state and generated tonemapping LUT availability by camera and frame so multi-camera rendering cannot reuse state from another camera in the same frame.
- Made HSR Chromatic Aberration, Radial Blur, and Tonemapping volume components inactive unless enabled with overridden values that produce a visible effect.
- Stopped planar reflections from baking HSR character skinned meshes when the character controller is using BuiltIn skinning while keeping those characters visible in reflections.
- Reduced compute skinning validation overhead by replacing per-frame deep bone hashing with dirty/snapshot checks, caching segment root/bone/bindpose data, and sharing one controller-level constant buffer across renderers.
- Prevented HSR Rotatable from null-referencing when no active camera exists yet.
- Stopped the hair shadow depth pass from falling back to arbitrary material pass 0 when expected depth passes are missing.
- Replaced the transparent buffer draw-item int hash dedupe with an equality key so hash collisions cannot drop valid draw items.
- Replaced render-time HSR scene controller root scans with a scene-scoped controller registry used by lighting and transparent buffer passes.
- Cached transparent buffer draw-item snapshots across frames until renderer, material, or controller state changes, with a lightweight active-renderer check before drawing cached items.
- Cached planar reflection renderer membership by scene, layer mask, receiver shader set, and participant registry version, while keeping lightweight active/layer validation before drawing.
- Fixed the planar reflection legacy receiver cache so it does not rely on LINQ `HashSet` array conversion.
- Added pre-enqueue volume activity checks for HSR bloom, chromatic aberration, radial blur, tonemapping, and uber post-processing so disabled effects do not force intermediate color targets.
- Removed full-resolution dummy color attachments from the Lighting GBuffer depth rebuild pass so it writes only the depth target.
- Replaced per-frame bloom pass-data array allocations with reusable cached bloom snapshots and fixed texture handle fields.
- Removed the timed play-mode HSR scene light root scan fallback; scene light refresh now uses registered character and scene lights during play, with allocation-free root scans reserved for explicit repair and validation paths.
- Gated HSR compute skinning dispatch behind visible, shadow, or reflection consumers and bone transform dirty state so static or fully culled characters do not upload matrices and dispatch every `LateUpdate`.
- Removed the dormant compute-skinning blendshape bake path so blendshape renderers stay on built-in skinning until a GPU or explicit bounded bake path exists.
- Cached inherited lighting shadow globals once per camera frame and switched shadow matrix capture to Unity's non-alloc global matrix list API.
- Replaced self-shadow and manikin shadow global array clones with reusable render graph snapshot rings, including disabled/global binding paths.
- Cached self-shadow and manikin material pass resolution by material, shader, preferred pass, and exclusion state so candidate filtering and drawing do not rescan shader passes every frame.
- Reused HSR character renderer-scope scratch collections and list-based hierarchy queries so setup/topology refreshes avoid fresh component arrays and pruning collections when the renderer list is unchanged.
- Made planar reflection runtime collection prefer explicit participants, kept a cached legacy compatibility path for unmarked scenes, and removed CPU skinned-mesh baking by drawing skinned reflection casters through the reflected view path.
- Made placement roster validation dirty-driven and reused scratch model sets so active-model getters no longer prune lists or allocate during polling.
- Made render participant registry pruning lazy and scene-versioned so render passes do not scan all registered reflection/manikin participants on every version/query.
- Cached normalized character ID lookups by override, display, and source name so runtime exact-name queries are O(1) without repeated list scans or trimmed string allocations.
- Centralized simulator input action fallback loading behind a shared lazy cache so camera and placement controllers do not repeatedly call `Resources.Load` when their serialized asset reference is missing.
- Centralized runtime material pass resolution behind an ID-keyed cache so planar reflection and hair shadow pass lookups no longer retain material instances.
- Serialized the default HSR compute-skinning shader during auto setup, made the editor hook an authoring-only fallback, and logs when runtime compute skinning is requested without an assigned shader.
- Added unregister support for RuntimeEditorBridge edit-mode cleanup callbacks so editor installers can remove assembly-reload and quit handlers.
- Moved character placement state and layout back into the runtime scene assembly, split Q/E switching into a simulator-only input adapter, and made edit-mode placement/environment application explicit through manager and auto-setup calls.
- Added shared runtime/editor deduplication helpers for render-scene resolution, renderer traversal, runtime string keys, once-only runtime logging, editor path normalization, managed file transactions, JSON stores, AssetDatabase editing scopes, manager UI primitives, scene-light lookup, render-capture settings, and generated editor textures.
- Replaced duplicated call-site logic with the new helpers across render-scene checks, material pass lookups, renderer traversal, scene-light lookup, editor path handling, JSON stores, managed file rollback transactions, AssetDatabase edit scopes, and manager UI primitives.
- Synced Rotatable objects once before rendering, with yaw-change caching, so fast simulator/camera rotation cannot leave them one frame behind without adding per-camera callback work.

## Assets
- Restricted resource sync destinations to the package Resources folder so API-controlled paths cannot target the package root.
- Changed asset downloads to stage files under Library before copying them into Assets during a short AssetDatabase edit phase.
- Wrapped asset download finalization in a backup transaction, including `.meta` files, and tightened Assets-path validation against traversal and malformed segments.

## Renders
- Released generated fallback turnaround background textures on assembly reload and editor quit.

## API
- Made API refresh scheduling tolerate corrupt EditorPrefs timestamps.
- Preserved cancellation when restoring a user profile instead of falling back to cached profile data.


## Prefabs
- Fixed an issue where the roof of the Honkai Star Rail Character Screen wouldn't rotate with the camera.