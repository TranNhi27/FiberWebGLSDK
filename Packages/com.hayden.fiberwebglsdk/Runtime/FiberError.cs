// FiberError.cs
//
// Structured error for all Fiber SDK operations. Callers switch on Code
// (compiler-checked, stable across SDK versions) and fall back to Message
// for logging/debugging or as generic display text - the SDK does not
// decide player-facing wording, that's the consuming game's concern.
using System;
using UnityEngine;

namespace FiberWebGLSDK
{
    [Serializable]
    public struct FiberError
    {
        public FiberErrorCode Code;
        public string Message;

        public FiberError(FiberErrorCode code, string message)
        {
            Code = code;
            Message = message;
        }

        public override string ToString() => $"[{Code}] {Message}";

        // Parses the {code, message} JSON produced by bridge.js's classifyError().
        // Unity's JsonUtility can't deserialize enums by name (only by int), so
        // this goes through a string-based intermediate + Enum.TryParse instead
        // of decorating FiberError itself with [Serializable] JSON string fields.
        //
        // Enum.TryParse falling back to Unknown (rather than throwing) is
        // deliberate: bridge.js and this C# package will drift out of version
        // sync eventually (bridge.js adds a new code before a dev updates their
        // package reference) - an unrecognized code should degrade gracefully,
        // not crash the consuming game.
        public static FiberError Parse(string json)
        {
            try
            {
                var raw = JsonUtility.FromJson<FiberErrorJson>(json);
                if (!Enum.TryParse(raw.code, out FiberErrorCode code))
                    code = FiberErrorCode.Unknown;
                return new FiberError(code, raw.message);
            }
            catch
            {
                // json itself wasn't well-formed - keep the raw text so it's
                // still visible in a Debug.Log even though we can't classify it.
                return new FiberError(FiberErrorCode.ParseError, json);
            }
        }

        [Serializable] private struct FiberErrorJson { public string code; public string message; }
    }

    public enum FiberErrorCode
    {
        Unknown = 0,
        EditorNotSupported,            // running in Editor, not a real WebGL build (thrown C#-side, never crosses from JS)
        ConfigMissing,                 // FiberNodeConfig / TextAsset not assigned (thrown C#-side, pre-flight check)
        CrossOriginIsolationRequired,  // fiber-js: "requires a cross-origin isolated page"
        NotInitialized,                // fiber-js: "Fiber is not started"
        ConnectionFailed,              // peer connection / wrong address scheme (/tcp/ vs /ws/)
        ChannelOpenFailed,             // OpenChannel RPC rejected, or our own poll timed out
        PaymentFailed,                 // payment reached terminal Failed status, or our own poll timed out
        RpcError,                      // any other rejected fiber-js promise we haven't specifically classified
        ParseError                     // JSON from bridge.js didn't match the expected shape
    }
}