# Baseline — Git Working Tree State

Captured 2026-08-06 from `C:\Users\rebui\Desktop\FlashBeat`.

## `git status --short`

```text
 M .gitattributes
 M .gitignore
 M "Assets/NotoSansJP-Medium SDF.asset"
 M "Assets/NotoSansJP-Medium SDF.asset.meta"
 M Assets/NotoSansJP-Medium.ttf
 M "Assets/Prefab/YoutubePlayer 1.prefab"
 M Assets/Prefab/YoutubePlayer.prefab
 M Assets/Scenes/GameScene.unity
 M Assets/Scripts/BGFlash.cs
 M Assets/Scripts/GManager.cs
 M Assets/Scripts/GameSceneManager.cs
 M Assets/Scripts/Judge.cs
 M Assets/Scripts/Light.cs
 M Assets/Scripts/MakeFileSceneManager.cs
 M Assets/Scripts/MusicManager.cs
 M Assets/Scripts/Notes.cs
 M Assets/Scripts/NotesManager.cs
 M Assets/Scripts/OpeningSceneManager.cs
 M Assets/Scripts/OptionSceneManager.cs
 M Assets/Scripts/ResultSceneManager.cs
 M Assets/Scripts/SaveLoadManager.cs
 M Assets/Scripts/SelectSceneManager.cs
 M Assets/Scripts/SelfDestroy.cs
 M Assets/Scripts/SimpleTransition.cs
 M Assets/Scripts/TipingSceneManager.cs
 M Assets/Scripts/TitleSceneManager.cs
 M Assets/Scripts/VideoTime.cs
 D Assets/Scripts/YoutubePlayy.cs
 D Assets/Scripts/YoutubePlayy.cs.meta
 D Assets/TextAssetsYuigo.asset
 D Assets/TextAssetsYuigo.asset.meta
 M "Assets/TextMesh Pro/Fonts/LiberationSans.ttf"
 M "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat"
 M "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset"
 M "Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset"
 M "Assets/TextMesh Pro/Resources/TMP Settings.asset"
 M "Assets/TextMesh Pro/Shaders/TMP_Bitmap-Custom-Atlas.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_Bitmap-Mobile.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_Bitmap.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_SDF SSD.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Masking.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Overlay.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_SDF-Surface-Mobile.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_SDF-Surface.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_SDF.shader"
 M "Assets/TextMesh Pro/Shaders/TMP_Sprite.shader"
 M "Assets/TextMesh Pro/Shaders/TMPro.cginc.meta"
 M "Assets/TextMesh Pro/Shaders/TMPro_Mobile.cginc"
 M "Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc"
 M "Assets/TextMesh Pro/Shaders/TMPro_Surface.cginc"
 M "Assets/YuGothB SDF.asset"
 M "Assets/YuGothB SDF.asset.meta"
 M Assets/YuGothB.ttc
 M Packages/manifest.json
 M Packages/packages-lock.json
 M ProjectSettings/EditorBuildSettings.asset
 M ProjectSettings/PackageManagerSettings.asset
 M ProjectSettings/ProjectSettings.asset
 M ProjectSettings/ProjectVersion.txt
 M ProjectSettings/SceneTemplateSettings.json
?? .editorconfig
?? Assets/Editor.meta
?? Assets/Editor/
?? Assets/Scripts/FlashBeat.asmdef
?? Assets/Scripts/FlashBeat.asmdef.meta
?? "Assets/Simple Scene Fade Load System/SimpleFadeSystem.asmdef"
?? "Assets/Simple Scene Fade Load System/SimpleFadeSystem.asmdef.meta"
?? Assets/Tests.meta
?? Assets/Tests/
?? "Assets/TextMesh Pro/Shaders/SDFFunctions.hlsl"
?? "Assets/TextMesh Pro/Shaders/SDFFunctions.hlsl.meta"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF SSD SpaceWarp.shader"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF SSD SpaceWarp.shader.meta"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF SpaceWarp.shader"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF SpaceWarp.shader.meta"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP LIT.shadergraph"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP LIT.shadergraph.meta"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP UNLIT.shadergraph"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP UNLIT.shadergraph.meta"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD SpaceWarp.shader"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD SpaceWarp.shader.meta"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SpaceWarp.shader"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SpaceWarp.shader.meta"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile-2-Pass.shader"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile-2-Pass.shader.meta"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-URP Lit.shadergraph"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-URP Lit.shadergraph.meta"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-URP Unlit.shadergraph"
?? "Assets/TextMesh Pro/Shaders/TMP_SDF-URP Unlit.shadergraph.meta"
?? CLAUDE.md
?? ProjectSettings/MultiplayerManager.asset
?? ProjectSettings/Packages/com.unity.ai.assistant/
?? ProjectSettings/PhysicsCoreProjectSettings2D.asset
?? ProjectSettings/ProjectAuditorSettings.asset
?? build.log
?? build_out.log
?? build_run.log
?? docs/superpowers/plans/baseline-console.md
?? docs/superpowers/plans/baseline-tests.md
?? test-run.log
```

## `git log --oneline -10`

```text
174ceb7 Add FlashBeat refactor implementation plan
67e6164 Revise refactor spec: restore YouTube streaming as primary path
ad427c5 Integrate missing project data and normalize with LFS
69e868c Add proper .gitignore and .gitattributes (LFS + LF line endings)
6c02edc Update README.md
be16961 Update README.md
ee11d76 first commit
```

## Interpretation

- **Pre-existing modifications** (the long `M` list under `Assets/` plus `Packages/` and `ProjectSettings/`) are the residual dirty working tree from integration commit `ad427c5` ("Integrate missing project data and normalize with LFS"). That commit pulled in upstream/Unity-generated files that were re-touched on checkout, plus LFS pointer normalization — none of these changes were authored by the current refactor work.
- **Recent spec/plan commits already committed**:
  - `174ceb7` — "Add FlashBeat refactor implementation plan"
  - `67e6164` — "Revise refactor spec: restore YouTube streaming as primary path"
  Both are now part of `HEAD` and form the authoritative spec/plan input for Task 1.
- **Untracked artifacts** are not part of any plan: build/test logs (`build.log`, `build_out.log`, `build_run.log`, `test-run.log`), Unity-generated `ProjectSettings/` noise (`MultiplayerManager.asset`, `Packages/com.unity.ai.assistant/`, `PhysicsCoreProjectSettings2D.asset`, `ProjectAuditorSettings.asset`), the newly-written baseline captures (`baseline-console.md`, `baseline-tests.md`), the new `CLAUDE.md` root file, and assorted TextMesh Pro shader/HLSL files Unity added during package resolution.
- **Deletions** (`Assets/Scripts/YoutubePlayy.cs` + `.meta`, `Assets/TextAssetsYuigo.asset` + `.meta`) are pre-existing from `ad427c5` — old YouTube-script file and the legacy TMP font asset that the integration commit dropped.
- **Bottom line for Task 1**: the working tree starts dirty but the noise is all upstream/Unity-generated. No baseline-relative refactor work is already staged or uncommitted; the refactor begins from a clean conceptual baseline on top of `174ceb7`.
