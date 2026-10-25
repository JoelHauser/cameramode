using Comfort.Common;
using EFT;
using EFT.CameraControl;
using EFT.EnvironmentEffect;
using UnityEngine;

namespace CameraMode
{
    /// <summary>
    /// UFO mode: fly anywhere, through walls, and stay where you are when it ends.
    ///
    /// From the game's own code: every movement state hands its motion for the frame (walking,
    /// gravity, sliding) to MovementContext.DirectApplyMotion, which runs it through
    /// CharacterController.Move (walls, floors) and EmitStepNoise (footsteps). While flying, the
    /// <see cref="Patches"/> replace that call for your player with <see cref="Fly"/>, which
    /// writes MovementContext.TransformPosition directly, the way Player.Teleport moves a player.
    /// No collision, no gravity, no footsteps. Mouse look is untouched.
    ///
    /// Falling: CheckFlying deals fall damage from the height a fall started at
    /// (_startFallingHeight, raised while airborne). ResetFlying sets that to the current height, so
    /// it is called every flying frame and the "fall" on landing is never more than one frame.
    ///
    /// Leaving: optionally drop onto the surface below (a raycast with
    /// LayersMaskController.PlayerStaticCollisionsMask, the mask the player's own character
    /// controller and the game's spawn grounding use), refuse to land inside a wall
    /// (Physics.CheckCapsule with the player's controller radius and height), then Player.Teleport
    /// to the spot, which also resets height smoothing and the indoor/outdoor environment.
    ///
    /// Invisible to AI and no damage while flying come from <see cref="AiStealth"/> and
    /// <see cref="GodMode"/>, shared with Camera Mode through CameraModeState.SyncShared.
    /// </summary>
    internal static class UfoMode
    {
        /// <summary>How far below you "Land on the surface below" looks, in metres.</summary>
        private const float LandingRange = 300f;

        /// <summary>Gap left between your feet and the surface you land on.</summary>
        private const float LandingClearance = 0.05f;

        /// <summary>How often the indoor/outdoor sound environment is updated while flying.</summary>
        private const float EnvironmentInterval = 0.5f;

        private static Vector3 _velocity;
        private static int _flownFrame = -1;
        private static float _nextEnvironment;

        internal static bool Active { get; private set; }

        internal static Player Player { get; private set; }

        internal static void Toggle()
        {
            if (Active)
            {
                TryLand();
            }
            else
            {
                TryTakeOff();
            }
        }

        /// <summary>From the DirectApplyMotion patch: true when this context is the flying player's.</summary>
        internal static bool IsFlying(MovementContext context)
        {
            return Active && Player != null && context == Player.MovementContext;
        }

        /// <summary>Every frame from the plugin's Update.</summary>
        internal static void Tick()
        {
            if (!Active)
            {
                return;
            }
            Player player = Player;
            if (player == null || MainPlayer() != player)
            {
                Stop("raid ended", raidEnding: true);
                return;
            }
            if (player.HealthController == null || !player.HealthController.IsAlive)
            {
                Stop("player died", raidEnding: false);
            }
        }

        /// <summary>
        /// Every frame from LateUpdate: flies for any frame the game scheduled no motion in (some
        /// movement states skip ApplyMotion), so flight never stalls.
        /// </summary>
        internal static void LateTick()
        {
            if (Active && Player != null && _flownFrame != Time.frameCount)
            {
                Fly(Player.MovementContext, Time.deltaTime);
            }
        }

        /// <summary>One frame of flight, at most once a frame.</summary>
        internal static void Fly(MovementContext context, float deltaTime)
        {
            if (_flownFrame == Time.frameCount || context == null)
            {
                return;
            }
            _flownFrame = Time.frameCount;

            Player player = Player;
            Vector3 direction = Direction(player);
            float speed = Settings.UfoSpeed.Value * (Input.GetKey(Settings.UfoFastKey.Value) ? Settings.UfoFastMultiplier.Value : 1f);
            float step = 1f - Mathf.Exp(-10f * deltaTime);
            _velocity = Vector3.Lerp(_velocity, direction * speed, step);

            context.TransformPosition += _velocity * deltaTime;
            context.FreefallTime = 0f;
            context.ResetFlying();
            player.ResetHeightInterpolation();

            if (Time.unscaledTime >= _nextEnvironment)
            {
                _nextEnvironment = Time.unscaledTime + EnvironmentInterval;
                EnvironmentManager environment = EnvironmentManager.Instance;
                if (environment != null)
                {
                    environment.UpdateEnvironmentForPlayer(player);
                }
            }
        }

        /// <summary>Called on raid teardown, while the player still exists.</summary>
        internal static void OnRaidEnding()
        {
            Stop("raid ended", raidEnding: true);
        }

        private static Vector3 Direction(Player player)
        {
            if (MenuWindow.IsOpen)
            {
                return Vector3.zero;
            }

            Camera camera = CameraManager.Instance.Camera;
            Transform view = camera != null ? camera.transform : player.Transform.Original;
            Vector3 right = Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;
            Vector3 forward = Settings.UfoFlyWhereYouLook.Value
                ? view.forward
                : Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;

            // The game's own movement input, so it follows the player's key bindings.
            Vector2 input = Vector2.ClampMagnitude(player.InputDirection, 1f);
            float vertical = (Input.GetKey(Settings.UfoUpKey.Value) ? 1f : 0f) - (Input.GetKey(Settings.UfoDownKey.Value) ? 1f : 0f);

            Vector3 direction = forward * input.y + right * input.x + Vector3.up * vertical;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private static Player MainPlayer()
        {
            return Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance.MainPlayer : null;
        }

        private static void TryTakeOff()
        {
            Player player = MainPlayer();
            if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
            {
                Toast.Show("UFO mode works only in raid", force: true);
                return;
            }

            Player = player;
            Active = true;
            _velocity = Vector3.zero;
            _flownFrame = -1;
            _nextEnvironment = 0f;
            CameraModeState.SyncShared(raidEnding: false);
            CameraModePlugin.Log.LogInfo("UFO mode on");
            Toast.Show("UFO mode on");
        }

        private static void TryLand()
        {
            Player player = Player;
            if (player == null)
            {
                Stop("no player", raidEnding: false);
                return;
            }

            Vector3 spot = player.Transform.position;
            if (Settings.UfoLandOnGround.Value
                && Physics.Raycast(spot + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, LandingRange,
                    LayersMaskController.PlayerStaticCollisionsMask, QueryTriggerInteraction.Ignore))
            {
                spot = hit.point + Vector3.up * LandingClearance;
            }

            if (Settings.UfoBlockInsideWalls.Value && InsideSomething(player, spot))
            {
                CameraModePlugin.Log.LogInfo($"UFO mode: refused to land inside something at {spot}");
                Toast.Show("Can't land here: you'd be stuck inside something", force: true);
                return;
            }

            Active = false;
            _velocity = Vector3.zero;
            player.Teleport(spot);
            Player = null;
            CameraModeState.SyncShared(raidEnding: false);
            CameraModePlugin.Log.LogInfo($"UFO mode off, landed at {spot}");
            Toast.Show("UFO mode off");
        }

        private static void Stop(string reason, bool raidEnding)
        {
            if (!Active)
            {
                return;
            }
            Active = false;
            Player = null;
            _velocity = Vector3.zero;
            CameraModeState.SyncShared(raidEnding);
            CameraModePlugin.Log.LogInfo($"UFO mode off ({reason})");
        }

        /// <summary>Whether the player's capsule, stood at <paramref name="feet"/>, would overlap a wall or floor.</summary>
        private static bool InsideSomething(Player player, Vector3 feet)
        {
            ICharacterController controller = player.CharacterController;
            float radius = controller != null ? controller.radius : 0.35f;
            float height = controller != null ? controller.height : 1.8f;
            // Slightly thinner than the real capsule: touching the floor or a wall is fine, inside is not.
            float r = Mathf.Max(0.05f, radius * 0.8f);
            Vector3 bottom = feet + Vector3.up * (radius + 0.05f);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius + 0.06f, height - radius);
            return Physics.CheckCapsule(bottom, top, r, LayersMaskController.PlayerStaticCollisionsMask, QueryTriggerInteraction.Ignore);
        }
    }
}
