# Camera Mode

Client-only BepInEx plugin for SPT 4.1.x (EFT 0.16.9.40743). GUID `com.mybutthasarash.cameramode`.
F9 toggles Camera Mode, F10 opens its IMGUI menu, F7 takes a screenshot, F8 toggles UFO mode (noclip flight). Every option is a ConfigEntry in `Settings.cs`,
so the menu and F12 edit the same values. Build with `scripts\pack.ps1 -SPTPath H:\SPT4.1.X [-Install]`
from PowerShell, not Bash: Bash mangles `H:\SPT4.1.X`. The pack script also rejects config names
BepInEx would throw on.

Repo: https://github.com/JoelHauser/cameramode (branch `main`, first pushed 2026-10-25 as 0.5.1).
Commit as Joel Hauser <joel.hauser@gmail.com>. No GitHub release yet; release zips come from
`scripts\pack.ps1` into `dist\` (git-ignored) and contain only `BepInEx/plugins/CameraMode.dll`.

## State as it stands

- 0.1.0 (one key, fixed behaviour) installed 2026-10-24, never run; backed up in
  H:\SPTMods\plugin-backups\2026-10-25-CameraMode-0.1.0.
- 0.2.0 (options + menu) built 2026-10-25, never installed.
- 0.3.0 (photo mode: freeze AI, photo effects, roll, depth of field, letterbox, grid, screenshots)
  installed 2026-10-25. **Joel ran it in game** and reported: the cursor flashed in the menu, presets
  "stuck" (no obvious way back to the stock look), and asked for a reset button on every option.
- 0.4.0 **installed in H:\SPT4.1.X 2026-10-25 (hash = build; 0.3.0 backed up in plugin-backups), not run in game.** Fixes those three (cursor through
  ToggleShowInGameCursorEvent; presets are whole looks starting from defaults, "Clean" renamed
  "Default (EFT)", the lit preset clicked again goes back to it; a Reset button on every changed
  option) and adds UFO mode.
- 0.5.0 **installed in H:\SPT4.1.X 2026-10-25 (hash = build; 0.4.0 backed up in
  H:\SPTMods\plugin-backups\2026-10-25-CameraMode-0.4.0), not run in game.** Joel called the 0.4.0 menu "clunky and
  not user friendly" and asked for a Freeze AI key. Menu redone: a quick bar of cards (Camera
  Mode / UFO / Freeze AI / Screenshot, with state and key), a sidebar of 7 pages, one-line rows laid
  out by hand, a hover help bar instead of always-visible descriptions, dependent options greyed out
  rather than hidden, all keys on one Keys page, and a styled scrollbar (GUI.skin's
  verticalScrollbar/Thumb swapped for the window only, named like the stock styles because the
  scroll view looks the thumb up by name). The Freeze AI key (default Page Down; F1-F4 are EFT game
  keys) freezes bots any time in raid. `CameraModeState.SyncFreeze`: frozen while the key says so OR
  Camera Mode has "Freeze AI in place" on; the key unfreezing overrides Camera Mode until it next
  turns on.
- 0.5.1 **installed in H:\SPT4.1.X 2026-10-25 (hash = build; 0.5.0 backed up in plugin-backups), not run in game.** Joel saw bots with "superspeed" after
  using Freeze AI (log: freeze toggled repeatedly, ORBIT running). Freeze now also pauses
  `AICoreController` (bot brains + ORBIT's tick); see Freeze AI below.
  The 0.1.0 cfg section "Camera Mode" is orphaned by the numbered sections; harmless.
- 0.5.2 **pushed 2026-10-28 (4bd50b8), not installed, not run in game.** Letterbox screenshots are cropped to
  the shape instead of saving the bars; see Letterbox below.
- 0.5.3 **installed in H:\SPT4.1.X 2026-10-29 (hash = build; installed 0.5.2 backed up in plugin-backups), not run in game.** Joel wanted to free look (hold Left Alt)
  and press the screenshot key. BepInEx `KeyboardShortcut.IsDown` fails while any other keyboard key
  is held (its ModifierKeyTest blocks every SupportedKeyCode except mouse buttons), so Alt or W killed
  F7. The screenshot key now uses `CameraModePlugin.IsDownWhileHoldingOthers`: main key down + its own
  modifiers held, other keys ignored. The other keys still use IsDown.
- 0.5.4 **installed in H:\SPT4.1.X 2026-10-29 (hash = build; 0.5.3 backed up in plugin-backups), not run in game.** Checked Joel's 4:3 question: the
  2026-10-28 21:20 shot is 1920x1440, no bars, correct. But three Letterbox Off shots on 2026-10-29
  (18:32, 18:34, 18:52, 0.5.2) logged `Screenshot saved` and no file exists anywhere, no error in
  either log. `ScreenCapture.CaptureScreenshot` is gone; Off now goes through the same ReadPixels
  path as the letterbox shapes (Overlay.Frame(0) = whole screen).
- Look in `BepInEx\LogOutput.log` for these lines on a toggle:
  `HUD: N objects, N canvases hidden`, `Model: N renderers hidden`, `Screen: N effects hidden`,
  `AI: player removed from N enemy and N neutral lists`, `Godmode: on`, `Vitals: ...`,
  `Freeze: N bots frozen`, `Photo effects: volume on layer N (name)` (first effect used),
  `Screenshot saved: path`, `UFO mode on`, `UFO mode off, landed at (x, y, z)`,
  `UFO mode: refused to land inside something`, `Camera Mode on/off (reason)`. If `Camera Mode <version> loaded` is
  missing, the Awake crash is in Player.log, not LogOutput.log.

## Structure

`CameraModeState` turns parts on and off in order, each inside its own try/catch, and redoes a single
part when one of its settings changes (`Settings.PartOf`). Parts: `HudHider`, `ModelHider`,
`ScreenEffects`, `AiStealth`, `GodMode`, `Vitals`, `FreezeAi`. `CameraFov`, `CameraRoll`, `PhotoEffects`
and `Overlay` read their settings every frame. `MenuWindow` + `MenuSkin` draw the menu (every control
change goes through `Defer`, applied after the window, so the IMGUI layout never changes mid-event).
`Toast` draws the on/off message, `Screenshot` captures without the menu, and `Presets` holds the looks.

## How each part works (from the decompiled 4.1.x Assembly-CSharp)

- **HUD** (`HudHider`): switches off `Canvas` components under `CommonUI.EftBattleUIScreen`,
  `GameUI`, and PreloaderUI overlays (`NotifierView`, `_malfunctionGlow`, `ClientWatermark`,
  `_alphaVersionLabel`, `_fpsCounter`, `_raidStartIntro`, `_rttObject`, `_lossObject`), in four
  setting groups. The Escape menu is `CommonUI.MenuScreen`, a sibling of the battle screen under
  CommonUI, so CommonUI as a whole must never be hidden. Not SetActive or CanvasGroup: the screen
  manager owns both (`UIScreen.SoftShow` fades `UIScreen.CanvasGroup`, which is GetOrAddComponent).
  A target without its own Canvas gets a nested one added (with the parent's shader channels and a
  GraphicRaycaster), and it stays afterwards. **Unverified: whether those objects already have their
  own Canvas in the prefab.** The log line shows the counts.
- **Model** (`ModelHider`): arms = `PlayerBody.BodySkins[EBodyModelPart.Hands]`, watch =
  `PlayerBody._watches` (private), held item = `HandsController.ControllerGameObject` (not a child of
  the player; InGameCutsceneController reparents it), body = the other BodySkins plus
  `PlayerBody.GetRenderersNonAlloc`. Hidden with `forceRenderingOff`, or with `shadowCastingMode =
  ShadowsOnly` under "Keep shadows" (how the game hides the body in first person). The game toggles
  arms with `.enabled` (`LoddedSkin.EnableRenderers`), so neither collides. `OfflinePlayerCulling`
  writes forceRenderingOff, but only for bots (`!IsYourPlayer`). Every 0.25 s the current set is
  collected again, and renderers that no longer belong to the player are restored.
- **Screen effects** (`ScreenEffects`): `CameraManager.VisorEffect` is switched off each LateUpdate
  and handed back with `SetIntensity(Intensity)`, which works out `enabled` again.
  `EffectsController._bloodOnScreen` (private), `RainScreenDrops` and `ScreenWater` stay enabled all
  raid in vanilla, so their saved `enabled` is restored.
- **AI** (`AiStealth` + AddEnemy/AddNeutral prefixes, gated on `AiStealth.Engaged`): every enemy path
  ends in `BotsGroup.AddEnemy`. `BotHearingSensor.OnSoundPlayedInner` ignores players in neither
  `Enemies` nor `Neutrals`. On entry the player is removed (`BotsGroup.RemoveEnemy` also clears
  Neutrals and member memories). On exit, groups get the player back with the recorded `EBotEnemyCause`.
- **Godmode** (`GodMode` + Kill prefix, gated on `GodMode.Engaged`): `ActiveHealthController.SetDamageCoeff(0)`,
  the BTR's own mechanism (`LocalPlayer.IgnoreDamage`). It also zeroes `HealthValue.DownMult`. `Kill`
  ignores the coefficient (lethal toxin), so it is blocked for the player. The BTR resets the
  coefficient to 1 on exit, so Tick re-zeroes it. `_wasInvincibleBefore` is saved and restored.
- **Vitals** (`Vitals`): `Stamina.ForceMode` on Physical.Stamina/HandsStamina/Oxygen (what bots and
  Berserk use; Consume takes nothing, CanAdd allows), re-set each frame and refilled on entry.
  `BaseHealthController.MetabolismDisabled` is the offline raid setting's flag.
- **FOV** (`CameraFov`): written in `Camera.onPreCull` for `CameraManager.Camera`, because
  SetFov's coroutine, ApplyFoV, EffectsController's FOV effect and PlayerCameraFovChanger all write
  it at different points in the frame. It eases back to `CameraManager.Fov` (the game's own value)
  and then stops writing. Skipped while `HandsController.IsAiming`.
- **Freeze AI** (`FreezeAi` + BotOwner.UpdateManual/FixedUpdate prefixes, gated on `FreezeAi.Engaged`):
  those two are a bot's body and senses (BotsList calls UpdateManual). They are NOT its brain: the
  decision agents (vanilla plus mod layers) run from `BotsController.AICoreController.Update`, which
  has the game's own `Stop()`/`Activate()` (used at raid end/start). Since 0.5.1 the freeze also stops
  it, and starts it again only if the freeze stopped it, never on raid teardown. In 0.3.0-0.5.0 it
  kept running, and **ORBIT** (installed; its whole tick is a postfix on `AICoreController.Update` that
  returns when `_enable` is false) kept driving frozen bots: ghost bots slid along by transform, and
  stuck remediation vaulted, jumped and teleported bots that looked stuck. Joel saw "superspeed" bots.
  ORBIT ghosts (bots beyond its sleep distance, 260 m in Joel's setup; wake <210 m) always glide
  without animation; that is ORBIT's own and easy to see from up high in UFO mode; Joel said not to worry about it. On freeze each bot gets `StopMove`,
  `Player.Move(Vector2.zero)` (Move stores the direction until the next call), and
  `IFirearmHandsController.SetTriggerPressed(false)`. "Freeze AI poses" sets
  `BodyAnimatorCommon`/`ArmsAnimatorCommon` (`IAnimator` in AnimationSystem.Types) to speed 0. The game
  only sets animator speed in one-off places, so it is re-set every 0.25 s and the saved speed restored.
- **Photo effects** (`PhotoEffects`): EFT's FPS Camera uses Post Processing Stack v2 (AmandsGraphics
  drives the same `PostProcessLayer`). One extra global volume from `PostProcessManager.QuickVolume` on
  the lowest layer in `PostProcessLayer.volumeLayer`, priority 1000. The shaders were found by name
  in the game data: Uber, GrainBaker, Bloom, DepthOfField, Lut2DBaker. ColorGrading is forced to
  LowDefinitionRange (Lut2DBaker, no HDR compute path). EFT's own colour and tonemap come from
  PrismEffects, not PPv2 grading. Joel saw the effects in game (0.3.0/0.4.0), so the volume works.
  Autofocus raycasts with `LayersMaskController.HighPolyWithTerrainMask`.
  **Effects stuck (0.3.0/0.4.0) because of EFT's PPv2 fork:** `PostProcessManager.UpdateSettings`
  only calls `ReplaceData` (reset the bundles to defaults) when the layer has a `volumeTrigger`, and
  the FPS camera has none. So values a volume blends into `PostProcessLayer.GetBundle(type).settings`
  stay there after the volume goes. That is also why AmandsGraphics can write bundle values directly.
  Since the 0.5.0 rebuild, each effect is a `Slot`: snapshot the bundle when the effect turns on, clear our
  overrides and write the snapshot back when it turns off. PPv2's `parameters` and `SetValue` are
  internal, so the snapshot copies `ParameterOverride<T>.value` and `overrideState` by reflection
  over the same public fields PPv2 enumerates (`enabled` included).
- **Roll** (`CameraRoll`): the rotation is added in `Camera.onPreCull` and taken off in
  `Camera.onPostRender`, so only the rendered picture tilts.
- **Letterbox and grid** (`Overlay`): IMGUI. The grid, menu and message are skipped while
  `Screenshot.Capturing`. Screenshots go to the folder PreloaderUI uses
  (Documents\Escape from Tarkov\Screenshots). Letterbox Off was `ScreenCapture.CaptureScreenshot` up to 0.5.3.
  Up to 0.5.1 the bars were saved in the PNG (Joel: "it basically just creates black bars"); since
  0.5.2 a letterbox shot is `ReadPixels` of just `Overlay.Frame` after WaitForEndOfFrame (GUI y is
  top-down, ReadPixels bottom-up), so 4:3 on 3440x1440 saves 1920x1440. Resolution is capped by the
  screen: the shape is cropped, not re-rendered. Since 0.5.4 Off uses the same path (whole screen). Log line: `Screenshot WxH (ratio)`.
- **Shared parts**: AI stealth and godmode are wanted by Camera Mode, UFO mode or both.
  `CameraModeState.SyncShared` turns each on while either mode asks for it; `AiStealth` and
  `GodMode` keep their own player reference instead of reading a mode's.
- **UFO mode** (`UfoMode` + MovementContext.DirectApplyMotion prefix): every movement state's
  per-frame motion (gravity included) ends in DirectApplyMotion → CharacterController.Move +
  EmitStepNoise. For the flying player it is replaced by a direct write to
  `MovementContext.TransformPosition` (how Player.Teleport moves a player). There is no collision,
  no gravity and no footsteps, and mouse look is untouched. A LateUpdate fallback covers frames with
  no scheduled motion. Horizontal input is `Player.InputDirection` (the game's own bindings); up,
  down and fast are plain KeyCodes, because BepInEx shortcuts refuse to fire with an extra modifier
  held. Fall damage comes from CheckFlying → HandleFall(_startFallingHeight - y), and ResetFlying()
  every frame keeps it at one frame's worth. On landing: raycast down with
  `PlayerStaticCollisionsMask` (the player CC's own mask, also the spawn GroundHelper's), CheckCapsule
  with the CC radius/height to refuse spots inside walls, then `Player.Teleport`.
- **Menu input** (`MenuWindow`): the cursor is shown through the game's `ToggleShowInGameCursorEvent`
  (`GlobalEventsController.Instance.CreateCommonEvent<…>().Invoke(show)`). 0.3.0 wrote Cursor.visible
  every OnGUI, and `InputManager.Update` re-hid it every frame, so the cursor flashed. `GamePlayerOwner.SetIgnoreInput(true)` holds the player still; its previous value is read from
  private static `bool_0`, the only field the setter stores to (Cecil-checked). `Input.ResetInputAxes`
  runs once a frame.
- **Teardown**: GameWorld.Dispose prefix (ClientGameWorld.Dispose calls base first), plus Tick
  checks for death, a destroyed player, or a new MainPlayer.

## Things to check in the first raid

1. F10 opens the menu, the mouse works in it, the player stands still, and Escape closes it.
2. HUD gone, the Escape menu still opens and works, inventory opens, F12 opens.
3. HUD back after toggling off, after leaving the raid with Camera Mode on, and at the start of the next raid.
4. Weapon swap while on: the new weapon is hidden within a quarter second.
5. Bots walk past without reacting. After toggling off, they fight again.
6. FOV override and zoom key change the view, and aiming down sights still zooms normally.
7. Flip a few switches while Camera Mode is on: each applies right away.
8. Freeze AI: bots stop mid-step and carry on after turning it off.
9. Effects: the Noir preset turns the picture black and white; vignette, grain and depth of field show.
10. F7 saves a screenshot with no menu or grid in it.
11. The menu cursor no longer flashes. Default (EFT) brings back the stock picture; Reset buttons appear on changed options.
12. UFO: F8, fly up through a ceiling onto a roof, F8 again: you stand on the roof. Bots ignore you while flying.
13. 0.5.0 menu: cards toggle their mode, hovering a row fills the help bar, greyed rows don't respond,
    and the scrollbar is the thin styled one (no "Unable to find style" errors in the log).
14. Page Down freezes and unfreezes bots, with and without Camera Mode. The log says "brains paused"; with
    ORBIT, no bot slides, jumps or teleports while frozen, and they walk normally after.
15. Effects don't stick: Noir, then Default (EFT), gives the stock picture. So does Noir followed by
    turning Camera Mode off, and so does dragging one slider back to 0. Test in a freshly started game,
    since a game that ran 0.3.0/0.4.0 keeps the stuck values until it restarts.
