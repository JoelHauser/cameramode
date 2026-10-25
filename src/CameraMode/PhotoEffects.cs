using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.CameraControl;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Object = UnityEngine.Object;
using PPBloom = UnityEngine.Rendering.PostProcessing.Bloom;
using PPChromaticAberration = UnityEngine.Rendering.PostProcessing.ChromaticAberration;
using PPDepthOfField = UnityEngine.Rendering.PostProcessing.DepthOfField;
using PPGrain = UnityEngine.Rendering.PostProcessing.Grain;
using PPLensDistortion = UnityEngine.Rendering.PostProcessing.LensDistortion;
using PPVignette = UnityEngine.Rendering.PostProcessing.Vignette;

namespace CameraMode
{
    /// <summary>
    /// The Photo effects page and depth of field, through the game's own post-processing.
    ///
    /// EFT's main camera ("FPS Camera") carries Unity's Post Processing Stack v2: a
    /// PostProcessLayer whose bundles hold the settings each effect renders with, and
    /// PostProcessVolumes that are blended into those bundles. This adds one more global volume
    /// (PostProcessManager.QuickVolume) on a layer the camera listens to, at a priority above the
    /// game's.
    ///
    /// Why effects used to stick (0.3.0, 0.4.0): stock PPv2 resets every bundle to its defaults
    /// each frame before blending the volumes in (PostProcessManager.ReplaceData). EFT's copy only
    /// does that when the layer has a volumeTrigger, and the FPS camera has none. So whatever a
    /// volume once wrote into a bundle stays there after the volume goes away; it is also why
    /// AmandsGraphics can write bundle values directly. Disabling our volume therefore changed
    /// nothing on screen.
    ///
    /// So each effect is handled on its own (a <see cref="Slot"/>): the moment it is turned on, the
    /// layer's bundle for that effect is snapshotted; while on, our volume overrides it; the moment
    /// it is turned off (slider back to 0, Default preset, Camera Mode off), our overrides for it
    /// are cleared and the snapshot is written back. An effect that is off overrides nothing, so
    /// the game's own use of that effect is never touched.
    ///
    /// The shaders these effects use are in the game build (searched for by name in its data
    /// files): Uber (vignette, grain, chromatic aberration, lens distortion, colour), GrainBaker,
    /// Bloom, DepthOfField and Lut2DBaker. Colour grading is forced to LowDefinitionRange, the mode
    /// that bakes with Lut2DBaker, so it never needs the HDR compute path.
    /// </summary>
    internal static class PhotoEffects
    {
        private const float VolumePriority = 1000f;

        /// <summary>How far autofocus looks, in metres.</summary>
        private const float AutoFocusRange = 500f;

        /// <summary>
        /// One effect: our settings in the volume, and the layer bundle's values from before we
        /// touched it. Parameters are found the way PPv2 itself finds them (public instance fields
        /// deriving from ParameterOverride); its own copy method is internal, so the public
        /// <c>value</c> field of each ParameterOverride&lt;T&gt; is copied by reflection.
        /// </summary>
        private sealed class Slot
        {
            internal readonly PostProcessEffectSettings Mine;
            internal readonly Func<bool> Wanted;
            internal readonly Action Apply;
            internal readonly FieldInfo[] Parameters;

            internal bool Active;
            internal PostProcessLayer SavedFrom;
            internal object[] SavedValues;
            internal bool[] SavedStates;

            internal Slot(PostProcessEffectSettings mine, Func<bool> wanted, Action apply)
            {
                Mine = mine;
                Wanted = wanted;
                Apply = apply;
                Parameters = mine.GetType()
                    .GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Where(f => typeof(ParameterOverride).IsAssignableFrom(f.FieldType))
                    .ToArray();
            }

            internal Type Type => Mine.GetType();
        }

        private static readonly Dictionary<Type, FieldInfo> ValueFields = new Dictionary<Type, FieldInfo>();

        private static PostProcessVolume _volume;
        private static ColorGrading _grading;
        private static PPVignette _vignette;
        private static PPGrain _grain;
        private static PPChromaticAberration _aberration;
        private static PPLensDistortion _lens;
        private static PPBloom _bloom;
        private static PPDepthOfField _dof;
        private static Slot[] _slots;

        private static Camera _camera;
        private static PostProcessLayer _layer;
        private static float _focus = 10f;
        private static bool _warned;

        /// <summary>Every frame from LateUpdate, whether Camera Mode is on or not.</summary>
        internal static void LateTick()
        {
            if (_slots == null)
            {
                CreateSettings();
            }

            PostProcessLayer layer = CurrentLayer();
            if (layer == null)
            {
                return;
            }

            bool cameraMode = CameraModeState.Active;
            bool any = false;
            foreach (Slot slot in _slots)
            {
                bool want = cameraMode && slot.Wanted();
                if (want)
                {
                    if (!EnsureVolume(layer))
                    {
                        return;
                    }
                    if (!slot.Active)
                    {
                        Save(slot, layer);
                        slot.Active = true;
                    }
                    slot.Apply();
                    any = true;
                }
                else if (slot.Active)
                {
                    slot.Mine.SetAllOverridesTo(false, excludeEnabled: false);
                    Restore(slot, layer);
                    slot.Active = false;
                }
            }

            if (_volume != null)
            {
                _volume.weight = 1f;
                if (_volume.enabled != any)
                {
                    _volume.enabled = any;
                }
            }
        }

        private static void CreateSettings()
        {
            _grading = Create<ColorGrading>();
            _vignette = Create<PPVignette>();
            _grain = Create<PPGrain>();
            _aberration = Create<PPChromaticAberration>();
            _lens = Create<PPLensDistortion>();
            _bloom = Create<PPBloom>();
            _dof = Create<PPDepthOfField>();

            _slots = new[]
            {
                new Slot(_grading, () => Settings.Brightness.Value != 0 || Settings.Contrast.Value != 0 || Settings.Saturation.Value != 0
                                         || Settings.Temperature.Value != 0 || Settings.Tint.Value != 0 || Settings.HueShift.Value != 0,
                    ApplyGrading),
                new Slot(_vignette, () => Settings.Vignette.Value > 0, ApplyVignette),
                new Slot(_grain, () => Settings.Grain.Value > 0, ApplyGrain),
                new Slot(_aberration, () => Settings.ChromaticAberration.Value > 0, ApplyAberration),
                new Slot(_lens, () => Settings.LensDistortion.Value != 0, ApplyLens),
                new Slot(_bloom, () => Settings.Bloom.Value > 0, ApplyBloom),
                new Slot(_dof, () => Settings.DepthOfField.Value, ApplyDepthOfField),
            };
        }

        private static PostProcessLayer CurrentLayer()
        {
            Camera camera = CameraManager.Instance.Camera;
            if (camera == null)
            {
                return null;
            }
            if (camera != _camera || _layer == null)
            {
                _camera = camera;
                _layer = camera.GetComponent<PostProcessLayer>();
                // A new camera (a new raid) starts with its own bundles: old snapshots don't apply.
                foreach (Slot slot in _slots)
                {
                    if (slot.Active)
                    {
                        slot.Mine.SetAllOverridesTo(false, excludeEnabled: false);
                        slot.Active = false;
                    }
                }
            }
            if (_layer == null && CameraModeState.Active)
            {
                WarnOnce("the main camera has no PostProcessLayer; photo effects are off");
            }
            return _layer;
        }

        private static bool EnsureVolume(PostProcessLayer layer)
        {
            int layerIndex = LowestLayer(layer.volumeLayer.value);
            if (layerIndex < 0)
            {
                WarnOnce("the camera's PostProcessLayer listens to no layer; photo effects are off");
                return false;
            }
            if (_volume == null || _volume.gameObject.layer != layerIndex)
            {
                if (_volume != null)
                {
                    Object.Destroy(_volume.gameObject);
                }
                _volume = PostProcessManager.instance.QuickVolume(layerIndex, VolumePriority,
                    _grading, _vignette, _grain, _aberration, _lens, _bloom, _dof);
                Object.DontDestroyOnLoad(_volume.gameObject);
                CameraModePlugin.Log.LogInfo($"Photo effects: volume on layer {layerIndex} ({LayerMask.LayerToName(layerIndex)})");
            }
            return true;
        }

        // ---- snapshot and restore ---------------------------------------------------------------

        private static void Save(Slot slot, PostProcessLayer layer)
        {
            PostProcessEffectSettings bundle = BundleSettings(layer, slot.Type);
            slot.SavedFrom = layer;
            slot.SavedValues = new object[slot.Parameters.Length];
            slot.SavedStates = new bool[slot.Parameters.Length];
            if (bundle == null)
            {
                return;
            }
            for (int i = 0; i < slot.Parameters.Length; i++)
            {
                if (slot.Parameters[i].GetValue(bundle) is ParameterOverride parameter)
                {
                    slot.SavedValues[i] = ValueField(parameter)?.GetValue(parameter);
                    slot.SavedStates[i] = parameter.overrideState;
                }
            }
        }

        private static void Restore(Slot slot, PostProcessLayer layer)
        {
            if (slot.SavedFrom != layer || slot.SavedValues == null)
            {
                return;
            }
            PostProcessEffectSettings bundle = BundleSettings(layer, slot.Type);
            if (bundle == null)
            {
                return;
            }
            for (int i = 0; i < slot.Parameters.Length; i++)
            {
                if (slot.Parameters[i].GetValue(bundle) is ParameterOverride parameter)
                {
                    ValueField(parameter)?.SetValue(parameter, slot.SavedValues[i]);
                    parameter.overrideState = slot.SavedStates[i];
                }
            }
            slot.SavedValues = null;
            slot.SavedStates = null;
            slot.SavedFrom = null;
        }

        private static PostProcessEffectSettings BundleSettings(PostProcessLayer layer, Type type)
        {
            try
            {
                return layer.GetBundle(type)?.settings;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        private static FieldInfo ValueField(ParameterOverride parameter)
        {
            Type type = parameter.GetType();
            if (!ValueFields.TryGetValue(type, out FieldInfo field))
            {
                field = type.GetField("value", BindingFlags.Instance | BindingFlags.Public);
                ValueFields[type] = field;
            }
            return field;
        }

        // ---- each effect ------------------------------------------------------------------------

        private static void ApplyGrading()
        {
            _grading.enabled.Override(true);
            _grading.gradingMode.Override(GradingMode.LowDefinitionRange);
            _grading.brightness.Override(Settings.Brightness.Value);
            _grading.contrast.Override(Settings.Contrast.Value);
            _grading.saturation.Override(Settings.Saturation.Value);
            _grading.temperature.Override(Settings.Temperature.Value);
            _grading.tint.Override(Settings.Tint.Value);
            _grading.hueShift.Override(Settings.HueShift.Value);
        }

        private static void ApplyVignette()
        {
            _vignette.enabled.Override(true);
            _vignette.mode.Override(VignetteMode.Classic);
            _vignette.color.Override(Color.black);
            _vignette.intensity.Override(Settings.Vignette.Value / 100f * 0.6f);
            _vignette.smoothness.Override(Mathf.Max(0.01f, Settings.VignetteSoftness.Value / 100f));
            _vignette.roundness.Override(1f);
            _vignette.rounded.Override(false);
        }

        private static void ApplyGrain()
        {
            _grain.enabled.Override(true);
            _grain.intensity.Override(Settings.Grain.Value / 100f);
            _grain.size.Override(Mathf.Lerp(0.3f, 3f, Settings.GrainSize.Value / 100f));
            _grain.colored.Override(false);
        }

        private static void ApplyAberration()
        {
            _aberration.enabled.Override(true);
            _aberration.intensity.Override(Settings.ChromaticAberration.Value / 100f);
        }

        private static void ApplyLens()
        {
            _lens.enabled.Override(true);
            _lens.intensity.Override(Settings.LensDistortion.Value * 0.6f);
            // Barrel distortion pulls black in at the corners; scale in just enough to cover them.
            _lens.scale.Override(1f - Mathf.Max(0, Settings.LensDistortion.Value) / 100f * 0.25f);
        }

        private static void ApplyBloom()
        {
            _bloom.enabled.Override(true);
            _bloom.intensity.Override(Settings.Bloom.Value / 100f * 10f);
            _bloom.threshold.Override(1f);
        }

        private static void ApplyDepthOfField()
        {
            float target = Settings.AutoFocus.Value ? AutoFocusDistance() : Settings.FocusDistance.Value;
            float step = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            _focus = Mathf.Lerp(_focus, target, step);
            _dof.enabled.Override(true);
            _dof.focusDistance.Override(Mathf.Max(0.1f, _focus));
            _dof.aperture.Override(Settings.Aperture.Value);
            _dof.focalLength.Override(Settings.FocalLength.Value);
            _dof.kernelSize.Override(KernelSize.Medium);
        }

        /// <summary>
        /// What the centre of the screen points at, with the game's own sight-line mask:
        /// a -1 mask would stop on triggers and loot colliders.
        /// </summary>
        private static float AutoFocusDistance()
        {
            Transform eye = _camera.transform;
            if (Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, AutoFocusRange, LayersMaskController.HighPolyWithTerrainMask))
            {
                return hit.distance;
            }
            return AutoFocusRange;
        }

        private static T Create<T>() where T : PostProcessEffectSettings
        {
            T settings = ScriptableObject.CreateInstance<T>();
            settings.SetAllOverridesTo(false, excludeEnabled: false);
            settings.hideFlags = HideFlags.HideAndDontSave;
            return settings;
        }

        private static int LowestLayer(int mask)
        {
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void WarnOnce(string message)
        {
            if (!_warned)
            {
                _warned = true;
                CameraModePlugin.Log.LogWarning("Photo effects: " + message);
            }
        }
    }
}
