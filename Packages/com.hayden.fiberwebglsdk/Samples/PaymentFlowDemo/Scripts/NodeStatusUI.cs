using UnityEngine;
using TMPro;
using FiberWebGLSDK;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// Displays the local node's own state: boot status, pubkey, and CKB address.
    /// Owns nothing to do with peers, channels, or payments.
    /// </summary>
    public class NodeStatusUI : MonoBehaviour
    {
        [SerializeField] private FiberPaymentGateway gateway;

        [Header("Display")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text nodePubkeyText;
        [SerializeField] private TMP_Text nodeAddressText;

        [Header("Copy Buttons")]
        [SerializeField] private CopyToClipboardButton pubkeyCopyButton;
        [SerializeField] private CopyToClipboardButton addressCopyButton;

        private IPaymentGateway Gateway => gateway;

        private void Start()
        {
            statusText.text = "Initializing...";
            nodePubkeyText.text = "";
            nodeAddressText.text = "";

            Gateway.Initialize(
                onReady: OnReady,
                onError: err => statusText.text = $"Init failed: {err}");
        }

        private void OnReady()
        {
            statusText.text = "Node ready.";

            Gateway.GetNodeInfo(
                onResult: info =>
                {
                    nodePubkeyText.text = FiberUIFormat.MiddleEllipsis(info.Pubkey);
                    nodeAddressText.text = FiberUIFormat.MiddleEllipsis(info.CkbAddress);

                    pubkeyCopyButton?.SetFullText(info.Pubkey);
                    addressCopyButton?.SetFullText(info.CkbAddress);
                },
                // The node booted fine but we couldn't read its identity - a distinct
                // failure from init failing, and previously indistinguishable from it.
                onError: err => statusText.text = $"Node ready, but node info failed: {err}");
        }
    }
}