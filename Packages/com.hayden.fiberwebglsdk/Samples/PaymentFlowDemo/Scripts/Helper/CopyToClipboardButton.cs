using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// Attach to a Button. Copies text to the clipboard on click, via
    /// ClipboardBridge so it works correctly in WebGL builds as well as
    /// the Editor/standalone.
    ///
    /// Prefers fullTextOverride (set from code via SetFullText) over sourceText,
    /// so the FULL value is copied even when the on-screen text is truncated
    /// for display (e.g. "ckb1qzda0c...rqjga7w43hru0t4").
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CopyToClipboardButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text sourceText;

        // Populated by SetFullText() at runtime, not typed in the Inspector.
        // Serialized purely so its current value is visible while debugging.
        [SerializeField] private string fullTextOverride;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(CopyToClipboard);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(CopyToClipboard);
        }

        /// Sets the full, untruncated value to copy. Call this whenever the
        /// displayed text is a shortened version of the real value.
        public void SetFullText(string fullText)
        {
            fullTextOverride = fullText;
        }

        private void CopyToClipboard()
        {
            string textToCopy = !string.IsNullOrEmpty(fullTextOverride)
                ? fullTextOverride
                : sourceText != null ? sourceText.text : string.Empty;

            ClipboardBridge.Copy(textToCopy);
        }
    }
}