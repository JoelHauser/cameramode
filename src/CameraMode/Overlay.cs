using UnityEngine;

namespace CameraMode
{
    /// <summary>
    /// Letterbox bars and the composition grid, drawn with IMGUI on top of the picture while
    /// Camera Mode is on. The bars only preview the shape: screenshots are cropped to the frame
    /// (<see cref="Screenshot"/>). The grid is a guide and is skipped while a screenshot is taken.
    /// </summary>
    internal static class Overlay
    {
        private static readonly Color GridColor = new Color(1f, 1f, 1f, 0.35f);

        internal static void Draw(float scale)
        {
            if (!CameraModeState.Active)
            {
                return;
            }

            Rect frame = Frame(Ratio(Settings.LetterboxBars.Value));
            Color color = GUI.color;

            if (frame.width < Screen.width || frame.height < Screen.height)
            {
                GUI.color = Color.black;
                Texture2D white = Texture2D.whiteTexture;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, frame.yMin), white);
                GUI.DrawTexture(new Rect(0f, frame.yMax, Screen.width, Screen.height - frame.yMax), white);
                GUI.DrawTexture(new Rect(0f, frame.yMin, frame.xMin, frame.height), white);
                GUI.DrawTexture(new Rect(frame.xMax, frame.yMin, Screen.width - frame.xMax, frame.height), white);
            }

            if (Settings.CompositionGrid.Value && !Screenshot.Capturing)
            {
                GUI.color = GridColor;
                float line = Mathf.Max(1f, Mathf.Round(scale));
                for (int i = 1; i <= 2; i++)
                {
                    float x = frame.x + frame.width * i / 3f;
                    float y = frame.y + frame.height * i / 3f;
                    GUI.DrawTexture(new Rect(Mathf.Round(x), frame.y, line, frame.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(frame.x, Mathf.Round(y), frame.width, line), Texture2D.whiteTexture);
                }
            }

            GUI.color = color;
        }

        /// <summary>Width over height for each letterbox choice; 0 for none.</summary>
        internal static float Ratio(Letterbox letterbox)
        {
            switch (letterbox)
            {
                case Letterbox.Flat_1_85:
                    return 1.85f;
                case Letterbox.Wide_2_00:
                    return 2f;
                case Letterbox.Scope_2_39:
                    return 2.39f;
                case Letterbox.Ultra_2_76:
                    return 2.76f;
                case Letterbox.Classic_4_3:
                    return 4f / 3f;
                case Letterbox.Square_1_1:
                    return 1f;
                case Letterbox.Portrait_4_5:
                    return 0.8f;
                default:
                    return 0f;
            }
        }

        internal static string Label(Letterbox letterbox)
        {
            switch (letterbox)
            {
                case Letterbox.Flat_1_85:
                    return "1.85:1";
                case Letterbox.Wide_2_00:
                    return "2:1";
                case Letterbox.Scope_2_39:
                    return "2.39:1";
                case Letterbox.Ultra_2_76:
                    return "2.76:1";
                case Letterbox.Classic_4_3:
                    return "4:3";
                case Letterbox.Square_1_1:
                    return "1:1";
                case Letterbox.Portrait_4_5:
                    return "4:5";
                default:
                    return "Off";
            }
        }

        /// <summary>The largest centred rectangle of the given shape; the whole screen for none.</summary>
        /// <remarks>GUI coordinates: y counts down from the top.</remarks>
        internal static Rect Frame(float ratio)
        {
            float width = Screen.width;
            float height = Screen.height;
            if (ratio <= 0f)
            {
                return new Rect(0f, 0f, width, height);
            }
            if (width / height > ratio)
            {
                float framed = Mathf.Round(height * ratio);
                return new Rect(Mathf.Round((width - framed) / 2f), 0f, framed, height);
            }
            float tall = Mathf.Round(width / ratio);
            return new Rect(0f, Mathf.Round((height - tall) / 2f), width, tall);
        }
    }
}
