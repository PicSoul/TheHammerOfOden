using BepInEx.Configuration;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Detaches the camera from your character so you can fly it around what you are building.
    /// </summary>
    /// <remarks>
    /// Building from inside your own body is the awkward part of building. A wall you are
    /// standing against cannot be seen, a roof cannot be reached without a scaffold you then
    /// have to take down, and judging whether something lines up means walking away and back.
    ///
    /// Placement follows the camera rather than the character, done by lending the placement
    /// code the camera's eye for the duration of its update and giving it straight back. That
    /// is the whole trick: Valheim aims from Character.m_eye, so moving the eye moves where
    /// pieces go without touching the placement code at all. Borrowing it for one method and
    /// returning it immediately keeps every other system - attacks, interaction, hover text -
    /// looking out of your actual head.
    ///
    /// Nothing here shifts the world's reference point. Valheim creates and destroys objects
    /// around the player, and a build camera can be made to reach further by moving that
    /// reference to the camera, which is what MyBuildCamera does inside ZNetScene.Update.
    /// That is also why it costs what it costs: the game then keeps alive everything around
    /// the camera as well as everything around your body, and churns the difference every
    /// frame. The distance cap here exists so that the camera stays inside the world that is
    /// already loaded, and pays nothing.
    /// </remarks>
    internal static class BuildCamera
    {
        private static bool _active;
        private static Vector3 _position;
        private static float _yaw;
        private static float _pitch;

        private static GameObject _light;

        private static float _nextSweep;
        private static int _itemMask = -1;
        private static readonly Collider[] Nearby = new Collider[64];

        private static Vector3 _eyePosition;
        private static Quaternion _eyeRotation;
        private static bool _eyeBorrowed;

        internal static bool IsActive => _active;

        internal static Vector3 Position => _position;

        internal static Quaternion Rotation => Quaternion.Euler(_pitch, _yaw, 0f);

        /// <summary>Called once per placement update to service the toggle.</summary>
        internal static void HandleInput(Player player)
        {
            KeyboardShortcut shortcut = ModConfig.BuildCameraKey.Value;

            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, true))
            {
                return;
            }

            if (_active)
            {
                Deactivate(player);
                Notify.Show(player, "Build camera: off");
                return;
            }

            Activate(player);
            Notify.Show(player, "Build camera: on");
        }

        private static void Activate(Player player)
        {
            Camera camera = MainCamera.Get();
            if (camera == null || player == null)
            {
                return;
            }

            // Starts exactly where you were looking from, so switching in does not move the
            // view at all - the camera simply stops following you.
            _position = camera.transform.position;

            Vector3 angles = camera.transform.rotation.eulerAngles;
            _pitch = Normalise(angles.x);
            _yaw = angles.y;

            _active = true;
        }

        internal static void Deactivate(Player player)
        {
            _active = false;

            if (_light != null)
            {
                Object.Destroy(_light);
                _light = null;
            }

            Wisplight.Remove();

            // Whatever happened, the eye belongs back in the player's head.
            ReturnEye(player);
        }

        /// <summary>Flies the camera, and keeps it within reach of the player.</summary>
        internal static void Move(Player player, float dt)
        {
            if (!_active || dt <= 0f)
            {
                return;
            }

            float sensitivity = ModConfig.CameraSensitivity.Value;
            float invert = ModConfig.InvertCameraY.Value ? -1f : 1f;

            // ZInput rather than UnityEngine.Input, so the player's own bindings, gamepad and
            // sensitivity settings all still apply and nothing has to be configured twice.
            Vector2 look = ZInput.GetMouseDelta();

            _yaw += look.x * sensitivity;
            _pitch -= look.y * sensitivity * invert;

            // Short of straight up and down, where the yaw axis becomes meaningless and the
            // view rolls as you pass through it.
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);

            Quaternion rotation = Rotation;

            Vector3 move = rotation * new Vector3(Axis("Right", "Left"), 0f, Axis("Forward", "Backward"));

            // The gamepad stick, added rather than replacing, so either works without a mode.
            Vector2 stick = ZInput.GetJoyLeftStick();
            move += rotation * new Vector3(stick.x, 0f, -stick.y);

            if (Held(ModConfig.CameraUpKey.Value))
            {
                move += Vector3.up;
            }

            if (Held(ModConfig.CameraDownKey.Value))
            {
                move += Vector3.down;
            }

            if (move.sqrMagnitude > 1f)
            {
                move = move.normalized;
            }

            float speed = ModConfig.CameraSpeed.Value;
            if (Held(ModConfig.CameraBoostKey.Value))
            {
                speed *= ModConfig.CameraBoost.Value;
            }

            _position += move * (speed * dt);

            Tether(player);
            Sweep(player);
        }

        /// <summary>
        /// Picks up loose items the camera passes over.
        /// </summary>
        /// <remarks>
        /// Flying over a building site is exactly when you notice the wood and stone lying
        /// about it, and landing to collect them is the thing the camera was meant to save.
        ///
        /// A layer-masked OverlapSphere on a timer, not a scan of the scene. MyBuildCamera
        /// does this with Object.FindObjectsByType, which walks every object in the game,
        /// and with nothing to throttle it - which is half of why it costs what it costs.
        /// This looks only at the item layer, only inside the sweep radius, and only a few
        /// times a second; between sweeps it does nothing at all.
        ///
        /// Each item goes through Valheim's own Humanoid.Pickup, so stacking, the pickup
        /// message and the effect are the game's rather than an imitation of them. What is
        /// added is a weight check first, because vanilla pickup has none and a sweep you did
        /// not ask for should not leave you staggering.
        /// </remarks>
        private static void Sweep(Player player)
        {
            if (!ModConfig.CameraPickup.Value || player == null || Time.time < _nextSweep)
            {
                return;
            }

            _nextSweep = Time.time + Mathf.Max(0.05f, ModConfig.CameraPickupInterval.Value);

            if (_itemMask == -1)
            {
                _itemMask = LayerMask.GetMask("item");
            }

            int found = Physics.OverlapSphereNonAlloc(
                _position, ModConfig.CameraPickupRange.Value, Nearby, _itemMask);

            for (int i = 0; i < found; i++)
            {
                if (Nearby[i] == null)
                {
                    continue;
                }

                ItemDrop drop = Nearby[i].GetComponentInParent<ItemDrop>();

                if (drop == null || !drop.CanPickup(false) || drop.m_itemData == null)
                {
                    continue;
                }

                if (!Carrying.CanTake(player, drop.m_itemData, drop.m_itemData.m_stack))
                {
                    continue;
                }

                player.Pickup(drop.gameObject, false, false);
            }
        }

        /// <summary>
        /// Keeps the camera inside the world that is already loaded around the player.
        /// </summary>
        /// <remarks>
        /// Not an arbitrary limit. Valheim keeps objects alive around your character, and a
        /// camera beyond that sees a world that is still being built - or forces the game to
        /// build a second one around the camera, which is the expensive road this deliberately
        /// does not take.
        /// </remarks>
        private static void Tether(Player player)
        {
            if (player == null)
            {
                return;
            }

            Vector3 anchor = player.transform.position;
            Vector3 offset = _position - anchor;

            float limit = ModConfig.CameraRange.Value;
            if (offset.magnitude > limit)
            {
                _position = anchor + offset.normalized * limit;
            }
        }

        /// <summary>Drives the game camera, in place of vanilla following the player.</summary>
        internal static void ApplyTo(GameCamera gameCamera)
        {
            if (!_active || gameCamera == null)
            {
                return;
            }

            gameCamera.transform.SetPositionAndRotation(_position, Rotation);

            UpdateLight(gameCamera);
        }

        private static void UpdateLight(GameCamera gameCamera)
        {
            if (!ModConfig.CameraLight.Value)
            {
                if (_light != null)
                {
                    Object.Destroy(_light);
                    _light = null;
                }

                return;
            }

            if (_light == null)
            {
                _light = new GameObject("HoO_CameraLight");

                Light light = _light.AddComponent<Light>();
                light.type = LightType.Point;

                // A shadowed light this size is one of the most expensive things a scene can
                // hold, and it is here to let you see what you are building, not to be
                // realistic about it.
                light.shadows = LightShadows.None;
            }

            _light.transform.SetPositionAndRotation(_position, Rotation);

            Light configured = _light.GetComponent<Light>();
            configured.intensity = ModConfig.CameraLightIntensity.Value;
            configured.range = ModConfig.CameraLightRange.Value;
        }

        /// <summary>
        /// Lends the placement code the camera's eye, for the length of one update.
        /// </summary>
        /// <remarks>
        /// Valheim aims placement from Character.m_eye, so this is all that is needed to make
        /// pieces go where the camera is looking. It is borrowed and returned rather than
        /// moved outright because the eye is also where attacks, interaction and hover text
        /// come from, and those should keep working from your actual head.
        /// </remarks>
        internal static void BorrowEye(Transform eye)
        {
            if (!_active || eye == null || _eyeBorrowed)
            {
                return;
            }

            _eyePosition = eye.position;
            _eyeRotation = eye.rotation;
            _eyeBorrowed = true;

            eye.SetPositionAndRotation(_position, Rotation);
        }

        internal static void ReturnEye(Transform eye)
        {
            if (!_eyeBorrowed || eye == null)
            {
                _eyeBorrowed = false;
                return;
            }

            eye.SetPositionAndRotation(_eyePosition, _eyeRotation);
            _eyeBorrowed = false;
        }

        private static void ReturnEye(Player player)
        {
            if (player != null)
            {
                ReturnEye(player.m_eye);
            }
            else
            {
                _eyeBorrowed = false;
            }
        }

        /// <summary>
        /// Movement read through Valheim's own named buttons, not from fixed keys.
        /// </summary>
        /// <remarks>
        /// Someone who has rebound their movement keys has done so once, in the game's own
        /// settings, and should not have to do it again here.
        /// </remarks>
        private static float Axis(string positive, string negative)
        {
            float value = 0f;

            if (ZInput.GetButton(positive))
            {
                value += 1f;
            }

            if (ZInput.GetButton(negative))
            {
                value -= 1f;
            }

            return value;
        }

        private static bool Held(KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && ZInput.GetKey(shortcut.MainKey, true);
        }

        /// <summary>Euler angles come back as 0..360; pitch is easier to clamp as -180..180.</summary>
        private static float Normalise(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
