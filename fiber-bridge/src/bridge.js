// fiber-bridge/src/bridge.js
//
// Thin JS wrapper around @nervosnetwork/fiber-js, exposing a plain
// window.FiberBridge object that FiberBridge.jslib calls into.
//
// Build with:
//   npx esbuild src/bridge.js --bundle --format=iife --outfile=dist/fiber-bridge.bundle.js
//
// Then copy dist/fiber-bridge.bundle.js into your Unity WebGL Template
// folder, next to index.html.
import { Fiber, randomSecretKey } from "@nervosnetwork/fiber-js";
import { secp256k1 } from "@noble/curves/secp256k1.js";
import { bytesFrom, ccc, hashCkb, hexFrom } from "@ckb-ccc/core";

let fiber = null;

// CKB testnet's standard SECP256K1_BLAKE160 lock script. Change for mainnet.
const TESTNET_SECP256K1_BLAKE160_CODE_HASH =
    "0x9bd7e06f3ecf4be0f2fcd2188b23f1b9fcc88e5d4b65a8637b17723bbda3cce8";

// localStorage keys for identity persistence. Written only after
// fiber.start() succeeds, so a failed start never overwrites a working identity.
const LS_FIBER_KEY_PAIR = "FIBER_KEY_PAIR";
const LS_CKB_SECRET_KEY = "CKB_SECRET_KEY";
const LS_DATABASE_PREFIX = "DATABASE_PREFIX";

// ---------------------------------------------------------------------
// Error classification
//
// Every catch block in this file funnels through here instead of doing
// onError(String(e.message || e)) directly. This turns a raw JS error
// message into a small { code, message } JSON payload that matches the
// C# FiberErrorCode enum, so Unity code can switch on `code` (compiler-
// checked, stable) while still keeping `message` around for logging.
//
// `code` values here are limited to what fiber-js's docs actually confirm
// as common error strings. Anything we haven't specifically confirmed
// falls back to "RpcError" rather than guessing - same discipline as the
// ChannelReady state-name lesson from openChannel.
// ---------------------------------------------------------------------
function classifyError(e) {
    const msg = String(e?.message || e);
    let code = "RpcError";

    if (msg.includes("cross-origin isolated")) {
        code = "CrossOriginIsolationRequired";
    } else if (msg.includes("not started")) {
        code = "NotInitialized";
    } else if (msg.includes("/tcp/") || msg.toLowerCase().includes("connect")) {
        code = "ConnectionFailed";
    }

    return JSON.stringify({ code: code, message: msg });
}

function hexToBytes(hex) {
    const clean = hex.startsWith("0x") ? hex.slice(2) : hex;
    const bytes = new Uint8Array(clean.length / 2);
    for (let i = 0; i < bytes.length; i++) {
        bytes[i] = parseInt(clean.substr(i * 2, 2), 16);
    }
    return bytes;
}

function bytesToHex(bytes) {
    return "0x" + Array.from(bytes).map(b => b.toString(16).padStart(2, "0")).join("");
}

// Derives a CKB wallet address from a raw CKB secret key. Separate identity
// from the Fiber network pubkey - one key controls on-chain CKB funds, the
// other identifies this node on the Fiber P2P network.
function deriveCkbAddress(ckbSecretKeyBytes) {
    const publicKey = hexFrom(secp256k1.getPublicKey(ckbSecretKeyBytes, true));
    const signerScript = ccc.Script.from({
        codeHash: TESTNET_SECP256K1_BLAKE160_CODE_HASH,
        hashType: "type",
        args: bytesFrom(hashCkb(publicKey)).slice(0, 20)
    });
    return ccc.Address.from({ prefix: "ckt", script: signerScript }).toString();
}

window.FiberBridge = {
    // Boots the WASM Fiber node. Call once, e.g. when the Unity build starts.
    // Reuses a saved identity from localStorage if one exists, otherwise
    // generates a fresh one.
    initialize: async function(configText, onReady, onError) {
        try {
            const savedFiberKeyPair = window.localStorage.getItem(LS_FIBER_KEY_PAIR);
            const savedCkbSecretKey = window.localStorage.getItem(LS_CKB_SECRET_KEY);
            const savedDatabasePrefix = window.localStorage.getItem(LS_DATABASE_PREFIX);

            const fiberKeyPairHex = savedFiberKeyPair || bytesToHex(randomSecretKey());
            const ckbSecretKeyHex = savedCkbSecretKey || bytesToHex(randomSecretKey());
            const databasePrefix = savedDatabasePrefix || ("player_" + Date.now());

            const fiberKeyPairBytes = hexToBytes(fiberKeyPairHex);
            const ckbSecretKeyBytes = hexToBytes(ckbSecretKeyHex);

            fiber = new Fiber();
            await fiber.start(
                configText,
                fiberKeyPairBytes,
                ckbSecretKeyBytes,
                undefined,
                "info",
                databasePrefix
            );

            window.localStorage.setItem(LS_FIBER_KEY_PAIR, fiberKeyPairHex);
            window.localStorage.setItem(LS_CKB_SECRET_KEY, ckbSecretKeyHex);
            window.localStorage.setItem(LS_DATABASE_PREFIX, databasePrefix);

            window.FiberBridge._ckbAddress = deriveCkbAddress(ckbSecretKeyBytes);
            onReady();
        } catch (e) {
            onError(classifyError(e));
        }
    },

    // Connect to a peer before opening a channel or paying it. Only the
    // multiaddr is needed - not a separate pubkey.
    connectPeer: async function(peerAddress, onConnected, onError) {
        try {
            await fiber.connectPeer({ address: peerAddress, save: true });
            onConnected();
        } catch (e) {
            onError(classifyError(e));
        }
    },

    // Opens a channel with an already-connected peer.
    // Field is `pubkey`, not `peer_id` - renamed in v0.8.0.
    //
    // IMPORTANT: fiber.openChannel() resolving only means the open REQUEST
    // was accepted - the channel still needs on-chain funding confirmation
    // before it can carry a payment. Calling onReady() immediately here was
    // firing too early (UI showed "ready" while the channel was still
    // pending). Instead, poll listChannels until the channel's state
    // actually reaches a ready/usable state before reporting success.
    openChannel: async function(peerPubkey, fundingAmountHex, isPublic, onReady, onError) {
        try {
            await fiber.openChannel({
                pubkey: peerPubkey,
                funding_amount: fundingAmountHex,
                public: isPublic
            });
        } catch (e) {
            onError(classifyError(e));
            return;
        }

        const POLL_INTERVAL_MS = 2000;
        const TIMEOUT_MS = 120000; // testnet confirmation can take a couple minutes
        const startTime = Date.now();

        const poll = async () => {
            try {
                const result = await fiber.listChannels({ pubkey: peerPubkey, include_closed: false });
                const channels = result.channels || [];
                // Confirmed via live testing + Diagnostics panel: the real
                // ready state is "ChannelReady" (PascalCase, no underscore) -
                // not "CHANNEL_READY" as originally assumed.
                const readyChannel = channels.find(c => c.state && c.state.state_name === "ChannelReady");
                if (readyChannel) {
                    onReady(readyChannel.channel_id);
                    return;
                }
            } catch (e) {
                // transient listChannels error - keep polling rather than failing outright
            }

            if (Date.now() - startTime > TIMEOUT_MS) {
                // We know the real code here - it's our own timeout, not a
                // caught fiber-js exception - so tag it directly instead of
                // routing through classifyError()'s generic string-matching.
                onError(JSON.stringify({
                    code: "ChannelOpenFailed",
                    message: "Channel did not confirm within timeout - it may still be pending on-chain funding or manual hub acceptance."
                }));
                return;
            }
            setTimeout(poll, POLL_INTERVAL_MS);
        };
        poll();
    },

    // Closes a channel and settles its balance on-chain.
    // close_script and fee_rate are intentionally omitted - confirmed via
    // `fnn-cli channel shutdown_channel --help` that both default sensibly
    // server-side (close_script defaults to the node's own funding lock
    // script) when not provided.
    //
    // Same lesson as openChannel: shutdownChannel resolving only means the
    // close request was accepted, not that on-chain settlement finished.
    // Poll listChannels (open-only) until the channel_id no longer appears -
    // deliberately state-name-agnostic, since we don't have a confirmed
    // terminal "closed" state string and guessing one has burned us twice already.
    closeChannel: async function(channelId, peerPubkey, force, onClosed, onError) {
        try {
            await fiber.shutdownChannel({
                channel_id: channelId,
                force: force
            });
        } catch (e) {
            onError(classifyError(e));
            return;
        }

        const POLL_INTERVAL_MS = 2000;
        const TIMEOUT_MS = 120000;
        const startTime = Date.now();

        const poll = async () => {
            try {
                const result = await fiber.listChannels({ pubkey: peerPubkey, include_closed: false });
                const channels = result.channels || [];
                const stillOpen = channels.some(c => c.channel_id === channelId);
                if (!stillOpen) {
                    onClosed();
                    return;
                }
            } catch (e) {
                // transient error - keep polling
            }

            if (Date.now() - startTime > TIMEOUT_MS) {
                onError(classifyError(new Error("Channel close did not confirm within timeout.")));
                return;
            }
            setTimeout(poll, POLL_INTERVAL_MS);
        };
        poll();
    },

    // Pay a specific peer via keysend. No invoice needed.
    // Do NOT include a payment_hash field - keysend payments generate one
    // internally. Including it (even as undefined/empty) fails with
    // "keysend payment should not have payment_hash".
    //
    // sendPayment() resolving only means a payment SESSION was created and
    // dispatched, same "accepted request, not completed operation" shape as
    // openChannel/closeChannel. Confirmed from fiber's own RPC docs:
    // PaymentStatus lifecycle is Created -> Inflight -> Success | Failed.
    // Poll get_payment (getPayment in fiber-js) until a terminal status,
    // same discipline as the listChannels polls elsewhere in this file.
    payPeer: async function(peerPubkey, amountShannonsHex, onSuccess, onError) {
        let initial;
        try {
            initial = await fiber.sendPayment({
                target_pubkey: peerPubkey,
                amount: amountShannonsHex,
                keysend: true
            });
        } catch (e) {
            onError(classifyError(e));
            return;
        }

        const paymentHash = initial.payment_hash;
        const POLL_INTERVAL_MS = 1000;
        const TIMEOUT_MS = 60000; // payment routing resolves far faster than on-chain channel confirmation

        const startTime = Date.now();

        const poll = async () => {
            let payment;
            try {
                payment = await fiber.getPayment({ payment_hash: paymentHash });
            } catch (e) {
                // transient getPayment error - keep polling
            }

            if (payment) {
                if (payment.status === "Success") {
                    onSuccess(JSON.stringify(payment));
                    return;
                }
                if (payment.status === "Failed") {
                    // We know the real code here - it's a terminal RPC-
                    // reported failure, not a caught exception - so tag it
                    // directly instead of routing through classifyError().
                    onError(JSON.stringify({
                        code: "PaymentFailed",
                        message: payment.failed_error || "Payment failed."
                    }));
                    return;
                }
                // Created / Inflight - not terminal yet, keep polling.
            }

            if (Date.now() - startTime > TIMEOUT_MS) {
                onError(JSON.stringify({
                    code: "PaymentFailed",
                    message: "Payment did not reach a terminal status within timeout."
                }));
                return;
            }
            setTimeout(poll, POLL_INTERVAL_MS);
        };
        poll();
    },

    // Returns this node's Fiber pubkey and CKB address - two different
    // values from two different keys.
    getNodeInfo: async function(onResult, onError) {
        try {
            const info = await fiber.nodeInfo();
            onResult(JSON.stringify({
                pubkey: info.pubkey, // v0.8.0+ renamed from node_id to pubkey
                ckbAddress: window.FiberBridge._ckbAddress || ""
            }));
        } catch (e) {
            onError(classifyError(e));
        }
    },

    // v0.8.0: PeerInfo no longer has a peer_id field at all - only pubkey and address.
    listPeers: async function(onResult, onError) {
        try {
            const result = await fiber.listPeers();
            onResult(JSON.stringify({
                peers: (result.peers || []).map(p => ({
                    pubkey: p.pubkey,
                    address: p.address
                }))
            }));
        } catch (e) {
            onError(classifyError(e));
        }
    },

    // v0.8.0: filters by pubkey, not peer_id (peer_id removed from the API).
    // Channel state is a nested { state_name, state_flags } object; the
    // Channel type's peer identifier field is also pubkey now (was peer_id).
    //
    // pubkey is optional per Fiber's RPC docs: "if not provided, all channels
    // will be listed". The field must be ABSENT, not an empty string - passing
    // "" filters for a peer whose pubkey is empty and matches nothing. So it's
    // only added when a pubkey is actually given, letting the diagnostics panel
    // pass "" to list channels across every peer.
    listChannels: async function(peerPubkey, includeClosed, onResult, onError) {
        try {
            const params = { include_closed: includeClosed };
            if (peerPubkey) params.pubkey = peerPubkey;

            const result = await fiber.listChannels(params);
            onResult(JSON.stringify({
                channels: (result.channels || []).map(c => ({
                    channel_id: c.channel_id,
                    channel_outpoint: c.channel_outpoint,
                    enabled: c.enabled,
                    state_name: c.state.state_name,
                    state_flags: c.state.state_flags,
                    pubkey: c.pubkey
                }))
            }));
        } catch (e) {
            onError(classifyError(e));
        }
    }
};
