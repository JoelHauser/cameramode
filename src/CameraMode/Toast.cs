using UnityEngine;

namespace CameraMode
{
    /// <summary>
    /// The short "Camera Mode on/off" message at the top of the screen. Drawn with IMGUI, so it is
    /// not part of the HUD Camera Mode hides; it fades out on its own. Off with the
    /// "Show on and off message" setting.
    /// </summary>
    internal static class Toast
    {
        private const float Duration = 1.6f;
        private const float FadeTime = 0.5f;

        private static string _text;
        private static float _shownAt = -100f;

        /// <param name="force">Show even with on/off messages turned off (screenshot saved).</param>
        internal static void Show(string text, bool force = false)
        {
            if (!force && !Settings.ShowMessages.Value)
            {
                return;
            }
            _text = text;
            _shownAt = Time.unscaledTime;
        }

        internal static void Draw(MenuSkin skin, float scale)
        {
            float age = Time.unscaledTime - _shownAt;
            if (_text == null || age > Duration || Screenshot.Capturing)
            {
                return;
            }
            float alpha = Mathf.Clamp01((Duration - age) / FadeTime);

            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.color = new Color(1f, 1f, 1f, alpha);

            var content = new GUIContent(_text);
            Vector2 size = skin.Toast.CalcSize(content);
            float width = Screen.width / scale;
            GUI.Label(new Rect((width - size.x) / 2f, 60f, size.x, size.y), content, skin.Toast);

            GUI.color = color;
            GUI.matrix = matrix;
        }
    }
}
