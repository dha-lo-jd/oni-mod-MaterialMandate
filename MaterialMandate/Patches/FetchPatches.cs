using System;
using System.Collections.Generic;
using HarmonyLib;

namespace MaterialMandate.Patches;

// Delivery of construction materials is restricted to the designated targets while any of them can still supply it.
//
// Two places decide what a duplicant carries to a construction site:
//  1. FetchChore.FindFetchTarget picks the "root" pickupable. FetchManager.pickups cannot be used for that, because it keeps
//     only the nearest pickupable per tag set, so designated targets are evaluated here by themselves.
//  2. FetchAreaChore.StatesInstance.Begin then adds every same-element pickupable nearby to the trip (see below).
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

// A trip that starts from a designated target carries designated targets only.
//
// Begin plans the trip: it adds every same-material pickupable near the root to the load, and from that total it tells
// each fetch chore how much this trip will bring (the chore's order then issues a new chore for whatever is missing).
// The plan must therefore see the targets only. Trimming the load afterwards is too late: the chore would still count
// on the nearby non-targets, bring less than it promised, and the building would be finished short of material.
// So while Begin runs for such a trip, every other pickupable is reported as unreachable to its gathering pass.
[HarmonyPatch(typeof(FetchAreaChore.StatesInstance), nameof(FetchAreaChore.StatesInstance.Begin))]
public static class FetchAreaChoreBeginPatch
{
    private static readonly AccessTools.FieldRef<FetchAreaChore.StatesInstance, FetchChore> RootChore =
        AccessTools.FieldRefAccess<FetchAreaChore.StatesInstance, FetchChore>("rootChore");

    // The mandate of the trip being planned right now; null outside Begin. Chores begin on the main thread.
    internal static ConstructionMandate Planning;

    public static void Prefix(FetchAreaChore.StatesInstance __instance, Chore.Precondition.Context context)
    {
        Planning = null;
        var rootChore = RootChore(__instance);
        var mandate = rootChore == null ? null : ConstructionMandate.From(rootChore.destination);
        if (mandate == null) return;

        // A trip that starts from a non-target means the targets are exhausted: that is the normal fallback.
        var rootFetchable = context.data as Pickupable;
        if (rootFetchable != null && mandate.Matches(rootFetchable)) Planning = mandate;
    }

    public static void Finalizer()
    {
        Planning = null;
    }
}

[HarmonyPatch(typeof(ChoreConsumer), nameof(ChoreConsumer.CanReach))]
public static class ChoreConsumerCanReachPatch
{
    public static bool Prefix(IApproachable approachable, ref bool __result)
    {
        var mandate = FetchAreaChoreBeginPatch.Planning;
        if (mandate == null) return true;
        if (approachable is Pickupable pickup && !mandate.Matches(pickup))
        {
            __result = false;
            return false;
        }

        return true;
    }
}
