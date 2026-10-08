// FiberBridge.jslib
//
// Logic-free shim: converts pointers to strings, calls the matching
// window.FiberBridge method, routes the result back via SendMessage.
// No Fiber logic here - that's in bridge.js.
//
// EVERY CALLBACK ARGUMENT MATTERS. Unity's SendMessage silently DROPS an
// undefined argument, turning a one-arg call into a zero-arg one - which
// arrives in C# as "no such method" and reads exactly like the operation
// never completed. That is what made every successful channel open look
// like a timeout for several sessions. When adding a callback here, check
// that bridge.js actually passes a value and that the C# method takes one.
mergeInto(LibraryManager.library, {

    Fiber_Initialize: function(configTextPtr, callbackTargetPtr) {
        var configText = UTF8ToString(configTextPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.initialize(
            configText,
            function() { SendMessage(callbackTarget, "OnFiberReady"); },
            function(err) { SendMessage(callbackTarget, "OnFiberError", err); }
        );
    },

    Fiber_ConnectPeer: function(peerAddressPtr, callbackTargetPtr) {
        var peerAddress = UTF8ToString(peerAddressPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.connectPeer(
            peerAddress,
            function() { SendMessage(callbackTarget, "OnPeerConnected"); },
            function(err) { SendMessage(callbackTarget, "OnPeerConnectError", err); }
        );
    },

    // udtScriptPtr is an EMPTY STRING for a plain CKB channel, or a JSON
    // { code_hash, hash_type, args } to fund with a UDT instead. One entry
    // point for both assets rather than two that drift apart.
    Fiber_OpenChannel: function(peerPubkeyPtr, fundingAmountHexPtr, udtScriptPtr, isPublic, callbackTargetPtr) {
        var peerPubkey = UTF8ToString(peerPubkeyPtr);
        var fundingAmountHex = UTF8ToString(fundingAmountHexPtr);
        var udtScript = UTF8ToString(udtScriptPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.openChannel(
            peerPubkey,
            fundingAmountHex,
            udtScript,
            !!isPublic,
            function(channelId) { SendMessage(callbackTarget, "OnChannelReady", channelId); },
            function(err) { SendMessage(callbackTarget, "OnChannelError", err); }
        );
    },

    // onClosed NOW CARRIES A PAYLOAD - { channelId, shutdownTxHash } - where it
    // previously took no arguments. The C# handler must accept a string, or
    // SendMessage will not find it and the close will look like it hung.
    Fiber_CloseChannel: function(channelIdPtr, peerPubkeyPtr, force, callbackTargetPtr) {
        var channelId = UTF8ToString(channelIdPtr);
        var peerPubkey = UTF8ToString(peerPubkeyPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.closeChannel(
            channelId,
            peerPubkey,
            !!force,
            function(json) { SendMessage(callbackTarget, "OnChannelClosed", json); },
            function(err) { SendMessage(callbackTarget, "OnCloseChannelError", err); }
        );
    },

    Fiber_AbandonChannel: function(channelIdPtr, callbackTargetPtr) {
        var channelId = UTF8ToString(channelIdPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.abandonChannel(
            channelId,
            function() { SendMessage(callbackTarget, "OnChannelAbandoned"); },
            function(err) { SendMessage(callbackTarget, "OnAbandonChannelError", err); }
        );
    },

    // Empty udtScriptPtr = CKB. Non-empty = pay that UDT, routed only over
    // channels funded with the same one.
    Fiber_PayPeer: function(peerPubkeyPtr, amountHexPtr, udtScriptPtr, callbackTargetPtr) {
        var peerPubkey = UTF8ToString(peerPubkeyPtr);
        var amountHex = UTF8ToString(amountHexPtr);
        var udtScript = UTF8ToString(udtScriptPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.payPeer(
            peerPubkey,
            amountHex,
            udtScript,
            function(resultJson) { SendMessage(callbackTarget, "OnPaymentSuccess", resultJson); },
            function(err) { SendMessage(callbackTarget, "OnPaymentError", err); }
        );
    },

    // Shares OnPaymentSuccess/OnPaymentError with Fiber_PayPeer, deliberately -
    // the C# side stores ONE callback pair for both, so a PayInvoice and a
    // PayPeer must never be in flight at the same time; the second overwrites
    // the first's callbacks and the first payment reports nothing.
    //
    // A separate entry point rather than a parameter on Fiber_PayPeer because
    // there is no amount to pass. Keysend sends what the SENDER chose; an
    // invoice already carries the amount, signed by the receiver. This function
    // having nowhere to put an amount is the entire point of it existing.
    Fiber_PayInvoice: function(invoicePtr, callbackTargetPtr) {
        var invoice = UTF8ToString(invoicePtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.payInvoice(
            invoice,
            function(resultJson) { SendMessage(callbackTarget, "OnPaymentSuccess", resultJson); },
            function(err) { SendMessage(callbackTarget, "OnPaymentError", err); }
        );
    },

    // Decodes an invoice locally. MOVES NOTHING - this is the check that runs
    // before paying, so the client can refuse an invoice whose amount is not
    // the one the server said and the UI displayed.
    //
    // The C# side reads amount, currency, paymentHash and udtTypeScript off this
    // JSON BY NAME. JsonUtility matches on field name and silently zeroes
    // anything absent, so a renamed or missing key here becomes an invoice that
    // reads as costing zero shannons - which passes every amount check written
    // against it. Verify bridge.js emits all four before trusting a green run.
    Fiber_ParseInvoice: function(invoicePtr, callbackTargetPtr) {
        var invoice = UTF8ToString(invoicePtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.parseInvoice(
            invoice,
            function(json) { SendMessage(callbackTarget, "OnParseInvoiceResult", json); },
            function(err) { SendMessage(callbackTarget, "OnParseInvoiceError", err); }
        );
    },

    // Note the shape: bridge.js reports "cannot route" through onResult, not
    // onError, because that is an ANSWER to the question rather than a failure
    // to answer it. Only a malformed call reaches the error callback.
    Fiber_DryRunPayment: function(peerPubkeyPtr, amountHexPtr, udtScriptPtr, callbackTargetPtr) {
        var peerPubkey = UTF8ToString(peerPubkeyPtr);
        var amountHex = UTF8ToString(amountHexPtr);
        var udtScript = UTF8ToString(udtScriptPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.dryRunPayment(
            peerPubkey,
            amountHex,
            udtScript,
            function(json) { SendMessage(callbackTarget, "OnDryRunResult", json); },
            function(err) { SendMessage(callbackTarget, "OnDryRunError", err); }
        );
    },

    // getNodeInfo's failure routes to OnFiberError, the generic handler, rather
    // than a dedicated one. Not an oversight - keep it. The C# side has no
    // OnNodeInfoError method, and inventing a "better" name here would send the
    // error to a method that does not exist, where SendMessage drops it silently
    // and the read looks like it hung forever.
    Fiber_GetNodeInfo: function(callbackTargetPtr) {
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.getNodeInfo(
            function(json) { SendMessage(callbackTarget, "OnNodeInfoResult", json); },
            function(err) { SendMessage(callbackTarget, "OnFiberError", err); }
        );
    },

    Fiber_GetBalance: function(callbackTargetPtr) {
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.getBalance(
            function(json) { SendMessage(callbackTarget, "OnBalanceResult", json); },
            function(err) { SendMessage(callbackTarget, "OnBalanceError", err); }
        );
    },

    // Separate entry point from Fiber_GetBalance, not a parameter on it. The two
    // read different things by different means - CKB capacity comes from the
    // chain client's own sum, a UDT amount has to be collected out of cell data -
    // and a caller asking for one has no use for the other.
    Fiber_GetUdtBalance: function(udtScriptPtr, callbackTargetPtr) {
        var udtScript = UTF8ToString(udtScriptPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.getUdtBalance(
            udtScript,
            function(json) { SendMessage(callbackTarget, "OnUdtBalanceResult", json); },
            function(err) { SendMessage(callbackTarget, "OnUdtBalanceError", err); }
        );
    },

    Fiber_ListPeers: function(callbackTargetPtr) {
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.listPeers(
            function(json) { SendMessage(callbackTarget, "OnListPeersResult", json); },
            function(err) { SendMessage(callbackTarget, "OnListPeersError", err); }
        );
    },

    Fiber_ListChannels: function(peerPubkeyPtr, includeClosed, callbackTargetPtr) {
        var peerPubkey = UTF8ToString(peerPubkeyPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.listChannels(
            peerPubkey,
            !!includeClosed,
            function(json) { SendMessage(callbackTarget, "OnListChannelsResult", json); },
            function(err) { SendMessage(callbackTarget, "OnListChannelsError", err); }
        );
    }

});
