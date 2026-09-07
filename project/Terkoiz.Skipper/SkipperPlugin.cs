using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT.UI;
using JetBrains.Annotations;
using UnityEngine;

namespace Terkoiz.Skipper
{
    /// <summary>
    /// Names must match the server's Wallet enum, which parses them case-insensitively
    /// out of the request body.
    /// </summary>
    public enum SkipCurrency
    {
        Roubles,
        Dollars,
        Euros,
    }

    [BepInPlugin("com.terkoiz.skipper", "Terkoiz.Skipper", "1.1.5")]
    public class SkipperPlugin : BaseUnityPlugin
    {
        internal const string SkipButtonName = "SkipButton";

        internal new static ManualLogSource Logger { get; private set; }

        private const string MainSectionName = "Main";
        private const string CostSectionName = "Cost";
        internal static ConfigEntry<bool> ModEnabled;
        internal static ConfigEntry<bool> AlwaysDisplay;
        internal static ConfigEntry<KeyboardShortcut> DisplayHotkey;

        internal static ConfigEntry<bool> FreeSkip;
        internal static ConfigEntry<SkipCurrency> Currency;
        internal static ConfigEntry<bool> PriceFromReward;
        internal static ConfigEntry<int> FlatCost;
        internal static ConfigEntry<float> RewardPercent;
        internal static ConfigEntry<int> MinCost;
        internal static ConfigEntry<int> MaxCost;

        [UsedImplicitly]
        internal void Start()
        {
            Logger = base.Logger;
            InitConfiguration();

            new QuestObjectiveViewPatch().Enable();
        }

        private void InitConfiguration()
        {
            ModEnabled = Config.Bind(
                MainSectionName,
                "1. Enabled",
                true,
                "Global mod toggle. Will need to re-open the quest window for the setting change to take effect.");

            AlwaysDisplay = Config.Bind(
                MainSectionName,
                "2. Always display Skip button",
                false,
                "If enabled, the Skip button will always be visible.");

            DisplayHotkey = Config.Bind(
                MainSectionName,
                "3. Display hotkey",
                new KeyboardShortcut(KeyCode.LeftControl),
                "Holding down this key will make the Skip buttons appear.");

            // Cost. Everything below is sent to the server mod on each skip, so
            // changes take effect immediately - there is nothing to reload.
            FreeSkip = Config.Bind(
                CostSectionName,
                "1. Skip for free",
                false,
                "Tick this and skipping costs nothing. Everything else in this section is ignored, "
                + "and the server mod is not contacted at all.");

            Currency = Config.Bind(
                CostSectionName,
                "2. Currency",
                SkipCurrency.Roubles,
                "Which currency the fee is taken in. Reward-based prices are worked out in roubles "
                + "and converted at the handbook rate, so switching to dollars charges the same value, "
                + "not the same number.");

            PriceFromReward = Config.Bind(
                CostSectionName,
                "3. Price from quest reward",
                false,
                "Off: every skip costs the flat fee below. On: the fee is worked out from what the "
                + "quest actually pays - its experience plus the handbook value of its reward items - "
                + "so a big quest costs more to skip than a small one.");

            FlatCost = Config.Bind(
                CostSectionName,
                "4. Flat fee",
                50_000,
                new ConfigDescription(
                    "What one objective costs to skip when 'Price from quest reward' is off. Also the "
                    + "fallback when a quest has no priceable reward, such as one that only pays trader "
                    + "standing.",
                    new AcceptableValueRange<int>(0, 50_000_000)));

            RewardPercent = Config.Bind(
                CostSectionName,
                "5. Reward price percent",
                20f,
                new ConfigDescription(
                    "Percentage of a quest's whole payout that skipping ALL of its objectives costs. "
                    + "The server splits it across the objectives, so at 20% a six-objective quest "
                    + "charges roughly 3.3% per step and skipping the lot costs 20%. Only used when "
                    + "'Price from quest reward' is on.",
                    new AcceptableValueRange<float>(0f, 500f)));

            MinCost = Config.Bind(
                CostSectionName,
                "6. Minimum fee",
                0,
                new ConfigDescription(
                    "Floor for a reward-based fee, so a near-worthless quest is not free.",
                    new AcceptableValueRange<int>(0, 50_000_000)));

            MaxCost = Config.Bind(
                CostSectionName,
                "7. Maximum fee",
                0,
                new ConfigDescription(
                    "Ceiling for a reward-based fee. 0 means no ceiling.",
                    new AcceptableValueRange<int>(0, 50_000_000)));
        }

        [UsedImplicitly]
        internal void Update()
        {
            if (!ModEnabled.Value || AlwaysDisplay.Value)
            {
                return;
            }

            if (QuestObjectiveViewPatch.LastSeenObjectivesBlock == null || !QuestObjectiveViewPatch.LastSeenObjectivesBlock.activeSelf)
            {
                return;
            }

            if (DisplayHotkey.Value.IsDown())
            {
                ChangeButtonVisibility(true);
            }

            if (DisplayHotkey.Value.IsUp())
            {
                ChangeButtonVisibility(false);
            }
        }

        private static void ChangeButtonVisibility(bool setVisibilityTo)
        {
            foreach (var button in QuestObjectiveViewPatch.LastSeenObjectivesBlock.GetComponentsInChildren<DefaultUIButton>(includeInactive: true))
            {
                if (button.name != SkipButtonName) continue;

                button.gameObject.SetActive(setVisibilityTo);
            }
        }
    }
}
