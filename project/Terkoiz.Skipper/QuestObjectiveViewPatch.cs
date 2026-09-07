using System;
using System.Linq;
using System.Reflection;
using SPT.Reflection.Patching;
using EFT;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace Terkoiz.Skipper
{
    using Object=UnityEngine.Object;

    public class QuestObjectiveViewPatch : ModulePatch
    {
        private static Type _underlyingQuestControllerType;
        internal static GameObject LastSeenObjectivesBlock;
        
        
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuestObjectiveView), nameof(QuestObjectiveView.Show));
        }

        [PatchPostfix]
        private static void PatchPostfix([CanBeNull]DefaultUIButton ____handoverButton, QuestController questController, Condition condition, Quest quest, QuestObjectiveView __instance)
        {
            if (!SkipperPlugin.ModEnabled.Value)
                return;

            // The handover button is usually only missing in the non-trader task view screens, where we don't want to allow skipping either way
            if (____handoverButton == null)
                return;

            if (_underlyingQuestControllerType == null)
                ResolveQuestControllerClass();

            LastSeenObjectivesBlock = __instance.transform.parent.gameObject;

            var skipButton = Object.Instantiate(____handoverButton, ____handoverButton.transform.parent.transform);

            skipButton.SetRawText("SKIP", 22);
            skipButton.gameObject.name = SkipperPlugin.SkipButtonName;
            skipButton.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().minWidth = 100f;
            skipButton.gameObject.SetActive(SkipperPlugin.AlwaysDisplay.Value && !quest.IsConditionDone(condition));
            
            skipButton.OnClick.RemoveAllListeners();
            skipButton.OnClick.AddListener(() => BeginSkip(skipButton, questController, quest, condition));
        }


        /// <summary>
        /// Asks the server what this skip costs, then puts that number in front of the
        /// player before anything is taken.
        ///
        /// The quote is a separate round trip from the charge on purpose: the
        /// confirmation window has to name a price, and pricing it locally would mean
        /// duplicating the reward maths the server already does - and being wrong about
        /// it the moment the two drift apart.
        /// </summary>
        private static void BeginSkip(DefaultUIButton skipButton, QuestController questController, Quest quest, Condition condition)
        {
            if (SkipperPlugin.FreeSkip.Value)
            {
                Confirm(
                    "이 퀘스트 목표를 즉시 완료 처리할까요?",
                    () => DoSkip(skipButton, questController, quest, condition));

                return;
            }

            var quote = SkipperApi.Quote(QuestIdentity.Of(quest));

            if (quote == null)
            {
                // Failing open would make every skip silently free, which is worse than
                // saying what is wrong.
                Notify(
                    "Skipper가 서버 모드에 연결하지 못해 비용을 청구할 수 없습니다.\n\n"
                    + "terkoiz-skipper-server.dll을 SPT_Runtime\\user\\mods\\Terkoiz.Skipper 에 넣거나, "
                    + "F12에서 '무료로 스킵'을 켜세요.");

                return;
            }

            if (!quote.Ok)
            {
                Notify(quote.Message);

                return;
            }

            Confirm(
                "이 퀘스트 목표를 즉시 완료 처리할까요?\n\n"
                + $"비용 {quote.Charged:N0} {quote.Symbol}  ·  보유 {quote.Balance:N0} {quote.Symbol}",
                () =>
                {
                    // Priced again at the moment of payment rather than trusting the
                    // quote: the player may have spent the money in another window
                    // while the confirmation sat open.
                    var charge = SkipperApi.Charge(QuestIdentity.Of(quest));

                    if (charge == null)
                    {
                        Notify(
                            "Skipper가 서버 모드와의 연결을 잃었습니다. 비용도 청구되지 않았고 스킵도 되지 않았습니다.");

                        return;
                    }

                    if (!charge.Ok)
                    {
                        Notify(charge.Message);

                        return;
                    }

                    SkipperPlugin.Logger.LogInfo($"Skip charged: {charge.Message}");

                    // The money has moved in the profile; this is what makes the
                    // running game notice.
                    ProfileSync.Request();

                    DoSkip(skipButton, questController, quest, condition);
                });
        }


        /// <summary>
        /// A one-button message.
        ///
        /// Deliberately built out of ShowMessageWindow, the same call the original Skip
        /// confirmation used, rather than a warning-specific overload: this is the only
        /// ItemUiContext member this mod has ever proven exists, and a wrong guess here
        /// would throw inside a button handler and read as a dead button.
        /// </summary>
        private static void Notify(string description) =>
            ItemUiContext.Instance.ShowMessageWindow(
                description: description,
                acceptAction: () => { },
                cancelAction: () => { },
                caption: "Skipper");

        private static void Confirm(string description, Action onAccept) =>
            ItemUiContext.Instance.ShowMessageWindow(
                description: description,
                acceptAction: () => onAccept(),
                cancelAction: () => { },
                caption: "확인");

        /// <summary>
        /// The original skip, unchanged. Kept separate so the paid and free paths run
        /// exactly the same completion code.
        /// </summary>
        private static void DoSkip(DefaultUIButton skipButton, QuestController questController, Quest quest, Condition condition)
        {
            if (quest.IsConditionDone(condition))
            {
                skipButton.gameObject.SetActive(false);
                return;
            }

            SkipperPlugin.Logger.LogDebug($"Setting condition {condition.id} value to {condition.value}");

            // This line will force any condition checker to pass, as the 'condition.value' field contains the "goal" of any quest condition
            quest.ProgressCheckers[condition].SetCurrentValueGetter(_ => condition.value);

            SetConditionCurrentValue(questController, quest, condition);

            skipButton.gameObject.SetActive(false);
        }

        private static void ResolveQuestControllerClass()
        {
            _underlyingQuestControllerType = AccessTools.GetTypesFromAssembly(typeof(AbstractGame).Assembly)
                .SingleOrDefault(t =>
                    t.GetEvent(
                    "OnConditionQuestTimeExpired",
                    BindingFlags.DeclaredOnly |
                    BindingFlags.Public |
                    BindingFlags.Instance
                    ) != null
                );

            if (_underlyingQuestControllerType == null)
            {
                SkipperPlugin.Logger.LogError("Failed to locate ConditionsConnectorsManagerClient");
                return;
            }

            SkipperPlugin.Logger.LogDebug($"Resolved underlying quest controller type to {_underlyingQuestControllerType.FullName}");
        }
        
        private static void SetConditionCurrentValue(QuestController questController, Quest quest, Condition condition)
        {
            var conditionControllerField = questController.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(field =>
                    field.FieldType.IsGenericType &&
                    field.FieldType.GetGenericTypeDefinition() == _underlyingQuestControllerType
                );

            if (conditionControllerField == null)
            {
                SkipperPlugin.Logger.LogError($"Failed to locate {_underlyingQuestControllerType.Name} field on {questController.GetType().Name}");
                return;
            }

            var conditionController = conditionControllerField.GetValue(questController);
            AccessTools.Method(conditionController.GetType(), "SetConditionCurrentValue")?.Invoke(
            conditionController,
            [
                quest,
                EQuestStatus.AvailableForFinish,
                condition,
                condition.value,
                true
            ]);
        }
    }
}