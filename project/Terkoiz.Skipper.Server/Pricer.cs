using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace Terkoiz.Skipper.Server;

/// <summary>What a skip costs and how that number was arrived at.</summary>
public sealed record Quote(int Amount, Wallet Wallet, string Explanation);

/// <summary>
/// Works out what one objective is worth skipping.
///
/// Two modes, chosen by the player in F12:
///
/// - <b>Flat</b>: whatever they typed, every time. Nothing here is involved.
/// - <b>By reward</b>: the quest's own payout decides. Experience is converted to
///   money at a configurable rate, reward items are valued at handbook price, the
///   total is scaled by a percentage, and then SPLIT ACROSS THE QUEST'S OBJECTIVES -
///   so skipping every objective of a quest costs that percentage of what the quest
///   pays, rather than that percentage per objective.
///
/// The whole calculation happens in roubles because that is the only currency the
/// handbook quotes, and is converted to the player's chosen wallet at the end through
/// SPT's own <see cref="HandbookHelper.FromRoubles"/>. Doing it the other way round
/// would price a dollar skip off a rouble handbook value and charge about 145 times
/// too much.
/// </summary>
[Injectable]
public class Pricer(
    QuestHelper questHelper,
    HandbookHelper handbookHelper,
    ProfileHelper profileHelper,
    ISptLogger<Pricer> logger)
{
    public Quote Quote(MongoId sessionId, ChargeRequest request)
    {
        var wallet = WalletInfo.Parse(request.Currency);

        if (!request.ByReward)
        {
            return new Quote(Math.Max(0, request.Amount), wallet, "정액");
        }

        var roubles = QuestValueInRoubles(sessionId, request.QuestId, out var objectives, out var detail);

        if (roubles <= 0d)
        {
            // A quest with no priceable reward (pure trader standing, say) would
            // otherwise skip for free and look like the fee had broken. Fall back to
            // the flat amount, which the player has already set.
            logger.Debug($"[Skipper] quest '{request.QuestId}' has no priceable reward - falling back to the flat fee.");

            return new Quote(Math.Max(0, request.Amount), wallet, "정액 · 보상 가치를 매길 수 없는 퀘스트");
        }

        // Split across objectives so the PERCENTAGE is of the whole quest, not of each
        // step. A one-objective quest is unaffected; a six-objective one costs a sixth
        // per step.
        var scaled = roubles * (request.RewardPercent / 100d) / Math.Max(1, objectives);

        var converted = wallet == Wallet.Roubles
            ? scaled
            : handbookHelper.FromRoubles(scaled, WalletInfo.For(wallet).Tpl);

        var clamped = Clamp(converted, request);

        return new Quote(
            clamped,
            wallet,
            $"{detail}, 전체 보상의 {request.RewardPercent:0.#}%를 목표 {objectives}개로 분할");
    }

    /// <summary>
    /// Rouble value of everything the quest pays out on success.
    /// </summary>
    private double QuestValueInRoubles(MongoId sessionId, string? questId, out int objectives, out string detail)
    {
        objectives = 1;
        detail = "퀘스트 정보 없음";

        if (string.IsNullOrWhiteSpace(questId))
        {
            return 0d;
        }

        var pmcData = profileHelper.GetPmcProfile(sessionId);

        if (pmcData is null)
        {
            logger.Error($"[Skipper] pricing: no PMC profile for session '{sessionId}'.");

            return 0d;
        }

        var quest = questHelper.GetQuestFromDb(new MongoId(questId), pmcData);

        if (quest is null)
        {
            logger.Debug($"[Skipper] pricing: quest '{questId}' is not in the database.");

            return 0d;
        }

        objectives = quest.Conditions?.AvailableForFinish?.Count ?? 1;

        // "Success" is the payout branch; Started and Fail are not what a skip buys.
        var rewards = quest.Rewards is not null && quest.Rewards.TryGetValue("Success", out var success)
            ? success
            : [];

        var experience = 0d;
        var items = 0d;

        foreach (var reward in rewards)
        {
            switch (reward.Type)
            {
                case RewardType.Experience:
                    experience += reward.Value ?? 0d;
                    break;

                case RewardType.Item when reward.Items is { Count: > 0 }:
                    items += ItemValue(reward.Items);
                    break;
            }
        }

        detail = $"경험치 {experience:N0} + 아이템 {items:N0}₽";

        return experience * ExperienceToRoubles + items;
    }

    /// <summary>
    /// Handbook value of a reward's items, stack sizes included.
    ///
    /// GetTemplatePriceForItems prices the templates but does not multiply by
    /// StackObjectsCount, so a reward of 10 bitcoin would otherwise be priced as one.
    /// </summary>
    private double ItemValue(List<Item> items) =>
        items.Sum(item => handbookHelper.GetTemplatePrice(item.Template) * Math.Max(1, item.Upd?.StackObjectsCount ?? 1));

    private static int Clamp(double value, ChargeRequest request)
    {
        var floor = Math.Max(0, request.MinAmount);
        var ceiling = request.MaxAmount > 0 ? request.MaxAmount : int.MaxValue;

        if (ceiling < floor)
        {
            (floor, ceiling) = (ceiling, floor);
        }

        var rounded = (long)Math.Round(value, MidpointRounding.AwayFromZero);

        return (int)Math.Clamp(rounded, floor, ceiling);
    }

    /// <summary>
    /// Roubles per point of experience, used to price the XP half of a reward.
    ///
    /// Not a config knob: the player already has a percentage and a min/max to move
    /// the result with, and a third multiplier interacting with those two makes the
    /// number impossible to predict. 100 is a round figure that puts a typical early
    /// quest's XP in the same order as its item reward rather than swamping it.
    /// </summary>
    private const double ExperienceToRoubles = 100d;
}
