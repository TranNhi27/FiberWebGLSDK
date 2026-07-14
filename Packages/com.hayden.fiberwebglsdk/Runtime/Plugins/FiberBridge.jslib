// FiberBridge.jslib
//
// Logic-free shim: converts pointers to strings, calls the matching
// window.FiberBridge method, routes the result back via SendMessage.
// No Fiber logic here - that's in bridge.js.
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

    Fiber_OpenChannel: function(peerPubkeyPtr, fundingAmountHexPtr, isPublic, callbackTargetPtr) {
        var peerPubkey = UTF8ToString(peerPubkeyPtr);
        var fundingAmountHex = UTF8ToString(fundingAmountHexPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.openChannel(
            peerPubkey,
            fundingAmountHex,
            !!isPublic,
            // bridge.js's openChannel poll resolves with the confirmed channel_id.
            // Forward it - CloseChannel needs it later, and re-fetching it via
            // ListChannels would be a wasted round-trip for immutable data.
            function(channelId) { SendMessage(callbackTarget, "OnChannelReady", channelId); },
            function(err) { SendMessage(callbackTarget, "OnChannelError", err); }
        );
    },

    // closeChannel's onClosed takes no args (unlike openChannel's) - it polls
    // until the channel_id disappears from listChannels, so there's nothing to return.
    Fiber_CloseChannel: function(channelIdPtr, peerPubkeyPtr, force, callbackTargetPtr) {
        var channelId = UTF8ToString(channelIdPtr);
        var peerPubkey = UTF8ToString(peerPubkeyPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.closeChannel(
            channelId,
            peerPubkey,
            !!force,
            function() { SendMessage(callbackTarget, "OnChannelClosed"); },
            function(err) { SendMessage(callbackTarget, "OnCloseChannelError", err); }
        );
    },

    Fiber_PayPeer: function(peerPubkeyPtr, amountHexPtr, callbackTargetPtr) {
        var peerPubkey = UTF8ToString(peerPubkeyPtr);
        var amountHex = UTF8ToString(amountHexPtr);
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.payPeer(
            peerPubkey,
            amountHex,
            function(resultJson) { SendMessage(callbackTarget, "OnPaymentSuccess", resultJson); },
            function(err) { SendMessage(callbackTarget, "OnPaymentError", err); }
        );
    },

    Fiber_GetNodeInfo: function(callbackTargetPtr) {
        var callbackTarget = UTF8ToString(callbackTargetPtr);

        window.FiberBridge.getNodeInfo(
            function(json) { SendMessage(callbackTarget, "OnNodeInfoResult", json); },
            function(err) { SendMessage(callbackTarget, "OnNodeInfoError", err); }
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
