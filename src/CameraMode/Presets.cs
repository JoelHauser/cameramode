using BepInEx.Configuration;

namespace CameraMode
{
    /// <summary>
    /// One-click looks for the Photo effects tab. Each preset is a whole look: every photo effect
    /// goes back to its default first (vignette softness, grain size, letterbox included), then the
    /// preset's own values are set, so nothing from an earlier preset sticks. "Default (EFT)" is
    /// the game's own picture. The composition grid is a guide, not a look, and is left alone.
    /// </summary>
    internal static class Presets
    {
        internal sealed class Look
        {
            internal string Name;
            internal int Brightness, Contrast, Saturation, Temperature, Tint, HueShift;
            internal int Vignette, Grain, ChromaticAberration, LensDistortion, Bloom;
            internal Letterbox Letterbox = Letterbox.Off;
        }

        internal static readonly Look Default = new Look { Name = "Default (EFT)" };

        internal static readonly Look[] All =
        {
            Default,
            new Look { Name = "Noir", Brightness = -5, Contrast = 35, Saturation = -100, Vignette = 45, Grain = 35 },
            new Look { Name = "Vintage", Contrast = -10, Saturation = -35, Temperature = 25, Tint = 5, Vignette = 40, Grain = 45, ChromaticAberration = 15, LensDistortion = 12 },
            new Look { Name = "Cinematic", Contrast = 20, Saturation = 10, Temperature = -8, Vignette = 30, Letterbox = Letterbox.Scope_2_39 },
            new Look { Name = "Cold", Contrast = 10, Saturation = -15, Temperature = -40, Tint = -5, Vignette = 15 },
            new Look { Name = "Warm", Contrast = 5, Saturation = 10, Temperature = 35, Vignette = 15 },
            new Look { Name = "Dream", Brightness = 10, Contrast = -20, Saturation = 15, Vignette = 20, Bloom = 30 },
            new Look { Name = "Neon", Contrast = 15, Saturation = 30, Tint = 25, HueShift = -10, ChromaticAberration = 35, Vignette = 25, Bloom = 15 },
        };

        internal static void Apply(Look look, ConfigFile config)
        {
            foreach (ConfigDefinition definition in config.Keys)
            {
                ConfigEntryBase entry = config[definition];
                if (definition.Section == Settings.Effects && entry != Settings.CompositionGrid)
                {
                    entry.BoxedValue = entry.DefaultValue;
                }
            }
            Settings.Brightness.Value = look.Brightness;
            Settings.Contrast.Value = look.Contrast;
            Settings.Saturation.Value = look.Saturation;
            Settings.Temperature.Value = look.Temperature;
            Settings.Tint.Value = look.Tint;
            Settings.HueShift.Value = look.HueShift;
            Settings.Vignette.Value = look.Vignette;
            Settings.Grain.Value = look.Grain;
            Settings.ChromaticAberration.Value = look.ChromaticAberration;
            Settings.LensDistortion.Value = look.LensDistortion;
            Settings.Bloom.Value = look.Bloom;
            Settings.LetterboxBars.Value = look.Letterbox;
        }

        /// <summary>True when the effect settings are exactly this look, so the menu can show it as picked.</summary>
        internal static bool IsCurrent(Look look)
        {
            return Settings.Brightness.Value == look.Brightness
                   && Settings.Contrast.Value == look.Contrast
                   && Settings.Saturation.Value == look.Saturation
                   && Settings.Temperature.Value == look.Temperature
                   && Settings.Tint.Value == look.Tint
                   && Settings.HueShift.Value == look.HueShift
                   && Settings.Vignette.Value == look.Vignette
                   && Settings.Grain.Value == look.Grain
                   && Settings.ChromaticAberration.Value == look.ChromaticAberration
                   && Settings.LensDistortion.Value == look.LensDistortion
                   && Settings.Bloom.Value == look.Bloom
                   && Settings.LetterboxBars.Value == look.Letterbox
                   && Equals(Settings.VignetteSoftness.Value, Settings.VignetteSoftness.DefaultValue)
                   && Equals(Settings.GrainSize.Value, Settings.GrainSize.DefaultValue);
        }
    }
}
