using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Cached access to the active camera.
    /// </summary>
    /// <remarks>
    /// Unity implements Camera.main as a search for objects tagged MainCamera, so calling it
    /// several times a frame is a real cost - and it grows worse alongside mods that add
    /// their own cameras, since the search has more to sift through.
    ///
    /// The reference is revalidated whenever it goes missing or inactive, so a mod that
    /// swaps cameras is still followed; it is simply not re-searched every frame.
    /// </remarks>
    internal static class MainCamera
    {
        private static Camera _camera;
        private static float _nextCheck;

        internal static Camera Get()
        {
            // A destroyed camera compares equal to null through Unity's operator, so this
            // also covers the camera being torn down.
            if (_camera == null || !_camera.isActiveAndEnabled || Time.unscaledTime >= _nextCheck)
            {
                _camera = Camera.main;
                _nextCheck = Time.unscaledTime + 1f;
            }

            return _camera;
        }

        internal static Quaternion Facing =>
            Get() is Camera camera && camera != null ? camera.transform.rotation : Quaternion.identity;

        internal static void Invalidate()
        {
            _camera = null;
            _nextCheck = 0f;
        }
    }
}
