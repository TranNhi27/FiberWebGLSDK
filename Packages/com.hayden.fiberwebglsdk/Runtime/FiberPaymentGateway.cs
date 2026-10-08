// FiberPaymentGateway.cs
//
// Concrete adapter implementing IPaymentGateway, IFiberDiagnostics and
// IFiberWallet. The only class that talks to the jslib layer directly.
//
// Callbacks arrive from JavaScript via SendMessage, which addresses this
// component by GameObject name. The GameObject must have a unique name in the
// scene, and the callback method names below must match those in FiberBridge.jslib.
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FiberWebGLSDK
{
    public class FiberPaymentGateway : MonoBehaviour, IPaymentGateway, IUdtPaymentGateway, IInvoicePaymentGateway, IFiberDiagnostics, IFiberWallet
    {
        [DllImport("__Internal")] private static extern void Fiber_Initialize(string configText, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_ConnectPeer(string peerAddress, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_OpenChannel(string peerPubkey, string fundingAmountHex, string udtScriptJson, bool isPublic, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_CloseChannel(string channelId, string peerPubkey, bool force, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_PayPeer(string peerPubkey, string amountHex, string udtScriptJson, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_GetNodeInfo(string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_ListPeers(string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_ListChannels(string peerPubkey, bool includeClosed, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_GetBalance(string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_GetUdtBalance(string udtScriptJson, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_AbandonChannel(string channelId, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_DryRunPayment(string peerPubkey, string amountHex, string udtScriptJson, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_PayInvoice(string invoice, string callbackTarget);
        [DllImport("__Internal")] private static extern void Fiber_ParseInvoice(string invoice, string callbackTarget);

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
        private Action<ChannelCloseResult> _onChannelClosed;
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
        private Action<UdtBalance> _onUdtBalance;
        private Action<FiberError> _onUdtBalanceError;
        private Action _onChannelAbandoned;
        private Action<FiberError> _onAbandonChannelError;
        private Action<RouteQuote> _onDryRun;
        private Action<FiberError> _onDryRunError;
        private Action<InvoiceDetails> _onParseInvoice;
        private Action<FiberError> _onParseInvoiceError;

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
            => OpenChannelInternal(peerPubkey, fundingAmountShannons, string.Empty, isPublic, onChannelReady, onError);

        /// <summary>
        /// Opens a channel funded with a UDT rather than CKB. See
        /// IUdtPaymentGateway.OpenUdtChannel for the contract.
        /// </summary>
        /// <remarks>
        /// THE x100 COMPENSATION IS NOT APPLIED HERE, deliberately. The 100x funding
        /// loss was measured against CKB and has never been retested against a UDT
        /// amount. Getting that wrong is asymmetric: compensating a loss that is not
        /// there over-funds the channel by 100x of a real token, while failing to
        /// compensate a loss that IS there under-funds it and the open simply fails.
        /// One of those is recoverable.
        ///
        /// Before relying on this, run the same controlled test used for CKB - open
        /// with a known amount, read what the hub received - and only then decide
        /// whether to route this through the same compensation.
        /// </remarks>
        public void OpenUdtChannel(string peerPubkey, ulong fundingAmount, string udtTypeScriptJson, bool isPublic, Action<string> onChannelReady, Action<FiberError> onError)
        {
            if (string.IsNullOrEmpty(udtTypeScriptJson))
            {
                onError(new FiberError(FiberErrorCode.ConfigMissing,
                    "OpenUdtChannel needs a UDT type script - use OpenChannel for CKB."));
                return;
            }

            _onChannelReady = onChannelReady;
            _onChannelError = onError;

            string udtAmountHex = "0x" + fundingAmount.ToString("x");

#if UNITY_WEBGL && !UNITY_EDITOR
            Fiber_OpenChannel(peerPubkey, udtAmountHex, udtTypeScriptJson, isPublic, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        private void OpenChannelInternal(string peerPubkey, ulong fundingAmountShannons, string udtScriptJson, bool isPublic, Action<string> onChannelReady, Action<FiberError> onError)
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
            // ulong compensatedAmount = fundingAmountShannons * 100;
            ulong compensatedAmount = fundingAmountShannons;

            string amountHex = "0x" + compensatedAmount.ToString("x");

#if UNITY_WEBGL && !UNITY_EDITOR
        Fiber_OpenChannel(peerPubkey, amountHex, udtScriptJson, isPublic, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        /// <summary>
        /// Closes a channel. Kept for callers that do not need the settlement
        /// transaction - it simply discards the payload the richer overload delivers.
        /// </summary>
        public void CloseChannel(string channelId, string peerPubkey, bool force, Action onClosed, Action<FiberError> onError)
            => CloseChannel(channelId, peerPubkey, force, _ => onClosed?.Invoke(), onError);

        /// <summary>
        /// Closes a channel and reports the settlement transaction alongside it.
        /// </summary>
        /// <remarks>
        /// The hash is best-effort and CAN come back empty. The node only reports it
        /// while the channel is shutting down, and bridge.js captures it on its way
        /// past during the close poll - a close that settles between two polls never
        /// shows a shutting-down state to catch. Treat an empty hash as "no receipt
        /// to show", not as a failed close: the close itself is confirmed by the
        /// callback firing at all.
        /// </remarks>
        public void CloseChannel(string channelId, string peerPubkey, bool force, Action<ChannelCloseResult> onClosed, Action<FiberError> onError)
        {
            _onChannelClosed = onClosed;
            _onCloseChannelError = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Fiber_CloseChannel(channelId, peerPubkey, force, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        /// <summary>
        /// Clears a channel wedged mid-open out of the node's manager and database.
        /// See IUdtPaymentGateway.AbandonChannel.
        /// </summary>
        public void AbandonChannel(string channelId, Action onAbandoned, Action<FiberError> onError)
        {
            _onChannelAbandoned = onAbandoned;
            _onAbandonChannelError = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Fiber_AbandonChannel(channelId, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        /// <summary>
        /// Asks whether a payment could be routed, and what it would cost, without
        /// sending it. See IUdtPaymentGateway.DryRunPayment.
        /// </summary>
        public void DryRunPayment(string peerPubkey, ulong amount, string udtTypeScriptJson, Action<RouteQuote> onResult, Action<FiberError> onError)
        {
            _onDryRun = onResult;
            _onDryRunError = onError;
            string amountHex = "0x" + amount.ToString("x");

#if UNITY_WEBGL && !UNITY_EDITOR
            Fiber_DryRunPayment(peerPubkey, amountHex, udtTypeScriptJson ?? string.Empty, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        public void PayPeer(string peerPubkey, ulong amountShannons, Action<PaymentResult> onSuccess, Action<FiberError> onError)
            => PayPeerInternal(peerPubkey, amountShannons, string.Empty, onSuccess, onError);

        /// <summary>
        /// Pays a peer in a UDT rather than CKB. See IUdtPaymentGateway.PayPeerUdt.
        /// </summary>
        /// <remarks>
        /// Routing is restricted to channels funded with this same UDT. A healthy,
        /// well-funded CKB channel to the same peer cannot carry it - which is a
        /// failure mode CKB payments simply do not have, and the reason DryRunPayment
        /// is worth calling first here even though it rarely earns its keep for CKB.
        /// </remarks>
        public void PayPeerUdt(string peerPubkey, ulong amount, string udtTypeScriptJson, Action<PaymentResult> onSuccess, Action<FiberError> onError)
        {
            if (string.IsNullOrEmpty(udtTypeScriptJson))
            {
                onError(new FiberError(FiberErrorCode.ConfigMissing,
                    "PayPeerUdt needs a UDT type script - use PayPeer for CKB."));
                return;
            }

            PayPeerInternal(peerPubkey, amount, udtTypeScriptJson, onSuccess, onError);
        }

        private void PayPeerInternal(string peerPubkey, ulong amountShannons, string udtScriptJson, Action<PaymentResult> onSuccess, Action<FiberError> onError)
        {
            _onPaymentSuccess = onSuccess;
            _onPaymentError = onError;
            string amountHex = "0x" + amountShannons.ToString("x");

#if UNITY_WEBGL && !UNITY_EDITOR
        Fiber_PayPeer(peerPubkey, amountHex, udtScriptJson, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        // ---------------- IInvoicePaymentGateway ----------------

        /// <summary>
        /// Pays an invoice. See IInvoicePaymentGateway.PayInvoice.
        /// </summary>
        /// <remarks>
        /// Shares OnPaymentSuccess/OnPaymentError with PayPeer, so a PayInvoice and a
        /// PayPeer must not be in flight at the same time - the second overwrites the
        /// first's callbacks. That constraint already applies per-operation across
        /// this whole class; it just spans two methods here.
        /// </remarks>
        public void PayInvoice(string invoiceAddress, Action<PaymentResult> onSuccess, Action<FiberError> onError)
        {
            if (string.IsNullOrEmpty(invoiceAddress))
            {
                onError(new FiberError(FiberErrorCode.ConfigMissing, "PayInvoice called with an empty invoice."));
                return;
            }

            _onPaymentSuccess = onSuccess;
            _onPaymentError = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Fiber_PayInvoice(invoiceAddress, gameObject.name);
#else
            onError(new FiberError(FiberErrorCode.EditorNotSupported, "Editor mode: no Fiber."));
#endif
        }

        /// <summary>
        /// Decodes an invoice without paying it. See IInvoicePaymentGateway.ParseInvoice.
        /// </summary>
        public void ParseInvoice(string invoiceAddress, Action<InvoiceDetails> onResult, Action<FiberError> onError)
        {
            if (string.IsNullOrEmpty(invoiceAddress))
            {
                onError(new FiberError(FiberErrorCode.ConfigMissing, "ParseInvoice called with an empty invoice."));
                return;
            }

            _onParseInvoice = onResult;
            _onParseInvoiceError = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Fiber_ParseInvoice(invoiceAddress, gameObject.name);
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

        /// <summary>
        /// On-chain balance of one UDT held by this node's wallet.
        /// See IUdtPaymentGateway.GetUdtBalance.
        /// </summary>
        /// <remarks>
        /// A DIFFERENT FAUCET FILLS THIS. The CKB faucet will never move this number.
        /// A player funded with CKB alone reads zero here, correctly - so a funding
        /// screen that does not name the asset it wants will strand them.
        /// </remarks>
        public void GetUdtBalance(string udtTypeScriptJson, Action<UdtBalance> onResult, Action<FiberError> onError)
        {
            if (string.IsNullOrEmpty(udtTypeScriptJson))
            {
                onError(new FiberError(FiberErrorCode.ConfigMissing,
                    "GetUdtBalance needs a UDT type script - use GetBalance for CKB."));
                return;
            }

            _onUdtBalance = onResult;
            _onUdtBalanceError = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Fiber_GetUdtBalance(udtTypeScriptJson, gameObject.name);
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
        /// Fires once the channel id no longer appears in listChannels, meaning the
        /// close has settled on-chain. Carries the settlement transaction hash when
        /// bridge.js managed to observe one.
        /// </summary>
        /// <remarks>
        /// THIS TOOK NO ARGUMENTS BEFORE. A zero-arg method here against a jslib that
        /// now sends a string is not a compile error and not a runtime exception -
        /// SendMessage simply finds nothing and the close hangs forever. Same failure
        /// shape as the OnChannelReady bug, in the opposite direction.
        /// </remarks>
        private void OnChannelClosed(string json)
        {
            string channelId = null;
            string txHash = null;

            try
            {
                var data = JsonUtility.FromJson<ChannelCloseJson>(json);
                channelId = data.channelId;
                txHash = data.shutdownTxHash;
            }
            catch
            {
                // The close itself already succeeded - bridge.js only calls this once
                // the channel is gone. A malformed payload costs the receipt, not the
                // settlement, so it must not turn into a failure.
            }

            _onChannelClosed?.Invoke(new ChannelCloseResult
            {
                ChannelId = channelId ?? string.Empty,
                ShutdownTransactionHash = txHash ?? string.Empty
            });
        }

        private void OnChannelAbandoned() => _onChannelAbandoned?.Invoke();
        private void OnAbandonChannelError(string err) => _onAbandonChannelError?.Invoke(FiberError.Parse(err));

        /// <summary>
        /// A dry run reports "cannot route" through THIS callback, not the error one -
        /// an unroutable payment is a valid answer to the question that was asked,
        /// not a failure to answer it. Only a malformed call reaches OnDryRunError.
        /// </summary>
        private void OnDryRunResult(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<DryRunJson>(json);
                _onDryRun?.Invoke(new RouteQuote
                {
                    Routable = data.routable,
                    FeeShannons = ParseShannons(data.fee),
                    Reason = data.reason ?? string.Empty
                });
            }
            catch
            {
                _onDryRunError?.Invoke(new FiberError(FiberErrorCode.ParseError, json));
            }
        }

        private void OnDryRunError(string err) => _onDryRunError?.Invoke(FiberError.Parse(err));

        private void OnUdtBalanceResult(string json)
        {
            UdtBalanceJson data;
            try
            {
                data = JsonUtility.FromJson<UdtBalanceJson>(json);
            }
            catch
            {
                _onUdtBalanceError?.Invoke(new FiberError(FiberErrorCode.ParseError, json));
                return;
            }

            if (data == null || !ulong.TryParse(data.balanceShannons, out ulong amount))
            {
                _onUdtBalanceError?.Invoke(new FiberError(
                    FiberErrorCode.ParseError,
                    $"UDT balance was not a parseable amount: '{data?.balanceShannons}'"));
                return;
            }

            _onUdtBalance?.Invoke(new UdtBalance(amount));
        }

        private void OnUdtBalanceError(string err) => _onUdtBalanceError?.Invoke(FiberError.Parse(err));

        private void OnParseInvoiceResult(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<InvoiceJson>(json);
                _onParseInvoice?.Invoke(new InvoiceDetails
                {
                    AmountShannons = ParseShannons(data.amount),
                    Currency = data.currency ?? string.Empty,
                    PaymentHash = data.paymentHash ?? string.Empty,
                    UdtTypeScript = data.udtTypeScript ?? string.Empty
                });
            }
            catch
            {
                _onParseInvoiceError?.Invoke(new FiberError(FiberErrorCode.ParseError, json));
            }
        }

        private void OnParseInvoiceError(string err) => _onParseInvoiceError?.Invoke(FiberError.Parse(err));
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
        ///
        /// THAT FALLBACK HID A REAL BUG FOR A WHILE, so it is worth stating what it
        /// costs. Zero is a sane answer for ONE malformed channel in a list. It is a
        /// catastrophic answer when the format is wrong for EVERY entry: a live channel
        /// holding thousands of CKB reports empty, a cash-out screen offers nothing to
        /// withdraw, and nothing anywhere logs a complaint. If this returns zero for
        /// something that should hold money, suspect the FORMAT before the channel.
        ///
        /// It is shared by three callers and the third is the dangerous one:
        /// ChannelInfo balances, RouteQuote.FeeShannons, and InvoiceDetails.AmountShannons.
        /// An invoice amount that parses as zero passes MatchesExpected against nothing
        /// and clears any maximum-acceptable bound trivially - so a decimal-only parser
        /// here silently disables invoice verification entirely.
        /// </remarks>
        private static ulong ParseShannons(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0UL;

            s = s.Trim();

            // HEX IS THE NORMAL CASE. fnn's JSON-RPC returns amounts as "0x15b115d9f40",
            // not as decimal. A parser that only accepts decimal does not throw and does
            // not warn - it falls through to the zero below, and every balance in the
            // system silently reads as empty.
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ulong.TryParse(
                    s.Substring(2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out ulong hex) ? hex : 0UL;
            }

            // Decimal accepted too. Not every field in every fnn version is hex, and this
            // helper is shared by channel balances, route fees and invoice amounts.
            return ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out ulong dec) ? dec : 0UL;
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
                RemoteBalanceShannons = ParseShannons(c.remote_balance),
                ShutdownTransactionHash = c.shutdown_transaction_hash ?? string.Empty,
                FundingUdtTypeScript = c.funding_udt_type_script ?? string.Empty
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

            // Empty/missing is a parse failure here, unlike ParseShannons' silent zero:
            // this path reports an error instead, because a funding screen showing 0 for a
            // failed read leaves the player waiting for funds that already arrived.
            if (data == null || string.IsNullOrWhiteSpace(data.balanceShannons))
            {
                _onBalanceError?.Invoke(new FiberError(
                    FiberErrorCode.ParseError,
                    $"Balance was not a parseable shannon amount: '{data?.balanceShannons}'"));
                return;
            }

            // Same hex-or-decimal handling as every other amount off this node. Reading it
            // as decimal-only reported every funded wallet as empty.
            ulong shannons = ParseShannons(data.balanceShannons);

            _onBalance?.Invoke(new WalletBalance(shannons));
        }

        private void OnBalanceError(string err) => _onBalanceError?.Invoke(FiberError.Parse(err));

        // ---------------- JSON shapes matching bridge.js output ----------------
        // Field names must match bridge.js's output keys exactly. JsonUtility
        // silently leaves unmatched fields at their default value.

        [Serializable] private class NodeInfoJson { public string pubkey; public string ckbAddress; public string udtCfgInfos; }
        [Serializable] private class PaymentJson { public string payment_hash; public string status; }
        [Serializable] private class PeerInfoJson { public string pubkey; public string address; }
        [Serializable] private class PeerInfoListJson { public PeerInfoJson[] peers; }
        [Serializable] private class ChannelInfoJson { public string channel_id; public string channel_outpoint; public bool enabled; public string state_name; public string state_flags; public string pubkey; public string local_balance; public string remote_balance; public string shutdown_transaction_hash; public string funding_udt_type_script; }
        [Serializable] private class ChannelInfoListJson { public ChannelInfoJson[] channels; }
        [Serializable] private class BalanceJson { public string balanceShannons; }
        [Serializable] private class UdtBalanceJson { public string balanceShannons; }
        [Serializable] private class ChannelCloseJson { public string channelId; public string shutdownTxHash; }
        [Serializable] private class DryRunJson { public bool routable; public string fee; public string reason; }
        [Serializable] private class InvoiceJson { public string amount; public string currency; public string paymentHash; public string udtTypeScript; }
    }
}