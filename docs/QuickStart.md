# Quick Start

A complete integration, start to finish: initialize the node, connect to a peer, open a
channel, send a payment. Each step builds on the last - copy the whole thing into one
script to see the full flow, or lift individual steps into your own code.

This assumes you've already completed scene setup (see the README's Quick Start section):
a `FiberPaymentGateway` component in your scene, with a `FiberNodeConfig` assigned.

---

## 1. Initialize

Boots the WASM node. Call this once, e.g. on game start.

```csharp
[SerializeField] private FiberPaymentGateway gateway;
private IPaymentGateway Gateway => gateway;
private IFiberDiagnostics Diagnostics => gateway;

private void Start()
{
    Gateway.Initialize(
        onReady: () => Debug.Log("Node ready."),
        onError: err => Debug.Log($"Init failed: {err.Code} - {err.Message}")
    );
}
```

Remember: this only works in a real WebGL build, not the Editor - see the README if
you haven't already.

---

## 2. Connect to a peer

Needs the peer's **multiaddr** (not their pubkey) - something like
`/ip4/1.2.3.4/tcp/8228/ws/p2p/Qm...`.

```csharp
Gateway.ConnectPeer(
    peerAddress: "/ip4/1.2.3.4/tcp/8228/ws/p2p/Qm...",
    onConnected: () => Debug.Log("Connected."),
    onError: err => Debug.Log($"Connect failed: {err.Code}")
);
```

---

## 3. Find the peer's pubkey

`ConnectPeer` doesn't hand you the peer's pubkey - `OpenChannel` needs it, so look it
up via `ListPeers` right after connecting.

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

## 4. Open a channel

Funds a channel with the connected peer. **This can take a couple of minutes** - the
callback fires on on-chain confirmation, not on request acceptance (see the README's
"one rule that matters most" if that's new to you). There's no client-side minimum
check - a too-small amount surfaces as `ChannelOpenFailed` from the hub instead. See
[Troubleshooting.md](Troubleshooting.md) if you hit that.

```csharp
ulong fundingAmountShannons = 10_000_000_000; // 100 CKB (1 CKB = 100,000,000 shannons)

Gateway.OpenChannel(
    peerPubkey: peerPubkey,      // from step 3
    fundingAmountShannons: fundingAmountShannons,
    isPublic: false,
    onChannelReady: channelId =>
    {
        Debug.Log($"Channel open: {channelId}");
        // hang onto channelId - PayPeer doesn't need it, but CloseChannel does
    },
    onError: err => Debug.Log($"Open channel failed: {err.Code}")
);
```

---

## 5. Pay

Once the channel is confirmed ready, send a keysend payment - no invoice or channel id
needed, just the peer's pubkey.

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

## 6. (Optional) Close the channel

Settles the channel's balance on-chain. Needs both the channel id (from step 4) and the
peer's pubkey.

```csharp
Gateway.CloseChannel(
    channelId: channelId,       // from step 4
    peerPubkey: peerPubkey,
    force: false,
    onClosed: () => Debug.Log("Channel closed."),
    onError: err => Debug.Log($"Close failed: {err.Code}")
);
```

---

## Where to go next

- Full method reference, including data types: [API.md](API.md)
- Why the code is split into interfaces + one adapter, and how the sample scene wires
  its screens together: [Architecture.md](Architecture.md)
- Errors you'll actually hit, and the known funding-amount quirk: [Troubleshooting.md](Troubleshooting.md)
