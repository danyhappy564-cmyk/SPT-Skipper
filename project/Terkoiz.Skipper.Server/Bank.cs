using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;

namespace Terkoiz.Skipper.Server;

/// <summary>
/// Reads what the player has, and takes the skip fee out of it.
///
/// Ported from the debit half of SPT Casino's Bank, which paid for these lessons once
/// already:
///
/// - The response must come from <c>EventOutputHolder.GetOutput</c>. A hand-built
///   <see cref="ItemEventRouterResponse"/> initialises nothing, and RemoveItemByCount
///   reaches straight into <c>output.ProfileChanges[sessionId]</c> - so it throws
///   AFTER the money has already gone.
/// - A debit that half-succeeds is the worst case, so the log says how much may
///   already be missing rather than reporting a bare failure.
/// - The arithmetic disagreeing with the stash afterwards is the most valuable signal
///   there is, so it is checked and shouted about.
/// </summary>
[Injectable]
public class Bank(
    InventoryHelper inventoryHelper,
    ProfileHelper profileHelper,
    ISptLogger<Bank> logger)
{
    /// <summary>
    /// Total of every stack of this currency the profile holds, counting money inside
    /// containers as well as loose in the stash - which is what a player would call
    /// their balance.
    /// </summary>
    public int GetBalance(MongoId sessionId, Wallet wallet)
    {
        var pmcData = profileHelper.GetPmcProfile(sessionId);

        if (pmcData is null)
        {
            logger.Error($"[Skipper] GetBalance: no PMC profile for session '{sessionId}'.");

            return 0;
        }

        return StacksOf(pmcData, WalletInfo.For(wallet).Tpl).Sum(item => item.GetItemStackSize());
    }

    /// <summary>
    /// Takes the fee. Returns false WITHOUT touching anything when the player is
    /// short, so the caller can refuse the skip rather than half-charging for it.
    /// </summary>
    public bool TryDebit(MongoId sessionId, Wallet wallet, int amount, ItemEventRouterResponse output, out int balance)
    {
        balance = 0;

        var pmcData = profileHelper.GetPmcProfile(sessionId);

        if (pmcData is null)
        {
            logger.Error($"[Skipper] TryDebit: no PMC profile for session '{sessionId}'.");

            return false;
        }

        var tpl = WalletInfo.For(wallet).Tpl;
        var before = StacksOf(pmcData, tpl).Sum(item => item.GetItemStackSize());

        balance = before;

        // A zero or negative fee is a free skip, not a failed one.
        if (amount <= 0)
        {
            return true;
        }

        if (before < amount)
        {
            logger.Debug($"[Skipper] debit refused: wanted {amount:N0} {wallet}, player has {before:N0}.");

            return false;
        }

        var remaining = amount;

        // Smallest stacks first, so the stash ends up with fewer loose piles rather
        // than more.
        var stacks = StacksOf(pmcData, tpl).OrderBy(item => item.GetItemStackSize()).ToList();

        foreach (var stack in stacks)
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(remaining, stack.GetItemStackSize());

            try
            {
                inventoryHelper.RemoveItemByCount(pmcData, stack.Id, take, sessionId, output);
            }
            catch (Exception ex)
            {
                // Partial removal may already have happened, so the player could be
                // short with nothing to show for it. Say exactly how much.
                logger.Error(
                    $"[Skipper] RemoveItemByCount threw taking {take:N0} from stack {stack.Id}. "
                    + $"{amount - remaining:N0} of {amount:N0} {wallet} may already be gone.",
                    ex);

                balance = StacksOf(pmcData, tpl).Sum(item => item.GetItemStackSize());

                return false;
            }

            remaining -= take;
        }

        var after = StacksOf(pmcData, tpl).Sum(item => item.GetItemStackSize());

        balance = after;

        if (after != before - amount)
        {
            // InventoryHelper did something other than what was asked, and every
            // balance reported from here on is suspect.
            logger.Error($"[Skipper] debit mismatch: {wallet} is {after:N0} but should be {before - amount:N0}.");
        }

        return remaining == 0;
    }

    private static IEnumerable<Item> StacksOf(PmcData pmcData, MongoId tpl) =>
        pmcData.Inventory?.Items?.Where(item => item.Template == tpl) ?? [];
}
