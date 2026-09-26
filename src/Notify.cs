using System.Runtime.CompilerServices;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Shows the mod's feedback on screen - in its own message panel by default, or the game's
    /// centre message if the player prefers.
    /// </summary>
    /// <remarks>
    /// The panel is where it belongs: the centre message cuts off anything longer than a line
    /// and fades in about two seconds. See MessagePanel.
    ///
    /// The centre message is still offered because it is where vanilla puts its own snapping
    /// message, so some will want build feedback in the same place. It replaces whatever is
    /// showing, which is why it was chosen over TopLeft: that one enqueues and drains at about a
    /// message a second, so a value nudged twenty times played out for twenty seconds.
    ///
    /// Repeats of the identical text are dropped. Holding a key that reports an already clamped
    /// value would otherwise restart the timer on every press.
    /// </remarks>
    internal static class Notify
    {
        private static string _last;
        private static float _lastAt;

        internal static void Show(
            Player player,
            string text,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
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

            if (ModConfig.MessageStyle.Value == MessageStyle.GameCentre)
            {
                ((Character)player).Message(MessageHud.MessageType.Center, text);
                return;
            }

            string shown = Localization.instance != null ? Localization.instance.Localize(text) : text;
            MessagePanel.Add(file + ":" + line, shown);
            HammerOfOdenPlugin.Debug("Message: " + shown);
        }
    }
}
