// IFiberWallet.cs
//
// On-chain CKB wallet reads. SEPARATE from IPaymentGateway and IFiberDiagnostics
// on purpose: this queries the CKB chain, not the Fiber node. Different system,
// different failure modes.
//
// A funding screen needs this BEFORE any peer is connected or any channel exists -
// the player has to watch funds arrive before there is anything to open a channel
// with. Depending on IFiberWallet alone also means a boot screen literally cannot
// move money, because the methods that do are not on this interface.
using System;
using System.Globalization;

namespace FiberWebGLSDK
{
    public interface IFiberWallet
    {
        /// <summary>
        /// This node's on-chain CKB balance. Available immediately after Initialize -
        /// no peer or channel required.
        /// </summary>
        /// <param name="onError">
        /// Common code: NotInitialized (Initialize has not run, so there is no wallet
        /// address to query yet).
        /// </param>
        void GetBalance(Action<WalletBalance> onResult, Action<FiberError> onError);
    }

    /// <summary>
    /// An on-chain wallet balance. Distinct from a channel's local/remote balance,
    /// which is money already locked inside a channel.
    /// </summary>
    public struct WalletBalance
    {
        public const ulong ShannonsPerCkb = 100_000_000UL;

        /// <summary>Raw balance in shannons. ulong, because CKB amounts overflow double.</summary>
        public ulong Shannons;

        public WalletBalance(ulong shannons) => Shannons = shannons;

        public bool IsAtLeast(ulong requiredShannons) => Shannons >= requiredShannons;

        /// <summary>
        /// "1,234.56" - CKB with two decimals, grouped. Integer maths throughout:
        /// converting to double first would silently lose precision on large balances,
        /// and a funding screen that displays a rounded number is a funding screen the
        /// player cannot reconcile against the faucet.
        /// </summary>
        public string ToCkbString()
        {
            ulong whole = Shannons / ShannonsPerCkb;
            ulong fraction = (Shannons % ShannonsPerCkb) / (ShannonsPerCkb / 100UL);
            return whole.ToString("N0", CultureInfo.InvariantCulture) + "." +
                   fraction.ToString("D2", CultureInfo.InvariantCulture);
        }

        public override string ToString() => ToCkbString() + " CKB";
    }
}
