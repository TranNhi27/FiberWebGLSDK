// ClipboardBridge.jslib
//
// Logic-free shim, same convention as FiberBridge.jslib: converts the
// incoming pointer to a string and hands it to the browser's Clipboard
// API. No fallback/error-handling logic here - that belongs in C#/JS
// glue that can actually report back to the user, not in this shim.
mergeInto(LibraryManager.library, {

    CopyTextToClipboard: function (textPtr) {
        var text = UTF8ToString(textPtr);

        // navigator.clipboard.writeText requires a secure context (https)
        // and returns a Promise. We don't await it here - Unity's jslib
        // functions are fire-and-forget by default. If you need a
        // success/failure callback back into C#, that's a follow-up
        // (SendMessage from the .then()/.catch()), not needed for a
        // simple "copy on click" button.
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).catch(function (err) {
                console.error("Clipboard write failed:", err);
            });
        } else {
            console.warn("navigator.clipboard.writeText unavailable - page may not be a secure context (https).");
        }
    }

});
