using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Utils;

namespace Terkoiz.Skipper.Server;

/// <summary>
/// Action names shared with the client. Item-event actions are matched
/// case-sensitively by SPT, so these strings must stay byte-identical to the ones the
/// plugin sends.
/// </summary>
public static class SkipperActions
{
    public const string Sync = "SkipperSync";
}

/// <summary>
/// Asks for the fee to be taken.
///
/// PascalCase property names, deliberately. SPT binds request bodies case-sensitively,
/// so lowercase keys bind nothing and every field silently takes its default - which
/// is how a 50,000 fee arrives as 0 while looking like it bound correctly.
/// </summary>
public record ChargeRequest : IRequestData
{
    [JsonPropertyName("Currency")]
    public string? Currency { get; set; }

    /// <summary>The flat fee, and the fallback whenever a quest cannot be priced.</summary>
    [JsonPropertyName("Amount")]
    public int Amount { get; set; }

    /// <summary>Price this skip off the quest's own rewards instead of the flat fee.</summary>
    [JsonPropertyName("ByReward")]
    public bool ByReward { get; set; }

    /// <summary>Which quest is being skipped. Only read when <see cref="ByReward"/> is set.</summary>
    [JsonPropertyName("QuestId")]
    public string? QuestId { get; set; }

    /// <summary>Percentage of the quest's whole payout that skipping all of it costs.</summary>
    [JsonPropertyName("RewardPercent")]
    public double RewardPercent { get; set; }

    [JsonPropertyName("MinAmount")]
    public int MinAmount { get; set; }

    /// <summary>Zero means no ceiling.</summary>
    [JsonPropertyName("MaxAmount")]
    public int MaxAmount { get; set; }

    /// <summary>
    /// Work out the price and report it, but do not take anything. The confirmation
    /// window asks for this so it can name the fee before the player agrees to it.
    /// </summary>
    [JsonPropertyName("QuoteOnly")]
    public bool QuoteOnly { get; set; }
}

/// <summary>
/// The reply to a charge. <see cref="Ok"/> is the only thing the client branches on;
/// everything else is there so the confirmation window can explain itself.
/// </summary>
public record ChargeResponse
{
    [JsonPropertyName("Ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("Charged")]
    public int Charged { get; set; }

    [JsonPropertyName("Balance")]
    public int Balance { get; set; }

    [JsonPropertyName("Currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("Symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("Message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>How the amount was arrived at, for the log and the confirm window.</summary>
    [JsonPropertyName("Explanation")]
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>
/// Body of the do-nothing item event the client sends after money has moved.
///
/// SPT holds the profile changes it has made for a session until the client's next
/// item event and hands them over on that reply, so this exists purely to give the
/// game a reason to ask. Without it the client goes on believing in stacks the server
/// has already deleted, and the next drag in the stash fails with "Unable to merge
/// stacks as destination item ... cannot be found".
/// </summary>
public record SkipperSyncRequest : BaseInteractionRequestData;
