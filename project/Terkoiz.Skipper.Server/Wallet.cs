using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;

namespace Terkoiz.Skipper.Server;

/// <summary>Which currency a skip is paid in.</summary>
public enum Wallet
{
    Roubles,
    Dollars,
    Euros,
}

/// <summary>
/// The template id and display text behind each currency.
///
/// This exists rather than a call into SPT's PaymentService because both of that
/// service's entry points derive the currency from a trader, so neither can settle
/// anything denominated in dollars or euros. Walking the player's own stacks is the
/// only route that handles all three.
/// </summary>
public sealed record WalletInfo(Wallet Wallet, MongoId Tpl, string Symbol, string Label)
{
    private static readonly Dictionary<Wallet, WalletInfo> Table = new()
    {
        [Wallet.Roubles] = new(Wallet.Roubles, Money.ROUBLES, "₽", "roubles"),
        [Wallet.Dollars] = new(Wallet.Dollars, Money.DOLLARS, "$", "dollars"),
        [Wallet.Euros] = new(Wallet.Euros, Money.EUROS, "€", "euros"),
    };

    public static WalletInfo For(Wallet wallet) => Table[wallet];

    public static IEnumerable<WalletInfo> All => Table.Values;

    /// <summary>
    /// Parses what the client sent. Unknown or missing text falls back to roubles
    /// rather than failing the charge - a config the player cannot spell should not
    /// cost them a free skip, and the reply names what was actually charged.
    /// </summary>
    public static Wallet Parse(string? text) =>
        Enum.TryParse(text, ignoreCase: true, out Wallet parsed) && Table.ContainsKey(parsed)
            ? parsed
            : Wallet.Roubles;
}
