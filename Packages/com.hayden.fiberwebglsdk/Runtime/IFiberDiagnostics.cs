// IFiberDiagnostics.cs
//
// Read-only network and channel state for the underlying Fiber node.
// Value-moving operations live on IPaymentGateway; on-chain wallet balance
// lives on IFiberWallet.
using System;

namespace FiberWebGLSDK
{
    /// <summary>
    /// Read-only queries against the local Fiber node. No method here moves value.
    /// </summary>
    public interface IFiberDiagnostics
    {
        /// <summary>
        /// Lists currently connected peers.
        /// </summary>
        /// <remarks>
        /// A peer may take a short time to appear after ConnectPeer reports success.
        /// Retry if a pubkey is needed immediately after connecting.
        /// </remarks>
        void ListPeers(Action<PeerInfo[]> onResult, Action<FiberError> onError);

        /// <summary>
        /// Lists channels, optionally filtered to one peer.
        /// </summary>
        /// <param name="peerPubkey">
        /// The peer's Fiber pubkey, or an empty string to list channels across all peers.
        /// </param>
        /// <param name="includeClosed">Include channels that have already settled on-chain.</param>
        void ListChannels(string peerPubkey, bool includeClosed, Action<ChannelInfo[]> onResult, Action<FiberError> onError);
    }

    [Serializable]
    public struct PeerInfo
    {
        /// <summary>Fiber network identity. Pass this to OpenChannel and PayPeer.</summary>
        public string Pubkey;

        /// <summary>
        /// The peer's multiaddr. Contains a libp2p peer id, which is a different
        /// identifier from Pubkey and is not interchangeable with it.
        /// </summary>
        public string Address;
    }

    /// <summary>
    /// A channel's current state and capacity.
    /// </summary>
    /// <remarks>
    /// Before using a channel, check three things: StateName is "ChannelReady",
    /// Enabled is true, and the balance for the direction of payment is sufficient.
    /// State and Enabled alone do not indicate that a channel can carry a payment.
    /// </remarks>
    [Serializable]
    public struct ChannelInfo
    {
        public string ChannelId;
        public string ChannelOutpoint;

        /// <summary>
        /// Whether the channel is enabled. A disabled channel can report
        /// StateName "ChannelReady" and still reject payments.
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// Channel state, e.g. "ChannelReady", "ShuttingDown", "NegotiatingFunding".
        /// </summary>
        public string StateName;

        public string StateFlags;
        public string PeerPubkey;

        /// <summary>
        /// This node's remaining send capacity, in shannons.
        /// Check this before PAYING the peer.
        /// </summary>
        public ulong LocalBalanceShannons;

        /// <summary>
        /// The peer's remaining send capacity, in shannons.
        /// Check this before expecting to RECEIVE from the peer.
        /// </summary>
        /// <remarks>
        /// A channel drained in one direction still reports ChannelReady and
        /// Enabled. Payments in that direction fail with an insufficient-balance
        /// error at send time. When reusing an existing channel, require enough
        /// balance for all payments expected over its lifetime, not just the next one.
        /// </remarks>
        public ulong RemoteBalanceShannons;
    }
}