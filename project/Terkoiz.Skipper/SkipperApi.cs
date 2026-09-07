using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;

namespace Terkoiz.Skipper
{
    /// <summary>What the server said a skip costs, and whether it took the money.</summary>
    internal sealed class ChargeResult
    {
        internal bool Ok;
        internal int Charged;
        internal int Balance;
        internal string Symbol = string.Empty;
        internal string Message = string.Empty;
    }

    /// <summary>
    /// Talks to the server half.
    ///
    /// Everything goes through SPT's own <see cref="RequestHandler"/>, which already
    /// knows the backend address, attaches the session cookie, speaks HTTPS to the
    /// self-signed certificate and handles the framing the listener expects.
    ///
    /// PascalCase body keys, deliberately: SPT binds request bodies case-sensitively,
    /// so lowercase keys bind nothing and every field silently takes its default -
    /// which is how a 50,000 fee arrives as 0 while looking like it bound correctly.
    /// </summary>
    internal static class SkipperApi
    {
        /// <summary>
        /// Asks what the skip would cost without paying for it. Used to fill in the
        /// confirmation window before the player agrees to anything.
        /// </summary>
        internal static ChargeResult Quote(string questId) => Send(questId, quoteOnly: true);

        /// <summary>Takes the fee. Returns null when the server could not be reached.</summary>
        internal static ChargeResult Charge(string questId) => Send(questId, quoteOnly: false);

        private static ChargeResult Send(string questId, bool quoteOnly)
        {
            var body =
                "{\"Currency\":\"" + SkipperPlugin.Currency.Value + "\""
                + ",\"Amount\":" + Num(SkipperPlugin.FlatCost.Value)
                + ",\"ByReward\":" + (SkipperPlugin.PriceFromReward.Value ? "true" : "false")
                + ",\"QuestId\":\"" + Escape(questId) + "\""
                + ",\"RewardPercent\":" + Num(SkipperPlugin.RewardPercent.Value)
                + ",\"MinAmount\":" + Num(SkipperPlugin.MinCost.Value)
                + ",\"MaxAmount\":" + Num(SkipperPlugin.MaxCost.Value)
                + ",\"QuoteOnly\":" + (quoteOnly ? "true" : "false")
                + "}";

            try
            {
                var raw = RequestHandler.PostJson("/skipper/charge", body);

                if (string.IsNullOrEmpty(raw))
                {
                    SkipperPlugin.Logger.LogError("The server returned nothing for /skipper/charge.");

                    return null;
                }

                var json = JObject.Parse(raw);

                return new ChargeResult
                {
                    Ok = json.Value<bool?>("Ok") ?? false,
                    Charged = json.Value<int?>("Charged") ?? 0,
                    Balance = json.Value<int?>("Balance") ?? 0,
                    Symbol = json.Value<string>("Symbol") ?? string.Empty,
                    Message = json.Value<string>("Message") ?? string.Empty,
                };
            }
            catch (Exception error)
            {
                SkipperPlugin.Logger.LogError(
                    $"Could not reach the Skipper server mod: {error.Message}. "
                    + "Is terkoiz-skipper-server.dll in SPT_Runtime\\user\\mods\\Terkoiz.Skipper?");

                return null;
            }
        }

        /// <summary>
        /// Invariant formatting, so a machine with a comma decimal separator does not
        /// send a number the server's parser rejects.
        /// </summary>
        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Num(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Escape(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
