using System.Collections.Generic;
using Diz.Skinning;
using EFT;
using EFT.Visual;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace CameraMode
{
    /// <summary>
    /// Hides the parts of your own model that are drawn in first person, each with its own setting.
    ///
    /// What is drawn in first person, from the game's own code:
    /// - Arms: PlayerBody.UpdatePlayerRenders turns on only BodySkins[EBodyModelPart.Hands] in
    ///   first person; the other body skins are drawn as shadow only, except the feet.
    /// - Watch: PlayerBody._watches, shown in first person by the same method.
    /// - Held item: HandsController.ControllerGameObject. It is not a child of the player
    ///   (the cutscene code has to reparent it onto the player), so it is collected on its own.
    /// - Body and legs: every BodySkin, plus PlayerBody.GetRenderersNonAlloc (equipment and dress).
    ///
    /// Two ways to hide, chosen when Camera Mode turns on:
    /// - Default: Renderer.forceRenderingOff. The game switches the arms with .enabled
    ///   (LoddedSkin.EnableRenderers), so the two do not fight. No shadow.
    /// - Keep shadows: shadowCastingMode = ShadowsOnly, the way the game itself hides the body in
    ///   first person. The original mode is saved per renderer and put back.
    ///
    /// Every refresh collects what belongs to the player right now and gives back anything hidden
    /// earlier that no longer does (a dropped weapon, a model returned to the asset pool and
    /// reused by a bot).
    /// </summary>
    internal static class ModelHider
    {
        private static readonly AccessTools.FieldRef<PlayerBody, PluggableBone> WatchesRef =
            AccessTools.FieldRefAccess<PlayerBody, PluggableBone>("_watches");

        /// <summary>Renderers this mod changed, with the shadow mode each had before.</summary>
        private static readonly Dictionary<Renderer, ShadowCastingMode> Hidden = new Dictionary<Renderer, ShadowCastingMode>();

        private static readonly HashSet<Renderer> Current = new HashSet<Renderer>();
        private static readonly List<Renderer> Scratch = new List<Renderer>();
        private static readonly List<Renderer> Released = new List<Renderer>();

        private static bool _keepShadows;

        internal static void Hide(Player player)
        {
            Hidden.Clear();
            _keepShadows = Settings.KeepShadows.Value;
            Refresh(player);
            CameraModePlugin.Log.LogInfo($"Model: {Hidden.Count} renderers hidden{(_keepShadows ? " (shadows kept)" : "")}");
        }

        /// <summary>Picks up a weapon swap, an attachment, a grenade or meds coming out.</summary>
        internal static void Refresh(Player player)
        {
            Collect(player);

            Released.Clear();
            foreach (KeyValuePair<Renderer, ShadowCastingMode> entry in Hidden)
            {
                if (entry.Key == null || !Current.Contains(entry.Key))
                {
                    Released.Add(entry.Key);
                }
            }
            foreach (Renderer renderer in Released)
            {
                if (renderer != null)
                {
                    Restore(renderer, Hidden[renderer]);
                }
                Hidden.Remove(renderer);
            }
            Released.Clear();

            foreach (Renderer renderer in Current)
            {
                Apply(renderer);
            }
            Current.Clear();
        }

        internal static void Show()
        {
            foreach (KeyValuePair<Renderer, ShadowCastingMode> entry in Hidden)
            {
                if (entry.Key != null)
                {
                    Restore(entry.Key, entry.Value);
                }
            }
            Hidden.Clear();
        }

        private static void Collect(Player player)
        {
            Current.Clear();
            PlayerBody body = player.PlayerBody;
            if (body != null)
            {
                if (Settings.HideArms.Value
                    && body.BodySkins.TryGetValue(EBodyModelPart.Hands, out LoddedSkin hands) && hands != null)
                {
                    foreach (Renderer renderer in hands.GetRenderers())
                    {
                        Add(renderer);
                    }
                }

                if (Settings.HideWatch.Value)
                {
                    PluggableBone watches = WatchesRef(body);
                    if (watches != null)
                    {
                        AddAllUnder(watches.gameObject);
                    }
                }

                if (Settings.HideBody.Value)
                {
                    foreach (KeyValuePair<EBodyModelPart, LoddedSkin> skin in body.BodySkins)
                    {
                        if (skin.Key == EBodyModelPart.Hands || skin.Value == null)
                        {
                            continue;
                        }
                        foreach (Renderer renderer in skin.Value.GetRenderers())
                        {
                            Add(renderer);
                        }
                    }
                    Scratch.Clear();
                    body.GetRenderersNonAlloc(Scratch);
                    foreach (Renderer renderer in Scratch)
                    {
                        Add(renderer);
                    }
                    Scratch.Clear();
                }
            }

            if (Settings.HideHeldItem.Value)
            {
                GameObject held = player.HandsController != null ? player.HandsController.ControllerGameObject : null;
                if (held != null)
                {
                    AddAllUnder(held);
                }
            }
        }

        private static void AddAllUnder(GameObject root)
        {
            Scratch.Clear();
            root.GetComponentsInChildren(true, Scratch);
            foreach (Renderer renderer in Scratch)
            {
                Add(renderer);
            }
            Scratch.Clear();
        }

        private static void Add(Renderer renderer)
        {
            if (renderer != null)
            {
                Current.Add(renderer);
            }
        }

        private static void Apply(Renderer renderer)
        {
            if (_keepShadows)
            {
                if (!Hidden.ContainsKey(renderer))
                {
                    Hidden[renderer] = renderer.shadowCastingMode;
                }
                if (renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
                return;
            }

            if (!Hidden.ContainsKey(renderer))
            {
                // Already off and not by this mod: leave it to whoever turned it off.
                if (renderer.forceRenderingOff)
                {
                    return;
                }
                Hidden[renderer] = renderer.shadowCastingMode;
            }
            renderer.forceRenderingOff = true;
        }

        private static void Restore(Renderer renderer, ShadowCastingMode savedShadowMode)
        {
            if (_keepShadows)
            {
                renderer.shadowCastingMode = savedShadowMode;
            }
            else
            {
                renderer.forceRenderingOff = false;
            }
        }
    }
}
