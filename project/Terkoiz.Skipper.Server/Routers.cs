using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.DI.Routing;
using SPTarkov.Server.Core.Utils;

namespace Terkoiz.Skipper.Server;

/// <summary>
/// The plain JSON route the Skip button calls.
///
/// Charging over a static route rather than an item event is deliberate: the money
/// move is the server's business and the client only needs a yes or no. The profile
/// changes it produces are collected separately, by the sync action below.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Routers)]
public class SkipperRouter(JsonUtil jsonUtil, Callbacks callbacks)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<ChargeRequest>(
                "/skipper/charge",
                async (url, info, sessionId, output, cancellationToken) =>
                    await callbacks.Charge(info, sessionId)),
        ]);

/// <summary>
/// The item-event action the client sends once money has moved, so the game collects
/// the profile changes SPT has been holding for it. See <see cref="SyncCallbacks"/>
/// for why a no-op handler is worth registering.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Routers)]
public sealed class SkipperItemEventRouter(SyncCallbacks callbacks)
    : ItemEventRouter([
        new ItemRouteAction<SkipperSyncRequest>(
            SkipperActions.Sync,
            async (url, pmcData, body, sessionID, output, cancellationToken) =>
                await callbacks.Sync(pmcData, body, sessionID, output)),
    ]);
