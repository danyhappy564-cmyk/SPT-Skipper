using System;
using Comfort.Common;
using EFT.UI;

namespace Terkoiz.Skipper
{
    /// <summary>
    /// Tells the game to pick up the money the server has just taken.
    ///
    /// The fee is paid over Skipper's own route, and currency moved that way leaves
    /// the profile correctly but leaves the RUNNING GAME none the wiser: the stash on
    /// screen still shows roubles that have already gone.
    ///
    /// That is not only a display fault. The client goes on believing in stacks the
    /// server has deleted, so the next time the player drags one in their stash the
    /// game sends an operation naming an item that is no longer there, and the server
    /// answers "Unable to merge stacks as destination item ... cannot be found".
    ///
    /// SPT holds the profile changes it has made for a session until the client's next
    /// item event and hands them back on that reply. So the fix is not to re-send the
    /// money - it has already moved, correctly - but to give the client a reason to
    /// ask. This sends an item event that does nothing at all, purely so the reply
    /// carries the changes the game then applies to its own inventory.
    /// </summary>
    internal static class ProfileSync
    {
        /// <summary>Must stay byte-identical to the server's SkipperActions.Sync.</summary>
        private const string SyncAction = "SkipperSync";

        /// <summary>
        /// The event body. A public field named exactly as the server reads it: SPT
        /// matches item-event actions case-sensitively, and this is the shape EFT's own
        /// operations take, so the game's serialiser writes it unchanged.
        /// </summary>
        private sealed class SyncOperation
        {
            public string Action = SyncAction;

            public override string ToString() => Action;
        }

        /// <summary>
        /// Asks the game to collect whatever the server has been holding for it. Safe
        /// to call when nothing changed - an empty set of changes applies as nothing.
        /// </summary>
        internal static void Request()
        {
            try
            {
                var session = ItemUiContext.Instance?.ClientSession;

                if (session == null)
                {
                    return;
                }

                session.SendOperationRightNow(new SyncOperation(), new Callback(OnSynced));
            }
            catch (Exception error)
            {
                // Never let this take the skip down with it. The money has already
                // moved; the worst case without it is a stash that reads stale until
                // the game reloads.
                SkipperPlugin.Logger.LogError($"Could not ask the game to resync: {error.Message}");
            }
        }

        private static void OnSynced(IResult result)
        {
            if (result != null && result.Failed)
            {
                SkipperPlugin.Logger.LogWarning(
                    $"The game refused the resync: {result.Error}. The stash may read stale until it reloads.");
            }
        }
    }
}
