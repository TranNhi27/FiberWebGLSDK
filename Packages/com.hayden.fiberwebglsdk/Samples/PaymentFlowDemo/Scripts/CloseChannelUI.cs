using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FiberWebGLSDK.Samples {
    /// <summary>
    /// Closes an open channel and settles its balance on-chain.
    ///
    /// A channel is identified by its id alone - a peer may hold several. The
    /// counterparty's pubkey is carried alongside it but is never entered by hand:
    /// the SDK uses it internally to confirm the channel has actually closed, and an
    /// incorrect pubkey would cause a close to be reported as successful when it was not.
    ///
    /// Populate via SetChannel(), typically from a channel row in the diagnostics panel
    /// or from OpenChannelUI.OnChannelOpened.
    /// </summary>
    public class CloseChannelUI : MonoBehaviour {
        [SerializeField] private FiberPaymentGateway gateway;

        [Header("Controls")]
        [SerializeField] private Button closeChannelButton;
        [SerializeField] private TMP_InputField channelIdInput;

        [Tooltip("Force-close without cooperative negotiation. Slower to settle and more " +
                 "expensive; intended for unresponsive peers.")]
        [SerializeField] private Toggle forceToggle;

        [Header("Display")]
        [Tooltip("Display-only. The channel's counterparty, supplied by SetChannel().")]
        [SerializeField] private TMP_Text peerPubkeyText;
        [SerializeField] private CopyToClipboardButton peerPubkeyCopyButton;
        [SerializeField] private TMP_Text statusText;

        private IPaymentGateway Gateway => gateway;

        /// <summary>
        /// The counterparty of the channel currently loaded into this screen. Set by
        /// SetChannel() rather than by user input - see the class summary.
        /// </summary>
        private string _peerPubkey;

        /// <summary>
        /// Raised once the channel is settled on-chain, carrying the closed channel's id.
        /// </summary>
        public event Action<string> OnChannelClosed;

        private void Start() {
            closeChannelButton.onClick.AddListener(OnCloseChannelClicked);

            statusText.text = "";
            Clear();
        }

        private void OnDestroy() {
            closeChannelButton.onClick.RemoveListener(OnCloseChannelClicked);
        }

        /// <summary>Loads a channel listed by IFiberDiagnostics into this screen.</summary>
        public void SetChannel(ChannelInfo channel) {
            SetChannel(channel.ChannelId, channel.PeerPubkey);
        }

        /// <summary>
        /// Loads a channel from its raw ids, for callers without a full ChannelInfo.
        /// OpenChannelUI.OnChannelOpened supplies exactly this pair.
        /// </summary>
        public void SetChannel(string channelId, string peerPubkey) {
            channelIdInput.text = channelId ?? string.Empty;

            _peerPubkey = peerPubkey;
            peerPubkeyText.text = string.IsNullOrEmpty(peerPubkey)
                ? "(no channel selected)"
                : FiberUIFormat.MiddleEllipsis(peerPubkey);

            peerPubkeyCopyButton?.SetFullText(peerPubkey ?? string.Empty);

            statusText.text = "";
        }

        /// <summary>Empties the screen. No channel is loaded until SetChannel() is called.</summary>
        public void Clear() {
            SetChannel(string.Empty, string.Empty);
        }

        private void OnCloseChannelClicked() {
            // Channel ids are hex; whitespace is never significant. Stripped throughout
            // rather than trimmed, as pasted values may contain embedded line breaks.
            string channelId = Regex.Replace(channelIdInput.text, @"\s+", "");

            if (string.IsNullOrEmpty(channelId)) {
                statusText.text = "No channel id.";
                return;
            }

            if (string.IsNullOrEmpty(_peerPubkey)) {
                statusText.text = "No peer for this channel. Select a channel from diagnostics.";
                return;
            }

            closeChannelButton.interactable = false;
            statusText.text = "Closing channel... (on-chain settlement can take a couple of minutes)";

            Gateway.CloseChannel(channelId, _peerPubkey, forceToggle.isOn,
                onClosed: () => {
                    statusText.text = "Channel closed and settled.";
                    closeChannelButton.interactable = true;
                    OnChannelClosed?.Invoke(channelId);
                },
                onError: err => {
                    // Re-enabled to allow a retry. A cooperative close against an offline
                    // peer will time out; retrying with force enabled is the usual recourse.
                    statusText.text = $"Close channel failed: {err}";
                    closeChannelButton.interactable = true;
                });
        }
    }
}