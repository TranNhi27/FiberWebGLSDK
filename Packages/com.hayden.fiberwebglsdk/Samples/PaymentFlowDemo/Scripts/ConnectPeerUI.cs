using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// Step 1 of the payment flow: connects to a peer by multiaddr, then resolves
    /// that peer's pubkey via ListPeers. Does not open a channel or move any funds -
    /// hands the resolved PeerInfo off via OnPeerResolved for OpenChannelUI to use.
    /// </summary>
    public class ConnectPeerUI : MonoBehaviour
    {
        [SerializeField] private FiberPaymentGateway gateway;

        [Header("Controls")]
        [SerializeField] private Button connectButton;
        [SerializeField] private TMP_InputField peerAddressInput;

        [Header("Display")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text resolvedPeerPubkeyText;      // display-only, confirms resolution worked
        [SerializeField] private CopyToClipboardButton pubkeyCopyButton;

        [Header("Defaults")]
        [SerializeField] private string defaultPeerAddress;

        // ConnectPeer's success callback fires once the dial is acknowledged, but
        // the peer isn't necessarily registered in the node's internal peer store
        // yet at that exact instant - ListPeers called immediately after can miss
        // it. Retry a few times with a short delay instead of checking once.
        private const int MaxResolveAttempts = 6;
        private const float ResolveRetryDelaySeconds = 0.75f;

        private int _resolveAttempts;
        private string _connectedPeerAddress;

        private IPaymentGateway Gateway => gateway;
        private IFiberDiagnostics Diagnostics => gateway;

        /// Fired once ConnectPeer + pubkey resolution both succeed.
        public event Action<PeerInfo> OnPeerResolved;

        private void Start()
        {
            connectButton.onClick.AddListener(OnConnectClicked);

            peerAddressInput.text = defaultPeerAddress;
            statusText.text = "";
            resolvedPeerPubkeyText.text = "";
        }

        private void OnDestroy()
        {
            connectButton.onClick.RemoveListener(OnConnectClicked);
        }

        private void OnConnectClicked()
        {
            connectButton.interactable = false;
            statusText.text = "Connecting to peer...";

            // Clear any pubkey resolved by a previous attempt. Leaving a stale
            // one on screen is genuinely dangerous - OpenChannelUI funds a
            // channel against whatever pubkey this step produces.
            resolvedPeerPubkeyText.text = "";

            // A multiaddr should never contain whitespace. Strip all of it, not
            // just leading/trailing - copy-pasting from a wrapped terminal line
            // can leave a stray space embedded mid-string, which .Trim() would miss.
            string peerAddress = Regex.Replace(peerAddressInput.text, @"\s+", "");
            _connectedPeerAddress = peerAddress;

            Gateway.ConnectPeer(peerAddress,
                onConnected: () =>
                {
                    statusText.text = "Connected. Resolving peer pubkey...";
                    _resolveAttempts = 0;
                    TryResolvePeerPubkey();
                },
                onError: err =>
                {
                    statusText.text = $"Connect failed: {err}";
                    connectButton.interactable = true;
                });
        }

        private void TryResolvePeerPubkey()
        {
            _resolveAttempts++;

            Diagnostics.ListPeers(
                onResult: peers =>
                {
                    foreach (var peer in peers)
                    {
                        if (peer.Address == _connectedPeerAddress)
                        {
                            statusText.text = "Connected. Pubkey resolved.";
                            resolvedPeerPubkeyText.text = FiberUIFormat.MiddleEllipsis(peer.Pubkey);
                            pubkeyCopyButton?.SetFullText(peer.Pubkey);   // copies the full pubkey, not the truncated display
                            connectButton.interactable = true;

                            OnPeerResolved?.Invoke(peer);
                            return;
                        }
                    }

                    if (_resolveAttempts < MaxResolveAttempts)
                    {
                        statusText.text = $"Connected. Waiting for peer to register... (attempt {_resolveAttempts}/{MaxResolveAttempts})";
                        StartCoroutine(RetryResolveAfterDelay());
                    }
                    else
                    {
                        statusText.text = "Connected, but peer never appeared in ListPeers after several attempts.";
                        connectButton.interactable = true;
                    }
                },
                onError: err =>
                {
                    statusText.text = $"Connected, but ListPeers failed: {err}";
                    connectButton.interactable = true;
                });
        }

        private IEnumerator RetryResolveAfterDelay()
        {
            yield return new WaitForSeconds(ResolveRetryDelaySeconds);
            TryResolvePeerPubkey();
        }
    }
}