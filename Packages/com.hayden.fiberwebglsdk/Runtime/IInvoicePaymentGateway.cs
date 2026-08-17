// IInvoicePaymentGateway.cs
//
// Paying invoices, as opposed to keysend.
//
// WHY THIS IS A SEPARATE INTERFACE FROM IPaymentGateway. The two are not variants
// of one idea. Keysend sends to a pubkey for an amount the SENDER chose. An
// invoice is a signed instruction from the RECEIVER that already carries the
// amount, the destination, the payment hash and an expiry - the sender cannot
// alter any of it without invalidating the signature.
//
// That difference is the entire point. A client that names its own price can name
// one shannon, and no amount of client-side care fixes it, because the client is
// the thing being cheated with. An invoice moves the decision to whoever issued
// it. So this interface is not a convenience wrapper over PayPeer - it is the
// mechanism by which prices stop being the client's business.
//
// The practical consequence for the game: whatever issues the invoice owns the
// price. That is the backend, not Unity. Nothing in this SDK creates invoices,
// deliberately - new_invoice belongs on the server, and putting it here would
// hand the client back the decision this whole interface exists to take away.
using System;

namespace FiberWebGLSDK
{
    /// <summary>
    /// Pays and inspects Fiber invoices.
    /// </summary>
    /// <remarks>
    /// Payment callbacks are shared with <see cref="IPaymentGateway.PayPeer"/> in the
    /// concrete implementation, so a PayInvoice and a PayPeer must not be in flight
    /// simultaneously - the second overwrites the first's callbacks.
    /// </remarks>
    public interface IInvoicePaymentGateway
    {
        /// <summary>
        /// Pays an invoice. The amount comes from the invoice, not from the caller.
        /// </summary>
        /// <param name="invoiceAddress">
        /// The encoded invoice, e.g. "fibt1...". Obtained from whoever is being paid.
        /// </param>
        /// <param name="onSuccess">
        /// Fires once the payment reaches a terminal Success status, not when it is
        /// dispatched.
        /// </param>
        /// <param name="onError">
        /// Common code: PaymentFailed (no route, insufficient balance, invoice expired
        /// or already paid, or never settled within the timeout).
        /// </param>
        void PayInvoice(string invoiceAddress, Action<PaymentResult> onSuccess, Action<FiberError> onError);

        /// <summary>
        /// Decodes an invoice locally, without paying it.
        /// </summary>
        /// <remarks>
        /// CALL THIS BEFORE PayInvoice AND CHECK THE AMOUNT. An invoice reaches the
        /// client over HTTP, and a client that pays whatever it is handed has moved
        /// the trust problem rather than solved it - it now trusts the transport and
        /// the backend absolutely. A wrong environment, a stale endpoint or a
        /// compromised server could issue an invoice for a hundred times the intended
        /// price, and without this check the payment would go through silently.
        ///
        /// Verifying the decoded amount against what the UI displayed costs one local
        /// call and turns that into a refusal.
        /// </remarks>
        void ParseInvoice(string invoiceAddress, Action<InvoiceDetails> onResult, Action<FiberError> onError);
    }

    /// <summary>
    /// The contents of a decoded invoice.
    /// </summary>
    public struct InvoiceDetails
    {
        /// <summary>
        /// The amount this invoice demands. In shannons for a CKB invoice, or in the
        /// token's own units when <see cref="UdtTypeScript"/> is set.
        /// </summary>
        public ulong AmountShannons;

        /// <summary>Network the invoice is for, e.g. "Fibt" on testnet.</summary>
        /// <remarks>
        /// Worth checking. A mainnet invoice against a testnet node fails, and it
        /// fails in a way that reads like a routing problem rather than a
        /// wrong-environment one.
        /// </remarks>
        public string Currency;

        /// <summary>Identifies this payment on the network.</summary>
        public string PaymentHash;

        /// <summary>
        /// The UDT this invoice is denominated in, as a type script JSON, or empty
        /// for CKB.
        /// </summary>
        public string UdtTypeScript;

        public bool IsCkbInvoice => string.IsNullOrEmpty(UdtTypeScript);

        /// <summary>
        /// Whether the invoice demands what the caller expected, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A tolerance rather than equality because a display price and an invoice
        /// amount can legitimately differ by rounding when the two are derived
        /// separately. Pass 0 to require exactness.
        /// </remarks>
        public bool MatchesExpected(ulong expectedShannons, ulong toleranceShannons = 0UL)
        {
            ulong diff = AmountShannons > expectedShannons
                ? AmountShannons - expectedShannons
                : expectedShannons - AmountShannons;

            return diff <= toleranceShannons;
        }
    }
}
