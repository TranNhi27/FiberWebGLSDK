# API Reference

All types are in the `FiberWebGLSDK` namespace. `FiberPaymentGateway` implements every
interface below. Depend on the one your code needs.

## Error handling

All failures deliver a `FiberError { FiberErrorCode Code, string Message }`. Switch on
`Code`, which stays stable across SDK versions, and use `Message` for logging or as
fallback display text. The SDK doesn't decide player-facing wording.

```csharp
onError: err =>
{
    switch (err.Code)
    {
        case FiberErrorCode.CrossOriginIsolationRequired:
            // the host page is missing COOP/COEP headers
            break;
        case FiberErrorCode.ChannelOpenFailed:
            // rejected, or not confirmed within the timeout - retryable
            break;
        default:
            Debug.Log(err.Message);
            break;
    }
}
```

| Code | Meaning |
|---|---|
| `Unknown` | Unclassified, or a code newer than your SDK copy |
| `EditorNotSupported` | Called outside a WebGL build |
| `ConfigMissing` | Missing config, or a required argument was empty |
| `CrossOriginIsolationRequired` | Host page is missing COOP/COEP headers |
| `NotInitialized` | Called before `Initialize` finished |
| `ConnectionFailed` | Couldn't connect to the peer |
| `ChannelOpenFailed` | Open rejected, or not ready within the timeout |
| `PaymentFailed` | Payment didn't reach `Success` |
| `RpcError` | Any other node RPC error |
| `ParseError` | A response from the JS side couldn't be parsed |

For the errors you're most likely to hit and their causes, see
[Troubleshooting.md](Troubleshooting.md).

**One call at a time per operation.** Calling a method again before its first call
finishes replaces the pending callbacks. `PayPeer`, `PayPeerUdt` and `PayInvoice` share
one set.

---

## IPaymentGateway

Value-moving operations: connect, open, pay, close.

### Initialize
```csharp
void Initialize(Action onReady, Action<FiberError> onError);
```
Starts the node. Call once. The node's key is created on first start and kept in the
browser's `localStorage`, so the same browser gets the same node back.

### ConnectPeer
```csharp
void ConnectPeer(string peerAddress, Action onConnected, Action<FiberError> onError);
```
`peerAddress` is a multiaddr, e.g. `/dns4/host/tcp/443/wss/p2p/Qm...`. Doesn't return
the peer's pubkey - use `ListPeers` afterwards.

### OpenChannel
```csharp
void OpenChannel(string peerPubkey, ulong fundingAmountShannons, bool isPublic,
                  Action<string> onChannelReady, Action<FiberError> onError);
```
Needs the peer's **pubkey** (not the peer id in their multiaddr - use `ListPeers` if you
only have the address). `onChannelReady` receives the confirmed `channelId`.

### CloseChannel
```csharp
void CloseChannel(string channelId, string peerPubkey, bool force,
                    Action onClosed, Action<FiberError> onError);

void CloseChannel(string channelId, string peerPubkey, bool force,
                    Action<ChannelCloseResult> onClosed, Action<FiberError> onError);
```
Needs both the channel id and the peer's pubkey (the pubkey is used to confirm
settlement via a `ListChannels` poll). The second overload also returns the settlement
transaction hash. That hash **can be empty** even when the close succeeded: it's only
visible while the channel is shutting down, and a fast close can finish between two
polls.

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
This node's own pubkey and CKB address. The address is what you fund from the faucet.

---

## IInvoicePaymentGateway

Pays invoices created by someone else, usually your game backend.

### ParseInvoice
```csharp
void ParseInvoice(string invoiceAddress,
                    Action<InvoiceDetails> onResult, Action<FiberError> onError);
```
Decodes an invoice locally, without paying it. **Call this before `PayInvoice` and check
the amount** against what you showed the player. The invoice reaches the client over
HTTP, so without this check a wrong or compromised server could charge any amount.

### PayInvoice
```csharp
void PayInvoice(string invoiceAddress,
                  Action<PaymentResult> onSuccess, Action<FiberError> onError);
```
`invoiceAddress` is the encoded invoice, e.g. `fibt1...` on testnet. The amount comes
from the invoice. Fails with `PaymentFailed` if there's no route, not enough balance, or
the invoice expired or was already paid.

---

## IUdtPaymentGateway

xUDT (token) versions of the channel and payment calls, plus two tools that work for
CKB as well.

A UDT is identified by its type script as a JSON string:
`{"code_hash":"0x...","hash_type":"type","args":"0x..."}`. Amounts are in the token's
smallest unit, not shannons.

### OpenUdtChannel
```csharp
void OpenUdtChannel(string peerPubkey, ulong fundingAmount, string udtTypeScriptJson,
                      bool isPublic, Action<string> onChannelReady, Action<FiberError> onError);
```
Same as `OpenChannel`, funded with a UDT. The peer must accept that UDT, and the wallet
must hold it - the CKB faucet won't fund it.

### PayPeerUdt
```csharp
void PayPeerUdt(string peerPubkey, ulong amount, string udtTypeScriptJson,
                  Action<PaymentResult> onSuccess, Action<FiberError> onError);
```
Only routes over channels funded with the **same** UDT. A CKB channel to the same peer
can't carry it.

### GetUdtBalance
```csharp
void GetUdtBalance(string udtTypeScriptJson,
                     Action<UdtBalance> onResult, Action<FiberError> onError);
```
On-chain balance of one UDT in this node's wallet.

### DryRunPayment
```csharp
void DryRunPayment(string peerPubkey, ulong amount, string udtTypeScriptJson,
                     Action<RouteQuote> onResult, Action<FiberError> onError);
```
Checks whether a payment can be routed, and at what fee, without sending it. Pass an
empty `udtTypeScriptJson` for CKB. "Not routable" comes back through `onResult` with
`Routable = false` and a `Reason`, not through `onError`.

### AbandonChannel
```csharp
void AbandonChannel(string channelId, Action onAbandoned, Action<FiberError> onError);
```
Removes a channel stuck partway through opening. Use it when a failed open makes every
retry fail too. For channels that are open and ready, use `CloseChannel`.

---

## IFiberWallet

The node's on-chain CKB wallet. This reads the CKB chain, not the Fiber node, and works
right after `Initialize`, before any channel exists.

### GetBalance
```csharp
void GetBalance(Action<WalletBalance> onResult, Action<FiberError> onError);
```
A failed read returns `ParseError`, never a zero balance.

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
| `ChannelInfo` | `ChannelId`, `ChannelOutpoint`, `Enabled`, `StateName`, `StateFlags`, `PeerPubkey`, `LocalBalanceShannons`, `RemoteBalanceShannons`, `ShutdownTransactionHash`, `FundingUdtTypeScript` (empty for CKB) |
| `ChannelCloseResult` | `ChannelId`, `ShutdownTransactionHash` (can be empty) |
| `NodeInfo` | `Pubkey`, `CkbAddress` |
| `PaymentResult` | `PaymentHash`, `Status`, `RawJson` (the full payload, for fields not mapped yet) |
| `InvoiceDetails` | `AmountShannons`, `Currency` (e.g. `Fibt` on testnet), `PaymentHash`, `UdtTypeScript` (empty for CKB) |
| `WalletBalance` | `Shannons`, `IsAtLeast(ulong)`, `ShannonsPerCkb` |
| `UdtBalance` | Token amount as a `ulong` |
| `RouteQuote` | `Routable`, `FeeShannons`, `Reason` |

---

## Amounts

All amounts are `ulong`: **shannons** for CKB (1 CKB = 100,000,000 shannons), or the
token's smallest unit for a UDT. They're hex-encoded internally before reaching the
node, and amounts coming back from the node are parsed from hex or decimal for you.
There's no client-side minimum-funding check - see
[Troubleshooting.md](Troubleshooting.md).