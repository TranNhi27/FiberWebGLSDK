# Architecture

## Package structure

```
Fiber-WebGL-SDK/
├── fiber-bridge/
│   ├── package.json
│   ├── src/
│   │   └── bridge.js            - wraps @nervosnetwork/fiber-js, exposes window.FiberBridge
│   └── dist/
│       └── fiber-bridge.bundle.js   - built output (committed - see "The JS bridge" below)
└── Packages/com.hayden.fiberwebglsdk/
    ├── Runtime/
    │   ├── IPaymentGateway.cs       - value-moving operations (connect, open, pay, close)
    │   ├── IFiberDiagnostics.cs     - read-only peer/channel listings
    │   ├── FiberPaymentGateway.cs   - concrete adapter; the only class touching the jslib
    │   ├── FiberError.cs            - structured error type + error codes
    │   ├── FiberNodeConfig.cs       - ScriptableObject wrapping the node's config.yml
    │   └── Plugins/
    │       └── FiberBridge.jslib    - WebGL native plugin, bridges to bridge.js
    └── Samples/
        └── PaymentFlowDemo/
            ├── Scripts/             - the sample UI (see below)
            ├── Config/               - demo testnet config (only present if the sample is imported)
            └── WebGLTemplate~/       - reference WebGL template; see Hosting.md
                └── fiber-bridge.bundle.js   - copy of fiber-bridge/dist/, loaded by index.html
```

`FiberPaymentGateway` is deliberately the **only** class that talks to the jslib layer.
Everything else - your game code and the sample UI alike - depends on `IPaymentGateway`
/ `IFiberDiagnostics` instead, so the WebGL-specific implementation can change (or be
swapped for a mock in tests) without touching calling code.

---

## The sample scene

`PaymentFlowDemo` implements a 5-screen flow, coordinated by `PaymentFlowManager`
(a mediator - the screens raise events and expose `SetX()` methods, but never reference
each other directly):

- **ConnectPeerUI** → resolves a peer's pubkey once connected
- **OpenChannelUI** → funds a channel, returns the confirmed channel id
- **PayPeerUI** → sends a keysend payment
- **CloseChannelUI** → settles a channel on-chain
- **FiberDiagnosticsUI** → two tabs (connected peers, open channels), each row clickable
  to route into the relevant screen above via a shared **RowActionPopup**

This structure is intentional: every screen is independently understandable, and the
manager is the only thing that knows how they connect. Extending the sample (e.g. adding
invoice support) means adding a screen and wiring it into the manager - existing screens
don't need to change.

---

## The JS bridge

`bridge.js` (in `fiber-bridge/src/`) wraps `@nervosnetwork/fiber-js` and exposes a
plain `window.FiberBridge` object. `FiberBridge.jslib` is the WebGL native plugin that
calls into it via `DllImport("__Internal")` - it's kept deliberately "dumb" (pure
string/callback marshaling, no Fiber logic), so it never needs to change when the
actual Fiber calls do.

If you modify `bridge.js`, you must rebuild the bundle and copy it into the WebGL
template before changes take effect in Unity - editing the source alone does nothing,
since Unity only ever loads the built bundle:

```
cd fiber-bridge
npx esbuild src/bridge.js --bundle --format=iife --outfile=dist/fiber-bridge.bundle.js
cp dist/fiber-bridge.bundle.js ../Packages/com.hayden.fiberwebglsdk/Samples/PaymentFlowDemo/WebGLTemplate~/fiber-bridge.bundle.js
```

Both `dist/fiber-bridge.bundle.js` and the copy inside `WebGLTemplate~/` are committed
to the repo, so cloning it and opening the Unity project works immediately - nobody
needs Node.js installed unless they're actually editing `bridge.js`.

### Call chain, end to end

```
                    +-----------------------+
                    |      Your Code         |
                    |  (MonoBehaviour, etc.) |
                    +-----------------------+
                                |
                                v
                    +-----------------------+
                    |    IPaymentGateway /   |
                    |    IFiberDiagnostics   |   <- the only thing you depend on
                    +-----------------------+
                                |
                                v
                    +-----------------------+
                    |  FiberPaymentGateway   |   <- concrete adapter (MonoBehaviour)
                    |  the ONLY class that   |
                    |  touches the jslib     |
                    +-----------------------+
                                |
                                v  [DllImport("__Internal")]
                    +-----------------------+
                    |   FiberBridge.jslib    |   <- pure string/callback marshaling,
                    |   (WebGL native plugin)|      no Fiber logic lives here
                    +-----------------------+
                                |
                                v
                    +-----------------------+
                    |      bridge.js         |   <- wraps @nervosnetwork/fiber-js,
                    |  window.FiberBridge    |      exposes a plain JS API
                    +-----------------------+
                                |
                                v
                    +-----------------------+
                    |  @nervosnetwork/       |
                    |  fiber-js (WASM node)  |
                    +-----------------------+
                                |
                                v
                         Fiber Network
                      (peers, channels,
                       on-chain settlement)
```

Responses flow back up the same path in reverse via Unity's `SendMessage`, landing on
private `On*` methods on `FiberPaymentGateway` (e.g. `OnChannelReady`,
`OnChannelError`) that resolve the `Action`/`Action<T>` callback you originally passed
in.

**Why it's layered this way:** each arrow is a place you could swap an implementation
without touching what's above it. Mock `IPaymentGateway` for unit tests without a real
node. Rebuild `bridge.js` without touching C#. Change the jslib's marshaling without
touching Fiber-specific logic. `FiberPaymentGateway` is the single seam between "Unity
code" and "everything WebGL/JS-specific" - that boundary is deliberate, not incidental.
