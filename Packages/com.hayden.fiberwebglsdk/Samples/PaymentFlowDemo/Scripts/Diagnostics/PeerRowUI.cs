using System;
using UnityEngine;
using TMPro;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// A single connected peer in the diagnostics list. Clicking the row requests
    /// the "open channel" action for this peer.
    /// </summary>
    public class PeerRowUI : ClickableRowUI
    {
        [Header("Display")]
        [SerializeField] private TMP_Text indexText;
        [SerializeField] private TMP_Text pubkeyText;
        [SerializeField] private TMP_Text addressText;

        private PeerInfo _peer;

        /// <summary>Raised when this row is clicked, carrying the peer it represents.</summary>
        public event Action<PeerInfo> OnOpenChannelRequested;

        /// <summary>Binds a peer to this row. Call once after the row is spawned.</summary>
        public void Bind(int displayIndex, PeerInfo peer)
        {
            _peer = peer;
            indexText.text = displayIndex.ToString();
            pubkeyText.text = FiberUIFormat.MiddleEllipsis(peer.Pubkey);
            addressText.text = FiberUIFormat.MiddleEllipsis(peer.Address);
        }

        protected override void OnRowClicked()
        {
            OnOpenChannelRequested?.Invoke(_peer);
        }
    }
}