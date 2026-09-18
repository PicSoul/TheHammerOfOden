using UnityEngine;

namespace TheHammerOfOden
{
    internal enum RotationAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    /// <summary>
    /// The rotation the placement ghost should be shown and placed at.
    /// </summary>
    /// <remarks>
    /// Euler rather than accumulated quaternions, so that resetting a single axis is
    /// exact and repeatable. Accumulating world-axis rotations drifts and makes
    /// "reset only the tilt" impossible to express.
    /// </remarks>
    internal static class RotationState
    {
        private static float _x;
        private static float _y;
        private static float _z;

        internal static Quaternion Current => Quaternion.Euler(_x, _y, _z);

        /// <summary>True when the piece is tilted off the vanilla flat-on-the-ground plane.</summary>
        internal static bool IsTilted => !Mathf.Approximately(_x, 0f) || !Mathf.Approximately(_z, 0f);

        internal static void Rotate(RotationAxis axis, int sign, float stepDegrees)
        {
            float delta = sign * stepDegrees;

            switch (axis)
            {
                case RotationAxis.X:
                    _x = Wrap(_x + delta);
                    break;
                case RotationAxis.Y:
                    _y = Wrap(_y + delta);
                    break;
                case RotationAxis.Z:
                    _z = Wrap(_z + delta);
                    break;
            }
        }

        internal static void ResetAxis(RotationAxis axis)
        {
            switch (axis)
            {
                case RotationAxis.X:
                    _x = 0f;
                    break;
                case RotationAxis.Y:
                    _y = 0f;
                    break;
                case RotationAxis.Z:
                    _z = 0f;
                    break;
            }
        }

        internal static void ResetAll()
        {
            _x = 0f;
            _y = 0f;
            _z = 0f;
        }

        /// <summary>Adopt the rotation of an already-placed piece.</summary>
        internal static void MatchPiece(Piece piece)
        {
            if (piece == null)
            {
                return;
            }

            Vector3 euler = piece.transform.rotation.eulerAngles;
            _x = Wrap(euler.x);
            _y = Wrap(euler.y);
            _z = Wrap(euler.z);
        }

        /// <summary>
        /// Keep the yaw vanilla owns in sync with ours, so that vanilla features reading
        /// m_placeRotation (the build menu, piece re-selection) stay coherent.
        /// </summary>
        internal static void SyncYawFrom(float degrees)
        {
            _y = Wrap(degrees);
        }

        internal static float Yaw => _y;

        internal static float Pitch => _x;

        internal static float Roll => _z;

        /// <summary>
        /// World-space axis that each increment actually turns the piece about.
        /// </summary>
        /// <remarks>
        /// Quaternion.Euler(x, y, z) applies Z, then X, then Y, so only yaw acts on a
        /// world axis. Working the increment out for pitch:
        ///
        ///   Q_new = Ry * Rx(x+d) * Rz = [Ry * Rx(d) * Ry-inverse] * Q_old
        ///
        /// which is a rotation about Ry * worldX. Roll picks up both preceding
        /// rotations the same way. The gizmo rings use these so they show the axis the
        /// scroll wheel will really move.
        /// </remarks>
        internal static Vector3 AxisDirection(RotationAxis axis)
        {
            Quaternion ry = Quaternion.Euler(0f, _y, 0f);

            switch (axis)
            {
                case RotationAxis.X:
                    return ry * Vector3.right;
                case RotationAxis.Z:
                    return ry * Quaternion.Euler(_x, 0f, 0f) * Vector3.forward;
                default:
                    return Vector3.up;
            }
        }

        private static float Wrap(float degrees)
        {
            degrees %= 360f;
            return (degrees < 0f) ? (degrees + 360f) : degrees;
        }
    }
}
