// FiberPaymentGateway.cs
//
// Concrete adapter implementing IPaymentGateway and IFiberDiagnostics.
// Only class that talks to the jslib layer directly.
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FiberWebGLSDK
{
    public class FiberPaymentGateway : MonoBehaviour, IPaymentGateway, IFiberDiagnostics
    {
        [DllImport("__Internal")] private static extern void Fiber_Initialize(string configText, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_ConnectPeer(string peerAddress, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_OpenChannel(string peerPubkey, string fundingAmountHex, bool isPublic, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_CloseChannel(string channelId, string peerPubkey, bool force, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_PayPeer(string peerPubkey, string amountHex, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_GetNodeInfo(string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_ListPeers(string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_ListChannels(string peerPubkey, bool includeClosed, string callbackTarget);

        [SerializeField] private FiberNodeConfig config;

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

        public void OpenChannel(string peerPubkey, ulong fundingAmountShannons, bool isPublic, Action<string> onChannelReady, Action<FiberError> onError)
        {
            _onChannelReady = onChannelReady;
            _onChannelError = onError;

            // WORKAROUND - funding_amount arrives at the native node ~100x smaller than
            // what's sent. Root-caused via DEBUG-1/2/3 logging on 2026-07-16: the C#
            // ulong, its hex encoding, and the value bridge.js hands to fiber.openChannel()
            // are all confirmed correct at every step - the loss happens inside
            // @nervosnetwork/fiber-js / the WASM node itself, on the pinned 0.8.0 release.
            // Confirmed against the public onyxia.fiber.channel testnet hub, so this is
            // not specific to any one node we control.
            //
            // Fiber's own v0.9 dev log calls out "more reliable channel funding" and
            // "fiber-js and npm release improvements" as work done in the 0.8.0 -> 0.9.0-rc
            // window, which lines up with this bug's shape - but 0.9.0-rc7 is a release
            // candidate and wasn't risked pre-deadline. Revisit removing this the next
            // time fiber-js is upgraded: bump the version, rerun an OpenChannel with
            // logging (see git history around 2026-07-16 for the exact DEBUG lines used),
            // and remove the ×100 below only if the funding amount arrives correct without it.
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

        // ---------------- Called by jslib via SendMessage ----------------

        private void OnFiberReady() => _onReady?.Invoke();
        private void OnFiberError(string err) => _onInitError?.Invoke(FiberError.Parse(err));

        private void OnPeerConnected() => _onConnected?.Invoke();
        private void OnPeerConnectError(string err) => _onConnectError?.Invoke(FiberError.Parse(err));

        // channelId comes from bridge.js's openChannel poll (the confirmed ChannelReady channel).
        private void OnChannelReady(string channelId) => _onChannelReady?.Invoke(channelId);
        private void OnChannelError(string err) => _onChannelError?.Invoke(FiberError.Parse(err));

        // bridge.js's closeChannel poll resolves with no args once the channel_id no
        // longer appears in listChannels (settled on-chain).
        private void OnChannelClosed() => _onChannelClosed?.Invoke();
        private void OnCloseChannelError(string err) => _onCloseChannelError?.Invoke(FiberError.Parse(err));

        // bridge.js sends the node's whole payment object here, not just a hash.
        // Parse out the fields the SDK surfaces and keep the raw JSON for callers
        // who need something we don't expose yet. JsonUtility ignores unknown
        // fields, so this stays forward-compatible as fiber's payment shape grows.
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
                // Malformed payload. The payment itself did succeed - bridge.js only
                // calls onSuccess on a terminal Success status - so report it rather
                // than swallowing it, with RawJson carrying whatever we got.
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
                PeerPubkey = c.pubkey
            });
            _onListChannels?.Invoke(channels);
        }

        private void OnListChannelsError(string err) => _onListChannelsError?.Invoke(FiberError.Parse(err));

        // ---------------- JSON shapes matching bridge.js output ----------------

        [Serializable] private class NodeInfoJson { public string pubkey; public string ckbAddress; }
        [Serializable] private class PaymentJson { public string payment_hash; public string status; }
        [Serializable] private class PeerInfoJson { public string pubkey; public string address; }
        [Serializable] private class PeerInfoListJson { public PeerInfoJson[] peers; }
        [Serializable] private class ChannelInfoJson { public string channel_id; public string channel_outpoint; public bool enabled; public string state_name; public string state_flags; public string pubkey; }
        [Serializable] private class ChannelInfoListJson { public ChannelInfoJson[] channels; }
    }
}