using System.Collections.Generic;
using EFT.CameraControl;
using HarmonyLib;
using UnityEngine;

namespace CameraMode
{
    /// <summary>
    /// Switches off the camera image effects that sit on top of the view.
    ///
    /// From the game's own code:
    /// - Visor and glasses: CameraManager.VisorEffect. The game decides whether it is on in
    ///   VisorEffect.SetIntensity (enabled = visible and intensity above 0), so it is switched off
    ///   every frame while hidden and handed back by calling SetIntensity(Intensity), which works
    ///   the right answer out again.
    /// - Blood: EffectsController._bloodOnScreen (private). Rain: EffectsController.RainScreenDrops
    ///   and ScreenWater. The game leaves these enabled for the raid and only feeds them hits and
    ///   drops (ScreenWater disables itself once in Start on hardware that lacks support), so the
    ///   enabled state seen when hiding is saved and put back.
    ///
    /// Re-asserted from LateUpdate, after the game's own Update has run.
    /// </summary>
    internal static class ScreenEffects
    {
        private static readonly AccessTools.FieldRef<EffectsController, BloodOnScreen> BloodRef =
            AccessTools.FieldRefAccess<EffectsController, BloodOnScreen>("_bloodOnScreen");

        /// <summary>Effects this mod switched off, with whether each was on before.</summary>
        private static readonly Dictionary<Behaviour, bool> Hidden = new Dictionary<Behaviour, bool>();

        private static VisorEffect _visor;
        private static bool _entered;

        internal static void Enter()
        {
            Hidden.Clear();
            _visor = null;
            _entered = true;
            LateTick();
            CameraModePlugin.Log.LogInfo($"Screen: {Hidden.Count} effects hidden{(_visor != null ? ", visor hidden" : "")}");
        }

        internal static void LateTick()
        {
            if (!_entered)
            {
                return;
            }
            CameraManager camera = CameraManager.Instance;

            if (Settings.HideVisor.Value)
            {
                VisorEffect visor = camera.VisorEffect;
                if (visor != null)
                {
                    _visor = visor;
                    if (visor.enabled)
                    {
                        visor.enabled = false;
                    }
                }
            }

            EffectsController effects = camera.EffectsController;
            if (effects == null)
            {
                return;
            }
            if (Settings.HideBlood.Value)
            {
                Hide(BloodRef(effects));
            }
            if (Settings.HideRain.Value)
            {
                Hide(effects.RainScreenDrops);
                Hide(effects.ScreenWater);
            }
        }

        internal static void Exit()
        {
            if (!_entered)
            {
                return;
            }
            _entered = false;

            if (_visor != null)
            {
                _visor.SetIntensity(_visor.Intensity);
            }
            _visor = null;

            foreach (KeyValuePair<Behaviour, bool> entry in Hidden)
            {
                if (entry.Key != null)
                {
                    entry.Key.enabled = entry.Value;
                }
            }
            Hidden.Clear();
        }

        private static void Hide(Behaviour effect)
        {
            if (effect == null)
            {
                return;
            }
            if (!Hidden.ContainsKey(effect))
            {
                Hidden[effect] = effect.enabled;
            }
            if (effect.enabled)
            {
                effect.enabled = false;
            }
        }
    }
}
