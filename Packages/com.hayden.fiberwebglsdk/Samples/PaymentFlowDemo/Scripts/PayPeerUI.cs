using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FiberWebGLSDK.Samples {
    /// <summary>
    /// Step 3 of the payment flow: sends a keysend payment to a peer.
    ///
    /// Payments route by peer pubkey, not by channel id - Fiber picks a route over
    /// whatever usable channels exist, so no channel needs to be named here. A funded
    /// channel to that peer must already be open (see OpenChannelUI).
    ///
    /// The pubkey can be pre-filled via SetPeer() - from OpenChannelUI's OnChannelOpened,
    /// or a row in the Diagnostics panel - or typed by hand.
    /// </summary>
    public class PayPeerUI : MonoBehaviour {
        [SerializeField] private FiberPaymentGateway gateway;

        [Header("Controls")]
        [SerializeField] private Button payButton;
        [SerializeField] private TMP_InputField peerPubkeyInput;
        [SerializeField] private TMP_InputField paymentAmountInput;

        [Header("Display")]
        [SerializeField] private TMP_Text statusText;

        [Tooltip("The whole PaymentHash panel. Hidden until a payment actually succeeds.")]
        [SerializeField] private GameObject paymentHashPanel;
        [SerializeField] private TMP_Text paymentHashText;
        [SerializeField] private CopyToClipboardButton paymentHashCopyButton;

        [Header("Defaults")]
        [Tooltip("In shannons. 1 CKB = 100,000,000 shannons.")]
        [SerializeField] private ulong defaultPaymentAmountShannons = 50_000_000; // 0.5 CKB

        private IPaymentGateway Gateway => gateway;

        /// Fired once a payment reaches a terminal Success status.
        public event Action<PaymentResult> OnPaymentSucceeded;

        private void Start() {
            payButton.onClick.AddListener(OnPayClicked);

            paymentAmountInput.text = defaultPaymentAmountShannons.ToString();
            statusText.text = "";

            ShowPaymentHash(null); // nothing paid yet - panel starts hidden
        }

        private void OnDestroy() {
            payButton.onClick.RemoveListener(OnPayClicked);
        }

        /// Pre-fills the pubkey field. The user can still edit it afterwards.
        public void SetPeer(string peerPubkey) {
            peerPubkeyInput.text = peerPubkey ?? string.Empty;
            statusText.text = "";

            ShowPaymentHash(null); // switching peers invalidates the last result
        }

        private void OnPayClicked() {
            // Pubkeys are hex - whitespace is never meaningful. Strip all of it, not just
            // the ends: pasting from a wrapped terminal line can leave a space mid-string.
            string peerPubkey = Regex.Replace(peerPubkeyInput.text, @"\s+", "");

            if (string.IsNullOrEmpty(peerPubkey)) {
                statusText.text = "Enter a peer pubkey.";
                return;
            }

            if (!ulong.TryParse(paymentAmountInput.text, out var amount)) {
                statusText.text = "Invalid payment amount.";
                return;
            }

            payButton.interactable = false;
            statusText.text = "Sending payment...";

            ShowPaymentHash(null); // hide the previous result while this one is in flight

            Gateway.PayPeer(peerPubkey, amount,
                onSuccess: result => {
                    statusText.text = "Payment sent.";
                    ShowPaymentHash(result.PaymentHash);

                    payButton.interactable = true;
                    OnPaymentSucceeded?.Invoke(result);
                },
                onError: err => {
                    // Retryable: no route (channel not funded or not ready yet), amount
                    // exceeds the channel balance, or the payment never reached a terminal
                    // status within the timeout. The hash panel stays hidden.
                    statusText.text = $"Payment failed: {err}";
                    payButton.interactable = true;
                });
        }

        /// Shows the hash panel with a value, or hides it entirely when there is nothing
        /// to show. A hash from a previous payment must never be visible while a new one
        /// is in flight or has failed - it would read as if this payment had succeeded.
        ///
        /// Note PaymentHash can be null even on success, if the node's response could not
        /// be parsed. The payment still succeeded, so that is reported - there is just no
        /// hash to display.
        private void ShowPaymentHash(string paymentHash) {
            bool hasHash = !string.IsNullOrEmpty(paymentHash);
            paymentHashPanel.SetActive(hasHash);

            if (!hasHash) return;

            paymentHashText.text = FiberUIFormat.MiddleEllipsis(paymentHash);
            paymentHashCopyButton?.SetFullText(paymentHash);
        }
    }
}