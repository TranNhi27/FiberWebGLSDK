# Quick Start

A complete integration, start to finish: initialize the node, fund it, connect to a peer,
open a channel, pay, and close. Each step builds on the last. Copy the whole thing into
one script to see the full flow, or lift individual steps into your own code.

This assumes you've already set up the scene (see the README's Quick Start section):
a `FiberPaymentGateway` component in your scene, with a `FiberNodeConfig` assigned.

---

## 1. Initialize

Starts the WASM node. Call this once, e.g. on game start.

```csharp
using FiberWebGLSDK;

[SerializeField] private FiberPaymentGateway gateway;
private IPaymentGateway Gateway => gateway;
private IInvoicePaymentGateway Invoices => gateway;
private IFiberWallet Wallet => gateway;
private IFiberDiagnostics Diagnostics => gateway;

private void Start()
{
    Gateway.Initialize(
        onReady: () => Debug.Log("Node ready."),
        onError: err => Debug.Log($"Init failed: {err.Code} - {err.Message}")
    );
}
```

This only works in a WebGL build, not the Editor (see the README). The node's key is
saved in the browser, so the same browser gets the same node back next time.

---

## 2. Fund the wallet

A new node starts with an empty wallet. Get its address, fund it from the
[faucet](https://faucet.nervos.org/), and check the balance before opening a channel.

```csharp
Gateway.GetNodeInfo(
    onResult: info => Debug.Log($"Fund this address: {info.CkbAddress}"),
    onError: err => Debug.Log($"GetNodeInfo failed: {err.Code}")
);

Wallet.GetBalance(
    onResult: balance => Debug.Log($"Balance: {balance.Shannons} shannons"),
    onError: err => Debug.Log($"Balance failed: {err.Code}") // a failed read, not a zero balance
);
```

`WalletBalance.IsAtLeast(amount)` is handy for a "fund your wallet" screen that polls
until there's enough for the channel plus a little for the on-chain fee.

---

## 3. Connect to a peer

Needs the peer's **multiaddr** (not their pubkey), something like
`/dns4/host/tcp/443/wss/p2p/Qm...`.

```csharp
Gateway.ConnectPeer(
    peerAddress: "/dns4/host/tcp/443/wss/p2p/Qm...",
    onConnected: () => Debug.Log("Connected."),
    onError: err => Debug.Log($"Connect failed: {err.Code}")
);
```

---

## 4. Find the peer's pubkey

`ConnectPeer` doesn't give you the peer's pubkey, and `OpenChannel` needs it, so look it
up with `ListPeers` right after connecting. If you run the peer yourself (a game hub),
you can put its pubkey in your config instead.

```csharp
Diagnostics.ListPeers(
    onResult: peers =>
    {
        // The peer you just connected to will be in here.
        var peer = peers[0];
        Debug.Log($"Peer pubkey: {peer.Pubkey}");
    },
    onError: err => Debug.Log($"ListPeers failed: {err.Code}")
);
```

---

## 5. Open a channel

Funds a channel with the connected peer. **This can take a couple of minutes**, because
the callback fires on on-chain confirmation, not when the request is accepted. There's no
client-side minimum check, so a too-small amount comes back as `ChannelOpenFailed` from
the peer. The amount is sent exactly as you pass it.

```csharp
ulong fundingAmountShannons = 10_000_000_000; // 100 CKB (1 CKB = 100,000,000 shannons)

Gateway.OpenChannel(
    peerPubkey: peerPubkey,      // from step 4
    fundingAmountShannons: fundingAmountShannons,
    isPublic: false,
    onChannelReady: channelId =>
    {
        Debug.Log($"Channel open: {channelId}");
        // keep channelId - PayPeer doesn't need it, but CloseChannel does
    },
    onError: err => Debug.Log($"Open channel failed: {err.Code}")
);
```

Returning player? Check `ListChannels(peerPubkey, false, ...)` first and reuse their
channel. If an open fails and every retry fails too, see
[Troubleshooting.md](Troubleshooting.md#retries-fail-after-a-failed-open).

---

## 6. Pay

Once the channel is ready, send a keysend payment. No invoice or channel id needed, just
the peer's pubkey.

```csharp
ulong amountShannons = 100_000_000; // 1 CKB

Gateway.PayPeer(
    peerPubkey: peerPubkey,
    amountShannons: amountShannons,
    onSuccess: result => Debug.Log($"Payment sent: {result.PaymentHash}, status: {result.Status}"),
    onError: err => Debug.Log($"Payment failed: {err.Code}")
);
```

---

## 7. (Optional) Pay an invoice

Use an invoice when the other side sets the price, like a shop item or an entry fee.
Decode it first and **check the amount matches what you showed the player** before
paying.

```csharp
Invoices.ParseInvoice(invoice,
    onResult: details =>
    {
        if (details.AmountShannons != shownPriceShannons) return; // don't pay a different price

        Invoices.PayInvoice(invoice,
            onSuccess: result => Debug.Log($"Paid: {result.PaymentHash}"), // grant the item here
            onError: err => Debug.Log($"Invoice payment failed: {err.Code}"));
    },
    onError: err => Debug.Log($"Invalid invoice: {err.Code}")
);
```

---

## 8. (Optional) Close the channel

Settles the channel's balance on-chain. Needs both the channel id (from step 5) and the
peer's pubkey. To show the player what they'll get back, read `LocalBalanceShannons`
from `ListChannels` first.

```csharp
Gateway.CloseChannel(
    channelId: channelId,       // from step 5
    peerPubkey: peerPubkey,
    force: false,
    onClosed: (ChannelCloseResult result) =>
        Debug.Log($"Channel closed. Tx: {result.ShutdownTransactionHash}"),
    onError: err => Debug.Log($"Close failed: {err.Code}")
);
```

The transaction hash can be empty even when the close succeeded. See
[API.md](API.md#closechannel).

---

## Where to go next

- Full method reference, including xUDT and dry-run routing: [API.md](API.md)
- Why the code is split into interfaces + one adapter, and how the sample scene wires
  its screens together: [Architecture.md](Architecture.md)
- Common errors and how to fix them: [Troubleshooting.md](Troubleshooting.md)