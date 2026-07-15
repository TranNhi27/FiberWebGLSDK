using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// Read-only diagnostics view over the node's peers and channels, presented as two
    /// tabs. Spawns a row per item and re-broadcasts row clicks outward - it performs no
    /// operations itself. A coordinating script (e.g. PaymentFlowManager) subscribes to
    /// the row events and routes them to the appropriate operation screen.
    ///
    /// Each tab refreshes when it becomes visible and on demand via the Refresh button.
    /// </summary>
    public class FiberDiagnosticsUI : MonoBehaviour
    {
        [SerializeField] private FiberPaymentGateway gateway;

        [Header("Tabs")]
        [SerializeField] private Button peersTabButton;
        [SerializeField] private Button channelsTabButton;
        [SerializeField] private GameObject peersTabContent;
        [SerializeField] private GameObject channelsTabContent;

        [Tooltip("Tint for the selected tab vs the unselected one.")]
        [SerializeField] private Color activeTabColor = new Color(0.20f, 0.60f, 1f);    // blue
        [SerializeField] private Color inactiveTabColor = new Color(0.55f, 0.55f, 0.55f); // grey

        [Header("Peers Tab")]
        [SerializeField] private Transform peersListParent;
        [SerializeField] private PeerRowUI peerRowPrefab;

        [Header("Channels Tab")]
        [SerializeField] private Transform channelsListParent;
        [SerializeField] private ChannelRowUI channelRowPrefab;

        [Header("Shared")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private TMP_Text statusText;

        private IFiberDiagnostics Diagnostics => gateway;

        // Spawned rows, tracked so they can be cleared before each refresh.
        private readonly List<GameObject> _peerRows = new List<GameObject>();
        private readonly List<GameObject> _channelRows = new List<GameObject>();

        private enum Tab { Peers, Channels }
        private Tab _activeTab = Tab.Peers;

        /// <summary>Raised when a peer row is clicked, carrying that peer.</summary>
        public event Action<PeerInfo> OnOpenChannelRequested;

        /// <summary>Raised when a channel row is clicked, carrying that channel.</summary>
        public event Action<ChannelInfo> OnChannelActionsRequested;

        private void Start()
        {
            peersTabButton.onClick.AddListener(() => SwitchTab(Tab.Peers));
            channelsTabButton.onClick.AddListener(() => SwitchTab(Tab.Channels));
            refreshButton.onClick.AddListener(RefreshActiveTab);

            SwitchTab(Tab.Peers);
        }

        private void OnEnable()
        {
            // Refresh whenever the panel becomes visible. Guarded because OnEnable also
            // fires before Start on first activation, when references aren't wired yet.
            if (gateway != null)
                RefreshActiveTab();
        }

        private void OnDestroy()
        {
            peersTabButton.onClick.RemoveAllListeners();
            channelsTabButton.onClick.RemoveAllListeners();
            refreshButton.onClick.RemoveListener(RefreshActiveTab);
        }

        private void SwitchTab(Tab tab)
        {
            _activeTab = tab;

            bool peers = tab == Tab.Peers;
            peersTabContent.SetActive(peers);
            channelsTabContent.SetActive(!peers);

            // Highlight the active tab.
            peersTabButton.image.color = peers ? activeTabColor : inactiveTabColor;
            channelsTabButton.image.color = peers ? inactiveTabColor : activeTabColor;

            RefreshActiveTab();
        }

        private void RefreshActiveTab()
        {
            if (_activeTab == Tab.Peers)
                RefreshPeers();
            else
                RefreshChannels();
        }

        private void RefreshPeers()
        {
            statusText.text = "Loading peers...";
            ClearRows(_peerRows);

            Diagnostics.ListPeers(
                onResult: peers =>
                {
                    for (int i = 0; i < peers.Length; i++)
                    {
                        var row = Instantiate(peerRowPrefab, peersListParent);
                        row.Bind(i + 1, peers[i]);
                        // Forward the row's click outward. The peer is captured by Bind,
                        // so the handler needs no arguments of its own.
                        row.OnOpenChannelRequested += HandleOpenChannelRequested;
                        _peerRows.Add(row.gameObject);
                    }

                    statusText.text = peers.Length == 0
                        ? "No connected peers. Connect to a peer first."
                        : $"{peers.Length} connected peer(s).";
                },
                onError: err => statusText.text = $"Failed to load peers: {err}");
        }

        private void RefreshChannels()
        {
            statusText.text = "Loading channels...";
            ClearRows(_channelRows);

            // Empty pubkey lists channels across all peers (see IFiberDiagnostics /
            // the bridge's listChannels). Closed channels are excluded.
            Diagnostics.ListChannels(string.Empty, includeClosed: false,
                onResult: channels =>
                {
                    for (int i = 0; i < channels.Length; i++)
                    {
                        var row = Instantiate(channelRowPrefab, channelsListParent);
                        row.Bind(i + 1, channels[i]);
                        row.OnChannelActionsRequested += HandleChannelActionsRequested;
                        _channelRows.Add(row.gameObject);
                    }

                    statusText.text = channels.Length == 0
                        ? "No open channels. Open a channel first."
                        : $"{channels.Length} open channel(s).";
                },
                onError: err => statusText.text = $"Failed to load channels: {err}");
        }

        private void HandleOpenChannelRequested(PeerInfo peer)
        {
            OnOpenChannelRequested?.Invoke(peer);
        }

        private void HandleChannelActionsRequested(ChannelInfo channel)
        {
            OnChannelActionsRequested?.Invoke(channel);
        }

        private void ClearRows(List<GameObject> rows)
        {
            foreach (var row in rows)
                Destroy(row);
            rows.Clear();
        }
    }
}