// IUdtPaymentGateway.cs
//
// UDT (xUDT) operations, plus two recovery/diagnostic calls that have no home on
// IPaymentGateway.
//
// A SEPARATE INTERFACE, NOT EXTRA METHODS ON IPaymentGateway. Every existing
// caller pays CKB and will keep paying CKB; adding UDT methods to the interface
// they depend on would force each of them to acknowledge a concept they never
// use. Interface Segregation, but also plain risk management - the CKB payment
// path is the one that has been tested end to end, and it should not move
// because a token feature landed next to it.
//
// The practical consequence: a class that only pays CKB depends on
// IPaymentGateway and cannot accidentally call a UDT method. A class that needs
// both takes both. FiberPaymentGateway implements them together because it is
// one node underneath, but nothing above it has to know that.
using System;

namespace FiberWebGLSDK
{
    /// <summary>
    /// Channel and payment operations denominated in a User Defined Token rather
    /// than CKB, plus recovery calls the CKB path does not need.
    /// </summary>
    /// <remarks>
    /// THE HUB MUST WHITELIST THE TOKEN. A Fiber node takes a udt_whitelist in its
    /// config (name, script, cell deps, auto-accept amount) and refuses a channel
    /// funded with anything absent from it. That refusal happens on the hub, so no
    /// amount of correctness on this side avoids it - check the peer's node_info
    /// before assuming a token is usable.
    ///
    /// Every method here takes the token's type script as a JSON string:
    /// {"code_hash":"0x...","hash_type":"type","args":"0x..."}. One string rather
    /// than three arguments, because the three are meaningless apart and passing
    /// them separately invites a caller to supply two of them.
    /// </remarks>
    public interface IUdtPaymentGateway
    {
        /// <summary>
        /// Opens a channel funded with a UDT instead of CKB.
        /// </summary>
        /// <param name="fundingAmount">
        /// Amount in the TOKEN'S OWN units, not shannons. A token's decimals are its
        /// own business and this SDK does not know them.
        /// </param>
        /// <param name="onError">
        /// Common codes: UdtNotSupported (the peer does not whitelist this token),
        /// ChannelOpenFailed (never confirmed within the timeout).
        /// </param>
        void OpenUdtChannel(string peerPubkey, ulong fundingAmount, string udtTypeScriptJson, bool isPublic, Action<string> onChannelReady, Action<FiberError> onError);

        /// <summary>
        /// Pays a peer in a UDT via keysend.
        /// </summary>
        /// <remarks>
        /// Routing is restricted to channels funded with this same token. A healthy,
        /// fully-funded CKB channel to the same peer cannot carry it - a failure mode
        /// CKB payments do not have, and the reason to call
        /// <see cref="DryRunPayment"/> first.
        /// </remarks>
        void PayPeerUdt(string peerPubkey, ulong amount, string udtTypeScriptJson, Action<PaymentResult> onSuccess, Action<FiberError> onError);

        /// <summary>
        /// This node's on-chain balance of one UDT.
        /// </summary>
        /// <remarks>
        /// Not the same thing as <see cref="IFiberWallet.GetBalance"/> and not filled
        /// by the same faucet. CKB capacity belongs to every cell; a token amount
        /// lives only in cells carrying that token's type script, and has to be
        /// collected and summed. A player funded with CKB alone reads zero here.
        /// </remarks>
        void GetUdtBalance(string udtTypeScriptJson, Action<UdtBalance> onResult, Action<FiberError> onError);

        /// <summary>
        /// Asks whether a payment could be routed, and what it would cost, without
        /// sending it. Pass an empty type script to quote a CKB payment.
        /// </summary>
        /// <remarks>
        /// An unroutable payment arrives through onResult with Routable false - that
        /// is a valid answer to the question, not a failure to answer it. onError
        /// fires only when the call itself was malformed.
        /// </remarks>
        void DryRunPayment(string peerPubkey, ulong amount, string udtTypeScriptJson, Action<RouteQuote> onResult, Action<FiberError> onError);

        /// <summary>
        /// Clears a channel wedged mid-open out of the node's manager and database.
        /// </summary>
        /// <remarks>
        /// RECOVERY, NOT CLEANUP. The node refuses to abandon a channel in a ready or
        /// closed state, so this cannot destroy a working channel or one mid-
        /// settlement. What it clears is a channel stuck in NegotiatingFunding or
        /// AwaitingChannelReady after a failed open - which otherwise sits there
        /// forever and makes every retry look like a duplicate.
        ///
        /// Accepts a temporary channel id as well as a real one, which is the point:
        /// a channel that died during the open never got a real one.
        /// </remarks>
        void AbandonChannel(string channelId, Action onAbandoned, Action<FiberError> onError);
    }

    /// <summary>
    /// An on-chain UDT balance. Deliberately a distinct type from
    /// <see cref="WalletBalance"/>: the two are different assets and adding them
    /// together is always a bug, so the compiler should say so.
    /// </summary>
    public struct UdtBalance
    {
        /// <summary>Raw amount in the token's own units. Decimals are the token's business.</summary>
        public ulong Amount;

        public UdtBalance(ulong amount) => Amount = amount;

        public bool IsAtLeast(ulong required) => Amount >= required;

        public override string ToString() => Amount.ToString();
    }

    /// <summary>
    /// The answer to "could this payment be routed, and what would it cost".
    /// </summary>
    public struct RouteQuote
    {
        /// <summary>Whether a route exists right now. False is an answer, not an error.</summary>
        public bool Routable;

        /// <summary>Routing fee, when Routable. Zero otherwise.</summary>
        public ulong FeeShannons;

        /// <summary>Why no route exists. Empty when Routable.</summary>
        public string Reason;
    }

    /// <summary>
    /// The outcome of a channel close.
    /// </summary>
    public struct ChannelCloseResult
    {
        public string ChannelId;

        /// <summary>
        /// The transaction that settles the channel's balance on-chain.
        /// </summary>
        /// <remarks>
        /// BEST EFFORT, AND OFTEN EMPTY. The node reports this only while a channel is
        /// shutting down, and the close completes when the channel disappears - so
        /// the hash exists in a window that a close settling between two polls skips
        /// entirely. An empty value means there is no receipt to show, NOT that the
        /// close failed; the close is confirmed by the callback firing at all.
        ///
        /// When it is empty and a player still needs proof, their own CKB address on
        /// an explorer shows the same transaction from the chain's side.
        /// </remarks>
        public string ShutdownTransactionHash;

        public bool HasTransaction => !string.IsNullOrEmpty(ShutdownTransactionHash);
    }
}
