namespace TheHammerOfOden
{
    internal enum ClippingMode
    {
        /// <summary>Vanilla: a piece that would penetrate something by more than 0.2m cannot be placed.</summary>
        Never = 0,

        /// <summary>Allowed only while free placement is active.</summary>
        WithFreePlacement = 1,

        /// <summary>Always allowed.</summary>
        Always = 2
    }

    /// <summary>
    /// Decides whether pieces may be placed intersecting other objects.
    /// </summary>
    /// <remarks>
    /// Tied to free placement by default, because the two express the same intent: free
    /// placement is what you reach for when you want a piece exactly where you put it
    /// rather than where the game would prefer, and being blocked for touching a neighbour
    /// is the other half of that same fight. Pairing them means one decision instead of
    /// two, and clipping is off again the moment you stop asking for it.
    /// </remarks>
    internal static class Clipping
    {
        internal static bool IsAllowed()
        {
            if (!ModConfig.IsEnabled)
            {
                return false;
            }

            switch (ModConfig.Clipping.Value)
            {
                case ClippingMode.Always:
                    return true;
                case ClippingMode.WithFreePlacement:
                    return FreePlacement.IsActiveNow();
                default:
                    return false;
            }
        }

        /// <summary>Step the mode on, for the optional toggle key.</summary>
        internal static ClippingMode Cycle()
        {
            ClippingMode next;

            switch (ModConfig.Clipping.Value)
            {
                case ClippingMode.Never:
                    next = ClippingMode.WithFreePlacement;
                    break;
                case ClippingMode.WithFreePlacement:
                    next = ClippingMode.Always;
                    break;
                default:
                    next = ClippingMode.Never;
                    break;
            }

            ModConfig.Clipping.Value = next;
            return next;
        }

        internal static string Describe(ClippingMode mode)
        {
            switch (mode)
            {
                case ClippingMode.Always:
                    return "always";
                case ClippingMode.WithFreePlacement:
                    return "with free placement";
                default:
                    return "never";
            }
        }
    }
}
