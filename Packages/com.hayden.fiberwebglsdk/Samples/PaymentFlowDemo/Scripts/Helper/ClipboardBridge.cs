using System.Runtime.InteropServices;
using UnityEngine;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// Copies text to the clipboard, using whichever mechanism the current
    /// platform actually supports. Callers (e.g. CopyToClipboardButton)
    /// don't need to know or care which one that is.
    ///
    /// WebGL builds have no OS clipboard access - GUIUtility.systemCopyBuffer
    /// silently no-ops there. The real copy has to go through the browser's
    /// navigator.clipboard.writeText(), via ClipboardBridge.jslib.
    /// </summary>
    public static class ClipboardBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CopyTextToClipboard(string text);
#endif

        public static void Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            CopyTextToClipboard(text);
#else
            // Editor and standalone builds: the OS clipboard API works fine here.
            GUIUtility.systemCopyBuffer = text;
#endif
        }
    }
}
