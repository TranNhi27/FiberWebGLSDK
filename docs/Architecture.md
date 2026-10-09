# Architecture

## Package structure

```
FiberWebGLSDK/
├── fiber-bridge/
│   ├── package.json
│   ├── src/
│   │   └── bridge.js            - wraps @nervosnetwork/fiber-js, exposes window.FiberBridge
│   └── dist/
│       └── fiber-bridge.bundle.js   - built output (committed - see "The JS bridge" below)
└── Packages/com.hayden.fiberwebglsdk/
    ├── Runtime/
    │   ├── IPaymentGateway.cs         - connect, open, pay, close
    │   ├── IInvoicePaymentGateway.cs  - decode and pay invoices
    │   ├── IUdtPaymentGateway.cs      - xUDT, dry-run routing, abandon stuck channels
    │   ├── IFiberWallet.cs            - on-chain CKB balance
    │   ├── IFiberDiagnostics.cs       - read-only peer/channel listings
    │   ├── FiberPaymentGateway.cs     - concrete adapter; the only class touching the jslib
    │   ├── FiberError.cs              - structured error type + error codes
    │   ├── FiberNodeConfig.cs         - ScriptableObject wrapping the node's config.yml
    │   └── Plugins/
    │       └── FiberBridge.jslib      - WebGL native plugin, bridges to bridge.js
    └── Samples/
        └── PaymentFlowDemo/
            ├── Scripts/             - the sample UI (see below)
            ├── Config/              - demo testnet config (only present if the sample is imported)
            └── WebGLTemplate~/      - reference WebGL template; see Hosting.md
                └── fiber-bridge.bundle.js   - copy of fiber-bridge/dist/, loaded by index.html
```

`FiberPaymentGateway` is the **only** class that talks to the jslib layer. Everything
else - your game code and the sample UI alike - depends on the interfaces instead, so
the WebGL implementation can change (or be swapped for a mock in tests) without
touching calling code.

---

## Why the interfaces are split

One class implements all five, but callers only see the one they need:

- **Reads vs writes.** `IFiberWallet` and `IFiberDiagnostics` can't move money. A
  funding screen that only holds `IFiberWallet` can never open a channel or pay by
  mistake, and the compiler enforces that.
- **Fiber vs CKB chain.** Fiber's RPC has no balance call, so the wallet balance is read
  from the CKB chain. It fails in different ways, so it gets its own interface.
- **New features don't touch old code.** Invoices and UDTs were added as new interfaces,
  so code written against `IPaymentGateway` in 1.0.0 still compiles unchanged.

---

## The sample scene

`PaymentFlowDemo` has 5 screens, coordinated by `PaymentFlowManager` (a mediator - the
screens raise events and expose `SetX()` methods, but never reference each other
directly):

- **ConnectPeerUI** → resolves a peer's pubkey once connected
- **OpenChannelUI** → funds a channel, returns the confirmed channel id
- **PayPeerUI** → sends a keysend payment
- **CloseChannelUI** → settles a channel on-chain
- **FiberDiagnosticsUI** → two tabs (connected peers, open channels), each row clickable
  to jump to the matching screen above via a shared **RowActionPopup**

Each screen can be understood on its own, and the manager is the only thing that knows
how they connect. To extend the sample (e.g. an invoice screen), add a screen and wire it
into the manager. Existing screens don't need to change.

---

## The JS bridge

`bridge.js` (in `fiber-bridge/src/`) wraps `@nervosnetwork/fiber-js` and exposes a
plain `window.FiberBridge` object. `FiberBridge.jslib` is the WebGL native plugin that
calls into it via `DllImport("__Internal")`. It only passes strings and callbacks back
and forth, with no Fiber logic, so it doesn't need to change when the Fiber calls do.

`bridge.js` is also where the "fire on confirmation" rule lives: open, close and pay
poll the node until the operation finishes, and only then call back.

If you modify `bridge.js`, rebuild the bundle and copy it into the WebGL template.
Editing the source alone does nothing, since Unity only loads the built bundle:

```
cd fiber-bridge
npx esbuild src/bridge.js --bundle --format=iife --outfile=dist/fiber-bridge.bundle.js
cp dist/fiber-bridge.bundle.js ../Packages/com.hayden.fiberwebglsdk/Samples/PaymentFlowDemo/WebGLTemplate~/fiber-bridge.bundle.js
```

Both `dist/fiber-bridge.bundle.js` and the copy inside `WebGLTemplate~/` are committed,
so cloning the repo and opening the Unity project works right away. You only need
Node.js if you're editing `bridge.js`.

### Adding a new operation

Each operation passes through four places, and the names have to match exactly. Most
mismatches fail silently:

| Where | If it doesn't match |
|---|---|
| `bridge.js` function (then rebuild + copy the bundle) | The call hangs |
| `Fiber_X` export in `FiberBridge.jslib` | Link error at build time |
| `On*` callback name in `FiberPaymentGateway` | Unity logs a warning, callback never fires |
| JSON field names in the gateway's `[Serializable]` classes | The field stays at its default value |

### Call chain, end to end

```
                    +-----------------------+
                    |      Your Code         |
                    |  (MonoBehaviour, etc.) |
                    +-----------------------+
                                |
                                v
                    +-----------------------+
                    |   SDK interfaces       |   <- the only thing you depend on
                    | (IPaymentGateway, ...) |
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
                    |   FiberBridge.jslib    |   <- passes strings/callbacks,
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

Responses come back up the same path through Unity's `SendMessage`, landing on private
`On*` methods on `FiberPaymentGateway` (e.g. `OnChannelReady`, `OnChannelError`) that
call the `Action`/`Action<T>` you passed in.

**Why it's layered this way:** at each arrow you can swap one side without touching the
other. Mock the interfaces for unit tests without a node. Rebuild `bridge.js` without
touching C#. Change the jslib without touching Fiber logic. `FiberPaymentGateway` is the
single line between "Unity code" and "everything WebGL/JS".