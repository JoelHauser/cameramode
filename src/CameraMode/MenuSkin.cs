using UnityEngine;

namespace CameraMode
{
    /// <summary>
    /// Colours, textures and GUIStyles for <see cref="MenuWindow"/>. IMGUI's stock skin is flat
    /// grey boxes, so the rounded shapes are drawn into small textures once and nine-sliced.
    /// Sizes are in 1080p units; the window scales the whole GUI to the screen height.
    /// </summary>
    internal sealed class MenuSkin
    {
        internal static readonly Color Background = new Color32(20, 22, 26, 246);
        internal static readonly Color Sidebar = new Color32(26, 28, 33, 255);
        internal static readonly Color Panel = new Color32(34, 37, 43, 255);
        internal static readonly Color PanelHover = new Color32(42, 46, 53, 255);
        internal static readonly Color Line = new Color32(48, 52, 60, 255);
        internal static readonly Color Text = new Color32(232, 232, 232, 255);
        internal static readonly Color Muted = new Color32(146, 152, 160, 255);
        internal static readonly Color Accent = new Color32(214, 186, 120, 255);
        internal static readonly Color AccentText = new Color32(30, 26, 18, 255);
        internal static readonly Color Off = new Color32(68, 73, 82, 255);
        internal static readonly Color OnState = new Color32(120, 200, 120, 255);

        internal readonly GUIStyle Window;
        internal readonly GUIStyle Title;
        internal readonly GUIStyle Subtitle;
        internal readonly GUIStyle Section;
        internal readonly GUIStyle Label;
        internal readonly GUIStyle Value;
        internal readonly GUIStyle Hint;
        internal readonly GUIStyle Help;
        internal readonly GUIStyle Button;
        internal readonly GUIStyle PrimaryButton;
        internal readonly GUIStyle Chip;
        internal readonly GUIStyle ChipActive;
        internal readonly GUIStyle KeyButton;
        internal readonly GUIStyle KeyButtonActive;
        internal readonly GUIStyle Reset;
        internal readonly GUIStyle Close;
        internal readonly GUIStyle Nav;
        internal readonly GUIStyle NavActive;
        internal readonly GUIStyle Card;
        internal readonly GUIStyle CardOn;
        internal readonly GUIStyle CardTitle;
        internal readonly GUIStyle CardTitleOn;
        internal readonly GUIStyle CardSub;
        internal readonly GUIStyle CardSubOn;
        internal readonly GUIStyle RowHover;
        internal readonly GUIStyle SidebarPanel;
        internal readonly GUIStyle TextField;
        internal readonly GUIStyle Toast;
        internal readonly GUIStyle SliderTrack;
        internal readonly GUIStyle SliderThumb;
        internal readonly GUIStyle ScrollTrack;
        internal readonly GUIStyle ScrollThumb;

        internal readonly Texture2D TrackOn;
        internal readonly Texture2D TrackOff;
        internal readonly Texture2D Knob;
        internal readonly Texture2D Dot;

        internal MenuSkin()
        {
            Texture2D button = Rounded(24, 6, Off);
            Texture2D buttonHover = Rounded(24, 6, new Color32(86, 92, 102, 255));
            Texture2D accent = Rounded(24, 6, Accent);
            Texture2D accentHover = Rounded(24, 6, new Color32(232, 206, 145, 255));
            Texture2D panel = Rounded(24, 8, Panel);
            Texture2D panelHover = Rounded(24, 8, PanelHover);

            TrackOn = Rounded(40, 20, Accent);
            TrackOff = Rounded(40, 20, Off);
            Knob = Rounded(32, 16, Color.white);
            Dot = Rounded(16, 8, Color.white);

            Window = new GUIStyle
            {
                normal = { background = Rounded(32, 12, Background) },
                border = new RectOffset(14, 14, 14, 14),
            };
            SidebarPanel = new GUIStyle
            {
                normal = { background = Rounded(24, 8, Sidebar) },
                border = new RectOffset(9, 9, 9, 9),
            };
            RowHover = new GUIStyle
            {
                normal = { background = Rounded(24, 7, PanelHover) },
                border = new RectOffset(8, 8, 8, 8),
            };

            Title = Text_(18, FontStyle.Bold, Text);
            Subtitle = Text_(11, FontStyle.Normal, Muted);
            Section = Text_(11, FontStyle.Bold, Accent);
            Section.alignment = TextAnchor.LowerLeft;
            Label = Text_(13, FontStyle.Normal, Text);
            Label.alignment = TextAnchor.MiddleLeft;
            Label.clipping = TextClipping.Clip;
            Value = Text_(13, FontStyle.Bold, Accent);
            Value.alignment = TextAnchor.MiddleRight;
            Hint = Text_(12, FontStyle.Normal, Muted);
            Hint.wordWrap = true;
            Hint.padding = new RectOffset(10, 10, 4, 6);
            Help = Text_(12, FontStyle.Normal, Muted);
            Help.wordWrap = true;
            Help.alignment = TextAnchor.MiddleLeft;

            Button = Box(button, buttonHover, 12, Text);
            PrimaryButton = Box(accent, accentHover, 13, AccentText);
            PrimaryButton.fontStyle = FontStyle.Bold;
            Chip = Box(panel, panelHover, 12, Text);
            ChipActive = Box(accent, accent, 12, AccentText);
            ChipActive.fontStyle = FontStyle.Bold;
            KeyButton = Box(button, buttonHover, 12, Text);
            KeyButton.fontStyle = FontStyle.Bold;
            KeyButtonActive = Box(accent, accent, 12, AccentText);
            KeyButtonActive.fontStyle = FontStyle.Bold;
            Reset = Box(Rounded(24, 6, new Color32(52, 56, 64, 255)), buttonHover, 11, Accent);
            Reset.padding = new RectOffset(2, 2, 1, 2);
            Close = Box(button, buttonHover, 15, Text);
            Close.padding = new RectOffset(0, 0, 0, 2);

            Nav = new GUIStyle(Box(null, panelHover, 13, Muted))
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 8, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            Nav.normal.background = null;
            NavActive = new GUIStyle(Box(panel, panel, 13, Text))
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 8, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                fontStyle = FontStyle.Bold,
            };

            Card = new GUIStyle(Box(panel, panelHover, 13, Text)) { padding = new RectOffset(0, 0, 0, 0) };
            CardOn = new GUIStyle(Box(Rounded(24, 8, new Color32(74, 66, 44, 255)), Rounded(24, 8, new Color32(88, 78, 52, 255)), 13, Text))
            {
                padding = new RectOffset(0, 0, 0, 0),
            };
            CardTitle = Text_(13, FontStyle.Bold, Text);
            CardTitleOn = Text_(13, FontStyle.Bold, Accent);
            CardSub = Text_(11, FontStyle.Normal, Muted);
            CardSubOn = Text_(11, FontStyle.Normal, new Color32(220, 206, 170, 255));

            TextField = new GUIStyle(Box(Rounded(24, 6, new Color32(14, 15, 18, 255)), Rounded(24, 6, new Color32(14, 15, 18, 255)), 12, Text))
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4),
                clipping = TextClipping.Clip,
            };

            Toast = new GUIStyle(Text_(18, FontStyle.Bold, Text))
            {
                normal = { background = Rounded(32, 12, new Color32(16, 17, 20, 220)), textColor = Text },
                border = new RectOffset(12, 12, 12, 12),
                padding = new RectOffset(20, 20, 10, 10),
                alignment = TextAnchor.MiddleCenter,
            };

            SliderTrack = new GUIStyle
            {
                normal = { background = Rounded(12, 3, Off) },
                border = new RectOffset(4, 4, 4, 4),
                fixedHeight = 6,
            };
            SliderThumb = new GUIStyle
            {
                normal = { background = Knob },
                hover = { background = Knob },
                active = { background = Knob },
                fixedWidth = 16,
                fixedHeight = 16,
                margin = new RectOffset(0, 0, -5, 0),
            };

            // Named like the stock styles: the scroll view looks its thumb up as name + "thumb".
            ScrollTrack = new GUIStyle
            {
                name = "verticalscrollbar",
                normal = { background = Rounded(12, 4, new Color32(28, 30, 35, 255)) },
                border = new RectOffset(5, 5, 5, 5),
                fixedWidth = 8,
                margin = new RectOffset(6, 0, 0, 0),
            };
            ScrollThumb = new GUIStyle
            {
                name = "verticalscrollbarthumb",
                normal = { background = Rounded(12, 4, new Color32(80, 86, 96, 255)) },
                hover = { background = Rounded(12, 4, new Color32(100, 106, 116, 255)) },
                active = { background = Rounded(12, 4, Accent) },
                border = new RectOffset(5, 5, 5, 5),
                fixedWidth = 8,
            };
        }

        private static GUIStyle Text_(int size, FontStyle style, Color color)
        {
            return new GUIStyle
            {
                fontSize = size,
                fontStyle = style,
                normal = { textColor = color },
                richText = false,
                wordWrap = false,
            };
        }

        private static GUIStyle Box(Texture2D normal, Texture2D hover, int size, Color text)
        {
            return new GUIStyle
            {
                normal = { background = normal, textColor = text },
                hover = { background = hover, textColor = text },
                active = { background = hover, textColor = text },
                focused = { background = normal, textColor = text },
                border = new RectOffset(7, 7, 7, 7),
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter,
                fontSize = size,
            };
        }

        /// <summary>A square texture with rounded corners and an anti-aliased edge.</summary>
        private static Texture2D Rounded(int size, int radius, Color color)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float distance = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
