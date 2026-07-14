// IPaymentGateway.cs
using System;

namespace FiberWebGLSDK
{
    /// <summary>
    /// Value-moving operations against a Fiber node: connecting to peers,
    /// opening and closing channels, and sending payments.
    ///
    /// Every operation is asynchronous and reports back through callbacks -
    /// none of them return a value or block. Failures are always delivered
    /// through onError as a structured <see cref="FiberError"/>; switch on
    /// its Code rather than matching on Message.
    ///
    /// See <see cref="IFiberDiagnostics"/> for read-only network and channel state.
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>
        /// Boots the underlying Fiber node. Call once, before any other operation.
        /// </summary>
        /// <param name="onReady">Invoked when the node has started and is ready to accept operations.</param>
        /// <param name="onError">
        /// Common codes: ConfigMissing (no FiberNodeConfig assigned),
        /// CrossOriginIsolationRequired (the hosting page is not cross-origin isolated),
        /// EditorNotSupported (running in the Editor rather than a WebGL build).
        /// </param>
        void Initialize(Action onReady, Action<FiberError> onError);

        /// <summary>
        /// Connects to a peer. Required before opening a channel with, or paying, that peer.
        /// </summary>
        /// <param name="peerAddress">
        /// The peer's multiaddr, e.g. "/ip4/1.2.3.4/tcp/8228/ws/p2p/{peerId}".
        /// This is the only identifier needed to dial a peer - the peer's pubkey is a
        /// separate value, resolvable via <see cref="IFiberDiagnostics.ListPeers"/>
        /// once connected.
        /// </param>
        /// <param name="onConnected">
        /// Invoked once the peer accepts the connection. The peer may not appear in
        /// ListPeers for a short time afterwards - retry if you need its pubkey immediately.
        /// </param>
        /// <param name="onError">Common code: ConnectionFailed (unreachable peer, or a transport the node cannot dial).</param>
        void ConnectPeer(string peerAddress, Action onConnected, Action<FiberError> onError);

        /// <summary>
        /// Opens a payment channel with a peer and funds it on-chain.
        /// The peer must already be connected via <see cref="ConnectPeer"/>.
        /// </summary>
        /// <param name="peerPubkey">
        /// The peer's Fiber network pubkey - distinct from the peer id embedded in its
        /// multiaddr. Resolve it from <see cref="IFiberDiagnostics.ListPeers"/> after connecting.
        /// </param>
        /// <param name="fundingAmountShannons">Amount of CKB to lock into the channel, in shannons (1 CKB = 100,000,000 shannons).</param>
        /// <param name="isPublic">Whether to announce this channel on the public gossip graph.</param>
        /// <param name="onChannelReady">
        /// Invoked with the new channel's id once the channel is confirmed on-chain and
        /// usable - not when the open request is merely accepted. On testnet this can take
        /// a couple of minutes. The id matches <see cref="IFiberDiagnostics.ChannelInfo.ChannelId"/>.
        /// </param>
        /// <param name="onError">Common code: ChannelOpenFailed (rejected by the peer, or never confirmed within the timeout).</param>
        void OpenChannel(string peerPubkey, ulong fundingAmountShannons, bool isPublic, Action<string> onChannelReady, Action<FiberError> onError);

        /// <summary>
        /// Closes an open channel and settles its balance on-chain.
        /// </summary>
        /// <param name="channelId">
        /// The channel to close. Available from <see cref="IFiberDiagnostics.ChannelInfo.ChannelId"/>,
        /// or from <see cref="OpenChannel"/>'s onChannelReady callback.
        /// </param>
        /// <param name="peerPubkey">The channel's counterparty. Required to confirm the channel has actually closed.</param>
        /// <param name="force">
        /// Skip cooperative negotiation with the peer and force the channel closed.
        /// Prefer false; a forced close can be slower to settle and more expensive.
        /// </param>
        /// <param name="onClosed">Invoked once the channel is settled on-chain, not when the close request is accepted.</param>
        /// <param name="onError">Common code: RpcError (rejected), or a timeout if the close never settles.</param>
        void CloseChannel(string channelId, string peerPubkey, bool force, Action onClosed, Action<FiberError> onError);

        /// <summary>
        /// Sends a payment to a peer via keysend - no invoice required.
        /// Routes over any usable channel to that peer, so no channel id is needed.
        /// </summary>
        /// <param name="peerPubkey">The recipient's Fiber network pubkey.</param>
        /// <param name="amountShannons">Amount to send, in shannons.</param>
        /// <param name="onSuccess">
        /// Invoked once the payment reaches a terminal Success status - not when it is
        /// merely dispatched. A payment passes through Created and Inflight first.
        /// </param>
        /// <param name="onError">Common code: PaymentFailed (no route, insufficient balance, or never settled within the timeout).</param>
        void PayPeer(string peerPubkey, ulong amountShannons, Action<PaymentResult> onSuccess, Action<FiberError> onError);

        /// <summary>
        /// Reads this node's own identity. Available once <see cref="Initialize"/> has completed.
        /// </summary>
        void GetNodeInfo(Action<NodeInfo> onResult, Action<FiberError> onError);
    }

    /// <summary>
    /// A completed (Success) payment. Only delivered to PayPeer's onSuccess -
    /// a failed payment arrives as a <see cref="FiberError"/> instead.
    /// </summary>
    public struct PaymentResult
    {
        /// The payment's hash, identifying it on the network.
        public string PaymentHash;

        /// The payment's terminal status as reported by the node. Always "Success" here;
        /// exposed for logging rather than for branching on.
        public string Status;

        /// The node's full, unparsed payment response. Useful for logging, or for reading
        /// fields this SDK does not surface yet. Do not depend on its shape.
        public string RawJson;
    }

    /// <summary>
    /// This node's own identity. The two values are derived from two different keys
    /// and are not interchangeable.
    /// </summary>
    public struct NodeInfo
    {
        /// This node's identity on the Fiber P2P network. Peers use this to pay you.
        public string Pubkey;

        /// This node's on-chain CKB wallet address. Fund it to have something to spend.
        public string CkbAddress;
    }
}