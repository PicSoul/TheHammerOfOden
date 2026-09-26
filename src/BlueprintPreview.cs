using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// A blueprint shown as a turning miniature in the blueprint book: the model code's drawing,
    /// filmed by a camera of its own into a picture the book shows.
    /// </summary>
    /// <remarks>
    /// The model is built once when a blueprint is picked in the book and kept while it stays
    /// picked. It hangs far above the world on a layer nothing but its own camera and light look
    /// at, so it is never seen in the world and the world is never seen behind it, and its light
    /// makes it look the same by day or night.
    ///
    /// It only costs anything while the book is open: the camera is switched off otherwise, and
    /// the model and its meshes are let go when the book closes.
    /// </remarks>
    internal static class BlueprintPreview
    {
        private const int Size = 512;
        private static readonly Vector3 Stage = new Vector3(0f, 9000f, 0f);

        private static GameObject _rig;
        private static Camera _camera;
        private static RenderTexture _texture;
        private static int _layer = -1;

        private static GameObject _model;
        private static readonly List<Mesh> Meshes = new List<Mesh>();
        private static Bounds _bounds;

        private static float _yaw = 30f;
        private static float _pitch = 25f;
        private static float _zoom = 1f;
        /// <summary>Turned or zoomed by hand since this blueprint was picked: it then stays where it was left.</summary>
        private static bool _held;

        internal static Texture Texture => _texture;
        internal static bool HasModel => _model != null;

        /// <summary>Shows these pieces - a blueprint's, in its own units - or nothing, for null.</summary>
        internal static void Show(List<CopyOrder> plan)
        {
            Clear();
            if (plan == null || plan.Count == 0 || !EnsureRig())
            {
                return;
            }

            // Drawn at full size: the camera stands back to fit it, so nothing small is left out.
            _model = ModelDisplay.Build(plan, 1f, Meshes, out _, out _);
            if (_model == null)
            {
                return;
            }

            _model.transform.position = Stage;
            SetLayer(_model.transform, _layer);

            bool any = false;
            foreach (Renderer renderer in _model.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                {
                    _bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    _bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!any)
            {
                _bounds = new Bounds(Stage, Vector3.one);
            }

            _yaw = 30f;
            _pitch = 25f;
            _zoom = 1f;
            _held = false;
        }

        /// <summary>Turns the camera round the model; called while the book is open.</summary>
        internal static void Tick(bool open)
        {
            if (_camera == null)
            {
                return;
            }

            bool shown = open && _model != null;
            if (_camera.enabled != shown)
            {
                _camera.enabled = shown;
            }

            if (!shown)
            {
                return;
            }

            // Turns on its own until the player takes hold of it - then it stays where it is put,
            // so the view can be kept as the blueprint's picture.
            if (!_held)
            {
                _yaw += 15f * Time.unscaledDeltaTime;
            }

            Frame();
        }

        /// <summary>Stands the camera back far enough to fit the whole model, from the current angle.</summary>
        private static void Frame()
        {
            float radius = Mathf.Max(0.5f, _bounds.extents.magnitude);
            float distance = radius / Mathf.Sin(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * _zoom;
            Quaternion around = Quaternion.Euler(_pitch, _yaw, 0f);

            _camera.transform.position = _bounds.center - around * Vector3.forward * distance;
            _camera.transform.rotation = around;
            _camera.nearClipPlane = Mathf.Max(0.05f, distance - radius * 2f);
            _camera.farClipPlane = distance + radius * 2f;
        }

        /// <summary>
        /// Takes a picture of these pieces for a blueprint's thumbnail - the small .png PlanBuild
        /// shows beside a blueprint, and the book can too. Whatever the book was showing is let go.
        /// </summary>
        internal static bool Capture(List<CopyOrder> plan, string pngPath)
        {
            if (plan == null || plan.Count == 0)
            {
                return false;
            }

            // Drawn from its own lowest corner, so the model sits near the stage.
            Vector3 corner = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            foreach (CopyOrder order in plan)
            {
                corner = Vector3.Min(corner, order.Position);
            }

            List<CopyOrder> local = new List<CopyOrder>(plan.Count);
            foreach (CopyOrder order in plan)
            {
                local.Add(new CopyOrder
                {
                    Prefab = order.Prefab,
                    Position = order.Position - corner,
                    Rotation = order.Rotation,
                    Scale = order.Scale,
                    BendDegrees = order.BendDegrees,
                    BendAxis = order.BendAxis,
                    BendRise = order.BendRise,
                    BendChoice = order.BendChoice
                });
            }

            Show(local);
            if (_model == null)
            {
                return false;
            }

            _yaw = 35f;
            _pitch = 28f;
            _zoom = 1f;
            Frame();

            try
            {
                return Photograph(pngPath);
            }
            finally
            {
                Clear();
            }
        }

        /// <summary>
        /// Saves what the book's preview shows right now - the angle and zoom the player has turned
        /// it to - as a blueprint's picture.
        /// </summary>
        internal static bool CaptureView(string pngPath)
        {
            if (_model == null || _camera == null)
            {
                return false;
            }

            Frame();
            return Photograph(pngPath);
        }

        /// <summary>The camera's current view, rendered once more at thumbnail size and written as a .png.</summary>
        private static bool Photograph(string pngPath)
        {
            const int side = 256;
            RenderTexture picture = RenderTexture.GetTemporary(side, side, 24, RenderTextureFormat.ARGB32);
            RenderTexture was = RenderTexture.active;
            try
            {
                _camera.targetTexture = picture;
                _camera.Render();

                RenderTexture.active = picture;
                Texture2D texture = new Texture2D(side, side, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, side, side), 0, 0);
                texture.Apply();
                System.IO.File.WriteAllBytes(pngPath, texture.EncodeToPNG());
                Object.Destroy(texture);
                return true;
            }
            catch (System.Exception ex)
            {
                HammerOfOdenPlugin.Error($"Could not save the blueprint picture '{pngPath}': {ex.Message}");
                return false;
            }
            finally
            {
                RenderTexture.active = was;
                _camera.targetTexture = _texture;
                RenderTexture.ReleaseTemporary(picture);
            }
        }

        internal static void Drag(Vector2 delta)
        {
            _yaw += delta.x * 0.4f;
            _pitch = Mathf.Clamp(_pitch + delta.y * 0.3f, -10f, 85f);
            _held = true;
        }

        internal static void Zoom(float scroll)
        {
            _zoom = Mathf.Clamp(_zoom * (1f + scroll * 0.1f), 0.35f, 3f);
            _held = true;
        }

        /// <summary>Lets go of the model and its meshes; the camera and picture are kept.</summary>
        internal static void Clear()
        {
            foreach (Mesh mesh in Meshes)
            {
                if (mesh != null)
                {
                    Object.Destroy(mesh);
                }
            }

            Meshes.Clear();

            if (_model != null)
            {
                Object.Destroy(_model);
                _model = null;
            }
        }

        // ------------------------------------------------------------------ the rig

        private static bool EnsureRig()
        {
            if (_rig != null)
            {
                return true;
            }

            _layer = FreeLayer();
            if (_layer < 0)
            {
                HammerOfOdenPlugin.Error("No free layer for the blueprint preview; the book shows no preview.");
                return false;
            }

            _texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { name = "HoO_BlueprintPreview" };

            _rig = new GameObject("HoO_BlueprintPreview");
            Object.DontDestroyOnLoad(_rig);

            GameObject eye = new GameObject("camera");
            eye.transform.SetParent(_rig.transform, false);
            _camera = eye.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.targetTexture = _texture;
            _camera.cullingMask = 1 << _layer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.07f, 0.08f, 0.1f, 1f);
            _camera.fieldOfView = 30f;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;

            GameObject lamp = new GameObject("light");
            lamp.transform.SetParent(_rig.transform, false);
            lamp.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.cullingMask = 1 << _layer;
            light.shadows = LightShadows.None;

            HammerOfOdenPlugin.Debug($"Blueprint preview uses layer {_layer}.");
            return true;
        }

        /// <summary>A layer the game has not named, highest first, so nothing else draws on it.</summary>
        private static int FreeLayer()
        {
            for (int layer = 31; layer >= 8; layer--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
                {
                    return layer;
                }
            }

            return -1;
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayer(t.GetChild(i), layer);
            }
        }
    }
}
