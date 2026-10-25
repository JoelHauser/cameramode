using BepInEx.Configuration;
using EFT;
using EFT.CameraControl;
using UnityEngine;

namespace CameraMode
{
    /// <summary>
    /// Field-of-view override and hold-to-zoom while Camera Mode is on.
    ///
    /// The game writes the main camera's FOV from several places: CameraManager.SetFov's
    /// coroutine, ApplyFoV, EffectsController's FOV effect and PlayerCameraFovChanger. Their order
    /// against this plugin's LateUpdate is not fixed, so the value is written in Camera.onPreCull,
    /// which runs for each camera after every script and before it renders.
    ///
    /// The game's own idea of the FOV is kept in CameraManager.Fov; the view eases back to that
    /// when the override ends, and after that this class writes nothing. While aiming down sights
    /// the game's aim zoom is left alone.
    /// </summary>
    internal static class CameraFov
    {
        /// <summary>Closer than this to the target counts as there, in degrees.</summary>
        private const float SnapDegrees = 0.05f;

        private static bool _driving;
        private static float _current;
        private static int _lastFrame = -1;
        private static bool _zoomHeld;

        internal static void Install()
        {
            Camera.onPreCull += OnPreCull;
        }

        /// <summary>From Update: the zoom key is read once a frame, only while Camera Mode is on.</summary>
        internal static void Tick()
        {
            KeyboardShortcut zoom = Settings.ZoomKey.Value;
            _zoomHeld = CameraModeState.Active
                        && !MenuWindow.IsOpen
                        && zoom.MainKey != KeyCode.None
                        && zoom.IsPressed();
        }

        private static void OnPreCull(Camera camera)
        {
            if (!_driving && !CameraModeState.Active)
            {
                return;
            }

            CameraManager manager = CameraManager.Instance;
            if (camera == null || camera != manager.Camera)
            {
                return;
            }

            float gameFov = manager.Fov;
            float target = TargetFov(gameFov);

            if (!_driving)
            {
                if (Mathf.Abs(target - gameFov) < SnapDegrees)
                {
                    return;
                }
                _driving = true;
                _current = camera.fieldOfView;
            }

            // onPreCull can run more than once a frame for the same camera; ease once a frame.
            if (_lastFrame != Time.frameCount)
            {
                _lastFrame = Time.frameCount;
                float step = 1f - Mathf.Exp(-Settings.ZoomSpeed.Value * Time.unscaledDeltaTime);
                _current = Mathf.Lerp(_current, target, step);
                if (Mathf.Abs(_current - target) < SnapDegrees)
                {
                    _current = target;
                }
            }
            camera.fieldOfView = _current;

            // Back at the game's own FOV with nothing to override: hand it back.
            if (!CameraModeState.Active || (_current == gameFov && target == gameFov))
            {
                if (Mathf.Abs(_current - gameFov) < SnapDegrees)
                {
                    camera.fieldOfView = gameFov;
                    _driving = false;
                }
            }
        }

        private static float TargetFov(float gameFov)
        {
            if (!CameraModeState.Active)
            {
                return gameFov;
            }
            Player player = CameraModeState.Player;
            if (player != null && player.HandsController != null && player.HandsController.IsAiming)
            {
                return gameFov;
            }
            if (_zoomHeld)
            {
                return Settings.ZoomFov.Value;
            }
            if (Settings.OverrideFov.Value)
            {
                return Settings.Fov.Value;
            }
            return gameFov;
        }
    }
}
