# Fiber WebGL SDK

A Unity package that runs a real [Fiber](https://github.com/nervosnetwork/fiber) payment
node inside a WebGL build, so a game can connect to peers, open payment channels, and
send CKB payments — directly from the browser, no server relay required.

Includes a sample scene (`PaymentFlowDemo`) demonstrating the full flow: connect to a
peer, open a channel, send a payment, close a channel, and browse connected peers/open
channels via a diagnostics panel.

---

## Features

- Runs a real Fiber node directly in the browser - no server relay required
- Two clean interfaces (`IPaymentGateway`, `IFiberDiagnostics`) to depend on, decoupled
  from the WebGL-specific implementation
- Open and fund payment channels with connected peers
- Send keysend payments - no invoice needed
- Read-only diagnostics API for listing connected peers and open channels
- Complete sample scene showing the full connect → open → pay → close flow

---

## Try the demo

The hosted demo ([genuine-tartufo-aa408b.netlify.app](https://genuine-tartufo-aa408b.netlify.app/))
boots a real Fiber node inside your browser - but it needs a **peer** to connect to.
You have two options:

**Option A - use Fiber's public testnet bootnode (easiest, nothing to run):**
paste one of these into the **CONNECT PEER** field - both are run by the Fiber team,
not us, so if one happens to be down, just try the other:

```
/dns4/onyxia.fiber.channel/tcp/443/wss/p2p/QmdyQWjPtbK4NWWsvy8s69NGJaQULwgeQDT5ZpNDrTNaeV
/dns4/thrall.fiber.channel/tcp/443/wss/p2p/Qmes1EBD4yNo9Ywkfe6eRw9tG1nVNGLDmMud1xJMsoYFKy
```

Both are reachable over `wss` directly from this HTTPS-hosted page - no setup needed
on your end.

**Option B - connect to your own native node:** if you're running your own `fnn` node
and want to test against it instead, it needs to be reachable over `wss` (a plain `ws`
address like `127.0.0.1` or a LAN IP won't connect from this HTTPS-hosted page). See
[docs/Hosting.md](docs/Hosting.md) for exposing a local node over `wss`.

1. Open the demo.
2. Wait for the node to boot — the **STATUS** field on the home screen will read
   `NODE READY` once ready (this generates a fresh identity in your browser's local
   storage; it takes a few seconds).
3. Click **CONNECT PEER**, paste in the address from Option A or B above, and click
   **Connect**.
4. Once connected, you're routed to **OPEN CHANNEL** with the peer's pubkey pre-filled.
   Click **Open Channel** (this locks a small amount of testnet CKB into the channel —
   confirmation can take a couple of minutes). This step currently has a known
   unit-scaling issue with the funding amount - see
   [docs/Troubleshooting.md](docs/Troubleshooting.md) if it's rejected.
5. Once the channel is open, you're routed to **PAY**. Click **Pay** to send a real
   keysend payment over Fiber. A payment hash appears on success.
6. Click **DIAGNOSTICS** from the home screen at any time to see your connected peers
   and open channels, and to close a channel.

If a step fails, the status text explains why and the step is retryable. Seeing an
error you don't recognize? Check [docs/Troubleshooting.md](docs/Troubleshooting.md).

---

## Requirements

Fiber's WASM node needs `SharedArrayBuffer`, which requires the page to be
**cross-origin isolated**. Any page hosting this build — the Unity dev server, Netlify,
your own static server — must send:

```
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: require-corp
```

Without these headers, initialization fails immediately with a `CrossOriginIsolationRequired`
error. You can confirm isolation is active by opening the browser console on the hosted
page and checking that `crossOriginIsolated` evaluates to `true`.

A hosted page also means the browser will only dial **secure** WebSocket peers (`wss`,
not `ws`) — plain `ws` addresses (e.g. a peer on `127.0.0.1` or a bare LAN IP) will not
connect from an HTTPS-hosted build, even though they work fine from `localhost` during
local testing. See [docs/Hosting.md](docs/Hosting.md) for the full setup.

---

## Installation

1. Clone this repository (or add it as a git submodule).
2. In Unity: **Window → Package Manager → + → Add package from disk**, and select
   `Packages/com.hayden.fiberwebglsdk/package.json`.
3. To use the sample scene, expand the package in Package Manager and import
   **Samples → PaymentFlowDemo**.

---

## Quick start

**1. Add the gateway to your scene.** Create an empty GameObject (e.g. `FiberGateway`)
and add the `FiberPaymentGateway` component to it. Assign a `FiberNodeConfig` asset in
its **Config** field (see `Samples/PaymentFlowDemo/Config/` for a working testnet
example) - without it, `Initialize` fails immediately with `ConfigMissing`.

**2. Reference it from your own script.** Everything goes through two interfaces.
Depend on these, not on `FiberPaymentGateway` directly, so your code stays testable and
decoupled from the WebGL-specific implementation. Drag the `FiberGateway` GameObject
into the `gateway` field in the Inspector:

```csharp
public class MyGameCode : MonoBehaviour
{
    [SerializeField] private FiberPaymentGateway gateway;

    private IPaymentGateway Gateway => gateway;
    private IFiberDiagnostics Diagnostics => gateway;

    private void Start()
    {
        Gateway.Initialize(
            onReady: () => Debug.Log("Node ready."),
            onError: err => Debug.Log($"Init failed: {err}")
        );
    }
}
```

This only talks to a real Fiber node in an actual WebGL build - pressing Play in the
Editor calls `onError` with `EditorNotSupported` instead, by design (there's no jslib
in Editor mode). Test the full flow via a WebGL build, not Play mode.

### The one rule that matters most

**Every callback in this SDK fires on terminal confirmation, not on request acceptance.**
`OpenChannel`'s `onChannelReady` doesn't fire when the open request is accepted — it
fires once the channel is confirmed on-chain and usable, which on testnet can take a
couple of minutes. The same is true for `CloseChannel` and `PayPeer`. Code that assumes
otherwise will believe an operation succeeded before it actually has.

This is just `Initialize` - for the complete flow (connect, open a channel, pay, close),
see [docs/QuickStart.md](docs/QuickStart.md). Full method-by-method reference:
[docs/API.md](docs/API.md)

---

## Documentation

| Doc | What's in it |
|---|---|
| [docs/QuickStart.md](docs/QuickStart.md) | Complete end-to-end example: initialize → connect → open → pay → close |
| [docs/API.md](docs/API.md) | `IPaymentGateway` / `IFiberDiagnostics` method signatures, error handling |
| [docs/Architecture.md](docs/Architecture.md) | Package structure, the sample scene's mediator pattern, the JS bridge, call-chain diagram |
| [docs/Hosting.md](docs/Hosting.md) | Serving your own build with the required headers |
| [docs/Troubleshooting.md](docs/Troubleshooting.md) | Known limitations and error codes you'll actually hit |

---

## License

See [LICENSE](LICENSE).
