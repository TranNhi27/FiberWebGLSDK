using UnityEngine;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// Shared display formatting for Fiber identifiers (pubkeys, addresses,
    /// channel ids) - long hex strings that need truncating to fit UI rows.
    /// </summary>
    public static class FiberUIFormat
    {
        /// Truncates the middle of a long string: "ckb1qzda0c...rqjga7w43hru0t4".
        /// TMP's Overflow.Ellipsis only truncates from the end, so this is done in code.
        public static string MiddleEllipsis(string value, int startChars = 10, int endChars = 8)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= startChars + endChars)
                return value;

            return $"{value.Substring(0, startChars)}...{value.Substring(value.Length - endChars)}";
        }
    }
}