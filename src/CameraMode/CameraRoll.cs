using EFT.CameraControl;
using UnityEngine;

namespace CameraMode
{
    /// <summary>
    /// Camera roll (a Dutch angle) while Camera Mode is on.
    ///
    /// The main camera's rotation belongs to the game: it sits under the player's procedural
    /// animation and is posed every frame. So the roll is added only for the render, in
    /// Camera.onPreCull, and the rotation taken off again in Camera.onPostRender. Nothing that
    /// reads the camera between frames (aiming, audio, the game's own camera code) ever sees it.
    /// </summary>
    internal static class CameraRoll
    {
        private static Camera _rolled;
        private static Quaternion _savedRotation;

        internal static void Install()
        {
            Camera.onPreCull += OnPreCull;
            Camera.onPostRender += OnPostRender;
        }

        private static void OnPreCull(Camera camera)
        {
            if (_rolled != null || !CameraModeState.Active || Settings.Roll.Value == 0 || camera == null)
            {
                return;
            }
            if (camera != CameraManager.Instance.Camera)
            {
                return;
            }
            Transform transform = camera.transform;
            _savedRotation = transform.localRotation;
            transform.rotation *= Quaternion.AngleAxis(Settings.Roll.Value, Vector3.forward);
            _rolled = camera;
        }

        private static void OnPostRender(Camera camera)
        {
            if (_rolled == null || camera != _rolled)
            {
                return;
            }
            camera.transform.localRotation = _savedRotation;
            _rolled = null;
        }
    }
}
