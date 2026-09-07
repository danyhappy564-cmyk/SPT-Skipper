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

        private const string MainSectionName = "1. 기본";
        private const string CostSectionName = "2. 비용";
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
                "1. 모드 사용",
                true,
                "모드 전체 on/off. 바꾼 뒤에는 퀘스트 창을 다시 열어야 적용됩니다.");

            AlwaysDisplay = Config.Bind(
                MainSectionName,
                "2. SKIP 버튼 항상 표시",
                false,
                "켜면 SKIP 버튼이 항상 보입니다. 끄면 아래 단축키를 누르고 있는 동안만 보입니다.");

            DisplayHotkey = Config.Bind(
                MainSectionName,
                "3. 표시 단축키",
                new KeyboardShortcut(KeyCode.LeftControl),
                "이 키를 누르고 있는 동안 SKIP 버튼이 나타납니다.");

            // Cost. Everything below is sent to the server mod on each skip, so
            // changes take effect immediately - there is nothing to reload.
            FreeSkip = Config.Bind(
                CostSectionName,
                "1. 무료로 스킵",
                false,
                "체크하면 스킵에 비용이 들지 않습니다. 이 섹션의 나머지 설정은 전부 무시되고, "
                + "서버 모드에 연락도 하지 않습니다.");

            Currency = Config.Bind(
                CostSectionName,
                "2. 통화",
                SkipCurrency.Roubles,
                "비용을 어느 화폐로 낼지 정합니다. Roubles = 루블, Dollars = 달러, Euros = 유로.\n"
                + "보상 기준 요금은 루블로 계산한 뒤 핸드북 환율로 환산하므로, 달러로 바꿔도 "
                + "같은 '가치'를 내는 것이지 같은 '숫자'를 내는 것이 아닙니다.");

            PriceFromReward = Config.Bind(
                CostSectionName,
                "3. 퀘스트 보상 기준으로 가격 산정",
                false,
                "끄면 아래 정액 요금이 매번 그대로 나갑니다.\n"
                + "켜면 그 퀘스트가 실제로 주는 것 — 경험치와 보상 아이템의 핸드북 가격 — 으로 "
                + "요금을 계산하므로, 큰 퀘스트일수록 스킵 비용이 비싸집니다.");

            FlatCost = Config.Bind(
                CostSectionName,
                "4. 정액 요금",
                50_000,
                new ConfigDescription(
                    "'퀘스트 보상 기준으로 가격 산정'이 꺼져 있을 때 목표 하나를 스킵하는 비용입니다.\n"
                    + "상인 평판만 주는 퀘스트처럼 값을 매길 수 없는 경우에도 이 값으로 넘어갑니다.",
                    new AcceptableValueRange<int>(0, 50_000_000)));

            RewardPercent = Config.Bind(
                CostSectionName,
                "5. 보상 대비 비율 (%)",
                20f,
                new ConfigDescription(
                    "퀘스트의 목표를 '전부' 스킵했을 때 그 퀘스트 전체 보상의 몇 %를 내는지입니다.\n"
                    + "서버가 이 값을 목표 개수로 나눠서 매기므로, 20%로 두면 목표 6개짜리 퀘스트는 "
                    + "한 단계당 약 3.3%씩 받고 전부 스킵하면 합쳐서 20%가 됩니다.\n"
                    + "'퀘스트 보상 기준으로 가격 산정'이 켜져 있을 때만 사용됩니다.",
                    new AcceptableValueRange<float>(0f, 500f)));

            MinCost = Config.Bind(
                CostSectionName,
                "6. 최소 요금",
                0,
                new ConfigDescription(
                    "보상 기준 요금의 하한선입니다. 보상이 거의 없는 퀘스트가 공짜로 스킵되는 것을 막습니다.",
                    new AcceptableValueRange<int>(0, 50_000_000)));

            MaxCost = Config.Bind(
                CostSectionName,
                "7. 최대 요금",
                0,
                new ConfigDescription(
                    "보상 기준 요금의 상한선입니다. 0이면 상한 없음.",
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
