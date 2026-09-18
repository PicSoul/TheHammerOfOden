using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Shows short build-mode feedback on screen.
    /// </summary>
    /// <remarks>
    /// Everything goes through MessageType.Center, which replaces whatever is showing and
    /// crossfades it. TopLeft looks like the obvious choice for a small status line, but it
    /// enqueues and MessageHud drains that queue at about one message a second - so a value
    /// nudged twenty times leaves twenty seconds of backlog playing out, long after you have
    /// put the hammer away.
    ///
    /// Center is also where vanilla puts its own snapping message, so build feedback all
    /// arrives in the same place.
    ///
    /// Repeats of the identical text are dropped. Holding a key that reports an already
    /// clamped value would otherwise restart the fade on every press and leave the text
    /// sitting there unchanging.
    /// </remarks>
    internal static class Notify
    {
        private static string _last;
        private static float _lastAt;

        internal static void Show(Player player, string text)
        {
            if (player == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            if (text == _last && Time.time - _lastAt < 0.4f)
            {
                return;
            }

            _last = text;
            _lastAt = Time.time;

            ((Character)player).Message(MessageHud.MessageType.Center, text);
        }
    }
}
