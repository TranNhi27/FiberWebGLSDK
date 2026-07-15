# Troubleshooting

## Known limitations

- **No invoice support yet.** `PayPeer` only supports keysend (direct pubkey payment).
  `newInvoice` / `sendPaymentWithInvoice` are not yet exposed - `bridge.js` has no
  functions for them. This is the natural next extension: everything downstream
  (polling pattern, error codes, UI conventions) is already established by `PayPeer`.
- **Channels are opened private by default** (`isPublic: false`) in the sample - the
  channel is not announced on the gossip graph. `IPaymentGateway.OpenChannel` still
  exposes the `isPublic` parameter for SDKs that need public channels.
- **No client-side minimum-funding validation.** Testnet hubs may enforce their own
  minimum (e.g. 100 CKB) - this is hub policy, not a protocol rule, so it isn't
  hardcoded into the SDK. A too-small funding amount surfaces as a rejected
  `ChannelOpenFailed` from the RPC instead of failing fast in Unity.

---

## Errors you'll actually hit

### `CrossOriginIsolationRequired`

The host page is missing the COOP/COEP headers Fiber's WASM node needs for
`SharedArrayBuffer`. See [Hosting.md](Hosting.md). Check in the browser console:

```js
crossOriginIsolated // must be true
```

### `ChannelOpenFailed` after a long wait

`OpenChannel` polls for on-chain confirmation for up to 2 minutes before giving up.
Common causes: the peer isn't actually connected, the pubkey was typo'd, the funding
amount is below the hub's auto-accept minimum (see below), or it's genuinely still
pending on-chain and just needs a retry.

### Funding amount arrives ~100x smaller than sent

**Symptom:** a channel open fails or never confirms, and (if you have terminal access to
the peer's own node) its log shows something like:

```
WARN fnn::fiber::network: Received OpenChannel request from peer ... with CKB funding
amount 100000000 is less than required auto-accept minimum 10000000000.
```

...even though the amount sent should already clear the minimum.

**Root cause - confirmed, not just suspected.** We instrumented all three hops the
funding amount passes through - the C# `ulong` right after parsing, the hex string
`FiberPaymentGateway.OpenChannel` builds, and the value `bridge.js` hands to
`fiber.openChannel()` - and all three showed the correct value (`10000000000` shannons,
`0x2540be400`) at every step, tested live against the public `onyxia.fiber.channel`
testnet hub. **The loss happens inside `@nervosnetwork/fiber-js` / the WASM node
itself**, on the pinned `0.8.0` release - not anywhere in this SDK's C#, jslib, or JS
bridge code.

Circumstantial support: Fiber's own v0.9 dev log calls out "more reliable channel
funding" and "fiber-js and npm release improvements" as work done between `0.8.0` and
`0.9.0-rc`, which lines up with this bug's shape. We have **not** verified whether it's
fixed on `0.9.0-rc7` - that upgrade was judged too risky to test right before a
deadline (release candidate, fast-moving package, no time to smoke-test the rest of the
flow if something else broke).

**Applied fix** (in `FiberPaymentGateway.OpenChannel`): compensate by ×100 before
hex-encoding.

```csharp
ulong compensatedAmount = fundingAmountShannons * 100;
string amountHex = "0x" + compensatedAmount.ToString("x");
```

**To revisit:** the next time `@nervosnetwork/fiber-js` gets upgraded, re-run this test
before assuming the workaround is still needed:
1. Temporarily log the funding amount right before `fiber.openChannel()` in `bridge.js`.
2. Open a channel against a known peer (e.g. `onyxia.fiber.channel`) with a known
   amount, without the ×100 compensation.
3. Compare what was sent against what the channel actually opened with (or what the
   peer's rejection message reports, if it's below their minimum).
4. If it now arrives correct, remove the ×100 compensation and this note.

If you're the one picking this back up later and it's still off by 100x on a newer
version, that's a strong, well-evidenced bug report worth filing against
[nervosnetwork/fiber](https://github.com/nervosnetwork/fiber/issues) - lead with the
exact hex sent vs. decimal received, since that's more than most reports start with.
