using BepInEx.Configuration;
using ServerSync;
using UnityEngine;

namespace TheHammerOfOden
{
    internal static class ModConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<ToolScope> Tools;
        internal static ConfigEntry<string> BuildToolTables;
        internal static ConfigEntry<string> TerrainToolTables;
        internal static ConfigEntry<KeyboardShortcut> MasterToggleKey;
        internal static ConfigEntry<KeyboardShortcut> HelpKey;
        internal static ConfigEntry<bool> ShowHammerGlow;
        internal static ConfigEntry<bool> ShowHammerSparks;
        internal static ConfigEntry<float> SparkRate;
        internal static ConfigEntry<Color> GlowColor;
        internal static ConfigEntry<float> GlowIntensity;
        internal static ConfigEntry<float> GlowRange;
        internal static ConfigEntry<float> GlowHeadOffset;

        internal static ConfigEntry<int> SnapDivisions;
        internal static ConfigEntry<KeyboardShortcut> SnapIncreaseKey;
        internal static ConfigEntry<KeyboardShortcut> SnapDecreaseKey;
        internal static ConfigEntry<KeyboardShortcut> XAxisKey;
        internal static ConfigEntry<KeyboardShortcut> ZAxisKey;
        internal static ConfigEntry<KeyboardShortcut> ResetAxisKey;
        internal static ConfigEntry<KeyboardShortcut> ResetAllKey;

        internal static ConfigEntry<bool> CopyRotationOnPieceCopy;
        internal static ConfigEntry<KeyboardShortcut> CopyRotationKey;
        internal static ConfigEntry<bool> CopyScaleOnPieceCopy;
        internal static ConfigEntry<bool> CopyBendOnPieceCopy;

        internal static ConfigEntry<FreePlacementMode> FreePlacement;
        internal static ConfigEntry<KeyboardShortcut> FreePlacementKey;

        internal static ConfigEntry<SurfacePlacementMode> SurfaceMode;
        internal static ConfigEntry<KeyboardShortcut> SurfacePlacementKey;
        internal static ConfigEntry<SurfaceTargets> SurfaceTarget;
        internal static ConfigEntry<bool> AlignToSurface;
        internal static ConfigEntry<float> SurfaceGap;

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
        internal static ConfigEntry<float> MarkerStroke;
        internal static ConfigEntry<Color> SnapPointColor;
        internal static ConfigEntry<Color> SnapPointActiveColor;

        internal static ConfigEntry<SnapPointDisplay> SnapDisplay;
        internal static ConfigEntry<bool> SnapPointsSeeThrough;
        internal static ConfigEntry<bool> ShowTargetSnapPoints;
        internal static ConfigEntry<float> TargetSnapPointRange;
        internal static ConfigEntry<float> TargetPreviewReach;
        internal static ConfigEntry<Color> TargetSnapPointColor;

        internal static ConfigEntry<bool> RememberSnapPoint;
        internal static ConfigEntry<bool> HoldToResetSnapPoint;
        internal static ConfigEntry<float> SnapPointResetHold;

        internal static ConfigEntry<DerivedSnapMode> DerivedSnaps;
        internal static ConfigEntry<KeyboardShortcut> CycleDerivedSnapPointsKey;
        internal static ConfigEntry<bool> ScaleSnapPointsWithPiece;
        internal static ConfigEntry<bool> SortSnapPoints;
        internal static ConfigEntry<bool> RenameSnapPoints;
        internal static ConfigEntry<bool> SnapToDerivedTargets;
        internal static ConfigEntry<float> DerivedTargetRange;
        internal static ConfigEntry<float> DerivedSnapDistance;

        internal static ConfigEntry<KeyboardShortcut> ScaleModifierKey;
        internal static ConfigEntry<KeyboardShortcut> ScaleWiderKey;
        internal static ConfigEntry<KeyboardShortcut> ScaleNarrowerKey;
        internal static ConfigEntry<KeyboardShortcut> ScaleTallerKey;
        internal static ConfigEntry<KeyboardShortcut> ScaleShorterKey;
        internal static ConfigEntry<KeyboardShortcut> ScaleDeeperKey;
        internal static ConfigEntry<KeyboardShortcut> ScaleShallowerKey;
        internal static ConfigEntry<KeyboardShortcut> ScaleUpKey;
        internal static ConfigEntry<KeyboardShortcut> ScaleDownKey;
        internal static ConfigEntry<KeyboardShortcut> ScaleResetKey;
        internal static ConfigEntry<float> ScaleStep;
        internal static ConfigEntry<float> ScaleMin;
        internal static ConfigEntry<float> ScaleMax;
        internal static ConfigEntry<bool> ResetScaleOnPieceChange;
        internal static ConfigEntry<ScaleRestriction> ScaleRestrictions;
        internal static ConfigEntry<float> ScaleMaxWithParticles;
        internal static ConfigEntry<float> RangeGrowth;
        internal static ConfigEntry<float> ScaleRepeatDelay;
        internal static ConfigEntry<float> ScaleRepeatRate;
        internal static ConfigEntry<bool> ScaleParticles;

        internal static ConfigEntry<float> OffsetStep;
        internal static ConfigEntry<float> OffsetLimit;
        internal static ConfigEntry<float> NudgeStep;
        internal static ConfigEntry<float> NudgeStepLarge;
        internal static ConfigEntry<NudgeFrameMode> NudgeFrame;
        internal static ConfigEntry<KeyboardShortcut> NudgeLargeModifierKey;
        internal static ConfigEntry<KeyboardShortcut> NudgeForwardKey;
        internal static ConfigEntry<KeyboardShortcut> NudgeBackwardKey;
        internal static ConfigEntry<KeyboardShortcut> NudgeLeftKey;
        internal static ConfigEntry<KeyboardShortcut> NudgeRightKey;
        internal static ConfigEntry<KeyboardShortcut> NudgeUpKey;
        internal static ConfigEntry<KeyboardShortcut> NudgeDownKey;
        internal static ConfigEntry<KeyboardShortcut> ResetOffsetKey;

        internal static ConfigEntry<KeyboardShortcut> FreezeKey;
        internal static ConfigEntry<bool> EditPlacedPieces;
        internal static ConfigEntry<KeyboardShortcut> EditKey;
        internal static ConfigEntry<string> BendNever;
        internal static ConfigEntry<string> BendAlways;
        internal static ConfigEntry<KeyboardShortcut> BendModifierKey;
        internal static ConfigEntry<KeyboardShortcut> BendAxisKey;
        internal static ConfigEntry<KeyboardShortcut> BendResetKey;
        internal static ConfigEntry<float> BendStep;
        internal static ConfigEntry<float> BendMaximum;
        internal static ConfigEntry<int> BendMinimumSlices;
        internal static ConfigEntry<float> BendSegment;
        internal static ConfigEntry<float> BendSolidFill;
        internal static ConfigEntry<bool> BendRelaxesPlacement;
        internal static ConfigEntry<bool> BendRebuildsCollision;
        internal static ConfigEntry<bool> BendCurvesGhostSnapPoints;
        internal static ConfigEntry<bool> ShowBendableMarker;
        internal static ConfigEntry<Color> BendableMarkerColour;
        internal static ConfigEntry<float> BendCollisionTolerance;
        internal static ConfigEntry<bool> EditRemovesCollision;
        internal static ConfigEntry<Color> EditGhostTint;
        internal static ConfigEntry<Color> EditGhostGlow;
        internal static ConfigEntry<bool> EditHidesPiece;
        internal static ConfigEntry<Color> EditGhostBoxColor;
        internal static ConfigEntry<bool> ResetOffsetOnUnfreeze;

        internal static ConfigEntry<KeyboardShortcut> StationRangeKey;
        internal static ConfigEntry<float> StationRangeStep;
        internal static ConfigEntry<float> StationRangeMin;
        internal static ConfigEntry<float> StationRangeMax;
        internal static ConfigEntry<float> StationAdjustDistance;
        internal static ConfigEntry<bool> RequireLookingAtStation;
        internal static ConfigEntry<bool> ShowStationArea;
        internal static ConfigEntry<bool> ExtendReachToStation;
        internal static ConfigEntry<float> ReachLimit;

        internal static ConfigEntry<KeyboardShortcut> UndoKey;
        internal static ConfigEntry<int> UndoDepth;
        internal static ConfigEntry<bool> UndoRefundsToInventory;

        internal static ConfigEntry<KeyboardShortcut> BuildCameraKey;
        internal static ConfigEntry<KeyboardShortcut> CameraUpKey;
        internal static ConfigEntry<KeyboardShortcut> CameraDownKey;
        internal static ConfigEntry<KeyboardShortcut> CameraBoostKey;
        internal static ConfigEntry<float> CameraSpeed;
        internal static ConfigEntry<float> CameraBoost;
        internal static ConfigEntry<float> CameraRange;
        internal static ConfigEntry<float> CameraSensitivity;
        internal static ConfigEntry<bool> InvertCameraY;
        internal static ConfigEntry<bool> CameraAboveGround;
        internal static ConfigEntry<float> CameraGroundClearance;
        internal static ConfigEntry<bool> CameraLight;
        internal static ConfigEntry<float> CameraLightIntensity;
        internal static ConfigEntry<float> CameraLightRange;
        internal static ConfigEntry<bool> CameraPickup;
        internal static ConfigEntry<float> CameraPickupRange;
        internal static ConfigEntry<float> CameraPickupInterval;
        internal static ConfigEntry<float> MistClearRange;
        internal static ConfigEntry<bool> RequiresWisplight;
        internal static ConfigEntry<bool> HideDemisterOrb;
        internal static ConfigEntry<bool> QuietUpgradeGlow;

        internal static ConfigEntry<bool> OpenDoorsWhileBuilding;
        internal static ConfigEntry<float> DoorReach;

        internal static ConfigEntry<KeyboardShortcut> ZoopModifierKey;
        internal static ConfigEntry<int> ZoopLimit;
        internal static ConfigEntry<float> ZoopSpacing;
        internal static ConfigEntry<int> ZoopPerFrame;

        internal static ConfigEntry<KeyboardShortcut> GridKey;
        internal static ConfigEntry<float> GridSize;
        internal static ConfigEntry<bool> GridHeight;

        internal static ConfigEntry<PlacementFreedom> Freedom;
        internal static ConfigEntry<ClippingMode> Clipping;
        internal static ConfigEntry<KeyboardShortcut> ClippingToggleKey;

        internal static ConfigEntry<bool> ResetOnPieceChange;
        internal static ConfigEntry<bool> DebugLogging;
        internal static ConfigEntry<bool> DebugParticles;
        internal static ConfigEntry<KeyboardShortcut> DebugMistKey;
        internal static ConfigEntry<KeyboardShortcut> DebugPatchesKey;
        internal static ConfigEntry<KeyboardShortcut> DebugMeshKey;

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

        /// <summary>
        /// Whether the settings the server decides are actually enforced on clients.
        ///
        /// Synchronising a value and enforcing it are two different things. Without this, a
        /// server hands its values to clients on connect and a client may still edit them
        /// afterwards - which is the right default for a friendly server, where the sync is a
        /// convenience rather than a rule. Turning it on makes those settings read-only for
        /// everyone but an admin, which is what a public server wants.
        ///
        /// Only an admin can change it, because it is itself a synced setting and the server
        /// is the one that decides.
        /// </summary>
        public static ConfigEntry<bool> LockServerSettings;

        private static ConfigSync _sync;

        /// <summary>
        /// Marks a setting as one the server decides.
        ///
        /// The split is between what the world allows and what you happen to like looking at.
        /// How far you can reach, how large a piece may be scaled, how many pieces one zoop may
        /// lay and whether undo hands the materials back are all things that would let one player
        /// build under different rules from everyone else, so a server gets to set them for the
        /// whole session. Keys, colours, marker sizes, camera speed and every other matter of
        /// taste stay yours, because a server has no business choosing them and players would
        /// rightly resent it if one did.
        ///
        /// Single player and a server without the mod both leave every value exactly as the
        /// config file has it, so nothing here changes anything for a solo game.
        /// </summary>
        private static ConfigEntry<T> Synced<T>(ConfigEntry<T> entry)
        {
            if (_sync != null)
            {
                SyncedConfigEntry<T> synced = _sync.AddConfigEntry(entry);
                synced.SynchronizedConfig = true;
            }

            return entry;
        }

        internal static void Bind(ConfigFile config, ConfigSync sync = null)
        {
            _sync = sync;

            LockServerSettings = config.Bind("Server", "LockSettings", false,
                "Enforce the server's values for the settings it decides, rather than only "
                + "handing them out. Off means a client may still change them afterwards, which "
                + "suits a server among friends; on makes them read-only for everyone but an "
                + "admin. Ignored in single player and on a server without this mod.");
            if (sync != null) { sync.AddLockingConfigEntry(LockServerSettings); }

            Enabled = config.Bind("General", "Enabled", true,
                "Master switch. Turn off to leave placement entirely to the vanilla game.");

            MasterToggleKey = config.Bind("General", "MasterToggleKey",
                new KeyboardShortcut(KeyCode.H, KeyCode.LeftShift),
                "Turn every feature of this mod on or off at once, without leaving the game. "
                + "Flips the Enabled setting above, so the choice is remembered. Only read while "
                + "a build tool is in hand, which is the only time any of it applies.");

            HelpKey = config.Bind("General", "HelpKey", new KeyboardShortcut(KeyCode.F3),
                "Open the in-game reference: every key this mod uses, read from the settings "
                + "themselves as they stand, so it cannot drift from what the keys actually do. On a "
                + "server that enforces its config, it shows the server's values, which are the ones "
                + "you are playing with rather than the ones in your own file. "
                + "F3 because it is free: Valheim itself only uses Ctrl+F3, to hide the HUD, and that "
                + "combination is left alone. F1 would have been the obvious choice and is taken by "
                + "shudnal's Configuration Manager.");

            ShowHammerGlow = config.Bind("General", "ShowHammerGlow", true,
                "Light the hammer while the mod is switched on, so you can see the state of the "
                + "master toggle at a glance instead of scrolling to find out.");

            ShowHammerSparks = config.Bind("General", "ShowHammerSparks", true,
                "Drift a few slow motes off the hammer while the mod is on. The light alone "
                + "reads well at night and washes out at noon; these show in any light.");

            SparkRate = config.Bind("General", "SparkRate", 18f,
                new ConfigDescription(
                    "Motes given off per second. Applied live, so you can settle on a number "
                    + "while looking at it rather than restarting to compare.",
                    new AcceptableValueRange<float>(1f, 60f)));

            GlowColor = config.Bind("General", "GlowColor", new Color(0.45f, 0.75f, 1f),
                "Colour of the hammer glow.");

            GlowIntensity = config.Bind("General", "GlowIntensity", 1.6f,
                new ConfigDescription("Brightness of the hammer glow.",
                    new AcceptableValueRange<float>(0f, 8f)));

            GlowRange = config.Bind("General", "GlowRange", 2.5f,
                new ConfigDescription(
                    "How far the hammer glow reaches, in metres. Kept short by default: this is "
                    + "meant to show on the tool, not to work as a torch.",
                    new AcceptableValueRange<float>(0.5f, 15f)));

            GlowHeadOffset = config.Bind("General", "GlowHeadOffset", 0.85f,
                new ConfigDescription(
                    "Where along the tool the glow sits, from 0 at the grip to 1 at the very end. "
                    + "The default puts it just inside the head rather than floating off the tip.",
                    new AcceptableValueRange<float>(0f, 1f)));

            Tools = config.Bind("General", "Tools", ToolScope.BuildingOnly,
                new ConfigDescription(
                    "Which tools this mod affects.\n"
                    + "BuildingOnly: tools that build, which means the hammer and any modded "
                    + "equivalent. The hoe, the cultivator and modded terrain tools get the build "
                    + "camera and nothing else - they share Valheim's placement code, which is how "
                    + "a mod reaches them by accident, and nothing here means much for a levelling "
                    + "operation. A tool counts as building if its piece table can remove built "
                    + "pieces, which is the test vanilla itself uses, so this is decided by what a "
                    + "tool does rather than by what it is called.\n"
                    + "AllTools: everything that uses the placement ghost, terrain tools included."));

            BuildToolTables = config.Bind("General", "BuildToolTables", "",
                "Piece tables to treat as building tools whatever the measurement says, separated "
                + "by commas. For a modded hammer whose table cannot remove pieces. Use the piece "
                + "table's name, such as _HammerPieceTable - turn DebugLogging on and the log "
                + "names the table of whatever you are holding.");

            TerrainToolTables = config.Bind("General", "TerrainToolTables", "",
                "Piece tables to leave to vanilla whatever the measurement says, separated by "
                + "commas. For a modded terrain tool that claims it can remove pieces and so "
                + "looks like a hammer. These still get the build camera, exactly as the hoe does.");

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

            CopyBendOnPieceCopy = config.Bind("Copy", "CopyBendOnPieceCopy", true,
                "Take a built piece's curve as well as its angle when you copy or edit it. Read from "
                + "what the piece recorded rather than measured off it, so copying an arch gives you "
                + "the same arch rather than something close to it.");

            CopyScaleOnPieceCopy = config.Bind("Copy", "CopyScaleOnPieceCopy", true,
                "When you copy a placed piece, also adopt the size it was built at. Pieces that cannot "
                + "be resized reset the scale to normal instead, so copying a chest does not leave a "
                + "stretched wall waiting behind it.");

            FreePlacement = Synced(config.Bind("Free Placement", "Mode", FreePlacementMode.Toggle,
                new ConfigDescription(
                    "Who controls free placement (no snap attraction, and terrain pieces freed from ground height).\n"
                    + "Vanilla: Valheim's own behaviour, held with AltPlace (Left Shift). Use this if you have "
                    + "rebound XAxisKey away from Left Shift.\n"
                    + "Hold: hold FreePlacementKey instead, leaving Left Shift free for pitch.\n"
                    + "Toggle: tap FreePlacementKey to turn it on and off.")));

            FreePlacementKey = config.Bind("Free Placement", "FreePlacementKey",
                new KeyboardShortcut(KeyCode.O),
                "Key used by the Hold and Toggle modes. Ignored in Vanilla mode.");

            SurfaceMode = config.Bind("Surface Placement", "Mode", SurfacePlacementMode.Toggle,
                new ConfigDescription(
                    "Lay a piece flat against whatever you are looking at - a wall, a ceiling, the "
                    + "side of a rock - instead of standing it upright on the ground.\n"
                    + "Off: never.\n"
                    + "Hold: while SurfacePlacementKey is held.\n"
                    + "Toggle: tap SurfacePlacementKey to turn it on and off.\n"
                    + "WhenTilted: whenever the piece is already pitched or rolled, with no key of "
                    + "its own. This is how Flip It does it, and costs you the ability to lay an "
                    + "untilted piece against a wall."));

            SurfacePlacementKey = config.Bind("Surface Placement", "SurfacePlacementKey",
                new KeyboardShortcut(KeyCode.P),
                "Key used by the Hold and Toggle modes. Ignored otherwise.");

            SurfaceTarget = config.Bind("Surface Placement", "Applies To", SurfaceTargets.NonStructural,
                new ConfigDescription(
                    "Which pieces may be laid against a surface.\n"
                    + "NonStructural: everything except the hammer's Build and Heavy Build tabs. "
                    + "Walls and floors already meet each other through snap points, which is more "
                    + "precise than a surface normal, and aligning them to one fights that.\n"
                    + "Everything: structural pieces too, for building against terrain."));

            AlignToSurface = config.Bind("Surface Placement", "AlignToSurface", true,
                "Turn the piece to match the surface, so its base lies flat against it. With this "
                + "off the piece still moves onto the surface but keeps the rotation you set by "
                + "hand, which is what you want when aiming a piece yourself.");

            SurfaceGap = config.Bind("Surface Placement", "SurfaceGap", 0f,
                new ConfigDescription(
                    "Distance to hold the piece off the surface, in metres. Raise it slightly if a "
                    + "flat piece flickers against what it is resting on.",
                    new AcceptableValueRange<float>(0f, 0.5f)));

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

            MarkerStroke = config.Bind("Snap Points", "MarkerStroke", 0.22f,
                new ConfigDescription(
                    "Outline thickness as a fraction of a marker's size. Proportional rather than fixed, "
                    + "so a marker scaled down for a small piece keeps its shape instead of thickening "
                    + "into a blob.",
                    new AcceptableValueRange<float>(0.05f, 0.6f)));

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
                    + "Anchors landing on a snap point the piece already has are dropped. "
                    + "Off: none. "
                    + "Centers: piece centre and the centre of each face. "
                    + "CentersAndCorners: also the eight corners, which mainly helps pieces that "
                    + "have no corner snap points of their own. "
                    + "CentersCornersAndEdges: also the midpoint of each of the twelve edges. "
                    + "Full: also a midpoint between the centre and every anchor above. "
                    + "Every extra anchor is another stop when cycling with Q and E."));

            CycleDerivedSnapPointsKey = config.Bind("Snap Points", "CycleDerivedSnapPointsKey",
                new KeyboardShortcut(KeyCode.Insert),
                "Step through the derived anchor modes while building, rather than editing this file.");

            ScaleSnapPointsWithPiece = config.Bind("Snap Points", "ScaleWithPiece", true,
                "Size the markers relative to the piece being placed, and shrink them further in the "
                + "denser modes. A fixed size either swamps a small piece or disappears on a large one.");

            RenameSnapPoints = config.Bind("Snap Points", "RenameSnapPoints", true,
                "Rename the piece's own snap points after where they sit, so \"Top 1\" becomes "
                + "\"Top Front\" or \"Top Right\". Valheim's names are bare ordinals that say nothing "
                + "about position and are reused for points in different places. Only the piece you "
                + "are holding is renamed, and only while you hold it.");

            RememberSnapPoint = config.Bind("Snap Points", "RememberSnapPoint", true,
                "Remember which anchor you last built each kind of piece by, and hold the next one "
                + "of that kind the same way. Choosing to place walls by their bottom corner then "
                + "survives switching to a beam and back, instead of resetting every time the piece "
                + "changes. Each kind of piece is remembered separately, and the record is kept "
                + "beside this file in com.pics0ul.valheim.thehammerofoden.snappoints.cfg, so it "
                + "survives a restart.");

            SortSnapPoints = config.Bind("Snap Points", "SortSnapPoints", true,
                "Put snap points into a predictable order for cycling with Q and E. Vanilla presents "
                + "them in whatever order the prefab happens to list them, so a piece can run "
                + "\"Bottom 1, Bottom 2, Top 3, Top 1\". Sorted, the piece's own points come first in "
                + "natural order, then derived anchors grouped centre, faces, corners, edges.");

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

            ScaleModifierKey = config.Bind("Scale", "ModifierKey",
                new KeyboardShortcut(KeyCode.LeftShift),
                "Hold this while pressing the scale keys below. Shared with the pitch modifier, which "
                + "is harmless: pitch responds to the scroll wheel and scaling to the numpad.");

            ScaleWiderKey = config.Bind("Scale", "WiderKey", new KeyboardShortcut(KeyCode.Keypad6),
                "Stretch the piece along its left-right axis.");

            ScaleNarrowerKey = config.Bind("Scale", "NarrowerKey", new KeyboardShortcut(KeyCode.Keypad4),
                "Compress the piece along its left-right axis.");

            ScaleTallerKey = config.Bind("Scale", "TallerKey", new KeyboardShortcut(KeyCode.Keypad8),
                "Stretch the piece vertically.");

            ScaleShorterKey = config.Bind("Scale", "ShorterKey", new KeyboardShortcut(KeyCode.Keypad2),
                "Compress the piece vertically.");

            ScaleDeeperKey = config.Bind("Scale", "DeeperKey", new KeyboardShortcut(KeyCode.Keypad9),
                "Stretch the piece along its front-back axis.");

            ScaleShallowerKey = config.Bind("Scale", "ShallowerKey", new KeyboardShortcut(KeyCode.Keypad7),
                "Compress the piece along its front-back axis.");

            ScaleUpKey = config.Bind("Scale", "UniformUpKey", new KeyboardShortcut(KeyCode.KeypadPlus),
                "Grow the piece on every axis at once.");

            ScaleDownKey = config.Bind("Scale", "UniformDownKey", new KeyboardShortcut(KeyCode.KeypadMinus),
                "Shrink the piece on every axis at once.");

            ScaleResetKey = config.Bind("Scale", "ResetKey", new KeyboardShortcut(KeyCode.Keypad5),
                "Return the piece to its normal size.");

            ScaleRestrictions = Synced(config.Bind("Scale", "Restrictions", ScaleRestriction.ProductionStations,
                new ConfigDescription(
                    "Which pieces are left out of resizing. "
                    + "ProductionStations: crafting stations, smelters, kilns, cooking stations, "
                    + "fermenters and beehives, whose behaviour is tied to where parts of the model "
                    + "are - build radii, ore and output points, the slots food sits on. Everything "
                    + "else, including chests, doors, portals, torches and station add-ons, can be "
                    + "resized. "
                    + "AnythingInteractive: also leaves out anything you can use at all. "
                    + "Nothing: no restriction.")));

            RangeGrowth = Synced(config.Bind("Scale", "RangeGrowth", 1f,
                new ConfigDescription(
                    "How much a resized piece's detection ranges grow. A portal only lights its effect "
                    + "when a player is within a fixed distance of it, and that distance has to grow "
                    + "with the piece or the effect stops working - but only by as much as the piece's "
                    + "surface moved outward, not by the whole scale factor. 1 matches the growth in "
                    + "size; lower keeps ranges tighter, 0 leaves them at vanilla values.",
                    new AcceptableValueRange<float>(0f, 3f))));

            ScaleMaxWithParticles = Synced(config.Bind("Scale", "MaximumWithParticles", 5f,
                new ConfigDescription(
                    "A separate ceiling for pieces that carry particle effects, such as portals and "
                    + "torches. At the default it matches Maximum and so does nothing; it is here as "
                    + "an escape hatch for a modded piece whose effects misbehave when resized, since "
                    + "a piece can gate its own effects on distances this mod knows nothing about.",
                    new AcceptableValueRange<float>(1f, 20f))));

            ScaleRepeatDelay = config.Bind("Scale", "RepeatDelay", 0.35f,
                new ConfigDescription(
                    "How long a scale key must be held before it starts repeating. Long enough that a "
                    + "single tap is never read as a hold.",
                    new AcceptableValueRange<float>(0.1f, 1f)));

            ScaleRepeatRate = config.Bind("Scale", "RepeatRate", 0.06f,
                new ConfigDescription("Seconds between steps while a scale key is held.",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            ScaleParticles = Synced(config.Bind("Scale", "ScaleParticles", true,
                "Draw a piece's particle effects larger along with its geometry. Only the particle "
                + "size changes; the space they move through is deliberately left alone, because "
                + "scaling that too throws them metres past the piece and the effect appears to "
                + "vanish when you stand near it."));

            ScaleStep = config.Bind("Scale", "Step", 0.05f,
                new ConfigDescription(
                    "Fraction changed per keypress. Applied multiplicatively, so growing and then "
                    + "shrinking returns to where you started.",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            ScaleMin = Synced(config.Bind("Scale", "Minimum", 0.2f,
                new ConfigDescription("Smallest multiple of a piece's normal size.",
                    new AcceptableValueRange<float>(0.05f, 1f))));

            ScaleMax = Synced(config.Bind("Scale", "Maximum", 5f,
                new ConfigDescription("Largest multiple of a piece's normal size.",
                    new AcceptableValueRange<float>(1f, 20f))));

            ResetScaleOnPieceChange = config.Bind("Scale", "ResetOnPieceChange", true,
                "Return to normal size when you select a different piece. Off keeps your scale across "
                + "pieces, which is useful when building a set to match.");

            EditPlacedPieces = Synced(config.Bind("Edit", "EditPlacedPieces", true,
                "Let a built piece be taken back into the placement ghost to be changed. The piece is "
                + "rebuilt rather than altered where it stands, because a built piece's position and "
                + "angle are read once when it spawns and never again - editing those in place would "
                + "look right to you and leave the piece where it was for everyone else. Synced, "
                + "because rebuilding a piece is a placement like any other and a server should be "
                + "able to say no to it."));

            EditKey = config.Bind("Edit", "EditKey",
                new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt),
                "Look at a built piece and press to take it into the ghost, with its rotation and "
                + "size already loaded. Change whatever you like using the usual controls, then place "
                + "to apply; the original comes down as the new one goes up, and the materials move "
                + "across rather than being charged twice. Press again to cancel and leave the piece "
                + "untouched. A chest, sign or item stand holding something is refused rather than "
                + "quietly emptied.");

            BendModifierKey = config.Bind("Bend", "BendModifierKey",
                new KeyboardShortcut(KeyCode.Keypad1),
                "Hold and turn the wheel to bend the piece in hand. A held modifier rather than a "
                + "mode you switch on, because the wheel already means four things depending on what "
                + "is held, and a fifth that persists invisibly is how you bend something you meant "
                + "to rotate.");

            BendAxisKey = config.Bind("Bend", "BendDirectionKey",
                new KeyboardShortcut(KeyCode.Keypad3),
                "Swap which way the piece curves. It always bends along its longest side - that is "
                + "measured, not chosen, because bending a pole across its thickness is not something "
                + "anyone wants - which leaves exactly two directions it can curve towards. For a wall "
                + "they are a round tower wall and an archway.");

            BendResetKey = config.Bind("Bend", "BendResetKey",
                new KeyboardShortcut(KeyCode.KeypadPeriod),
                "Straighten the piece in hand.");

            BendStep = config.Bind("Bend", "Step", 5f,
                "Degrees of bend per notch of the wheel.");

            BendMaximum = Synced(config.Bind("Bend", "Maximum", 180f,
                "How far a piece may be bent, in degrees from end to end, in either direction - the wheel runs both ways from straight, so a beam can arch or sag and a wall can wrap either way round a tower. 180 is a half circle: a "
                + "straight beam becomes an arch with its ends pointing straight up, rising about "
                + "0.318 of its own length. That is the practical ceiling - an arc's rise peaks at "
                + "0.362 of its length at around 267 degrees, by which point it has curled back "
                + "through itself and is a ring rather than a building piece."));

            BendSegment = config.Bind("Bend", "SegmentLength", 0.15f,
                "How finely a piece is cut up before it is curved, in metres along the bend. What "
                + "decides whether something looks curved is not how many vertices it has but how far "
                + "apart they are: a wall's uprights are short along the bend and ride the arc almost "
                + "rigidly, while its rails span the whole piece and have to curve across all of it. "
                + "Smaller is smoother and costs vertices; 0.15m is about fifteen segments on a two "
                + "metre piece.");

            ShowBendableMarker = config.Bind("Bend", "MarkBendablePieces", true,
                "Put a small arch on the build menu icon of every piece that can be bent. Whether a "
                + "piece bends depends on how it is built and none of that shows from the outside, so "
                + "without a mark the only way to find out is to select one and try. Marked on what "
                + "can bend rather than what cannot, because far fewer pieces can - marking the "
                + "exceptions would put an icon on nearly everything, which says nothing.");

            BendableMarkerColour = config.Bind("Bend", "MarkColour", new Color(0.55f, 0.85f, 1f, 0.85f),
                "Colour of that arch. Sits in the top right corner of the piece icon.");

            BendCurvesGhostSnapPoints = config.Bind("Bend", "CurveGhostSnapPoints", true,
                "Move the preview's snap points round the curve, so a bent piece snaps where it "
                + "looks like it should rather than where it would have if it were straight. This "
                + "was suspected of making the piece wander while being bent and was not the cause; "
                + "that was the collision being rebuilt on the preview, which no longer happens.");

            BendRebuildsCollision = Synced(config.Bind("Bend", "RebuildCollision", true,
                "Rebuild a bent piece's collision as a chain of boxes following the curve. Applies "
                + "to pieces you have built, not to the preview - a preview's colliders are measured "
                + "by things that hold onto them, and replacing those on every notch of the wheel is "
                + "what made the piece wander. Without this a built arch keeps the straight box it "
                + "started as, which you can walk through."));

            BendCollisionTolerance = config.Bind("Bend", "CollisionTolerance", 0.05f,
                "How far the rebuilt collision may stray from the true curve, in metres. The box "
                + "count follows from this rather than being fixed, so a gentle bend costs two boxes "
                + "where a semicircle costs four. Five centimetres is close enough not to be felt; "
                + "ends and middle alone would leave a nine centimetre gap you could catch a foot in.");

            BendRelaxesPlacement = Synced(config.Bind("Bend", "RelaxPlacementRules", true,
                "Set aside the rules about what a bent piece may rest on, while it is bent. Those "
                + "checks run against the preview's collider, and a preview deliberately keeps the "
                + "straight box it started as - rebuilding collision there is what made the piece "
                + "wander, since other things measure those colliders while it moves. So the game "
                + "judges a bent piece by a shape that is not the one on screen, and an arch whose "
                + "feet reach the ground is refused because the box they came from does not. It sets "
                + "aside the same rules surface placement does and no more: no-build zones, other "
                + "players' land and standing on somebody are all still refused."));

            BendSolidFill = config.Bind("Bend", "SolidFill", 0.8f,
                "How much of the space a piece's collision boxes span they must actually fill before "
                + "several of them count as one solid shape. The 4x2 stone wall is two stacked boxes "
                + "that together are simply the wall, and fill all of it; a step ladder is six small "
                + "boxes spread through a tall thin space that is mostly air. Rebuilding collision "
                + "for a slab cut in two is no harder than for a slab, so the first should bend and "
                + "the second should not. Lower this to let more loosely built pieces through.");

            BendMinimumSlices = config.Bind("Bend", "MinimumSlices", 3,
                "How many rings of vertices a mesh needs along the bend before it is curved rather "
                + "than hidden. A deformer can only move vertices that exist: a coarse stand-in mesh "
                + "has eight corners and nothing between them, so bending it lifts the corners onto "
                + "the curve and leaves flat faces spanning between - which is what draws straight "
                + "bars across a bent wall. Those meshes are hidden while the piece is bent and come "
                + "back the instant it is straightened. Raise this if something still looks faceted; "
                + "lower it to keep coarse meshes visible.");

            BendNever = Synced(config.Bind("Bend", "NeverBendable", "",
                "Prefab names that must never be bent, whatever they are made of, separated by commas. "
                + "The measurement is a good guess rather than a promise, and this is how to correct it "
                + "without waiting for a new build. Use the prefab name, such as wood_door, not the "
                + "name shown in game."));

            BendAlways = Synced(config.Bind("Bend", "AlwaysBendable", "",
                "Prefab names that may be bent even though the measurement says otherwise, separated by "
                + "commas. Be careful with this: a piece is normally refused because it is built from "
                + "several colliders, and forcing one through leaves its collision straight while its "
                + "shape curves away from it."));

            EditRemovesCollision = config.Bind("Edit", "EditRemovesCollision", true,
                "Stand the piece being edited down while you work on it, so its old self is not "
                + "solid in the space you are trying to move it into. Without this a small nudge or "
                + "a slight rescale is the one change you cannot make, because the placement check "
                + "sees the original and refuses. The cost is that structural support is worked out "
                + "from the colliders actually present, so for the length of the edit the piece "
                + "holds nothing up: editing a wall that a roof rests on can drop the roof if the "
                + "game recalculates support in that window. It is short and the collision comes "
                + "straight back, but turn this off if you would rather not risk it.");

            EditHidesPiece = config.Bind("Edit", "EditGhostMaterial", true,
                "Draw the piece being edited with a see-through material instead of its own. Its "
                + "shape is kept exactly - every plank and edge is still there to line up against - "
                + "while the material is one that actually blends. Valheim's piece shader cannot "
                + "fade: it treats alpha as a cutout, so recolouring the piece leaves it just as "
                + "solid. Turn this off to keep the piece's real materials and only tint it.");

            EditGhostBoxColor = config.Bind("Edit", "EditGhostColour", new Color(0.3f, 1f, 0.45f, 0.18f),
                "Colour the piece being edited is drawn in. Green by default so it cannot be confused "
                + "with the blue the hammer paints on whatever it is pointed at. Raise the alpha to "
                + "make it more solid, lower it to see further past it.");

            EditGhostTint = config.Bind("Edit", "EditGhostTint", new Color(0.55f, 0.7f, 1f, 0.5f),
                "How the piece being edited is drawn. The colour works; the alpha does not blend. "
                + "Valheim's piece shader treats alpha as a cutout rather than a fade, so above its "
                + "threshold the piece is fully solid and below it the piece vanishes outright - "
                + "0.25 made it disappear and 0.5 leaves it opaque, with nothing in between. There "
                + "is no partial transparency to be had here, so this tints rather than fades. "
                + "Applied through the game's own per-object material system, so no other piece in "
                + "the world changes.");

            EditGhostGlow = config.Bind("Edit", "EditGhostGlow", new Color(0.10f, 0.16f, 0.28f, 1f),
                "A faint light of its own for the piece being edited, which is what stops the "
                + "darkening reading as simply unlit and makes it read as set aside instead. Black "
                + "for none.");

            FreezeKey = config.Bind("Freeze", "FreezeKey",
                new KeyboardShortcut(KeyCode.Keypad0),
                "Pin the piece where it is so you can walk around it and look at it from "
                + "somewhere you could never have aimed from. The nudge keys below are how you "
                + "adjust it once aiming no longer moves it. Rotation still works while frozen.");

            ResetOffsetOnUnfreeze = config.Bind("Freeze", "ResetOffsetOnUnfreeze", true,
                "Clear the nudge when you unfreeze, so the next piece starts where you aim rather "
                + "than carrying the last piece's adjustment.");

            StationRangeKey = config.Bind("Station Range", "StationRangeKey",
                new KeyboardShortcut(KeyCode.LeftControl),
                "Hold and scroll to change the build range of the crafting station you are looking "
                + "at. Requires the hammer out, since that is when the scroll wheel belongs to "
                + "building. Left Alt would be the obvious choice and is taken here by roll.");

            StationRangeStep = Synced(config.Bind("Station Range", "Step", 1f,
                new ConfigDescription("Metres added or removed per scroll click.",
                    new AcceptableValueRange<float>(0.25f, 10f))));

            StationRangeMin = Synced(config.Bind("Station Range", "Minimum", 2f,
                new ConfigDescription("Smallest a station's build range may be set to.",
                    new AcceptableValueRange<float>(1f, 50f))));

            StationRangeMax = Synced(config.Bind("Station Range", "Maximum", 100f,
                new ConfigDescription("Largest a station's build range may be set to.",
                    new AcceptableValueRange<float>(10f, 500f))));

            RequireLookingAtStation = config.Bind("Station Range", "RequireLookingAtStation", true,
                "Only adjust the station under your crosshair. With this off it falls back to the "
                + "nearest station within AdjustDistance, which is handier when the bench is behind "
                + "a wall and ambiguous when several overlap.");

            StationAdjustDistance = config.Bind("Station Range", "AdjustDistance", 8f,
                new ConfigDescription(
                    "How far away a station may be to adjust it without looking at it. Ignored when "
                    + "RequireLookingAtStation is on.",
                    new AcceptableValueRange<float>(2f, 30f)));

            ShowStationArea = config.Bind("Station Range", "ShowStationArea", true,
                "Flash the station's area circle while changing its range, so you can see what you "
                + "are doing.");

            ExtendReachToStation = Synced(config.Bind("Station Range", "ExtendReachToStation", true,
                "Let you build anywhere the station reaches, rather than only as far as your arm. "
                + "Valheim limits building twice over - the station's circle says where you may "
                + "build, and a separate arm's-length limit says how far the aiming ray goes - and "
                + "the second has nothing to do with the first. This grants nothing that was not "
                + "already permitted; it saves you walking to it."));

            ReachLimit = Synced(config.Bind("Station Range", "ReachLimit", 50f,
                new ConfigDescription(
                    "Upper bound on the extended reach, whatever the station's range. Placing at "
                    + "great distance gets imprecise long before it gets useful.",
                    new AcceptableValueRange<float>(8f, 200f))));

            BuildCameraKey = config.Bind("Build Camera", "BuildCameraKey",
                new KeyboardShortcut(KeyCode.B),
                "Detach the camera from your character and fly it around what you are building. "
                + "Placement follows the camera, so you can put a piece where you could never "
                + "have stood to aim at it. Your character stays where it is.");

            CameraUpKey = config.Bind("Build Camera", "UpKey",
                new KeyboardShortcut(KeyCode.Space), "Fly the camera up.");

            CameraDownKey = config.Bind("Build Camera", "DownKey",
                new KeyboardShortcut(KeyCode.LeftControl), "Fly the camera down.");

            CameraBoostKey = config.Bind("Build Camera", "BoostKey",
                new KeyboardShortcut(KeyCode.LeftShift), "Hold to fly faster.");

            CameraSpeed = config.Bind("Build Camera", "Speed", 10f,
                new ConfigDescription("Metres per second.",
                    new AcceptableValueRange<float>(1f, 60f)));

            CameraBoost = config.Bind("Build Camera", "BoostMultiplier", 3f,
                new ConfigDescription("How much faster the boost key makes it.",
                    new AcceptableValueRange<float>(1f, 10f)));

            CameraRange = Synced(config.Bind("Build Camera", "Range", 40f,
                new ConfigDescription(
                    "How far the camera may get from your character, in metres. This is not an "
                    + "arbitrary limit: Valheim keeps objects alive around your body, and a "
                    + "camera beyond that either sees a half-built world or forces the game to "
                    + "load a second one around the camera. The second is what makes other "
                    + "build-camera mods expensive, and is deliberately not done here.",
                    new AcceptableValueRange<float>(5f, 120f))));

            CameraSensitivity = config.Bind("Build Camera", "Sensitivity", 2f,
                new ConfigDescription("Mouse sensitivity while flying.",
                    new AcceptableValueRange<float>(0.1f, 10f)));

            InvertCameraY = config.Bind("Build Camera", "InvertY", false,
                "Invert vertical mouse movement while flying.");

            CameraAboveGround = config.Bind("Build Camera", "KeepAboveGround", true,
                "Stop the camera sinking below the terrain. It still passes freely through "
                + "walls, roofs and anything else you have built - flying inside a building to "
                + "see what you are doing is most of the point - but under the ground there is "
                + "nothing to look at and no way to tell which way is up.");

            CameraGroundClearance = config.Bind("Build Camera", "GroundClearance", 0.5f,
                new ConfigDescription(
                    "How far above the terrain the camera is held, in metres. A little clearance "
                    + "stops the near plane clipping into the ground on a slope.",
                    new AcceptableValueRange<float>(0f, 5f)));

            CameraLight = config.Bind("Build Camera", "Light", true,
                "Carry a light with the camera, so you can see what you are building at night. "
                + "Casts no shadows - a shadowed light this size is one of the most expensive "
                + "things a scene can hold, and this is here to let you see.");

            CameraLightIntensity = config.Bind("Build Camera", "LightIntensity", 1.2f,
                new ConfigDescription("Brightness of the camera light.",
                    new AcceptableValueRange<float>(0f, 8f)));

            CameraLightRange = config.Bind("Build Camera", "LightRange", 20f,
                new ConfigDescription("How far the camera light reaches, in metres.",
                    new AcceptableValueRange<float>(2f, 80f)));

            CameraPickup = Synced(config.Bind("Build Camera", "Pickup", true,
                "Pick up loose items the camera passes over. Your carry weight is respected, "
                + "which vanilla pickup does not do - a sweep you did not ask for should not "
                + "leave you staggering."));

            CameraPickupRange = Synced(config.Bind("Build Camera", "PickupRange", 8f,
                new ConfigDescription("How far around the camera to collect from, in metres.",
                    new AcceptableValueRange<float>(1f, 40f))));

            CameraPickupInterval = config.Bind("Build Camera", "PickupInterval", 0.25f,
                new ConfigDescription(
                    "Seconds between sweeps. This is a cheap layer-masked query rather than a "
                    + "scan of the scene, but there is no reason to run it every frame either.",
                    new AcceptableValueRange<float>(0.05f, 2f)));

            MistClearRange = Synced(config.Bind("Mistlands", "MistClearRange", 0f,
                new ConfigDescription(
                    "How far a wisplight clears Mistlands fog around you, in metres. 0 leaves it "
                    + "exactly as Valheim has it, which is about 15.\n"
                    + "Given in metres rather than as a multiple on purpose: the mist is pushed "
                    + "aside by a force field, and a force field is a strong thing to make large. "
                    + "A multiplier hides how big the result actually is - eight times a base you "
                    + "cannot see is a hundred and twenty metres, which drags the whole sky about "
                    + "rather than clearing a space to build in. Twenty-five to thirty is plenty.",
                    new AcceptableValueRange<float>(0f, 60f))));

            OpenDoorsWhileBuilding = config.Bind("Doors", "OpenDoorsWhileBuilding", true,
                "Open and close doors with the usual use key while a build tool is in hand. "
                + "Vanilla switches interaction off entirely while building, which is right for "
                + "chests and stations - you would trigger those by accident lining up a piece - "
                + "and maddening for the door between you and more wood. Doors only.");

            DoorReach = config.Bind("Doors", "Reach", 5f,
                new ConfigDescription("How far you can be from a door to open it while building.",
                    new AcceptableValueRange<float>(1f, 20f)));

            QuietUpgradeGlow = config.Bind("Mistlands", "QuietUpgradeGlow", true,
                "Stop an upgraded item's glow from churning the Mistlands mist.\n"
                + "Valheim's upgrade sparkle carries a particle force field reaching five "
                + "metres, attached to your hand. The mist is a particle system, so the field "
                + "shoves it about wherever you walk - hold an upgraded axe and the fog boils "
                + "around you, hold a torch and it settles. The field is presumably meant to "
                + "shape the glow's own sparkles, so only the field is switched off and the "
                + "glow itself is left exactly as it was.");

            HideDemisterOrb = config.Bind("Mistlands", "HideDemisterOrb", true,
                "Hide the glowing wisp itself while keeping the mist it clears. The ball is the "
                + "part that does nothing - the mist is moved by a force field, not by the thing "
                + "you can see - and it is distracting in front of what you are building.");

            RequiresWisplight = Synced(config.Bind("Mistlands", "RequiresWisplight", true,
                "Keep Valheim's rule that clearing mist needs a wisplight. Turn this off and the "
                + "mist clears while a build tool is in hand whether you have one or not.\n"
                + "Detection is by status effect, not by item, so anything that grants mist "
                + "vision counts - the wisplight itself, a backpack with one built in, or "
                + "whatever a future mod adds - with no list of item names to keep up to date."));

            UndoKey = config.Bind("Undo", "UndoKey",
                new KeyboardShortcut(KeyCode.Z, KeyCode.LeftControl),
                "Take back the last thing you built - a whole run if you zooped, a single piece "
                + "if you did not. Each piece comes down through the same call the hammer makes, "
                + "so the materials come back exactly as they would if you removed it by hand.");

            UndoDepth = Synced(config.Bind("Undo", "Depth", 10,
                new ConfigDescription(
                    "How many placements back you can go. The limit is about what you can still "
                    + "remember doing rather than memory - a few thousand pieces would cost "
                    + "nothing to keep - so raise it if you want, knowing that undoing something "
                    + "from twenty minutes ago is more likely to surprise you than help.",
                    new AcceptableValueRange<int>(1, 50))));

            UndoRefundsToInventory = Synced(config.Bind("Undo", "RefundToInventory", true,
                "Hand undone materials straight to you, dropping only what will not fit, in one "
                + "pile at your feet. With this off, Valheim scatters them at each piece instead - "
                + "fine for one piece, and a long walk after undoing a run forty long. The amount "
                + "is the same either way."));

            ZoopModifierKey = config.Bind("Zoop", "ZoopModifierKey",
                new KeyboardShortcut(KeyCode.LeftShift),
                "Hold with a nudge direction key to lay a run of pieces that way. Press the same "
                + "direction again for one more and the opposite direction for one fewer. A "
                + "second direction turns the run into a grid and a third into a block, since "
                + "runs multiply rather than replace each other. The clear-offset key cancels it.");

            ZoopLimit = Synced(config.Bind("Zoop", "Limit", 60,
                new ConfigDescription(
                    "Most extra copies a single run may place. Each one is a real placement that "
                    + "pays its own materials, so this is about how much one keystroke should be "
                    + "able to commit you to. Small pieces eat it quickly - sixty one-metre floor "
                    + "tiles is a modest room - while a grid reaches it faster still, since eight "
                    + "by eight is already sixty-three copies. Large runs cost preview performance "
                    + "before they cost anything else.",
                    new AcceptableValueRange<int>(1, 500))));

            ZoopPerFrame = config.Bind("Zoop", "PiecesPerFrame", 8,
                new ConfigDescription(
                    "How many pieces of a run are built each frame. Placing a long run all at once "
                    + "stutters, because every copy is a real placement with its own object and "
                    + "effects; spreading it lets the run lay itself over a moment instead. Raise "
                    + "it if you would rather have the whole run immediately.",
                    new AcceptableValueRange<int>(1, 100)));

            ZoopSpacing = config.Bind("Zoop", "Spacing", 1f,
                new ConfigDescription(
                    "Multiplier on the gap between copies, where 1 is the piece's own width along "
                    + "the direction it is being laid in - so copies sit flush. 2 leaves a gap of "
                    + "one piece between each, which suits fence posts and pillars.",
                    new AcceptableValueRange<float>(0.25f, 5f)));

            GridKey = config.Bind("Grid", "GridKey",
                new KeyboardShortcut(KeyCode.G),
                "Restrict placement to a fixed world grid. Useful for spacing things that share no "
                + "snap points - torches along a wall, fence posts, chests in a row.");

            GridSize = config.Bind("Grid", "GridSize", 1f,
                new ConfigDescription(
                    "Grid step in metres. The grid is fixed to the world, not to where you started "
                    + "building, so it is the same grid everywhere and for everyone.",
                    new AcceptableValueRange<float>(0.05f, 8f)));

            GridHeight = config.Bind("Grid", "GridHeight", false,
                "Snap height to the grid as well as the ground plane. Off by default because "
                + "terrain is rarely level, and rounding height on a slope either buries a piece "
                + "or leaves it hanging.");

            NudgeStep = config.Bind("Placement Offset", "NudgeStep", 0.1f,
                new ConfigDescription("Metres moved per press of a nudge key.",
                    new AcceptableValueRange<float>(0.01f, 1f)));

            NudgeStepLarge = config.Bind("Placement Offset", "NudgeStepLarge", 1f,
                new ConfigDescription("Metres moved per press while the large modifier is held.",
                    new AcceptableValueRange<float>(0.05f, 8f)));

            NudgeFrame = config.Bind("Placement Offset", "NudgeFrame", NudgeFrameMode.World,
                new ConfigDescription(
                    "Which directions the nudge keys move along.\n"
                    + "World: along the world's own axes. Where you are looking picks which axis is "
                    + "meant, but the step runs along it exactly, so nudges made from anywhere land "
                    + "on the same lattice and pieces line up with each other.\n"
                    + "Camera: straight along your line of sight, at whatever angle you are standing "
                    + "at. Good for pushing a piece away from you, but turning between presses "
                    + "changes what the next one does."));

            NudgeLargeModifierKey = config.Bind("Placement Offset", "NudgeLargeModifierKey",
                new KeyboardShortcut(KeyCode.LeftControl),
                "Hold to move by NudgeStepLarge instead of NudgeStep. Left Control also does "
                + "something of Valheim's own while a ghost is up - the piece visibly changes - "
                + "which is harmless and overlaps only while you hold it.");

            NudgeForwardKey = config.Bind("Placement Offset", "NudgeForwardKey",
                new KeyboardShortcut(KeyCode.UpArrow), "Move the piece away from you.");

            NudgeBackwardKey = config.Bind("Placement Offset", "NudgeBackwardKey",
                new KeyboardShortcut(KeyCode.DownArrow), "Move the piece towards you.");

            NudgeLeftKey = config.Bind("Placement Offset", "NudgeLeftKey",
                new KeyboardShortcut(KeyCode.LeftArrow), "Move the piece left.");

            NudgeRightKey = config.Bind("Placement Offset", "NudgeRightKey",
                new KeyboardShortcut(KeyCode.RightArrow), "Move the piece right.");

            NudgeUpKey = config.Bind("Placement Offset", "NudgeUpKey",
                new KeyboardShortcut(KeyCode.Home), "Move the piece up.");

            NudgeDownKey = config.Bind("Placement Offset", "NudgeDownKey",
                new KeyboardShortcut(KeyCode.End), "Move the piece down.");

            ResetOffsetKey = config.Bind("Placement Offset", "ResetOffsetKey",
                new KeyboardShortcut(KeyCode.Delete),
                "Clear both the depth offset and the nudge, putting the piece back where you aim.");

            OffsetStep = config.Bind("Placement Offset", "Step", 0.05f,
                new ConfigDescription(
                    "How far each scroll notch pushes the piece along your aim when both rotation "
                    + "modifiers are held. Small values give the fine control needed to sink a piece "
                    + "into another by just the right amount.",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            OffsetLimit = Synced(config.Bind("Placement Offset", "Limit", 3f,
                new ConfigDescription(
                    "Maximum distance the piece can be pushed or pulled from where you are aiming.",
                    new AcceptableValueRange<float>(0.5f, 20f))));

            Freedom = Synced(config.Bind("Free Placement", "Freedom", PlacementFreedom.SurfacesAndSpacing,
                new ConfigDescription(
                    "Which vanilla placement rules free placement sets aside. "
                    + "Vanilla: none, free placement only affects snapping. "
                    + "Surfaces: what a piece may rest on - ground only, not on wood, tilting "
                    + "surfaces, cultivated soil. "
                    + "SurfacesAndSpacing: also the room a piece demands, such as forge extensions "
                    + "refusing to sit near each other. "
                    + "Everything: also biome, dungeon and weather restrictions. "
                    + "Wards, no-build zones and other players are never bypassed at any setting.")));

            Clipping = Synced(config.Bind("Clipping", "Mode", ClippingMode.WithFreePlacement,
                new ConfigDescription(
                    "Whether pieces may be placed intersecting other objects. Vanilla refuses when a "
                    + "piece would penetrate something by more than 0.2m, which makes tight arrangements "
                    + "and decorative overlaps impossible. Applies to every piece, including modded ones. "
                    + "Never: vanilla behaviour. "
                    + "WithFreePlacement: allowed only while free placement is on, since both express the "
                    + "same intent. "
                    + "Always: allowed at all times.")));

            ClippingToggleKey = config.Bind("Clipping", "ToggleKey", KeyboardShortcut.Empty,
                "Optional key to step through the clipping modes while building. Leave empty to disable.");

            ResetOnPieceChange = config.Bind("Rotation", "ResetOnPieceChange", false,
                "Zero all rotation when you select a different build piece. Off keeps your tilt "
                + "while you switch pieces, which is usually what you want mid-build.");

            DebugParticles = config.Bind("Debug", "DebugParticles", false,
                "Report what a scaled piece's particle effects are doing - visibility, particle count "
                + "and renderer bounds - so the cause of effects disappearing on large pieces can be "
                + "identified rather than guessed at. Noisy; switch on only while investigating.");

            DebugMistKey = config.Bind("Debug", "DebugMistKey",
                new KeyboardShortcut(KeyCode.F10),
                "Needs DebugLogging on. Press to list every demister and particle force field near you, with what each "
                + "one is set to. For working out what is acting on the Mistlands mist - hold "
                + "one item, press it, hold another, press it, and compare. Costs a full scene "
                + "search, so it runs only on the key press and never on a timer.");

            DebugPatchesKey = config.Bind("Debug", "DebugPatchesKey",
                new KeyboardShortcut(KeyCode.F11),
                "Needs DebugLogging on. Press to list every mod that has patched the methods involved in dying, in the "
                + "order their patches run. A stack trace cannot tell you this: Harmony compiles "
                + "all of a method's patches into one dynamic method, so an exception from any "
                + "of them shows the same single frame. Harmony does know, and this asks it.");

            DebugMeshKey = config.Bind("Debug", "DebugMeshKey",
                new KeyboardShortcut(KeyCode.F9),
                "Needs DebugLogging on. Look at a piece and press to write what it is made of to the log: every mesh "
                + "and whether it can be read at runtime, the collider types, the snap point "
                + "count and the shader. This exists to answer one question - whether a piece "
                + "could be bent into an arch - because a mesh imported with Read/Write disabled "
                + "cannot have its vertices touched at all, and that is worth measuring rather "
                + "than assuming either way.");

            DebugLogging = config.Bind("Debug", "DebugLogging", false,
                "Write diagnostics to the BepInEx log, and switch on the three diagnostic keys "
                + "below. They are off together on purpose: each holds a function key, and a "
                + "player who is not debugging should not lose one to a tool they will never "
                + "press. Turn this on and the keys work; turn it off and they are free again.");
        }
    }
}
