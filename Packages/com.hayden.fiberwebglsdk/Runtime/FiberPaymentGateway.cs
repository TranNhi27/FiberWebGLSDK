// FiberPaymentGateway.cs
//
// Concrete adapter implementing IPaymentGateway, IFiberDiagnostics and
// IFiberWallet. The only class that talks to the jslib layer directly.
//
// Callbacks arrive from JavaScript via SendMessage, which addresses this
// component by GameObject name. The GameObject must have a unique name in the
// scene, and the callback method names below must match those in FiberBridge.jslib.
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FiberWebGLSDK
{
    public class FiberPaymentGateway : MonoBehaviour, IPaymentGateway, IFiberDiagnostics, IFiberWallet
    {
        [DllImport("__Internal")] private static extern void Fiber_Initialize(string configText, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_ConnectPeer(string peerAddress, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_OpenChannel(string peerPubkey, string fundingAmountHex, bool isPublic, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_CloseChannel(string channelId, string peerPubkey, bool force, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_PayPeer(string peerPubkey, string amountHex, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_GetNodeInfo(string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_ListPeers(string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_ListChannels(string peerPubkey, bool includeClosed, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_GetBalance(string callbackTarget);

        [SerializeField] private FiberNodeConfig config;

        // One callback pair per operation. A second call to the same operation
        // before the first completes overwrites the pending callbacks; callers
        // must not issue concurrent calls to the same method.
        private Action _onReady;
        private Action<FiberError> _onInitError;
        private Action _onConnected;
        private Action<FiberError> _onConnectError;
        private Action<string> _onChannelReady;
        private Action<FiberError> _onChannelError;
        private Action _onChannelClosed;
        private Action<FiberError> _onCloseChannelError;
        private Action<PaymentResult> _onPaymentSuccess;
        private Action<FiberError> _onPaymentError;
        private Action<NodeInfo> _onNodeInfo;
        private Action<FiberError> _onNodeInfoError;
        private Action<PeerInfo[]> _onListPeers;
        private Action<FiberError> _onListPeersError;
        private Action<ChannelInfo[]> _onListChannels;
        private Action<FiberError> _onListChannelsError;
        private Action<WalletBalance> _onBalance;
        private Action<FiberError> _onBalanceError;

        // ---------------- IPaymentGateway ----------------

        public void Initialize(Action onReady, Action<FiberError> onError)
        {
            if (config == null || config.nodeConfig == null)
            {
                onError(new FiberError(FiberErrorCode.ConfigMissing, "FiberPaymentGateway: no FiberNodeConfig (or its nodeConfig TextAsset) assigned."));
                return;
            }

            _onReady = onReady;
            _onInitError = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
        Fiber_Initialize(config.nodeConfig.text, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Fiber only runs in a real WebGL build, not the Editor."));
#endif
        }

        public void ConnectPeer(string peerAddress, Action onConnected, Action<FiberError> onError)
        {
            _onConnected = onConnected;
            _onConnectError = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
        Fiber_ConnectPeer(peerAddress, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        /// <summary>
        /// Opens a channel. See IPaymentGateway.OpenChannel for the contract.
        /// </summary>
        /// <remarks>
        /// ACTIVE WORKAROUND: the funding amount is multiplied by 100 before being
        /// sent. Without it, funding_amount arrives at the node ~100x smaller than
        /// requested. A configured 100 CKB therefore sends 10,000 CKB on the wire.
        /// Set funding amounts against that behaviour, not against the literal value.
        ///
        /// Scope of the bug, established 2026-07-16 via DEBUG-1/2/3 logging: the C#
        /// ulong, its hex encoding, and the value bridge.js passes to
        /// fiber.openChannel() are correct at every step. The loss occurs inside
        /// @nervosnetwork/fiber-js or the WASM node, on the pinned 0.8.0 release.
        /// Reproduced against the public onyxia.fiber.channel testnet hub, so it is
        /// not specific to a single node.
        ///
        /// To remove: upgrade fiber-js past 0.8.0 (0.9.x notes "more reliable channel
        /// funding"), rerun an OpenChannel with the DEBUG logging from git history
        /// around 2026-07-16, and delete the multiplier only if the amount arrives
        /// correct without it. Verify the hub's
        /// open_channel_auto_accept_min_ckb_funding_amount at the same time; a hub
        /// minimum can mask or mimic this bug.
        /// </remarks>
        public void OpenChannel(string peerPubkey, ulong fundingAmountShannons, bool isPublic, Action<string> onChannelReady, Action<FiberError> onError)
        {
            _onChannelReady = onChannelReady;
            _onChannelError = onError;

            // WORKAROUND, RECONFIRMED (16 Aug): funding_amount arrives at the native node
            // ~100x smaller than what's sent. Verified with a controlled test: with this
            // multiplier removed, a known 10,000,000,000-shannon (100 CKB) request arrived
            // at the hub as exactly 100,000,000 shannons (1 CKB) - a clean 100x loss, not an
            // approximation. The multiplier is necessary and correct; do not remove it again
            // without an equally controlled test.
            //
            // Root-caused via DEBUG-1/2/3 logging on 2026-07-16: the C# ulong, its hex
            // encoding, and the value bridge.js hands to fiber.openChannel() are all confirmed
            // correct at every step - the loss happens inside @nervosnetwork/fiber-js / the
            // WASM node itself, on the pinned 0.8.0 release. Confirmed against the public
            // onyxia.fiber.channel testnet hub, so this is not specific to any one node we
            // control.
            //
            // Fiber's own v0.9 dev log calls out "more reliable channel funding" and
            // "fiber-js and npm release improvements" as work done in the 0.8.0 -> 0.9.0-rc
            // window, which lines up with this bug's shape - but 0.9.0-rc7 is a release
            // candidate and wasn't risked pre-deadline. Revisit removing this the next
            // time fiber-js is upgraded: bump the version, rerun an OpenChannel with
            // logging (see git history around 2026-07-16 for the exact DEBUG lines used),
            // and remove the x100 below only if the funding amount arrives correct without it.
            //
            // NOTE: because this exactly cancels the native /100 loss, the amount that
            // arrives at the hub equals config.FundingAmountShannons unchanged - NOT 100x
            // it. Size FiberSessionConfig's fundingAmountCkb with real margin above the
            // hub's open_channel_auto_accept_min_ckb_funding_amount; landing exactly on
            // that boundary is fragile (rounding, fees, or a strict < vs <= comparison
            // can tip it either way).
            ulong compensatedAmount = fundingAmountShannons * 100;
            string amountHex = "0x" + compensatedAmount.ToString("x");

#if UNITY_WEBGL && !UNITY_EDITOR
        Fiber_OpenChannel(peerPubkey, amountHex, isPublic, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        public void CloseChannel(string channelId, string peerPubkey, bool force, Action onClosed, Action<FiberError> onError)
        {
            _onChannelClosed = onClosed;
            _onCloseChannelError = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Fiber_CloseChannel(channelId, peerPubkey, force, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        public void PayPeer(string peerPubkey, ulong amountShannons, Action<PaymentResult> onSuccess, Action<FiberError> onError)
        {
            _onPaymentSuccess = onSuccess;
            _onPaymentError = onError;
            string amountHex = "0x" + amountShannons.ToString("x");

#if UNITY_WEBGL && !UNITY_EDITOR
        Fiber_PayPeer(peerPubkey, amountHex, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        public void GetNodeInfo(Action<NodeInfo> onResult, Action<FiberError> onError)
        {
            _onNodeInfo = onResult;
            _onNodeInfoError = onError;
#if UNITY_WEBGL && !UNITY_EDITOR
        Fiber_GetNodeInfo(gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        // ---------------- IFiberDiagnostics ----------------

        public void ListPeers(Action<PeerInfo[]> onResult, Action<FiberError> onError)
        {
            _onListPeers = onResult;
            _onListPeersError = onError;
#if UNITY_WEBGL && !UNITY_EDITOR
        Fiber_ListPeers(gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        public void ListChannels(string peerPubkey, bool includeClosed, Action<ChannelInfo[]> onResult, Action<FiberError> onError)
        {
            _onListChannels = onResult;
            _onListChannelsError = onError;
#if UNITY_WEBGL && !UNITY_EDITOR
        Fiber_ListChannels(peerPubkey, includeClosed, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        // ---------------- IFiberWallet ----------------

        public void GetBalance(Action<WalletBalance> onResult, Action<FiberError> onError)
        {
            _onBalance = onResult;
            _onBalanceError = onError;
#if UNITY_WEBGL && !UNITY_EDITOR
        Fiber_GetBalance(gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        // ---------------- Called by jslib via SendMessage ----------------
        // Method names here are the contract with FiberBridge.jslib. Renaming one
        // breaks silently at runtime: SendMessage logs a missing-method warning
        // rather than failing the build.

        private void OnFiberReady() => _onReady?.Invoke();
        private void OnFiberError(string err) => _onInitError?.Invoke(FiberError.Parse(err));

        private void OnPeerConnected() => _onConnected?.Invoke();
        private void OnPeerConnectError(string err) => _onConnectError?.Invoke(FiberError.Parse(err));

        /// <summary>
        /// channelId comes from bridge.js's openChannel poll, which returns only a
        /// channel confirmed ChannelReady and not present before the open began.
        /// </summary>
        private void OnChannelReady(string channelId) => _onChannelReady?.Invoke(channelId);
        private void OnChannelError(string err) => _onChannelError?.Invoke(FiberError.Parse(err));

        /// <summary>
        /// Fires with no arguments once the channel id no longer appears in
        /// listChannels, meaning the close has settled on-chain.
        /// </summary>
        private void OnChannelClosed() => _onChannelClosed?.Invoke();
        private void OnCloseChannelError(string err) => _onCloseChannelError?.Invoke(FiberError.Parse(err));

        /// <summary>
        /// Receives the node's full payment object, not just a hash. Unknown fields
        /// are ignored by JsonUtility, so this tolerates additions to the payment shape.
        /// </summary>
        /// <remarks>
        /// A parse failure still reports success. bridge.js only calls this on a
        /// terminal Success status, so the payment did complete; RawJson carries the
        /// unparsed payload.
        /// </remarks>
        private void OnPaymentSuccess(string resultJson)
        {
            string paymentHash = null;
            string status = null;

            try
            {
                var data = JsonUtility.FromJson<PaymentJson>(resultJson);
                paymentHash = data.payment_hash;
                status = data.status;
            }
            catch
            {
                // Fields stay null; RawJson below preserves the payload.
            }

            _onPaymentSuccess?.Invoke(new PaymentResult
            {
                PaymentHash = paymentHash,
                Status = status,
                RawJson = resultJson
            });
        }

        private void OnPaymentError(string err) => _onPaymentError?.Invoke(FiberError.Parse(err));

        private void OnNodeInfoResult(string json)
        {
            var data = JsonUtility.FromJson<NodeInfoJson>(json);
            _onNodeInfo?.Invoke(new NodeInfo { Pubkey = data.pubkey, CkbAddress = data.ckbAddress });
        }

        private void OnNodeInfoError(string err) => _onNodeInfoError?.Invoke(FiberError.Parse(err));

        private void OnListPeersResult(string json)
        {
            var wrapper = JsonUtility.FromJson<PeerInfoListJson>(json);
            var peers = Array.ConvertAll(wrapper.peers, p => new PeerInfo
            {
                Pubkey = p.pubkey,
                Address = p.address
            });
            _onListPeers?.Invoke(peers);
        }

        private void OnListPeersError(string err) => _onListPeersError?.Invoke(FiberError.Parse(err));

        /// <summary>
        /// Parses a shannon amount sent as a string. Returns 0 when unparseable.
        /// </summary>
        /// <remarks>
        /// Shannon amounts exceed the range a JSON number survives intact, so
        /// bridge.js stringifies them. 0 is the safe fallback for channel balances:
        /// it makes a malformed entry read as unusable capacity rather than failing
        /// the whole ListChannels call.
        /// </remarks>
        private static ulong ParseShannons(string s)
        {
            return ulong.TryParse(s, out ulong value) ? value : 0UL;
        }

        private void OnListChannelsResult(string json)
        {
            var wrapper = JsonUtility.FromJson<ChannelInfoListJson>(json);
            var channels = Array.ConvertAll(wrapper.channels, c => new ChannelInfo
            {
                ChannelId = c.channel_id,
                ChannelOutpoint = c.channel_outpoint,
                Enabled = c.enabled,
                StateName = c.state_name,
                StateFlags = c.state_flags,
                PeerPubkey = c.pubkey,
                LocalBalanceShannons = ParseShannons(c.local_balance),
                RemoteBalanceShannons = ParseShannons(c.remote_balance)
            });
            _onListChannels?.Invoke(channels);
        }

        private void OnListChannelsError(string err) => _onListChannelsError?.Invoke(FiberError.Parse(err));

        /// <summary>
        /// Parses the wallet balance. Reports ParseError rather than a zero balance
        /// when the payload is malformed.
        /// </summary>
        /// <remarks>
        /// A funding screen showing 0 for a failed read is worse than one showing an
        /// error: the player waits for funds that already arrived.
        /// </remarks>
        private void OnBalanceResult(string json)
        {
            BalanceJson data;
            try
            {
                data = JsonUtility.FromJson<BalanceJson>(json);
            }
            catch
            {
                _onBalanceError?.Invoke(new FiberError(FiberErrorCode.ParseError, json));
                return;
            }

            if (data == null || !ulong.TryParse(data.balanceShannons, out ulong shannons))
            {
                _onBalanceError?.Invoke(new FiberError(
                    FiberErrorCode.ParseError,
                    $"Balance was not a parseable shannon amount: '{data?.balanceShannons}'"));
                return;
            }

            _onBalance?.Invoke(new WalletBalance(shannons));
        }

        private void OnBalanceError(string err) => _onBalanceError?.Invoke(FiberError.Parse(err));

        // ---------------- JSON shapes matching bridge.js output ----------------
        // Field names must match bridge.js's output keys exactly. JsonUtility
        // silently leaves unmatched fields at their default value.

        [Serializable] private class NodeInfoJson { public string pubkey; public string ckbAddress; }
        [Serializable] private class PaymentJson { public string payment_hash; public string status; }
        [Serializable] private class PeerInfoJson { public string pubkey; public string address; }
        [Serializable] private class PeerInfoListJson { public PeerInfoJson[] peers; }
        [Serializable] private class ChannelInfoJson { public string channel_id; public string channel_outpoint; public bool enabled; public string state_name; public string state_flags; public string pubkey; public string local_balance; public string remote_balance; }
        [Serializable] private class ChannelInfoListJson { public ChannelInfoJson[] channels; }
        [Serializable] private class BalanceJson { public string balanceShannons; }
    }
}