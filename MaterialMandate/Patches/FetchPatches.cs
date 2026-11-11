using System;
using System.Collections.Generic;
using HarmonyLib;

namespace MaterialMandate.Patches;

// Delivery of construction materials is restricted to the designated targets while any of them can still supply it.
//
// Two places decide what a duplicant carries to a construction site:
//  1. FetchChore.FindFetchTarget picks the "root" pickupable. FetchManager.pickups cannot be used for that, because it keeps
//     only the nearest pickupable per tag set, so designated targets are evaluated here by themselves.
//  2. FetchAreaChore.StatesInstance.Begin then adds every same-element pickupable nearby to the trip.
[HarmonyPatch(typeof(FetchChore), nameof(FetchChore.FindFetchTarget))]
public static class FetchChoreFindFetchTargetPatch
{
    public static bool Prefix(FetchChore __instance, ChoreConsumerState consumer_state, ref Pickupable __result)
    {
        var destination = __instance.destination;
        var mandate = ConstructionMandate.From(destination);
        if (mandate == null || consumer_state.hasSolidTransferArm) return true;

        var worker = consumer_state.gameObject.GetComponent<KPrefabID>().InstanceID;
        Pickupable best = null;
        var bestCost = int.MaxValue;
        var waitForReserved = false;

        foreach (var candidate in mandate.Candidates())
        {
            if (!MatchesChore(candidate, __instance)) continue;
            if (!candidate.CouldBePickedUpByMinion(worker)) continue;
            var cost = candidate.GetNavigationCost(consumer_state.navigator, candidate.cachedCell);
            if (cost == -1) continue; // unreachable for this worker

            if (FetchManager.IsFetchablePickup(candidate, __instance, destination))
            {
                if (cost < bestCost)
                {
                    best = candidate;
                    bestCost = cost;
                }
            }
            else if (candidate.ReservedAmount > 0f && candidate.UnreservedFetchAmount <= 0f)
            {
                // Reserved by another delivery: the target is still in use, so wait instead of falling back.
                waitForReserved = true;
            }
        }

        if (best != null)
        {
            __result = best;
            return false;
        }

        if (!waitForReserved) return true; // targets exhausted / unreachable: normal supply

        __result = null;
        return false;
    }

    internal static bool MatchesChore(Pickupable pickup, FetchChore chore)
    {
        var id = pickup.KPrefabID;
        if (id == null) return false;
        if (chore.criteria == FetchChore.MatchCriteria.MatchID) return chore.tags.Contains(id.PrefabTag);
        return id.HasTag(chore.tagsFirst);
    }
}

[HarmonyPatch(typeof(FetchAreaChore.StatesInstance), nameof(FetchAreaChore.StatesInstance.Begin))]
public static class FetchAreaChoreBeginPatch
{
    public static void Postfix(FetchAreaChore.StatesInstance __instance)
    {
        var traverse = Traverse.Create(__instance);
        var rootChore = traverse.Field("rootChore").GetValue<FetchChore>();
        var mandate = rootChore == null ? null : ConstructionMandate.From(rootChore.destination);
        if (mandate == null) return;

        var fetchables = traverse.Field("fetchables").GetValue<List<Pickupable>>();
        if (fetchables == null || fetchables.Count == 0) return;

        // The trip started from a designated target: do not let it pick up anything else along the way.
        // (A trip started from a non-target means the targets are exhausted, which is the normal fallback.)
        if (!mandate.Matches(fetchables[0])) return;

        fetchables.RemoveAll(p => !mandate.Matches(p));
        var available = 0f;
        foreach (var pickup in fetchables) available += pickup.UnreservedFetchAmount;

        var requested = traverse.Field("fetchAmountRequested");
        requested.SetValue(Math.Min(requested.GetValue<float>(), available));
    }
}
