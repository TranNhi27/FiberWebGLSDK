# Changelog

### Added
- `IFiberWallet`: `GetBalance` for the node's on-chain CKB balance.
- `IInvoicePaymentGateway`: `ParseInvoice` and `PayInvoice`.
- `IUdtPaymentGateway`: `OpenUdtChannel`, `PayPeerUdt`, `GetUdtBalance`,
  `DryRunPayment`, `AbandonChannel`.
- `CloseChannel` overload that returns the settlement transaction hash.
- `ChannelInfo`: `LocalBalanceShannons`, `RemoteBalanceShannons`,
  `ShutdownTransactionHash`, `FundingUdtTypeScript`.

### Fixed
- Amounts returned in hex (`"0x..."`) were read as 0. Hex and decimal are both parsed now.

### Changed
- Removed the ×100 funding compensation from `OpenChannel`. Amounts are sent as given.
- All runtime types are in the `FiberWebGLSDK` namespace.

