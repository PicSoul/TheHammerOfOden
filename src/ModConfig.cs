using BepInEx.Configuration;
using UnityEngine;

namespace TheHammerOfOden
{
    internal static class ModConfig
    {
        internal static ConfigEntry<bool> Enabled;

        internal static ConfigEntry<int> SnapDivisions;
        internal static ConfigEntry<KeyboardShortcut> SnapIncreaseKey;
        internal static ConfigEntry<KeyboardShortcut> SnapDecreaseKey;
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
        internal static ConfigEntry<float> GizmoInactiveSaturation;
        internal static ConfigEntry<bool> ShowAngleMarkers;
        internal static ConfigEntry<float> AngleMarkerSize;
        internal static ConfigEntry<Color> GizmoColorX;
        internal static ConfigEntry<Color> GizmoColorY;
        internal static ConfigEntry<Color> GizmoColorZ;

        internal static ConfigEntry<bool> ShowSnapPoints;
        internal static ConfigEntry<bool> ShowInactiveSnapPoints;
        internal static ConfigEntry<float> SnapPointSize;
        internal static ConfigEntry<Color> SnapPointColor;
        internal static ConfigEntry<Color> SnapPointActiveColor;

        internal static ConfigEntry<SnapPointDisplay> SnapDisplay;
        internal static ConfigEntry<bool> SnapPointsSeeThrough;
        internal static ConfigEntry<bool> ShowTargetSnapPoints;
        internal static ConfigEntry<float> TargetSnapPointRange;
        internal static ConfigEntry<float> TargetPreviewReach;
        internal static ConfigEntry<Color> TargetSnapPointColor;

        internal static ConfigEntry<bool> HoldToResetSnapPoint;
        internal static ConfigEntry<float> SnapPointResetHold;

        internal static ConfigEntry<DerivedSnapMode> DerivedSnaps;
        internal static ConfigEntry<bool> SnapToDerivedTargets;
        internal static ConfigEntry<float> DerivedTargetRange;
        internal static ConfigEntry<float> DerivedSnapDistance;

        internal static ConfigEntry<float> OffsetStep;
        internal static ConfigEntry<float> OffsetLimit;

        internal static ConfigEntry<PlacementFreedom> Freedom;
        internal static ConfigEntry<ClippingMode> Clipping;
        internal static ConfigEntry<KeyboardShortcut> ClippingToggleKey;

        internal static ConfigEntry<bool> ResetOnPieceChange;
        internal static ConfigEntry<bool> DebugLogging;

        internal static bool IsEnabled => Enabled != null && Enabled.Value;
        internal static bool DebugEnabled => DebugLogging != null && DebugLogging.Value;

        /// <summary>
        /// Degrees per rotation step.
        /// </summary>
        /// <remarks>
        /// Counted over a full turn, not a half turn. Vanilla already works this way -
        /// m_placeRotation holds 16 positions of 22.5 degrees - so a whole circle is both
        /// the more natural unit and the one the game itself uses.
        /// </remarks>
        internal static float StepDegrees => 360f / Mathf.Max(2, SnapDivisions.Value);

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true,
                "Master switch. Turn off to leave placement entirely to the vanilla game.");

            SnapDivisions = config.Bind("Rotation", "SnapAnglesPerTurn", 32,
                new ConfigDescription(
                    "Number of snap angles in a full 360 degree turn. Vanilla uses 16 (22.5 degrees per "
                    + "step); 32 gives 11.25. Adjustable in game with the keys below.",
                    new AcceptableValueRange<int>(2, 512)));

            SnapIncreaseKey = config.Bind("Rotation", "SnapIncreaseKey",
                new KeyboardShortcut(KeyCode.PageUp),
                "Double the number of snap angles, for finer rotation.");

            SnapDecreaseKey = config.Bind("Rotation", "SnapDecreaseKey",
                new KeyboardShortcut(KeyCode.PageDown),
                "Halve the number of snap angles, for coarser rotation.");

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

            GizmoInactiveOpacity = config.Bind("Gizmo", "InactiveOpacity", 0.45f,
                new ConfigDescription(
                    "How faint the two axes you are not rotating appear. Low values keep the display "
                    + "readable without hiding the piece underneath.",
                    new AcceptableValueRange<float>(0f, 1f)));

            GizmoInactiveSaturation = config.Bind("Gizmo", "InactiveSaturation", 0.4f,
                new ConfigDescription(
                    "How much colour the two axes you are not rotating keep. Lower values wash them "
                    + "towards grey, so the axis you are actually rotating stands out by being the only "
                    + "vivid one rather than merely the brightest.",
                    new AcceptableValueRange<float>(0f, 1f)));

            ShowAngleMarkers = config.Bind("Gizmo", "ShowAngleMarkers", true,
                "Put a small bead on each ring at the piece's current angle for that axis, so you can "
                + "see how far it has been turned rather than only that it has been.");

            AngleMarkerSize = config.Bind("Gizmo", "AngleMarkerSize", 0.07f,
                new ConfigDescription("Radius of the angle beads, in metres.",
                    new AcceptableValueRange<float>(0.02f, 0.4f)));

            GizmoColorX = config.Bind("Gizmo", "ColorX", new Color(1f, 0.16f, 0.22f, 1f),
                "Colour of the pitch (X) ring.");

            GizmoColorY = config.Bind("Gizmo", "ColorY", new Color(0.18f, 1f, 0.33f, 1f),
                "Colour of the yaw (Y) ring.");

            GizmoColorZ = config.Bind("Gizmo", "ColorZ", new Color(0.2f, 0.62f, 1f, 1f),
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

            SnapDisplay = config.Bind("Snap Points", "Display", SnapPointDisplay.Relevant,
                new ConfigDescription(
                    "How many snap point markers to draw. "
                    + "All: every point on both pieces. Thorough, but a piece with many anchors "
                    + "becomes a mass of overlapping rings. "
                    + "Relevant: the point your piece is held by, plus the target points near enough "
                    + "to actually snap to. "
                    + "ActivePairOnly: just the two points currently snapping together."));

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

            TargetPreviewReach = config.Bind("Snap Points", "TargetPreviewReach", 1.15f,
                new ConfigDescription(
                    "How close a target snap point must be before it starts to appear. Markers fade in "
                    + "from here and reach full strength at DerivedSnapDistance, so you can see what you "
                    + "are approaching rather than having points appear only once already in range.",
                    new AcceptableValueRange<float>(0.25f, 8f)));

            TargetSnapPointColor = config.Bind("Snap Points", "TargetColor", new Color(0.35f, 0.9f, 1f, 0.55f),
                "Colour of snap points on the piece you are aiming at.");

            HoldToResetSnapPoint = config.Bind("Snap Points", "HoldToResetSnapPoint", true,
                "Hold either snap cycle key (Q or E) to jump straight back to automatic snapping, "
                + "instead of cycling all the way around to reach it. Tapping still steps one at a time.");

            SnapPointResetHold = config.Bind("Snap Points", "ResetHoldSeconds", 0.5f,
                new ConfigDescription(
                    "How long the cycle key must be held before snapping resets to automatic.",
                    new AcceptableValueRange<float>(0.15f, 3f)));

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

            DerivedTargetRange = config.Bind("Snap Points", "DerivedTargetRangeCap", 5f,
                new ConfigDescription(
                    "Upper limit on how far to look for nearby pieces when snapping to derived anchors. "
                    + "The search normally sizes itself to the piece you are holding - its anchor spread "
                    + "plus the snap distance - so this only takes effect for unusually large pieces. "
                    + "Lower it if building in a dense area costs frames.",
                    new AcceptableValueRange<float>(1f, 15f)));

            DerivedSnapDistance = config.Bind("Snap Points", "DerivedSnapDistance", 0.5f,
                new ConfigDescription(
                    "How close an anchor pair must be before it snaps. Vanilla uses 0.5m. Larger values "
                    + "grab from further away but make precise free placement harder.",
                    new AcceptableValueRange<float>(0.05f, 2f)));

            OffsetStep = config.Bind("Placement Offset", "Step", 0.05f,
                new ConfigDescription(
                    "How far each scroll notch pushes the piece along your aim when both rotation "
                    + "modifiers are held. Small values give the fine control needed to sink a piece "
                    + "into another by just the right amount.",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            OffsetLimit = config.Bind("Placement Offset", "Limit", 3f,
                new ConfigDescription(
                    "Maximum distance the piece can be pushed or pulled from where you are aiming.",
                    new AcceptableValueRange<float>(0.5f, 20f)));

            Freedom = config.Bind("Free Placement", "Freedom", PlacementFreedom.SurfacesAndSpacing,
                new ConfigDescription(
                    "Which vanilla placement rules free placement sets aside. "
                    + "Vanilla: none, free placement only affects snapping. "
                    + "Surfaces: what a piece may rest on - ground only, not on wood, tilting "
                    + "surfaces, cultivated soil. "
                    + "SurfacesAndSpacing: also the room a piece demands, such as forge extensions "
                    + "refusing to sit near each other. "
                    + "Everything: also biome, dungeon and weather restrictions. "
                    + "Wards, no-build zones and other players are never bypassed at any setting."));

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
