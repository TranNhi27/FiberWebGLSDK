// IFiberDiagnostics.cs
//
// Network and wallet status operations for the underlying Fiber node -
// peer connectivity and channel state, as opposed to IPaymentGateway's
// value-moving operations.
//
// NOTE: no GetBalance method yet. There's no dedicated balance RPC in the
// current fiber-js API - add this once nodeInfo()'s real response shape
// is confirmed against a live node.
using System;
namespace FiberWebGLSDK
{
    public interface IFiberDiagnostics
    {

        void ListPeers(Action<PeerInfo[]> onResult, Action<FiberError> onError);

        /// peerPubkey: the peer's Fiber pubkey 
        void ListChannels(string peerPubkey, bool includeClosed, Action<ChannelInfo[]> onResult, Action<FiberError> onError);
    }

    [Serializable]
    public struct PeerInfo
    {
        /// Fiber network identity. Used in OpenChannel / PayPeer.
        public string Pubkey;
        /// The peer's multiaddr.
        public string Address;
    }

    [Serializable]
    public struct ChannelInfo
    {
        public string ChannelId;
        public string ChannelOutpoint;
        public bool Enabled;
        public string StateName;
        public string StateFlags;
        public string PeerPubkey;
    }
}