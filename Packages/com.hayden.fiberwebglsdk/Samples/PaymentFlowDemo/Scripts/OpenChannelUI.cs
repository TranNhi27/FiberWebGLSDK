using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// Step 2 of the payment flow: funds a payment channel with a connected peer.
    ///
    /// The peer pubkey can arrive two ways: pre-filled via SetPeer() (from
    /// ConnectPeerUI's OnPeerResolved, or a Diagnostics peer row), or typed by hand
    /// - useful when the peer is already connected from an earlier session.
    ///
    /// A hand-typed pubkey is not verified against ListPeers first; OpenChannel will
    /// reject an unconnected or malformed one, surfacing as a ChannelOpenFailed error.
    ///
    /// Emits OnChannelOpened with the confirmed channelId, which CloseChannelUI needs.
    /// </summary>
    public class OpenChannelUI : MonoBehaviour
    {
        [SerializeField] private FiberPaymentGateway gateway;

        [Header("Controls")]
        [SerializeField] private Button openChannelButton;
        [SerializeField] private TMP_InputField peerPubkeyInput;
        [SerializeField] private TMP_InputField fundingAmountInput;
        [SerializeField] private Toggle isPublicToggle;

        [Header("Display")]
        [SerializeField] private TMP_Text statusText;

        [Header("Defaults")]
        [Tooltip("In shannons. The testnet hub enforces a 99 CKB (9,900,000,000 shannon) " +
                 "minimum, but that's hub policy rather than a protocol rule - deliberately " +
                 "not validated client-side.")]
        [SerializeField] private ulong defaultFundingAmountShannons = 10_000_000_000; // 100 CKB

        private IPaymentGateway Gateway => gateway;

        /// Fired once the channel is confirmed open on-chain. Carries the peer's pubkey
        /// and the new channelId - both needed to close or pay over it later.
        public event Action<string, string> OnChannelOpened;

        private void Start()
        {
            openChannelButton.onClick.AddListener(OnOpenChannelClicked);

            fundingAmountInput.text = defaultFundingAmountShannons.ToString();
            statusText.text = "";
        }

        private void OnDestroy()
        {
            openChannelButton.onClick.RemoveListener(OnOpenChannelClicked);
        }

        /// Pre-fills the pubkey field. The user can still edit it afterwards.
        public void SetPeer(PeerInfo peer)
        {
            peerPubkeyInput.text = peer.Pubkey ?? string.Empty;
            statusText.text = "";
        }

        private void OnOpenChannelClicked()
        {
            // Pubkeys are hex - whitespace is never meaningful. Strip all of it, not just
            // the ends: pasting from a wrapped terminal line can leave a space mid-string.
            string peerPubkey = Regex.Replace(peerPubkeyInput.text, @"\s+", "");

            if (string.IsNullOrEmpty(peerPubkey))
            {
                statusText.text = "Enter a peer pubkey.";
                return;
            }

            if (!ulong.TryParse(fundingAmountInput.text, out var fundingAmount))
            {
                statusText.text = "Invalid funding amount.";
                return;
            }

            openChannelButton.interactable = false;
            statusText.text = "Opening channel... (on-chain confirmation can take a couple of minutes)";

            Gateway.OpenChannel(peerPubkey, fundingAmount, isPublicToggle.isOn,
                onChannelReady: channelId =>
                {
                    statusText.text = "Channel open.";
                    openChannelButton.interactable = true;
                    OnChannelOpened?.Invoke(peerPubkey, channelId);
                },
                onError: err =>
                {
                    // Re-enabled rather than dead-ended: a failed open is retryable
                    // (typo'd pubkey, peer not connected, amount below the hub's minimum,
                    // or a timeout on a channel that's still pending on-chain).
                    statusText.text = $"Open channel failed: {err}";
                    openChannelButton.interactable = true;
                });
        }
    }
}