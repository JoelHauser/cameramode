using BepInEx.Configuration;
using UnityEngine;

namespace CameraMode
{
    /// <summary>The parts of Camera Mode that are applied and undone separately.</summary>
    internal enum Part
    {
        None,
        Hud,
        Model,
        Screen,
        Vitals,
        Freeze,

        /// <summary>AI stealth and godmode: wanted by Camera Mode, UFO mode, or both.</summary>
        Shared,
    }

    /// <summary>Letterbox (or pillarbox) bars for a frame shape. Names are what the F12 menu shows.</summary>
    internal enum Letterbox
    {
        Off,
        Flat_1_85,
        Wide_2_00,
        Scope_2_39,
        Ultra_2_76,
        Classic_4_3,
        Square_1_1,
        Portrait_4_5,
    }

    /// <summary>
    /// Every option, bound in one place. The in-game menu (<see cref="MenuWindow"/>) and the F12
    /// menu both edit these entries, so a change in one shows in the other and is saved.
    ///
    /// Section names are numbered because the F12 menu sorts sections alphabetically. Key names
    /// must not contain = \ " ' [ ] (BepInEx throws in Awake and the plugin never loads);
    /// scripts\pack.ps1 checks every Bind in this file for them.
    /// </summary>
    internal static class Settings
    {
        internal const string General = "1. General";
        internal const string Hud = "2. Hide HUD";
        internal const string Model = "3. Hide model";
        internal const string Screen = "4. Screen effects";
        internal const string Player = "5. Player";
        internal const string Camera = "6. Camera";
        internal const string Effects = "7. Photo effects";
        internal const string Ufo = "8. UFO mode";

        internal static ConfigEntry<KeyboardShortcut> UfoKey;
        internal static ConfigEntry<float> UfoSpeed;
        internal static ConfigEntry<float> UfoFastMultiplier;
        internal static ConfigEntry<KeyCode> UfoUpKey;
        internal static ConfigEntry<KeyCode> UfoDownKey;
        internal static ConfigEntry<KeyCode> UfoFastKey;
        internal static ConfigEntry<bool> UfoFlyWhereYouLook;
        internal static ConfigEntry<bool> UfoLandOnGround;
        internal static ConfigEntry<bool> UfoBlockInsideWalls;
        internal static ConfigEntry<bool> UfoInvisible;
        internal static ConfigEntry<bool> UfoNoDamage;

        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<KeyboardShortcut> MenuKey;
        internal static ConfigEntry<KeyboardShortcut> ScreenshotKey;
        internal static ConfigEntry<KeyboardShortcut> FreezeKey;
        internal static ConfigEntry<bool> HoldToUse;
        internal static ConfigEntry<bool> ShowMessages;

        internal static ConfigEntry<bool> HideBattleHud;
        internal static ConfigEntry<bool> HideRaidPanels;
        internal static ConfigEntry<bool> HideNotifications;
        internal static ConfigEntry<bool> HideOverlays;
        internal static ConfigEntry<string> ExtraObjects;

        internal static ConfigEntry<bool> HideArms;
        internal static ConfigEntry<bool> HideHeldItem;
        internal static ConfigEntry<bool> HideWatch;
        internal static ConfigEntry<bool> HideBody;
        internal static ConfigEntry<bool> KeepShadows;

        internal static ConfigEntry<bool> HideVisor;
        internal static ConfigEntry<bool> HideBlood;
        internal static ConfigEntry<bool> HideRain;

        internal static ConfigEntry<bool> AiIgnore;
        internal static ConfigEntry<bool> NoDamage;
        internal static ConfigEntry<bool> InfiniteStamina;
        internal static ConfigEntry<bool> NoHungerThirst;
        internal static ConfigEntry<bool> FreezeAi;
        internal static ConfigEntry<bool> FreezePoses;

        internal static ConfigEntry<bool> OverrideFov;
        internal static ConfigEntry<int> Fov;
        internal static ConfigEntry<KeyboardShortcut> ZoomKey;
        internal static ConfigEntry<int> ZoomFov;
        internal static ConfigEntry<float> ZoomSpeed;
        internal static ConfigEntry<int> Roll;
        internal static ConfigEntry<bool> DepthOfField;
        internal static ConfigEntry<bool> AutoFocus;
        internal static ConfigEntry<float> FocusDistance;
        internal static ConfigEntry<float> Aperture;
        internal static ConfigEntry<int> FocalLength;

        internal static ConfigEntry<int> Brightness;
        internal static ConfigEntry<int> Contrast;
        internal static ConfigEntry<int> Saturation;
        internal static ConfigEntry<int> Temperature;
        internal static ConfigEntry<int> Tint;
        internal static ConfigEntry<int> HueShift;
        internal static ConfigEntry<int> Vignette;
        internal static ConfigEntry<int> VignetteSoftness;
        internal static ConfigEntry<int> Grain;
        internal static ConfigEntry<int> GrainSize;
        internal static ConfigEntry<int> ChromaticAberration;
        internal static ConfigEntry<int> LensDistortion;
        internal static ConfigEntry<int> Bloom;
        internal static ConfigEntry<Letterbox> LetterboxBars;
        internal static ConfigEntry<bool> CompositionGrid;

        private static int _order = 1000;

        internal static void Bind(ConfigFile config)
        {
            ToggleKey = Bind(config, General, "Toggle key", new KeyboardShortcut(KeyCode.F9),
                "Turns Camera Mode on and off in raid.");
            MenuKey = Bind(config, General, "Menu key", new KeyboardShortcut(KeyCode.F10),
                "Opens and closes the Camera Mode menu.");
            ScreenshotKey = Bind(config, General, "Screenshot key", new KeyboardShortcut(KeyCode.F7),
                "Saves a screenshot without the menu to Documents, Escape from Tarkov, Screenshots.");
            FreezeKey = Bind(config, General, "Freeze AI key", new KeyboardShortcut(KeyCode.PageDown),
                "Freezes every bot in place, and unfreezes them. Works any time in raid, with or without Camera Mode.");
            HoldToUse = Bind(config, General, "Hold to use", false,
                "Camera Mode is on only while the toggle key is held down.");
            ShowMessages = Bind(config, General, "Show on and off message", true,
                "A short message at the top of the screen when Camera Mode turns on or off. It fades out in under two seconds.");

            HideBattleHud = Bind(config, Hud, "Battle HUD", true,
                "Health, stance, quick slots, interaction prompts, ammo count, fire mode, compass, malfunction glow.");
            HideRaidPanels = Bind(config, Hud, "Raid panels", true,
                "Extraction timers and messages, PMC count, BTR seats, transit panels, raid-start intro.");
            HideNotifications = Bind(config, Hud, "Notifications", true,
                "The pop-up messages in the corner (quests, items, warnings).");
            HideOverlays = Bind(config, Hud, "Watermark, version and FPS counter", true,
                "The profile watermark, version label, FPS counter and network ping and loss readouts.");
            ExtraObjects = Bind(config, Hud, "Extra objects to hide", "",
                "Comma-separated GameObject names of other mods' HUD to hide too.", advanced: true);

            HideArms = Bind(config, Model, "Arms", true,
                "Your first-person arms and hands.");
            HideHeldItem = Bind(config, Model, "Held item", true,
                "Your weapon or whatever is in your hands. It still works, it just isn't drawn.");
            HideWatch = Bind(config, Model, "Wrist watch", true,
                "The watch on your wrist.");
            HideBody = Bind(config, Model, "Body and legs", false,
                "Your own body and gear, seen when you look down.");
            KeepShadows = Bind(config, Model, "Keep shadows", false,
                "Hidden parts still cast their shadow on the world.");

            HideVisor = Bind(config, Screen, "Visor and glasses overlay", true,
                "The frame and tint of face shields, visors and glasses.");
            HideBlood = Bind(config, Screen, "Blood on screen", true,
                "Blood splatter on the screen from hits.");
            HideRain = Bind(config, Screen, "Rain and water drops", false,
                "Rain drops and water running down the screen.");

            AiIgnore = Bind(config, Player, "AI ignores you", true,
                "Bots don't see, hear or target you. They fight you again when Camera Mode turns off.");
            NoDamage = Bind(config, Player, "No damage", true,
                "You can't be hurt or killed.");
            InfiniteStamina = Bind(config, Player, "Infinite stamina", false,
                "Stamina, arm stamina and breath don't run out. Fills them when Camera Mode turns on.");
            NoHungerThirst = Bind(config, Player, "No hunger or thirst", false,
                "Energy and hydration stop draining.");
            FreezeAi = Bind(config, Player, "Freeze AI in place", false,
                "Freeze every bot whenever Camera Mode turns on: no moving, turning, shooting or talking. The Freeze AI key freezes them any time.");
            FreezePoses = Bind(config, Player, "Freeze AI poses", true,
                "Frozen bots also hold their exact pose mid-animation instead of idling.");

            OverrideFov = Bind(config, Camera, "Override field of view", false,
                "Use the field of view below while Camera Mode is on. Aiming down sights uses the game's zoom.");
            Fov = Bind(config, Camera, "Field of view", 75,
                "Field of view in degrees while Camera Mode is on.", range: new AcceptableValueRange<int>(30, 120));
            ZoomKey = Bind(config, Camera, "Zoom key", KeyboardShortcut.Empty,
                "Hold to zoom in while Camera Mode is on.");
            ZoomFov = Bind(config, Camera, "Zoom field of view", 30,
                "Field of view in degrees while the zoom key is held.", range: new AcceptableValueRange<int>(5, 60));
            ZoomSpeed = Bind(config, Camera, "Zoom speed", 8f,
                "How quickly the view zooms in and out. Higher is faster.", range: new AcceptableValueRange<float>(1f, 20f));
            Roll = Bind(config, Camera, "Camera roll", 0,
                "Tilts the view sideways, in degrees, for a Dutch angle. Only the picture tilts; aiming and movement don't.",
                range: new AcceptableValueRange<int>(-45, 45));
            DepthOfField = Bind(config, Camera, "Depth of field", false,
                "Blurs what is nearer or further than the focus distance.");
            AutoFocus = Bind(config, Camera, "Autofocus", true,
                "Focus on whatever is in the middle of the screen. Off: use the focus distance below.");
            FocusDistance = Bind(config, Camera, "Focus distance", 10f,
                "Distance in metres that stays sharp when autofocus is off.", range: new AcceptableValueRange<float>(0.3f, 100f));
            Aperture = Bind(config, Camera, "Aperture", 2.8f,
                "f-stop. Lower blurs more around the focus distance.", range: new AcceptableValueRange<float>(1f, 22f));
            FocalLength = Bind(config, Camera, "Focal length", 50,
                "Lens focal length in mm. Longer gives a shallower, stronger blur.", range: new AcceptableValueRange<int>(10, 300));

            Brightness = Bind(config, Effects, "Brightness", 0,
                "0 is the game's own picture.", range: new AcceptableValueRange<int>(-100, 100));
            Contrast = Bind(config, Effects, "Contrast", 0,
                "0 is the game's own picture.", range: new AcceptableValueRange<int>(-100, 100));
            Saturation = Bind(config, Effects, "Saturation", 0,
                "-100 is black and white.", range: new AcceptableValueRange<int>(-100, 100));
            Temperature = Bind(config, Effects, "Temperature", 0,
                "Negative is colder and bluer, positive warmer and more orange.", range: new AcceptableValueRange<int>(-100, 100));
            Tint = Bind(config, Effects, "Tint", 0,
                "Negative is greener, positive more magenta.", range: new AcceptableValueRange<int>(-100, 100));
            HueShift = Bind(config, Effects, "Hue shift", 0,
                "Rotates every colour around the colour wheel, in degrees.", range: new AcceptableValueRange<int>(-180, 180));
            Vignette = Bind(config, Effects, "Vignette", 0,
                "Darkens the corners. 0 is off.", range: new AcceptableValueRange<int>(0, 100));
            VignetteSoftness = Bind(config, Effects, "Vignette softness", 40,
                "How gradually the vignette fades in.", range: new AcceptableValueRange<int>(1, 100));
            Grain = Bind(config, Effects, "Film grain", 0,
                "0 is off.", range: new AcceptableValueRange<int>(0, 100));
            GrainSize = Bind(config, Effects, "Grain size", 30,
                "Size of the grain particles.", range: new AcceptableValueRange<int>(10, 100));
            ChromaticAberration = Bind(config, Effects, "Chromatic aberration", 0,
                "Colour fringes towards the edges, like a cheap lens. 0 is off.", range: new AcceptableValueRange<int>(0, 100));
            LensDistortion = Bind(config, Effects, "Lens distortion", 0,
                "Positive bulges the picture like a fisheye, negative pinches it. 0 is off.", range: new AcceptableValueRange<int>(-100, 100));
            Bloom = Bind(config, Effects, "Bloom", 0,
                "Extra glow around bright lights, on top of the game's own. 0 is off.", range: new AcceptableValueRange<int>(0, 100));
            LetterboxBars = Bind(config, Effects, "Letterbox", Letterbox.Off,
                "Black bars that frame the picture to a film or photo shape. They show in screenshots.");
            CompositionGrid = Bind(config, Effects, "Composition grid", false,
                "Rule-of-thirds lines to line up a shot. Never in screenshots.");

            UfoKey = Bind(config, Ufo, "UFO key", new KeyboardShortcut(KeyCode.F8),
                "Turns UFO mode on and off: fly anywhere, through walls. You stay where you are when it turns off.");
            UfoSpeed = Bind(config, Ufo, "Flying speed", 8f,
                "Metres per second.", range: new AcceptableValueRange<float>(1f, 60f));
            UfoFastMultiplier = Bind(config, Ufo, "Fast multiplier", 4f,
                "How much faster you fly while the fast key is held.", range: new AcceptableValueRange<float>(1f, 10f));
            UfoUpKey = Bind(config, Ufo, "Up key", KeyCode.Space,
                "Hold to rise. Forward, back and sideways use your normal movement keys.");
            UfoDownKey = Bind(config, Ufo, "Down key", KeyCode.LeftControl,
                "Hold to sink.");
            UfoFastKey = Bind(config, Ufo, "Fast key", KeyCode.LeftShift,
                "Hold to fly faster.");
            UfoFlyWhereYouLook = Bind(config, Ufo, "Fly where you look", true,
                "Forward follows the camera up and down. Off: forward stays level and only the up and down keys change height.");
            UfoLandOnGround = Bind(config, Ufo, "Land on the surface below", true,
                "When UFO mode turns off, drop straight onto whatever is under you (a roof, a floor, the ground). Off: you fall from where you are.");
            UfoBlockInsideWalls = Bind(config, Ufo, "Refuse to land inside walls", true,
                "Keep flying if turning UFO mode off would leave you stuck inside something.");
            UfoInvisible = Bind(config, Ufo, "Invisible to AI while flying", true,
                "Bots don't see, hear or target you while UFO mode is on.");
            UfoNoDamage = Bind(config, Ufo, "No damage while flying", true,
                "You can't be hurt while UFO mode is on.");
        }

        /// <summary>Which part of Camera Mode a changed entry belongs to, so only that part is redone.</summary>
        internal static Part PartOf(ConfigEntryBase entry)
        {
            if (entry == null)
            {
                return Part.None;
            }
            string section = entry.Definition.Section;
            if (section == Hud)
            {
                return Part.Hud;
            }
            if (section == Model)
            {
                return Part.Model;
            }
            if (section == Screen)
            {
                return Part.Screen;
            }
            if (entry == AiIgnore || entry == NoDamage || entry == UfoInvisible || entry == UfoNoDamage)
            {
                return Part.Shared;
            }
            if (entry == InfiniteStamina || entry == NoHungerThirst)
            {
                return Part.Vitals;
            }
            if (entry == FreezeAi || entry == FreezePoses)
            {
                return Part.Freeze;
            }
            // Camera and Photo effects are read every frame and need nothing redone.
            return Part.None;
        }

        private static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T value, string description,
            AcceptableValueBase range = null, bool advanced = false)
        {
            return config.Bind(section, key, value, new ConfigDescription(description, range,
                new ConfigurationManagerAttributes { Order = _order--, IsAdvanced = advanced ? true : (bool?)null }));
        }
    }
}
