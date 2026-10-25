# Camera Mode

A photo mode for SPT: a clean first-person view for screenshots and recordings, with photo effects. Press **F9** in raid to toggle it, **F10** to open the Camera Mode menu, **F7** to save a screenshot without the menu, **F8** for UFO mode, and **Page Down** to freeze or unfreeze every bot. Every key can be changed on the menu's Keys page or in F12.

By default Camera Mode hides the HUD, your arms, your weapon and the visor overlay. The AI ignores you and you take no damage. You still walk and look around normally, and the Escape menu, inventory and F12 work as usual. Press the key again and everything comes back. Camera Mode also turns itself off if you die or the raid ends.

## Options

The menu has a quick bar for the four actions at the top, a page list on the left, and a help bar at the bottom that explains whatever you hover over. Every option is also in F12, and changes apply right away. Any option you've changed shows a Reset button that puts it back to its default.

- **Hide:** battle HUD, raid panels, notifications, watermark/FPS counter, arms, held item, wrist watch, body and legs (with or without their shadows), visor and glasses overlay, blood on screen, rain drops.
- **Player:** no damage, infinite stamina, no hunger or thirst, AI ignores you, freeze AI in place (optionally mid-pose), any time with the Freeze AI key or whenever Camera Mode turns on.
- **Camera:** field of view, hold-to-zoom key (keyboard or mouse button), camera roll (Dutch angle), and depth of field with autofocus, focus distance, aperture and focal length.
- **Effects:** presets (Default (EFT), Noir, Vintage, Cinematic, Cold, Warm, Dream, Neon); brightness, contrast, saturation, temperature, tint and hue; vignette, film grain, chromatic aberration, lens distortion and bloom; letterbox bars (1.85:1, 2:1, 2.39:1, 2.76:1, 4:3, 1:1, 4:5); and a rule-of-thirds grid.
- **UFO:** fly anywhere through walls with your movement keys (Space up, Ctrl down, Shift fast), invisible to AI and unhurt while flying. When UFO mode turns off you land on whatever is below you (a roof, a floor) and stay there; it refuses to land you inside a wall.
- **General:** keys, hold-to-use instead of toggle, on/off message, extra objects from other mods to hide, reset all.

Screenshots are saved to `Documents\Escape from Tarkov\Screenshots`, the game's own screenshot folder.

## Install

Unzip `CameraMode-<version>.zip` into your SPT folder (the one with `EscapeFromTarkov.exe`). That puts `CameraMode.dll` in `BepInEx\plugins`. Client only; there is no server part.

## Build

Needs the .NET SDK and an SPT 4.1.x install that has been started once through the SPT Launcher.

```powershell
scripts\pack.ps1 -SPTPath H:\SPT4.1.X            # build + dist\CameraMode-<version>.zip
scripts\pack.ps1 -SPTPath H:\SPT4.1.X -Install   # also copy the DLL into BepInEx\plugins
```

## Limits

- Only HUD made with Unity UI canvases can be hidden. Overlays from other mods that sit under the game's HUD are hidden automatically. Others can be added by GameObject name under "Extra objects to hide". Overlays drawn with IMGUI (`OnGUI`) can't be hidden.
- Pain blur and flashbangs stay. Lights and lasers on the weapon still light the world, and ejected shell casings still show.
- Aiming down sights always uses the game's own zoom, and the effects aren't applied inside scope views.
- Freezing the AI stops bots, not the world: grenades, physics and the raid timer keep going.
- Built for solo SPT. With Fika, bots run on the host, so the AI options only work for the host.
