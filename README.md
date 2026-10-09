# Fiber WebGL SDK

A Unity package that runs a [Fiber](https://github.com/nervosnetwork/fiber) payment node
inside a WebGL build, so a game can connect to peers, open payment channels, and send
CKB payments straight from the browser, with no server relay.

Includes a sample scene (`PaymentFlowDemo`) that walks through the full flow: connect to
a peer, open a channel, send a payment, close a channel, and browse connected peers and
open channels in a diagnostics panel.

Used in production by **FiberSurvivors**, a survivors-like where players earn CKB for
clearing levels, buy upgrade cards with invoices mid-run, and cash out by closing their
channel.

---

## Features

- Runs a Fiber node in the browser, with no server relay
- Small interfaces to depend on, separate from the WebGL implementation:
  - `IPaymentGateway`: connect, open, pay, close
  - `IInvoicePaymentGateway`: decode and pay invoices
  - `IUdtPaymentGateway`: xUDT channels and payments, route dry-runs, clearing stuck channels
  - `IFiberWallet`: on-chain CKB balance, before any channel exists
  - `IFiberDiagnostics`: connected peers and open channels, with balances
- Structured errors (`FiberError`) instead of raw strings
- Sample scene covering connect → open → pay → close

---

## Try the demo

The hosted demo ([genuine-tartufo-aa408b.netlify.app](https://genuine-tartufo-aa408b.netlify.app/))
starts a Fiber node in your browser, but it needs a **peer** to connect to. You have two
options:

**Option A - use Fiber's public testnet bootnode (easiest, nothing to run):**
paste one of these into the **CONNECT PEER** field. Both are run by the Fiber team, so if
one is down, try the other:

```
/dns4/onyxia.fiber.channel/tcp/443/wss/p2p/QmdyQWjPtbK4NWWsvy8s69NGJaQULwgeQDT5ZpNDrTNaeV
/dns4/thrall.fiber.channel/tcp/443/wss/p2p/Qmes1EBD4yNo9Ywkfe6eRw9tG1nVNGLDmMud1xJMsoYFKy
```

Both are reachable over `wss` from the HTTPS-hosted page, with no setup on your end.

**Option B - connect to your own native node:** if you run your own `fnn` node, it
needs to be reachable over `wss` (a plain `ws` address like `127.0.0.1` or a LAN IP won't
connect from an HTTPS page). See [docs/Hosting.md](docs/Hosting.md) for exposing a local
node over `wss`.

1. Open the demo.
2. Wait for the node to start. The **STATUS** field on the home screen reads
   `NODE READY` when it's done (it creates a fresh identity in your browser's local
   storage, which takes a few seconds).
3. Fund the node's address from the [faucet](https://faucet.nervos.org/).
4. Click **CONNECT PEER**, paste an address from Option A or B, and click **Connect**.
5. Once connected, go to **OPEN CHANNEL**, either from the Open Channel screen or from
   Diagnostics. Check [docs/Troubleshooting.md](docs/Troubleshooting.md) if it's rejected.
6. Once the channel is open, you're taken to **PAY**. Click **Pay** to send a keysend
   payment over Fiber. A payment hash appears on success.
7. Click **DIAGNOSTICS** from the home screen at any time to see your peers and channels,
   and to close a channel.

If a step fails, the status text explains why and you can retry the step. For errors you
don't recognize, see [docs/Troubleshooting.md](docs/Troubleshooting.md).

---

## Requirements

Fiber's WASM node needs `SharedArrayBuffer`, so the page must be
**cross-origin isolated**. Any page hosting the build (your own static server, Netlify,
and so on) must send:

```
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: require-corp
```

Without these headers, initialization fails immediately with a
`CrossOriginIsolationRequired` error. To confirm isolation is on, open the browser
console on the hosted page and check that `crossOriginIsolated` is `true`.

A hosted page also means the browser only dials **secure** WebSocket peers (`wss`, not
`ws`). Plain `ws` addresses (a peer on `127.0.0.1` or a bare LAN IP) won't connect from
an HTTPS build, even though they work from `localhost` during local testing. See
[docs/Hosting.md](docs/Hosting.md) for the full setup.

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
and add the `FiberPaymentGateway` component to it. Assign a `FiberNodeConfig` asset to
its **Config** field (see `Samples/PaymentFlowDemo/Config/` for a testnet example).
Without it, `Initialize` fails immediately with `ConfigMissing`. Keep the GameObject's
name unique in the scene, since JavaScript calls back into it by name.

**2. Reference it from your own script.** Depend on the interfaces, not on
`FiberPaymentGateway` directly, so your code stays testable and independent of the WebGL
implementation. Pick the narrowest one you need: a funding screen that only holds
`IFiberWallet` can't move money. Drag the `FiberGateway` GameObject into the `gateway`
field in the Inspector:

```csharp
using FiberWebGLSDK;
using UnityEngine;

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

The SDK only talks to a Fiber node in a WebGL build. Pressing Play in the Editor calls
`onError` with `EditorNotSupported` instead, by design (there's no jslib in Editor mode),
so test the full flow in a WebGL build.

### The two rules that matter most

**1. Callbacks fire on confirmation, not on request.** `OpenChannel`'s `onChannelReady`
doesn't fire when the open request is accepted. It fires once the channel is confirmed
on-chain and usable, which on testnet can take a couple of minutes. The same goes for
`CloseChannel`, `PayPeer` and `PayInvoice`. Code that assumes otherwise will think an
operation succeeded before it has.

**2. One call at a time per operation.** Each operation keeps a single pair of
callbacks. Calling it again before the first call finishes replaces them, and the first
caller never hears back. `PayPeer`, `PayPeerUdt` and `PayInvoice` share one pair.

This only covers `Initialize`. For the complete flow (fund, connect, open a channel,
pay, pay an invoice, close), see [docs/QuickStart.md](docs/QuickStart.md). Method-by-method reference:
[docs/API.md](docs/API.md)

---

## Documentation

| Doc | What's in it |
|---|---|
| [docs/QuickStart.md](docs/QuickStart.md) | Example: initialize → fund → connect → open → pay → invoice → close |
| [docs/API.md](docs/API.md) | Every interface's methods, data types, error handling |
| [docs/Architecture.md](docs/Architecture.md) | Package structure, the sample scene's mediator pattern, the JS bridge, call-chain diagram |
| [docs/Hosting.md](docs/Hosting.md) | Serving your own build with the required headers |
| [docs/Troubleshooting.md](docs/Troubleshooting.md) | Known limitations and common errors |
| [CHANGELOG.md](CHANGELOG.md) | What changed since 1.0.0 |

---

## License

See [LICENSE](LICENSE).