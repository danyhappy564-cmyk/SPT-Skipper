using System;
using System.Reflection;
using EFT.Quests;
using HarmonyLib;

namespace Terkoiz.Skipper
{
    /// <summary>
    /// Digs the template id out of a live quest.
    ///
    /// The server needs it to look the quest up and price its rewards. EFT does not
    /// expose one obvious member for this and the name has moved between versions, so
    /// rather than hard-code a guess that fails silently on the next patch, this tries
    /// the plausible ones in order and remembers which worked.
    ///
    /// Returning null is a supported outcome, not a failure: the server falls back to
    /// the flat fee whenever the quest id is empty, so a rename here costs reward-based
    /// pricing and nothing else.
    /// </summary>
    internal static class QuestIdentity
    {
        private static bool _resolved;
        private static Func<Quest, string> _reader;

        internal static string Of(Quest quest)
        {
            if (quest == null)
            {
                return string.Empty;
            }

            if (!_resolved)
            {
                Resolve(quest);
            }

            if (_reader == null)
            {
                return string.Empty;
            }

            try
            {
                return _reader(quest) ?? string.Empty;
            }
            catch (Exception error)
            {
                SkipperPlugin.Logger.LogWarning($"Could not read the quest id: {error.Message}");

                return string.Empty;
            }
        }

        private static void Resolve(Quest quest)
        {
            _resolved = true;

            var type = quest.GetType();

            // Direct id on the quest itself.
            foreach (var name in new[] { "Id", "QuestId", "TemplateId" })
            {
                var reader = DirectReader(type, name);

                if (reader != null)
                {
                    _reader = reader;
                    SkipperPlugin.Logger.LogDebug($"Reading the quest id from {type.Name}.{name}");

                    return;
                }
            }

            // Or via the template the quest was built from.
            foreach (var holder in new[] { "Template", "template" })
            {
                var holderMember = AccessTools.Property(type, holder) != null || AccessTools.Field(type, holder) != null;

                if (!holderMember)
                {
                    continue;
                }

                _reader = q =>
                {
                    var template = AccessTools.Property(type, holder)?.GetValue(q)
                        ?? AccessTools.Field(type, holder)?.GetValue(q);

                    if (template == null)
                    {
                        return string.Empty;
                    }

                    var idMember = AccessTools.Property(template.GetType(), "Id");

                    return idMember?.GetValue(template)?.ToString() ?? string.Empty;
                };

                SkipperPlugin.Logger.LogDebug($"Reading the quest id from {type.Name}.{holder}.Id");

                return;
            }

            SkipperPlugin.Logger.LogWarning(
                $"No quest id member found on {type.FullName}. Reward-based pricing will fall back to the flat fee.");
        }

        private static Func<Quest, string> DirectReader(Type type, string name)
        {
            var property = AccessTools.Property(type, name);

            if (property != null && property.CanRead)
            {
                return q => property.GetValue(q)?.ToString();
            }

            var field = AccessTools.Field(type, name);

            return field != null ? new Func<Quest, string>(q => field.GetValue(q)?.ToString()) : null;
        }
    }
}
