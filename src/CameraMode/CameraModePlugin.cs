using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace CameraMode
{
    /// <summary>
    /// One key toggles Camera Mode: a clean first-person view for screenshots and recordings, with
    /// options for what is hidden, what happens to you and the AI, the camera, and photo effects.
    /// Movement and mouse look are untouched. A second key opens the Camera Mode menu, a third
    /// takes a screenshot without it.
    ///
    /// The options are in <see cref="Settings"/>, the state in <see cref="CameraModeState"/>, and
    /// each part has its own class so it can be undone on its own.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class CameraModePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.mybutthasarash.cameramode";
        public const string PluginName = "Camera Mode";

        /// <summary>Must match the csproj's Version. Two places, and they have to agree.</summary>
        public const string PluginVersion = "0.5.1";

        internal static ManualLogSource Log;

        private bool _effectsFailed;

        private void Awake()
        {
            Log = Logger;

            Settings.Bind(Config);
            Config.SettingChanged += (_, e) => CameraModeState.OnSettingChanged(e.ChangedSetting);
            MenuWindow.Init(Config, this);
            CameraFov.Install();
            CameraRoll.Install();

            new Harmony(PluginGuid).PatchAll(typeof(Patches));
            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void Update()
        {
            MenuWindow.Update();
            CameraModeState.Tick();
            UfoMode.Tick();
            CameraModeState.SharedTick();
            CameraFov.Tick();
            if (!MenuWindow.IsCapturingKey)
            {
                if (Settings.ScreenshotKey.Value.IsDown())
                {
                    Screenshot.Take(this);
                }
                if (Settings.UfoKey.Value.IsDown())
                {
                    UfoMode.Toggle();
                }
                if (Settings.FreezeKey.Value.IsDown())
                {
                    CameraModeState.ToggleFreeze();
                }
            }
        }

        private void LateUpdate()
        {
            CameraModeState.LateTick();
            UfoMode.LateTick();

            // Outside CameraModeState's per-part guard because it also runs while Camera Mode is
            // off (to switch the volume off). One error is logged, not one a frame.
            try
            {
                PhotoEffects.LateTick();
            }
            catch (Exception e)
            {
                if (!_effectsFailed)
                {
                    _effectsFailed = true;
                    Log.LogError($"Photo effects: {e}");
                }
            }
        }

        private void OnGUI()
        {
            float scale = MenuWindow.Scale;
            Overlay.Draw(scale);
            if (Screenshot.Capturing)
            {
                return;
            }
            Toast.Draw(MenuWindow.Skin, scale);
            MenuWindow.Draw();
        }
    }
}
