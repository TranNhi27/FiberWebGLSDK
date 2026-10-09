# Troubleshooting

## Known limitations

- **The node's key is stored in the browser.** The node creates its key on first start,
  keeps it in `localStorage`, and signs for the player, which is why there's no wallet
  prompt. That's fine for testnet faucet CKB, but not for real funds: a production game
  should let players approve channel open and close in their own wallet. Clearing site
  data also deletes the key, and any funds held by it.
- **Channels are opened private by default** (`isPublic: false`) in the sample, so the
  channel isn't announced on the gossip graph. `OpenChannel` still has the `isPublic`
  parameter if you need public channels.
- **No client-side minimum-funding check.** Hubs can set their own minimum (e.g. 100
  CKB). That's hub policy, not a protocol rule, so the SDK doesn't hardcode it. A
  too-small amount comes back as `ChannelOpenFailed` instead of failing early in Unity.

---

## Common errors

### `CrossOriginIsolationRequired`

The host page is missing the COOP/COEP headers Fiber's WASM node needs for
`SharedArrayBuffer`. See [Hosting.md](Hosting.md). Check in the browser console:

```js
crossOriginIsolated // must be true
```

### `ChannelOpenFailed` after a long wait

`OpenChannel` waits for on-chain confirmation for a while before giving up. Common
causes: the peer isn't connected, the pubkey has a typo, the funding amount is below the
hub's minimum, or the open is still pending on-chain.

**Check before retrying.** A slow CKB RPC behind the peer can push an open past the
timeout even though it succeeds a minute later. Call `ListChannels` for that peer - if
the channel is there and ready, just use it.

### Retries fail after a failed open

A failed open can leave a half-created channel in the node, and every retry then fails.
Find it with `ListChannels` and remove it with `AbandonChannel(channelId, ...)`, then
open again.

### `PaymentFailed` on a UDT payment

UDT payments only route over channels funded with the same UDT. A CKB channel to the
same peer can't carry them. `DryRunPayment` tells you why there's no route.

### `CloseChannel` succeeds but the transaction hash is empty

This can happen. The hash is only visible while the channel is shutting down, and a fast
close can finish between two checks. The close itself still succeeded. Link a block
explorer on the player's CKB address instead.

---

## Funding amount arrived ~100x smaller (fixed)

On `@nervosnetwork/fiber-js` `0.8.0`, funding amounts reached the peer about 100x
smaller than sent. We logged the amount at every step on our side (the C# `ulong`, the
hex string the gateway builds, and the value `bridge.js` passes to `fiber.openChannel()`)
and all were correct, so the loss happened inside fiber-js. Version 1.0.0 worked around
it by multiplying the funding amount by 100.

That loss no longer happens with the current bridge build, so **the ×100 has been
removed**: `OpenChannel` sends exactly the amount you pass in. If you added a ×100
anywhere in your own code (e.g. a funding check), remove it.

If you upgrade fiber-js and want to double-check, open a channel with a known amount
against a peer whose log you can read, and compare what arrived. A too-small amount
appears in the peer's log like this:

```
WARN fnn::fiber::network: Received OpenChannel request from peer ... with CKB funding
amount 100000000 is less than required auto-accept minimum 10000000000.
```