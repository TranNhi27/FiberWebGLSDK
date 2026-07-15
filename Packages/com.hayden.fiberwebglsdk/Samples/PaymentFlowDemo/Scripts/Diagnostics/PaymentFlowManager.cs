using UnityEngine;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// Coordinates the payment-flow sample. Shows one screen at a time and passes data
    /// between them, so the screens themselves stay unaware of each other - each raises
    /// an event when its step completes, and this manager decides what happens next.
    ///
    /// Navigation and data handoff are the manager's two jobs; the screens only expose
    /// events (outward) and Set* methods (inward). Nothing here is called by the screens
    /// directly - the dependency points one way, manager to screen.
    /// </summary>
    public class PaymentFlowManager : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] private GameObject nodeStatusPanel;   // always visible; not switched
        [SerializeField] private ConnectPeerUI connectPeerUI;
        [SerializeField] private OpenChannelUI openChannelUI;
        [SerializeField] private PayPeerUI payPeerUI;
        [SerializeField] private CloseChannelUI closeChannelUI;
        [SerializeField] private FiberDiagnosticsUI diagnosticsUI;

        [Header("Shared")]
        [SerializeField] private RowActionPopup rowActionPopup;

        // The set of switchable screens. NodeStatus is deliberately excluded - it's a
        // persistent status panel, not a step.
        private GameObject[] _switchableScreens;

        private void Awake()
        {
            _switchableScreens = new[]
            {
                connectPeerUI.gameObject,
                openChannelUI.gameObject,
                payPeerUI.gameObject,
                closeChannelUI.gameObject,
                diagnosticsUI.gameObject
            };
        }

        private void OnEnable()
        {
            // Screen completion events -> advance the flow.
            connectPeerUI.OnPeerResolved += HandlePeerResolved;
            openChannelUI.OnChannelOpened += HandleChannelOpened;
            payPeerUI.OnPaymentSucceeded += HandlePaymentSucceeded;
            closeChannelUI.OnChannelClosed += HandleChannelClosed;

            // Diagnostics row clicks -> raise an action popup.
            diagnosticsUI.OnOpenChannelRequested += HandlePeerRowClicked;
            diagnosticsUI.OnChannelActionsRequested += HandleChannelRowClicked;
        }

        private void OnDisable()
        {
            connectPeerUI.OnPeerResolved -= HandlePeerResolved;
            openChannelUI.OnChannelOpened -= HandleChannelOpened;
            payPeerUI.OnPaymentSucceeded -= HandlePaymentSucceeded;
            closeChannelUI.OnChannelClosed -= HandleChannelClosed;

            diagnosticsUI.OnOpenChannelRequested -= HandlePeerRowClicked;
            diagnosticsUI.OnChannelActionsRequested -= HandleChannelRowClicked;
        }

        private void Start()
        {
            // Start on the home screen (NodeStatus + the three nav buttons). Operation
            // screens open only when the user chooses one.
            ShowHome();
        }

        // ---------------- Screen completion -> handoff, no forced navigation ----------------
        //
        // None of these switch screens. Each still primes the next screen with the data
        // it produced - via SetPeer/SetChannel - so it's ready to go the moment the user
        // opens it, but the user reads their own success message and leaves on their own
        // terms (via the home nav buttons or a screen's back button), the same as PayPeer.

        // Connected + pubkey resolved -> pre-fill OpenChannel with that peer.
        private void HandlePeerResolved(PeerInfo peer)
        {
            openChannelUI.SetPeer(peer);
        }

        // Channel funded -> pre-fill PayPeer with the channel's peer. The pubkey is
        // carried through directly; a hand-typed pubkey has no PeerInfo, so OpenChannelUI
        // emits the raw pubkey string rather than a PeerInfo.
        private void HandleChannelOpened(string peerPubkey, string channelId)
        {
            payPeerUI.SetPeer(peerPubkey);
        }

        // Deliberately does not navigate away. PayPeerUI shows the payment hash on
        // success, and switching screens immediately would hide it before the user
        // sees it. The user leaves via the screen's own back button when ready - the
        // channel is still open and payable again, so there's no "next step" to force.
        private void HandlePaymentSucceeded(PaymentResult result)
        {
        }

        private void HandleChannelClosed(string channelId)
        {
            ShowScreen(diagnosticsUI.gameObject);
        }

        // ---------------- Diagnostics row clicks -> popup -> screen ----------------

        // A connected peer has one action: open a channel with it.
        private void HandlePeerRowClicked(PeerInfo peer)
        {
            rowActionPopup.Show("Peer actions", new[]
            {
                new RowActionPopup.Action("Open Channel", () =>
                {
                    openChannelUI.SetPeer(peer);
                    ShowScreen(openChannelUI.gameObject);
                })
            });
        }

        // An open channel has two actions: pay over it, or close it.
        private void HandleChannelRowClicked(ChannelInfo channel)
        {
            rowActionPopup.Show("Channel actions", new[]
            {
                new RowActionPopup.Action("Pay", () =>
                {
                    payPeerUI.SetPeer(channel.PeerPubkey);
                    ShowScreen(payPeerUI.gameObject);
                }),
                new RowActionPopup.Action("Close", () =>
                {
                    closeChannelUI.SetChannel(channel);
                    ShowScreen(closeChannelUI.gameObject);
                })
            });
        }

        // ---------------- Navigation ----------------
        // Public so the home-screen buttons and per-screen back buttons can wire to them
        // directly in the Inspector (each is public, void, no args).

        public void ShowConnectPeer() => ShowScreen(connectPeerUI.gameObject);
        public void ShowOpenChannel() => ShowScreen(openChannelUI.gameObject);
        public void ShowPayPeer() => ShowScreen(payPeerUI.gameObject);
        public void ShowCloseChannel() => ShowScreen(closeChannelUI.gameObject);
        public void ShowDiagnostics() => ShowScreen(diagnosticsUI.gameObject);

        /// <summary>Hides every operation screen, returning to the home screen (NodeStatus + nav buttons).</summary>
        public void ShowHome()
        {
            foreach (var s in _switchableScreens)
                s.SetActive(false);
        }

        private void ShowScreen(GameObject screen)
        {
            foreach (var s in _switchableScreens)
                s.SetActive(s == screen);
        }
    }
}