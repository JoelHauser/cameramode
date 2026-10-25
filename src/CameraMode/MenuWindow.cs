using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using EFT;
using EFT.GlobalEvents;
using HarmonyLib;
using UnityEngine;

namespace CameraMode
{
    /// <summary>
    /// The in-game Camera Mode menu (default F10, changeable on the Keys page).
    ///
    /// Layout: a quick bar of cards for the four actions (Camera Mode, UFO, Freeze AI,
    /// Screenshot) with their state and key; a sidebar of pages; one-line rows; and a help bar at
    /// the bottom that shows what the hovered option does. Options that depend on another one are
    /// greyed out rather than hidden, so nothing jumps around. Every option changed from its
    /// default shows a Reset button. It edits the same ConfigEntries as the F12 menu.
    ///
    /// Input while open:
    /// - The cursor is shown with the game's own ToggleShowInGameCursorEvent (what the Lighthouse
    ///   keeper zone uses). InputManager.Update re-hides the cursor every frame whenever it differs
    ///   from what the screen wants, unless that event's force-show flag is set.
    /// - GamePlayerOwner.SetIgnoreInput(true), the flag every in-raid screen sets, keeps the
    ///   player from walking, turning and shooting. Its previous value (private static bool_0,
    ///   the only field the setter stores to) is put back on close.
    /// - Input.ResetInputAxes once a frame, for anything else that reads Unity's axes.
    /// Escape closes the menu.
    ///
    /// Rows are laid out by hand inside a rect from GUILayoutUtility.GetRect, so every row of a
    /// kind lines up. Every change a control makes is queued with <see cref="Defer"/> and applied
    /// after the window is drawn: IMGUI lays a window out in one event and draws it in the next,
    /// and a change in between would throw "Getting control N's position" errors.
    /// </summary>
    internal static class MenuWindow
    {
        private const int WindowId = 0x43414d31;
        private const float Width = 680f;
        private const float MaxHeight = 720f;
        private const float HeaderHeight = 56f;
        private const float CardsTop = 62f;
        private const float CardHeight = 54f;
        private const float BodyTop = CardsTop + CardHeight + 14f;
        private const float FooterHeight = 40f;
        private const float SidebarWidth = 140f;
        private const float RowHeight = 34f;
        private const float ResetWidth = 46f;
        private const float KeyWidth = 128f;
        private const float LabelWidth = 168f;

        private const string DefaultHelp = "Hover over an option to see what it does.   Esc closes the menu.";

        /// <summary>Read once when the menu opens, so plain reflection is enough.</summary>
        private static readonly FieldInfo IgnoreInputField = AccessTools.Field(typeof(GamePlayerOwner), "bool_0");

        private static readonly string[] Pages = { "Hide", "Effects", "Camera", "Player & AI", "UFO", "Keys", "Settings" };

        private static readonly Letterbox[] LetterboxChoices =
        {
            Letterbox.Off, Letterbox.Flat_1_85, Letterbox.Wide_2_00, Letterbox.Scope_2_39,
            Letterbox.Ultra_2_76, Letterbox.Classic_4_3, Letterbox.Square_1_1, Letterbox.Portrait_4_5,
        };

        private static readonly List<Action> Pending = new List<Action>();

        private static ConfigFile _config;
        private static MonoBehaviour _host;
        private static MenuSkin _skin;
        private static Rect _rect;
        private static bool _placed;
        private static int _page;
        private static Vector2 _scroll;

        private static bool _savedIgnoreInput;
        private static int _resetFrame = -1;

        private static ConfigEntry<KeyboardShortcut> _capturing;
        private static ConfigEntry<KeyCode> _capturingKeyCode;
        private static string _extraObjectsDraft;

        /// <summary>What the help bar shows; set by whichever row or card the mouse is over.</summary>
        private static string _help;

        /// <summary>The mouse is inside the scrolling content area (not over a row scrolled out of view).</summary>
        private static bool _mouseInContent;

        /// <summary>Greys out and disables the rows drawn while it is false.</summary>
        private static bool _rowsEnabled = true;

        internal static bool IsOpen { get; private set; }

        internal static bool IsCapturingKey => _capturing != null || _capturingKeyCode != null;

        /// <summary>The scale the menu and message are drawn at: 1 at 1080p, larger above.</summary>
        internal static float Scale => Mathf.Clamp(Screen.height / 1080f, 1f, 2.5f);

        internal static MenuSkin Skin => _skin ?? (_skin = new MenuSkin());

        internal static void Init(ConfigFile config, MonoBehaviour host)
        {
            _config = config;
            _host = host;
        }

        internal static void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>Every frame from the plugin's Update.</summary>
        internal static void Update()
        {
            if (!IsCapturingKey && Settings.MenuKey.Value.IsDown())
            {
                Toggle();
            }
            if (IsOpen)
            {
                // Re-asserted every frame: a screen change underneath can clear it.
                GamePlayerOwner.SetIgnoreInput(true);
            }
        }

        internal static void Draw()
        {
            if (!IsOpen)
            {
                return;
            }

            // Before the window, so keys reach it whether or not the window has focus. When it
            // changes something, the window sits this event out (it is a key or click event, not
            // a repaint) rather than being drawn with a layout from before the change.
            if (HandleEvents() || !IsOpen)
            {
                return;
            }

            float scale = Scale;
            float screenWidth = Screen.width / scale;
            float screenHeight = Screen.height / scale;
            float height = Mathf.Min(MaxHeight, screenHeight - 60f);
            if (!_placed)
            {
                _rect = new Rect(screenWidth - Width - 40f, Mathf.Max(20f, (screenHeight - height) / 2f), Width, height);
                _placed = true;
            }
            _rect.height = height;

            Matrix4x4 matrix = GUI.matrix;
            GUISkin gui = GUI.skin;
            GUIStyle scrollbar = gui.verticalScrollbar;
            GUIStyle thumb = gui.verticalScrollbarThumb;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            // The scroll view takes its thumb from the global skin, which other IMGUI mods share:
            // swapped in for this window only.
            gui.verticalScrollbar = Skin.ScrollTrack;
            gui.verticalScrollbarThumb = Skin.ScrollThumb;
            try
            {
                _rect = GUI.Window(WindowId, _rect, DrawWindow, GUIContent.none, Skin.Window);
            }
            finally
            {
                gui.verticalScrollbar = scrollbar;
                gui.verticalScrollbarThumb = thumb;
                GUI.matrix = matrix;
            }
            _rect.x = Mathf.Clamp(_rect.x, 0f, Mathf.Max(0f, screenWidth - _rect.width));
            _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, screenHeight - _rect.height));

            if (Pending.Count > 0)
            {
                Action[] pending = Pending.ToArray();
                Pending.Clear();
                foreach (Action action in pending)
                {
                    action();
                }
            }

            if (IsOpen && !IsCapturingKey && _resetFrame != Time.frameCount)
            {
                _resetFrame = Time.frameCount;
                Input.ResetInputAxes();
            }
        }

        private static void Defer(Action action)
        {
            Pending.Add(action);
        }

        private static void Open()
        {
            _savedIgnoreInput = IgnoreInputField != null && (bool)IgnoreInputField.GetValue(null);
            _extraObjectsDraft = Settings.ExtraObjects.Value;
            Pending.Clear();
            IsOpen = true;
            GamePlayerOwner.SetIgnoreInput(true);
            ShowGameCursor(true);
        }

        private static void Close()
        {
            IsOpen = false;
            _capturing = null;
            _capturingKeyCode = null;
            Pending.Clear();
            GamePlayerOwner.SetIgnoreInput(_savedIgnoreInput);
            ShowGameCursor(false);
        }

        private static void ShowGameCursor(bool show)
        {
            try
            {
                GlobalEventsController.Instance.CreateCommonEvent<ToggleShowInGameCursorEvent>().Invoke(show);
            }
            catch (Exception e)
            {
                CameraModePlugin.Log.LogWarning($"Menu: could not {(show ? "show" : "hide")} the cursor: {e.Message}");
            }
        }

        // ---- window ---------------------------------------------------------------------------

        private static void DrawWindow(int id)
        {
            MenuSkin skin = Skin;
            _help = null;
            float width = _rect.width;
            float height = _rect.height;

            // Header.
            GUI.Label(new Rect(22f, 14f, 300f, 24f), "Camera Mode", skin.Title);
            GUI.Label(new Rect(22f, 36f, width - 100f, 16f),
                $"v{CameraModePlugin.PluginVersion}   ·   menu key {KeyText(Settings.MenuKey.Value)}   ·   drag here to move", skin.Subtitle);
            if (GUI.Button(new Rect(width - 46f, 14f, 28f, 28f), "×", skin.Close))
            {
                Defer(Close);
            }

            DrawCards(skin, new Rect(18f, CardsTop, width - 36f, CardHeight));

            // Sidebar.
            var sidebar = new Rect(14f, BodyTop, SidebarWidth, height - BodyTop - FooterHeight - 6f);
            if (Event.current.type == EventType.Repaint)
            {
                skin.SidebarPanel.Draw(sidebar, false, false, false, false);
            }
            for (int i = 0; i < Pages.Length; i++)
            {
                int page = i;
                var item = new Rect(sidebar.x + 6f, sidebar.y + 8f + i * 36f, sidebar.width - 12f, 32f);
                if (GUI.Button(item, Pages[i], i == _page ? skin.NavActive : skin.Nav))
                {
                    Defer(() =>
                    {
                        _page = page;
                        _scroll = Vector2.zero;
                        _capturing = null;
                        _capturingKeyCode = null;
                    });
                }
                if (i == _page && Event.current.type == EventType.Repaint)
                {
                    Fill(new Rect(item.x, item.y + 7f, 3f, item.height - 14f), MenuSkin.Accent);
                }
            }

            // Page content.
            var content = new Rect(sidebar.xMax + 12f, BodyTop, width - sidebar.xMax - 12f - 14f, sidebar.height);
            _mouseInContent = content.Contains(Event.current.mousePosition);
            GUILayout.BeginArea(content);
            _scroll = GUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            _rowsEnabled = true;
            switch (_page)
            {
                case 0:
                    DrawHidePage(skin);
                    break;
                case 1:
                    DrawEffectsPage(skin);
                    break;
                case 2:
                    DrawCameraPage(skin);
                    break;
                case 3:
                    DrawPlayerPage(skin);
                    break;
                case 4:
                    DrawUfoPage(skin);
                    break;
                case 5:
                    DrawKeysPage(skin);
                    break;
                default:
                    DrawSettingsPage(skin);
                    break;
            }
            _rowsEnabled = true;
            GUILayout.Space(10f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Help bar.
            var footer = new Rect(18f, height - FooterHeight - 4f, width - 36f, FooterHeight);
            if (Event.current.type == EventType.Repaint)
            {
                Fill(new Rect(footer.x, footer.y, footer.width, 1f), MenuSkin.Line);
            }
            GUI.Label(new Rect(footer.x + 4f, footer.y + 2f, footer.width - 8f, footer.height - 2f), _help ?? DefaultHelp, skin.Help);

            GUI.DragWindow(new Rect(0f, 0f, width - 56f, HeaderHeight));
        }

        private static void DrawCards(MenuSkin skin, Rect area)
        {
            const float gap = 8f;
            float cardWidth = (area.width - gap * 3f) / 4f;
            bool inRaid = CameraModeState.CanTurnOn;

            Card(skin, new Rect(area.x, area.y, cardWidth, area.height), "Camera Mode",
                CameraModeState.Active, Settings.ToggleKey.Value, CameraModeState.Active || inRaid,
                CameraModeState.Toggle,
                "Clean view: hides the HUD and your hands, plus whatever is set on the other pages. Works in raid.");
            Card(skin, new Rect(area.x + (cardWidth + gap), area.y, cardWidth, area.height), "UFO",
                UfoMode.Active, Settings.UfoKey.Value, UfoMode.Active || inRaid,
                UfoMode.Toggle,
                "Fly anywhere, through walls. Turn it off and you land on whatever is below you.");
            Card(skin, new Rect(area.x + (cardWidth + gap) * 2f, area.y, cardWidth, area.height), "Freeze AI",
                FreezeAi.Engaged, Settings.FreezeKey.Value, FreezeAi.Engaged || inRaid,
                CameraModeState.ToggleFreeze,
                "Freeze every bot in place, or let them go again. Works any time in raid.");
            Card(skin, new Rect(area.x + (cardWidth + gap) * 3f, area.y, cardWidth, area.height), "Screenshot",
                null, Settings.ScreenshotKey.Value, true,
                () => Screenshot.Take(_host),
                "Save the screen without this menu to Documents\\Escape from Tarkov\\Screenshots.");
        }

        /// <param name="on">Null for an action without an on/off state.</param>
        private static void Card(MenuSkin skin, Rect rect, string title, bool? on, KeyboardShortcut key, bool enabled,
            Action toggle, string help)
        {
            bool isOn = on == true;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = enabled;
            if (rect.Contains(Event.current.mousePosition))
            {
                _help = enabled ? help : help + "  (Not available outside a raid.)";
            }
            if (GUI.Button(rect, GUIContent.none, isOn ? skin.CardOn : skin.Card))
            {
                Defer(toggle);
            }
            if (Event.current.type == EventType.Repaint)
            {
                Color color = GUI.color;
                if (!enabled)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.45f);
                }
                float x = rect.x + 12f;
                if (on.HasValue)
                {
                    Color dot = GUI.color;
                    GUI.color = isOn ? MenuSkin.OnState : MenuSkin.Off;
                    GUI.DrawTexture(new Rect(x, rect.y + 13f, 9f, 9f), skin.Dot);
                    GUI.color = dot;
                    x += 15f;
                }
                GUI.Label(new Rect(x, rect.y + 7f, rect.width - (x - rect.x) - 6f, 20f), title, isOn ? skin.CardTitleOn : skin.CardTitle);
                string state = on.HasValue ? (isOn ? "On" : "Off") : "Save image";
                GUI.Label(new Rect(rect.x + 12f, rect.y + 30f, rect.width - 18f, 16f), $"{state}   ·   {KeyText(key)}", isOn ? skin.CardSubOn : skin.CardSub);
                GUI.color = color;
            }
            GUI.enabled = wasEnabled;
        }

        // ---- pages ----------------------------------------------------------------------------

        private static void DrawHidePage(MenuSkin skin)
        {
            Section(skin, "HUD");
            Switch(skin, Settings.HideBattleHud);
            Switch(skin, Settings.HideRaidPanels);
            Switch(skin, Settings.HideNotifications);
            Switch(skin, Settings.HideOverlays);

            Section(skin, "YOUR MODEL");
            Switch(skin, Settings.HideArms);
            Switch(skin, Settings.HideHeldItem);
            Switch(skin, Settings.HideWatch);
            Switch(skin, Settings.HideBody);
            Switch(skin, Settings.KeepShadows);

            Section(skin, "SCREEN EFFECTS");
            Switch(skin, Settings.HideVisor);
            Switch(skin, Settings.HideBlood);
            Switch(skin, Settings.HideRain);

            Hint(skin, "These apply while Camera Mode is on.");
        }

        private static void DrawEffectsPage(MenuSkin skin)
        {
            Section(skin, "PRESETS");
            Chips(skin, Presets.All.Length, i => Presets.All[i].Name, i => Presets.IsCurrent(Presets.All[i]), i =>
            {
                Presets.Look look = Presets.IsCurrent(Presets.All[i]) ? Presets.Default : Presets.All[i];
                Presets.Apply(look, _config);
            }, "Each preset is a whole look. Click the lit one again, or Default (EFT), for the game's own picture.");

            Section(skin, "COLOUR");
            IntSlider(skin, Settings.Brightness, Signed);
            IntSlider(skin, Settings.Contrast, Signed);
            IntSlider(skin, Settings.Saturation, Signed);
            IntSlider(skin, Settings.Temperature, Signed);
            IntSlider(skin, Settings.Tint, Signed);
            IntSlider(skin, Settings.HueShift, v => Signed(v) + "°");

            Section(skin, "LENS");
            IntSlider(skin, Settings.Vignette, OffAtZero);
            Indented(Settings.Vignette.Value > 0, () => IntSlider(skin, Settings.VignetteSoftness, Plain));
            IntSlider(skin, Settings.Grain, OffAtZero);
            Indented(Settings.Grain.Value > 0, () => IntSlider(skin, Settings.GrainSize, Plain));
            IntSlider(skin, Settings.ChromaticAberration, OffAtZero);
            IntSlider(skin, Settings.LensDistortion, v => v == 0 ? "Off" : Signed(v));
            IntSlider(skin, Settings.Bloom, OffAtZero);

            Section(skin, "FRAME");
            Chips(skin, LetterboxChoices.Length, i => Overlay.Label(LetterboxChoices[i]),
                i => Settings.LetterboxBars.Value == LetterboxChoices[i],
                i => Settings.LetterboxBars.Value = LetterboxChoices[i],
                Settings.LetterboxBars.Description.Description);
            Switch(skin, Settings.CompositionGrid);

            ActionRow(skin, "Reset all effects", "Put every photo effect back to the game's own picture.",
                () => Presets.Apply(Presets.Default, _config));
            Hint(skin, "Effects show while Camera Mode is on.");
        }

        private static void DrawCameraPage(MenuSkin skin)
        {
            Section(skin, "FIELD OF VIEW");
            Switch(skin, Settings.OverrideFov);
            Indented(Settings.OverrideFov.Value, () => IntSlider(skin, Settings.Fov, Degrees));

            Section(skin, "ZOOM");
            KeyBinder(skin, Settings.ZoomKey);
            IntSlider(skin, Settings.ZoomFov, Degrees);
            FloatSlider(skin, Settings.ZoomSpeed, 0.5f, v => v.ToString("0.0"));

            Section(skin, "ANGLE");
            IntSlider(skin, Settings.Roll, v => v == 0 ? "Level" : Signed(v) + "°");

            Section(skin, "DEPTH OF FIELD");
            Switch(skin, Settings.DepthOfField);
            bool dof = Settings.DepthOfField.Value;
            Indented(dof, () => Switch(skin, Settings.AutoFocus));
            Indented(dof && !Settings.AutoFocus.Value, () => FloatSlider(skin, Settings.FocusDistance, 0.1f, v => v.ToString("0.0") + " m"));
            Indented(dof, () => FloatSlider(skin, Settings.Aperture, 0.1f, v => "f/" + v.ToString("0.0")));
            Indented(dof, () => IntSlider(skin, Settings.FocalLength, v => v + " mm"));

            Hint(skin, "These apply while Camera Mode is on. Aiming down sights always uses the game's own zoom.");
        }

        private static void DrawPlayerPage(MenuSkin skin)
        {
            Section(skin, "YOU, WHILE CAMERA MODE IS ON");
            Switch(skin, Settings.NoDamage);
            Switch(skin, Settings.InfiniteStamina);
            Switch(skin, Settings.NoHungerThirst);

            Section(skin, "AI");
            Switch(skin, Settings.AiIgnore);
            Switch(skin, Settings.FreezeAi);
            Switch(skin, Settings.FreezePoses);
            KeyBinder(skin, Settings.FreezeKey);

            Hint(skin, "The Freeze AI key (or the Freeze AI card above) freezes bots any time in raid, with or without Camera Mode.");
        }

        private static void DrawUfoPage(MenuSkin skin)
        {
            Section(skin, "FLYING");
            FloatSlider(skin, Settings.UfoSpeed, 0.5f, v => v.ToString("0.0") + " m/s");
            FloatSlider(skin, Settings.UfoFastMultiplier, 0.5f, v => "×" + v.ToString("0.0"));
            Switch(skin, Settings.UfoFlyWhereYouLook);
            KeyCodeBinder(skin, Settings.UfoUpKey);
            KeyCodeBinder(skin, Settings.UfoDownKey);
            KeyCodeBinder(skin, Settings.UfoFastKey);

            Section(skin, "LANDING");
            Switch(skin, Settings.UfoLandOnGround);
            Switch(skin, Settings.UfoBlockInsideWalls);

            Section(skin, "WHILE FLYING");
            Switch(skin, Settings.UfoInvisible);
            Switch(skin, Settings.UfoNoDamage);

            Hint(skin, "Fly with your normal movement keys. UFO works on its own or together with Camera Mode, and you stay wherever you are when it turns off.");
        }

        private static void DrawKeysPage(MenuSkin skin)
        {
            Section(skin, "MODES");
            KeyBinder(skin, Settings.ToggleKey);
            Switch(skin, Settings.HoldToUse);
            KeyBinder(skin, Settings.UfoKey);
            KeyBinder(skin, Settings.FreezeKey);
            KeyBinder(skin, Settings.ScreenshotKey);
            KeyBinder(skin, Settings.MenuKey);

            Section(skin, "CAMERA");
            KeyBinder(skin, Settings.ZoomKey);

            Section(skin, "UFO FLYING");
            KeyCodeBinder(skin, Settings.UfoUpKey);
            KeyCodeBinder(skin, Settings.UfoDownKey);
            KeyCodeBinder(skin, Settings.UfoFastKey);

            Hint(skin, "Click a key, then press the new one (Ctrl, Shift and Alt combinations work). Escape cancels.");
        }

        private static void DrawSettingsPage(MenuSkin skin)
        {
            Section(skin, "MESSAGES");
            Switch(skin, Settings.ShowMessages);

            Section(skin, "OTHER MODS' HUD");
            Rect row = Row(RowHeight + 4f, Settings.ExtraObjects.Description.Description);
            var field = new Rect(row.x + 10f, row.y + 5f, row.width - 10f - 70f - ResetWidth - 16f, 26f);
            _extraObjectsDraft = GUI.TextField(field, _extraObjectsDraft ?? "", skin.TextField);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && _extraObjectsDraft != Settings.ExtraObjects.Value;
            if (GUI.Button(new Rect(field.xMax + 6f, row.y + 5f, 64f, 26f), "Apply", skin.Button))
            {
                string draft = _extraObjectsDraft;
                Defer(() => Settings.ExtraObjects.Value = draft);
            }
            GUI.enabled = enabled;
            ResetButton(skin, Settings.ExtraObjects, new Rect(row.xMax - ResetWidth - 6f, row.y + 7f, ResetWidth, 22f));
            Hint(skin, "GameObject names, separated by commas. Find a name in another mod's notes or with a scene viewer.");

            Section(skin, "RESET");
            ActionRow(skin, "Reset everything", "Put every Camera Mode setting back to its default, keys included.",
                () => ResetSection(null));
        }

        // ---- rows -----------------------------------------------------------------------------

        private static void Section(MenuSkin skin, string title)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 30f, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 10f, rect.height - 6f), title, skin.Section);
            if (Event.current.type == EventType.Repaint)
            {
                Fill(new Rect(rect.x + 10f, rect.yMax - 3f, rect.width - 20f, 1f), MenuSkin.Line);
            }
        }

        private static void Hint(MenuSkin skin, string text)
        {
            GUILayout.Space(2f);
            GUILayout.Label(text, skin.Hint);
        }

        /// <summary>Draws <paramref name="row"/> greyed out and disabled unless <paramref name="enabled"/>.</summary>
        private static void Indented(bool enabled, Action row)
        {
            bool previous = _rowsEnabled;
            _rowsEnabled = previous && enabled;
            row();
            _rowsEnabled = previous;
        }

        /// <summary>One row's rect, with the hover highlight and its help text.</summary>
        private static Rect Row(float height, string help)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true));
            bool hover = _mouseInContent && rect.Contains(Event.current.mousePosition);
            if (hover)
            {
                _help = help;
                if (Event.current.type == EventType.Repaint)
                {
                    Skin.RowHover.Draw(rect, false, false, false, false);
                }
            }
            return rect;
        }

        private static void BeginRow(out bool enabled, out Color color)
        {
            enabled = GUI.enabled;
            color = GUI.color;
            if (!_rowsEnabled)
            {
                GUI.enabled = false;
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
            }
        }

        private static void EndRow(bool enabled, Color color)
        {
            GUI.enabled = enabled;
            GUI.color = color;
        }

        private static void Switch(MenuSkin skin, ConfigEntry<bool> entry)
        {
            Rect row = Row(RowHeight, entry.Description.Description);
            BeginRow(out bool enabled, out Color color);

            var track = new Rect(row.xMax - 52f, row.y + 6f, 42f, 22f);
            // Before the row-wide button, so a click on Reset is its own and not a toggle.
            ResetButton(skin, entry, new Rect(track.x - ResetWidth - 10f, row.y + 6f, ResetWidth, 22f));
            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                Defer(() => entry.Value = !entry.Value);
            }

            GUI.Label(new Rect(row.x + 10f, row.y, track.x - row.x - ResetWidth - 24f, row.height), entry.Definition.Key, skin.Label);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(track, entry.Value ? skin.TrackOn : skin.TrackOff);
                float knob = track.height - 6f;
                GUI.DrawTexture(new Rect(entry.Value ? track.xMax - knob - 3f : track.x + 3f, track.y + 3f, knob, knob), skin.Knob);
            }
            EndRow(enabled, color);
        }

        private static void IntSlider(MenuSkin skin, ConfigEntry<int> entry, Func<int, string> format)
        {
            var range = (AcceptableValueRange<int>)entry.Description.AcceptableValues;
            float value = SliderRow(skin, entry, format(entry.Value), entry.Value, range.MinValue, range.MaxValue);
            int rounded = Mathf.RoundToInt(value);
            if (rounded != entry.Value)
            {
                Defer(() => entry.Value = rounded);
            }
        }

        private static void FloatSlider(MenuSkin skin, ConfigEntry<float> entry, float step, Func<float, string> format)
        {
            var range = (AcceptableValueRange<float>)entry.Description.AcceptableValues;
            float value = SliderRow(skin, entry, format(entry.Value), entry.Value, range.MinValue, range.MaxValue);
            value = Mathf.Clamp(Mathf.Round(value / step) * step, range.MinValue, range.MaxValue);
            if (!Mathf.Approximately(value, entry.Value))
            {
                Defer(() => entry.Value = value);
            }
        }

        private static float SliderRow(MenuSkin skin, ConfigEntryBase entry, string shown, float value, float min, float max)
        {
            Rect row = Row(RowHeight, entry.Description.Description);
            BeginRow(out bool enabled, out Color color);

            GUI.Label(new Rect(row.x + 10f, row.y, LabelWidth - 10f, row.height), entry.Definition.Key, skin.Label);
            var shownRect = new Rect(row.xMax - 66f, row.y, 58f, row.height);
            GUI.Label(shownRect, shown, skin.Value);
            var reset = new Rect(shownRect.x - ResetWidth - 8f, row.y + 6f, ResetWidth, 22f);
            ResetButton(skin, entry, reset);
            var slider = new Rect(row.x + LabelWidth + 4f, row.y + 14f, reset.x - (row.x + LabelWidth + 4f) - 14f, 6f);
            float result = GUI.HorizontalSlider(slider, value, min, max, skin.SliderTrack, skin.SliderThumb);

            EndRow(enabled, color);
            return result;
        }

        private static void KeyBinder(MenuSkin skin, ConfigEntry<KeyboardShortcut> entry)
        {
            bool capturing = _capturing == entry;
            Rect row = Row(RowHeight, capturing ? "Press the new key or mouse button. Escape cancels." : entry.Description.Description);
            BeginRow(out bool enabled, out Color color);

            GUI.Label(new Rect(row.x + 10f, row.y, row.width - KeyWidth - ResetWidth - 80f, row.height), entry.Definition.Key, skin.Label);
            var key = new Rect(row.xMax - KeyWidth - 8f, row.y + 4f, KeyWidth, 26f);
            if (GUI.Button(key, capturing ? "Press a key…" : KeyText(entry.Value), capturing ? skin.KeyButtonActive : skin.KeyButton))
            {
                Defer(() =>
                {
                    _capturingKeyCode = null;
                    _capturing = capturing ? null : entry;
                });
            }
            float left = key.x - 6f;
            if (!capturing && entry.Value.MainKey != KeyCode.None)
            {
                var clear = new Rect(left - 26f, row.y + 6f, 24f, 22f);
                if (GUI.Button(clear, "×", skin.Reset))
                {
                    Defer(() => entry.Value = KeyboardShortcut.Empty);
                }
                if (clear.Contains(Event.current.mousePosition))
                {
                    _help = "Clear this key, so nothing is bound to it.";
                }
                left = clear.x - 6f;
            }
            ResetButton(skin, entry, new Rect(left - ResetWidth, row.y + 6f, ResetWidth, 22f));

            EndRow(enabled, color);
        }

        /// <summary>A single key with no modifiers (UFO up, down and fast, which are held together).</summary>
        private static void KeyCodeBinder(MenuSkin skin, ConfigEntry<KeyCode> entry)
        {
            bool capturing = _capturingKeyCode == entry;
            Rect row = Row(RowHeight, capturing ? "Press the new key or mouse button. Escape cancels." : entry.Description.Description);
            BeginRow(out bool enabled, out Color color);

            GUI.Label(new Rect(row.x + 10f, row.y, row.width - KeyWidth - ResetWidth - 40f, row.height), entry.Definition.Key, skin.Label);
            var key = new Rect(row.xMax - KeyWidth - 8f, row.y + 4f, KeyWidth, 26f);
            if (GUI.Button(key, capturing ? "Press a key…" : KeyName(entry.Value), capturing ? skin.KeyButtonActive : skin.KeyButton))
            {
                Defer(() =>
                {
                    _capturing = null;
                    _capturingKeyCode = capturing ? null : entry;
                });
            }
            ResetButton(skin, entry, new Rect(key.x - ResetWidth - 6f, row.y + 6f, ResetWidth, 22f));

            EndRow(enabled, color);
        }

        /// <summary>A grid of buttons, four to a row; the lit ones are "current".</summary>
        private static void Chips(MenuSkin skin, int count, Func<int, string> label, Func<int, bool> lit, Action<int> click, string help)
        {
            const int perRow = 4;
            const float gap = 6f;
            for (int i = 0; i < count; i += perRow)
            {
                Rect row = Row(32f, help);
                float chipWidth = (row.width - 20f - gap * (perRow - 1)) / perRow;
                for (int j = i; j < i + perRow && j < count; j++)
                {
                    int index = j;
                    var chip = new Rect(row.x + 10f + (j - i) * (chipWidth + gap), row.y + 3f, chipWidth, 26f);
                    if (GUI.Button(chip, label(j), lit(j) ? skin.ChipActive : skin.Chip))
                    {
                        Defer(() => click(index));
                    }
                }
            }
            GUILayout.Space(4f);
        }

        private static void ActionRow(MenuSkin skin, string title, string help, Action action)
        {
            Rect row = Row(RowHeight + 4f, help);
            GUI.Label(new Rect(row.x + 10f, row.y, row.width - 150f, row.height), help, skin.Hint);
            if (GUI.Button(new Rect(row.xMax - 138f, row.y + 5f, 130f, 28f), title, skin.Button))
            {
                Defer(action);
            }
        }

        /// <summary>
        /// Puts one setting back to its default. Drawn only while the setting differs from it.
        /// </summary>
        private static void ResetButton(MenuSkin skin, ConfigEntryBase entry, Rect rect)
        {
            if (Equals(entry.BoxedValue, entry.DefaultValue))
            {
                return;
            }
            if (GUI.Button(rect, "Reset", skin.Reset))
            {
                Defer(() =>
                {
                    entry.BoxedValue = entry.DefaultValue;
                    _extraObjectsDraft = Settings.ExtraObjects.Value;
                });
            }
            if (rect.Contains(Event.current.mousePosition))
            {
                _help = $"Put \"{entry.Definition.Key}\" back to its default: {DefaultText(entry)}.";
            }
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // ---- keys -----------------------------------------------------------------------------

        /// <summary>
        /// Escape (close or cancel) and the key being bound. Runs before the window is drawn.
        /// Returns true when it changed something.
        /// </summary>
        private static bool HandleEvents()
        {
            Event e = Event.current;
            if (_capturingKeyCode != null)
            {
                // Any single key, modifiers included: Shift and Ctrl make good fly keys.
                KeyCode key = KeyCode.None;
                if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
                {
                    key = e.keyCode;
                }
                else if (e.type == EventType.MouseDown && e.button >= 2 && e.button <= 6)
                {
                    key = KeyCode.Mouse0 + e.button;
                }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.None)
                {
                    // IMGUI can report Shift, Ctrl and Alt on their own as a KeyDown with no keyCode.
                    key = e.shift ? KeyCode.LeftShift : e.control ? KeyCode.LeftControl : e.alt ? KeyCode.LeftAlt : KeyCode.None;
                }
                if (key == KeyCode.None)
                {
                    return false;
                }
                e.Use();
                if (key != KeyCode.Escape)
                {
                    _capturingKeyCode.Value = key;
                }
                _capturingKeyCode = null;
                return true;
            }
            if (_capturing != null)
            {
                KeyCode key = KeyCode.None;
                if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
                {
                    key = e.keyCode;
                }
                else if (e.type == EventType.MouseDown && e.button >= 2 && e.button <= 6)
                {
                    key = KeyCode.Mouse0 + e.button;
                }
                if (key == KeyCode.None)
                {
                    return false;
                }
                e.Use();
                if (key == KeyCode.Escape)
                {
                    _capturing = null;
                    return true;
                }
                if (IsModifier(key))
                {
                    // Wait for the main key; the modifier is read from the event with it.
                    return false;
                }
                var modifiers = new List<KeyCode>();
                if (e.control)
                {
                    modifiers.Add(KeyCode.LeftControl);
                }
                if (e.shift)
                {
                    modifiers.Add(KeyCode.LeftShift);
                }
                if (e.alt)
                {
                    modifiers.Add(KeyCode.LeftAlt);
                }
                _capturing.Value = new KeyboardShortcut(key, modifiers.ToArray());
                _capturing = null;
                return true;
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                e.Use();
                Close();
                return true;
            }
            return false;
        }

        private static string Plain(int value) => value.ToString();

        private static string Degrees(int value) => value + "°";

        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

        private static string OffAtZero(int value) => value == 0 ? "Off" : value.ToString();

        private static string DefaultText(ConfigEntryBase entry)
        {
            object value = entry.DefaultValue;
            switch (value)
            {
                case bool on:
                    return on ? "on" : "off";
                case KeyboardShortcut shortcut:
                    return KeyText(shortcut);
                case KeyCode key:
                    return KeyName(key);
                case Letterbox letterbox:
                    return Overlay.Label(letterbox);
                case string text:
                    return text.Length == 0 ? "empty" : text;
                case float number:
                    return number.ToString("0.0");
                default:
                    return value?.ToString() ?? "none";
            }
        }

        private static bool IsModifier(KeyCode key)
        {
            return key == KeyCode.LeftControl || key == KeyCode.RightControl
                   || key == KeyCode.LeftShift || key == KeyCode.RightShift
                   || key == KeyCode.LeftAlt || key == KeyCode.RightAlt
                   || key == KeyCode.AltGr || key == KeyCode.LeftCommand || key == KeyCode.RightCommand;
        }

        private static string KeyText(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
            {
                return "Not set";
            }
            IEnumerable<string> parts = shortcut.Modifiers.Select(ModifierName).Concat(new[] { KeyName(shortcut.MainKey) });
            return string.Join(" + ", parts);
        }

        private static string ModifierName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return "Ctrl";
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return "Shift";
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return "Alt";
                default:
                    return KeyName(key);
            }
        }

        private static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None:
                    return "Not set";
                case KeyCode.Mouse2:
                    return "Middle mouse";
                case KeyCode.Mouse3:
                    return "Mouse 4";
                case KeyCode.Mouse4:
                    return "Mouse 5";
                case KeyCode.Mouse5:
                    return "Mouse 6";
                case KeyCode.Mouse6:
                    return "Mouse 7";
                case KeyCode.LeftControl:
                    return "Left Ctrl";
                case KeyCode.RightControl:
                    return "Right Ctrl";
                case KeyCode.LeftShift:
                    return "Left Shift";
                case KeyCode.RightShift:
                    return "Right Shift";
                case KeyCode.LeftAlt:
                    return "Left Alt";
                case KeyCode.RightAlt:
                    return "Right Alt";
                case KeyCode.PageDown:
                    return "Page Down";
                case KeyCode.PageUp:
                    return "Page Up";
                default:
                    return key.ToString();
            }
        }

        /// <summary>Defaults back for one section, or for every Camera Mode setting when null.</summary>
        private static void ResetSection(string section)
        {
            _capturing = null;
            _capturingKeyCode = null;
            foreach (ConfigDefinition definition in _config.Keys.ToList())
            {
                if (section != null && definition.Section != section)
                {
                    continue;
                }
                ConfigEntryBase entry = _config[definition];
                entry.BoxedValue = entry.DefaultValue;
            }
            _extraObjectsDraft = Settings.ExtraObjects.Value;
        }
    }
}
