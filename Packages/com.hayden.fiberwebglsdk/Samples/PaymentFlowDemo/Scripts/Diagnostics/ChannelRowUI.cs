using System;
using UnityEngine;
using TMPro;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// A single open channel in the diagnostics list. Clicking the row requests the
    /// channel's actions (pay over it, or close it), carrying the channel it represents.
    /// </summary>
    public class ChannelRowUI : ClickableRowUI
    {
        [Header("Display")]
        [SerializeField] private TMP_Text indexText;
        [SerializeField] private TMP_Text channelIdText;
        [SerializeField] private TMP_Text peerPubkeyText;
        [SerializeField] private TMP_Text stateText;

        private ChannelInfo _channel;

        /// <summary>Raised when this row is clicked, carrying the channel it represents.</summary>
        public event Action<ChannelInfo> OnChannelActionsRequested;

        /// <summary>Binds a channel to this row. Call once after the row is spawned.</summary>
        public void Bind(int displayIndex, ChannelInfo channel)
        {
            _channel = channel;
            indexText.text = displayIndex.ToString();
            channelIdText.text = FiberUIFormat.MiddleEllipsis(channel.ChannelId);
            peerPubkeyText.text = FiberUIFormat.MiddleEllipsis(channel.PeerPubkey);
            stateText.text = channel.StateName;
        }

        protected override void OnRowClicked()
        {
            OnChannelActionsRequested?.Invoke(_channel);
        }
    }
}