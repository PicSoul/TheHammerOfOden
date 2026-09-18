using BepInEx.Configuration;
using UnityEngine;

namespace TheHammerOfOden
{
    internal static class ModConfig
    {
        internal static ConfigEntry<bool> Enabled;

        internal static ConfigEntry<int> SnapDivisions;
        internal static ConfigEntry<KeyboardShortcut> XAxisKey;
        internal static ConfigEntry<KeyboardShortcut> ZAxisKey;
        internal static ConfigEntry<KeyboardShortcut> ResetAxisKey;
        internal static ConfigEntry<KeyboardShortcut> ResetAllKey;

        internal static ConfigEntry<bool> CopyRotationOnPieceCopy;
        internal static ConfigEntry<KeyboardShortcut> CopyRotationKey;

        internal static ConfigEntry<FreePlacementMode> FreePlacement;
        internal static ConfigEntry<KeyboardShortcut> FreePlacementKey;

        internal static ConfigEntry<bool> ShowGizmo;
        internal static ConfigEntry<bool> GizmoActiveAxisOnly;
        internal static ConfigEntry<float> GizmoScale;
        internal static ConfigEntry<float> GizmoWidth;
        internal static ConfigEntry<float> GizmoInactiveOpacity;
        internal static ConfigEntry<Color> GizmoColorX;
        internal static ConfigEntry<Color> GizmoColorY;
        internal static ConfigEntry<Color> GizmoColorZ;

        internal static ConfigEntry<bool> ShowSnapPoints;
        internal static ConfigEntry<bool> ShowInactiveSnapPoints;
        internal static ConfigEntry<float> SnapPointSize;
        internal static ConfigEntry<Color> SnapPointColor;
        internal static ConfigEntry<Color> SnapPointActiveColor;

        internal static ConfigEntry<bool> SnapPointsSeeThrough;
        internal static ConfigEntry<bool> ShowTargetSnapPoints;
        internal static ConfigEntry<float> TargetSnapPointRange;
        internal static ConfigEntry<Color> TargetSnapPointColor;

        internal static ConfigEntry<DerivedSnapMode> DerivedSnaps;
        internal static ConfigEntry<bool> SnapToDerivedTargets;
        internal static ConfigEntry<float> DerivedTargetRange;
        internal static ConfigEntry<float> DerivedSnapDistance;

        internal static ConfigEntry<ClippingMode> Clipping;
        internal static ConfigEntry<KeyboardShortcut> ClippingToggleKey;

        internal static ConfigEntry<bool> ResetOnPieceChange;
        internal static ConfigEntry<bool> DebugLogging;

        internal static bool IsEnabled => Enabled != null && Enabled.Value;
        internal static bool DebugEnabled => DebugLogging != null && DebugLogging.Value;

        /// <summary>Degrees per rotation step. Matches Gizmo's "divisions per 180 degrees" convention.</summary>
        internal static float StepDegrees => 180f / Mathf.Max(2, SnapDivisions.Value);

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true,
                "Master switch. Turn off to leave placement entirely to the vanilla game.");

            SnapDivisions = config.Bind("Rotation", "SnapDivisions", 16,
                new ConfigDescription(
                    "Number of snap angles per 180 degrees. Vanilla uses 8 (22.5 degrees per step); 16 gives 11.25.",
                    new AcceptableValueRange<int>(2, 256)));

            XAxisKey = config.Bind("Rotation", "XAxisKey",
                new KeyboardShortcut(KeyCode.LeftShift),
                "Hold to rotate on the X axis (pitch) instead of yaw.");

            ZAxisKey = config.Bind("Rotation", "ZAxisKey",
                new KeyboardShortcut(KeyCode.LeftAlt),
                "Hold to rotate on the Z axis (roll) instead of yaw.");

            ResetAxisKey = config.Bind("Rotation", "ResetAxisKey",
                new KeyboardShortcut(KeyCode.J),
                "Press with an axis key held to zero that axis. Press alone to zero the yaw.");

            ResetAllKey = config.Bind("Rotation", "ResetAllKey",
                new KeyboardShortcut(KeyCode.U),
                "Press to zero every axis at once, returning the piece to flat.");

            CopyRotationOnPieceCopy = config.Bind("Copy", "CopyRotationOnPieceCopy", true,
                "When you copy a placed piece with the vanilla shortcut (hold AltPlace, default Left Shift, "
                + "and press Remove, default middle mouse), also adopt that piece's full 3-axis rotation. "
                + "Vanilla only copies the yaw.");

            CopyRotationKey = config.Bind("Copy", "CopyRotationKey",
                KeyboardShortcut.Empty,
                "Optional separate key to copy the rotation of the piece you are looking at, without "
                + "also switching to that piece. Leave empty to disable.");

            FreePlacement = config.Bind("Free Placement", "Mode", FreePlacementMode.Toggle,
                new ConfigDescription(
                    "Who controls free placement (no snap attraction, and terrain pieces freed from ground height).\n"
                    + "Vanilla: Valheim's own behaviour, held with AltPlace (Left Shift). Use this if you have "
                    + "rebound XAxisKey away from Left Shift.\n"
                    + "Hold: hold FreePlacementKey instead, leaving Left Shift free for pitch.\n"
                    + "Toggle: tap FreePlacementKey to turn it on and off."));

            FreePlacementKey = config.Bind("Free Placement", "FreePlacementKey",
                new KeyboardShortcut(KeyCode.O),
                "Key used by the Hold and Toggle modes. Ignored in Vanilla mode.");

            ShowGizmo = config.Bind("Gizmo", "ShowGizmo", true,
                "Draw rotation rings around the piece you are placing.");

            GizmoActiveAxisOnly = config.Bind("Gizmo", "ActiveAxisOnly", false,
                "Show only the ring for the axis you are currently rotating, instead of all three. "
                + "Much less busy, at the cost of not seeing the piece's other angles at a glance.");

            GizmoScale = config.Bind("Gizmo", "Scale", 1.15f,
                new ConfigDescription(
                    "Ring size relative to the piece. The rings are measured from the piece's own bounds, "
                    + "so a torch gets small rings and a longhouse gets large ones.",
                    new AcceptableValueRange<float>(0.5f, 3f)));

            GizmoWidth = config.Bind("Gizmo", "Width", 0.035f,
                new ConfigDescription(
                    "Ring line thickness. The active axis is drawn thicker than this automatically.",
                    new AcceptableValueRange<float>(0.005f, 0.2f)));

            GizmoInactiveOpacity = config.Bind("Gizmo", "InactiveOpacity", 0.25f,
                new ConfigDescription(
                    "How faint the two axes you are not rotating appear. Low values keep the display "
                    + "readable without hiding the piece underneath.",
                    new AcceptableValueRange<float>(0f, 1f)));

            GizmoColorX = config.Bind("Gizmo", "ColorX", new Color(1f, 0.35f, 0.35f, 0.9f),
                "Colour of the pitch (X) ring.");

            GizmoColorY = config.Bind("Gizmo", "ColorY", new Color(0.15f, 1f, 0.35f, 0.9f),
                "Colour of the yaw (Y) ring.");

            GizmoColorZ = config.Bind("Gizmo", "ColorZ", new Color(0.4f, 0.65f, 1f, 0.9f),
                "Colour of the roll (Z) ring.");

            ShowSnapPoints = config.Bind("Snap Points", "ShowSnapPoints", true,
                "Mark the placing piece's snap points in the world. Vanilla reuses snap point names, "
                + "so two different points can both read as \"Bottom 1\" when cycling with Q and E; "
                + "showing them removes the guesswork.");

            ShowInactiveSnapPoints = config.Bind("Snap Points", "ShowInactiveSnapPoints", true,
                "Also mark the snap points you have not selected, faintly. Off shows only the active one.");

            SnapPointSize = config.Bind("Snap Points", "Size", 0.09f,
                new ConfigDescription("Marker radius in metres. The selected point is drawn larger automatically.",
                    new AcceptableValueRange<float>(0.02f, 0.5f)));

            SnapPointColor = config.Bind("Snap Points", "Color", new Color(1f, 1f, 1f, 0.35f),
                "Colour of unselected snap point markers.");

            SnapPointActiveColor = config.Bind("Snap Points", "ActiveColor", new Color(0.72f, 0.25f, 1f, 1f),
                "Colour of the snap point currently selected with Q / E.");

            SnapPointsSeeThrough = config.Bind("Snap Points", "SeeThrough", true,
                "Draw snap point markers through solid objects, so a point on the far side or the "
                + "underside of a piece is still visible. Applies to both the piece you are placing "
                + "and the piece you are aiming at.");

            ShowTargetSnapPoints = config.Bind("Snap Points", "ShowTargetSnapPoints", true,
                "Also mark the snap points on the piece you are aiming at, so you can see what is "
                + "available to attach to.");

            TargetSnapPointRange = config.Bind("Snap Points", "TargetRange", 4f,
                new ConfigDescription(
                    "Only mark target snap points within this many metres of the piece you are placing. "
                    + "Vanilla auto-snap only reaches 0.5m, so a large value mostly adds clutter.",
                    new AcceptableValueRange<float>(0.5f, 20f)));

            TargetSnapPointColor = config.Bind("Snap Points", "TargetColor", new Color(0.35f, 0.9f, 1f, 0.55f),
                "Colour of snap points on the piece you are aiming at.");

            DerivedSnaps = config.Bind("Snap Points", "DerivedSnapPoints", DerivedSnapMode.Centers,
                new ConfigDescription(
                    "Extra anchors added to the piece you are placing, on top of the ones it ships with. "
                    + "Off: none. "
                    + "Centers: piece centre and the centre of each face (7 extra). "
                    + "CentersAndCorners: the above plus the eight corners (15 extra). "
                    + "Full: the above plus a midpoint between the centre and each of them (29 extra). "
                    + "Every extra anchor is another stop when cycling with Q and E, so higher settings "
                    + "trade convenience for a longer cycle."));

            SnapToDerivedTargets = config.Bind("Snap Points", "SnapToDerivedTargets", false,
                "Also snap to derived anchors on pieces already built, so you can line up with the "
                + "centre of a wall rather than only its shipped snap points. Off by default: it adds "
                + "snap targets that vanilla does not have, which changes how building feels.");

            DerivedTargetRange = config.Bind("Snap Points", "DerivedTargetRange", 5f,
                new ConfigDescription(
                    "How far to look for nearby pieces when snapping to derived anchors.",
                    new AcceptableValueRange<float>(1f, 15f)));

            DerivedSnapDistance = config.Bind("Snap Points", "DerivedSnapDistance", 0.5f,
                new ConfigDescription(
                    "How close an anchor pair must be before it snaps. Vanilla uses 0.5m. Larger values "
                    + "grab from further away but make precise free placement harder.",
                    new AcceptableValueRange<float>(0.05f, 2f)));

            Clipping = config.Bind("Clipping", "Mode", ClippingMode.WithFreePlacement,
                new ConfigDescription(
                    "Whether pieces may be placed intersecting other objects. Vanilla refuses when a "
                    + "piece would penetrate something by more than 0.2m, which makes tight arrangements "
                    + "and decorative overlaps impossible. Applies to every piece, including modded ones. "
                    + "Never: vanilla behaviour. "
                    + "WithFreePlacement: allowed only while free placement is on, since both express the "
                    + "same intent. "
                    + "Always: allowed at all times."));

            ClippingToggleKey = config.Bind("Clipping", "ToggleKey", KeyboardShortcut.Empty,
                "Optional key to step through the clipping modes while building. Leave empty to disable.");

            ResetOnPieceChange = config.Bind("Rotation", "ResetOnPieceChange", false,
                "Zero all rotation when you select a different build piece. Off keeps your tilt "
                + "while you switch pieces, which is usually what you want mid-build.");

            DebugLogging = config.Bind("Debug", "DebugLogging", false,
                "Write placement diagnostics to the BepInEx log.");
        }
    }
}
