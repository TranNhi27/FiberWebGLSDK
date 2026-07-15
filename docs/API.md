# API Reference

## Error handling

All failures deliver a `FiberError { FiberErrorCode Code, string Message }`. Switch on
`Code` — it's stable across SDK versions — and use `Message` for logging or as
generic fallback display text. The SDK does not decide player-facing wording.

```csharp
onError: err =>
{
    switch (err.Code)
    {
        case FiberErrorCode.CrossOriginIsolationRequired:
            // the host page is missing COOP/COEP headers
            break;
        case FiberErrorCode.ChannelOpenFailed:
            // rejected, or never confirmed within the timeout - retryable
            break;
        default:
            Debug.Log(err.Message);
            break;
    }
}
```

For the specific errors you're likely to hit and what causes them, see
[Troubleshooting.md](Troubleshooting.md).

---

## IPaymentGateway

Value-moving operations: connect, open, pay, close.

### Initialize
```csharp
void Initialize(Action onReady, Action<FiberError> onError);
```
Boots the node. Call once.

### ConnectPeer
```csharp
void ConnectPeer(string peerAddress, Action onConnected, Action<FiberError> onError);
```
`peerAddress` is a multiaddr, e.g. `/ip4/1.2.3.4/tcp/8228/ws/p2p/Qm...`. Does not
resolve the peer's pubkey - use `ListPeers` afterwards.

### OpenChannel
```csharp
void OpenChannel(string peerPubkey, ulong fundingAmountShannons, bool isPublic,
                  Action<string> onChannelReady, Action<FiberError> onError);
```
Needs the peer's **pubkey** (not their multiaddr's peer id - use `ListPeers` if you only
have the address). `onChannelReady` receives the confirmed `channelId` on success.

### CloseChannel
```csharp
void CloseChannel(string channelId, string peerPubkey, bool force,
                    Action onClosed, Action<FiberError> onError);
```
Needs both the channel id and the peer's pubkey (the pubkey is used to confirm
settlement via a `ListChannels` poll).

### PayPeer
```csharp
void PayPeer(string peerPubkey, ulong amountShannons,
              Action<PaymentResult> onSuccess, Action<FiberError> onError);
```
Routes by pubkey via keysend - no invoice or channel id needed.

### GetNodeInfo
```csharp
void GetNodeInfo(Action<NodeInfo> onResult, Action<FiberError> onError);
```
This node's own pubkey and CKB address.

---

Remember: every success callback here fires on **terminal on-chain confirmation**, not
request acceptance. See the README's "one rule that matters most" if you haven't yet.

---

## IFiberDiagnostics

Read-only peer/channel listings.

### ListPeers
```csharp
void ListPeers(Action<PeerInfo[]> onResult, Action<FiberError> onError);
```
Currently connected peers.

### ListChannels
```csharp
void ListChannels(string peerPubkey, bool includeClosed,
                    Action<ChannelInfo[]> onResult, Action<FiberError> onError);
```
Pass an empty string as `peerPubkey` to list channels across all peers.

---

## Data types

| Type | Fields |
|---|---|
| `PeerInfo` | `Pubkey`, `Address` |
| `ChannelInfo` | `ChannelId`, `ChannelOutpoint`, `Enabled`, `StateName`, `StateFlags`, `PeerPubkey` |
| `NodeInfo` | `Pubkey`, `CkbAddress` |
| `PaymentResult` | `PaymentHash`, `Status`, `RawJson` (forward-compatible - carries the full payload for fields not yet surfaced) |

---

## Amounts

All amounts (`fundingAmountShannons`, `amountShannons`) are in **shannons**
(1 CKB = 100,000,000 shannons), passed as `ulong` and hex-encoded internally before
reaching the native node. There's no client-side minimum-funding validation - see
[Troubleshooting.md](Troubleshooting.md) for what happens with a too-small amount, and
a known unit-scaling quirk worth reading before you hardcode a funding amount.
