using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;

namespace Terkoiz.Skipper.Server;

/// <summary>
/// HTTP adapter. Decides nothing itself - <see cref="Pricer"/> works out the fee and
/// <see cref="Bank"/> moves the money.
/// </summary>
[Injectable]
public class Callbacks(
    HttpResponseUtil httpResponseUtil,
    Pricer pricer,
    Bank bank,
    EventOutputHolder eventOutputHolder,
    ISptLogger<Callbacks> logger)
{
    public ValueTask<string> Charge(ChargeRequest request, MongoId sessionId)
    {
        var quote = pricer.Quote(sessionId, request);
        var info = WalletInfo.For(quote.Wallet);

        var response = new ChargeResponse
        {
            Charged = quote.Amount,
            Currency = info.Label,
            Symbol = info.Symbol,
            Explanation = quote.Explanation,
        };

        // A quote is a read. It must not touch the profile, so it never reaches the
        // bank's debit path at all.
        if (request.QuoteOnly)
        {
            response.Balance = bank.GetBalance(sessionId, quote.Wallet);
            response.Ok = response.Balance >= quote.Amount;
            response.Message = response.Ok
                ? $"{quote.Amount:N0} {info.Symbol}"
                : $"{quote.Amount:N0} {info.Symbol}가 필요한데 {response.Balance:N0} {info.Symbol} 있습니다.";

            return new ValueTask<string>(httpResponseUtil.NoBody(response));
        }

        // The output MUST come from EventOutputHolder. A hand-built response
        // initialises nothing and RemoveItemByCount reaches straight into
        // output.ProfileChanges[sessionId], so it would throw after the money had
        // already gone.
        var output = eventOutputHolder.GetOutput(sessionId);

        var ok = bank.TryDebit(sessionId, quote.Wallet, quote.Amount, output, out var balance);

        response.Ok = ok;
        response.Balance = balance;
        response.Message = ok
            ? $"{quote.Amount:N0} {info.Symbol} 지불했습니다. ({quote.Explanation})"
            : $"{quote.Amount:N0} {info.Symbol}가 필요한데 {balance:N0} {info.Symbol} 있습니다.";

        logger.Debug(
            $"[Skipper] charge {(ok ? "ok" : "REFUSED")}: {quote.Amount:N0} {info.Label} "
            + $"[{quote.Explanation}] balance now {balance:N0} (session {sessionId})");

        return new ValueTask<string>(httpResponseUtil.NoBody(response));
    }
}

/// <summary>
/// Answers the do-nothing sync action.
///
/// The reply's body is not the point and the client ignores it. What matters is that
/// this is an item-event response at all: SPT holds the profile changes it has made
/// for a session until the client's next item event and hands them over on that reply.
/// Registering the action is what keeps SPT from logging [UNHANDLED EVENT] SkipperSync
/// in red on every skip.
/// </summary>
[Injectable]
public class SyncCallbacks
{
    public ValueTask<ItemEventRouterResponse> Sync(
        PmcData pmcData,
        SkipperSyncRequest body,
        MongoId sessionId,
        ItemEventRouterResponse output) => new(output);
}
