using HarmonyLib;

namespace TheHammerOfOden
{
    /// <summary>
    /// Stands the hammer's own highlight down for the piece being edited.
    /// </summary>
    /// <remarks>
    /// The piece being edited is, by definition, the piece the hammer is aimed at, so
    /// UpdateWearNTearHover calls Highlight on it every frame. Highlight writes _Color and
    /// _EmissionColor to its support colour and then schedules ResetHighlight to clear both a
    /// fifth of a second later. Whatever the edit set is therefore overwritten immediately and
    /// wiped shortly after, which is why the edited piece looked untouched: the colour was
    /// being applied and taken straight back off, many times a second.
    ///
    /// Two systems writing one property and each undoing the other is the same fight the door
    /// openers had, and it has the same answer - own both ends rather than tune around them.
    /// While an edit is under way this piece's highlight belongs to the edit; every other piece
    /// in the world highlights exactly as it always did.
    /// </remarks>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Highlight))]
    internal static class WearNTearHighlightPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(WearNTear __instance)
        {
            if (!ModConfig.IsEnabled || !PlacementEdit.IsBeingEdited(__instance.gameObject))
            {
                return true;
            }

            // A reset from just before the edit started would still be pending, and would clear
            // the edit colour a fifth of a second in.
            __instance.CancelInvoke("ResetHighlight");
            return false;
        }
    }
}
