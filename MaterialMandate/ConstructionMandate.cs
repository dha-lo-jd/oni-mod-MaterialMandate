using System.Collections.Generic;
using UnityEngine;

namespace MaterialMandate;

// Attached to a Constructable: the designated use-targets (loose debris and containers) that must be consumed first.
public sealed class ConstructionMandate : MonoBehaviour
{
    public readonly List<Pickupable> Pickups = new List<Pickupable>();
    public readonly List<Storage> Storages = new List<Storage>();

    public bool Matches(Pickupable pickup)
    {
        if (pickup == null) return false;
        if (Pickups.Contains(pickup)) return true;
        return pickup.storage != null && Storages.Contains(pickup.storage);
    }

    // Every pickupable that may be delivered: the designated objects plus the contents of the designated containers.
    public IEnumerable<Pickupable> Candidates()
    {
        foreach (var pickup in Pickups)
            if (pickup != null)
                yield return pickup;

        foreach (var storage in Storages)
        {
            if (storage == null) continue;
            foreach (var item in storage.items)
            {
                var pickup = item == null ? null : item.GetComponent<Pickupable>();
                if (pickup != null) yield return pickup;
            }
        }
    }

    public static ConstructionMandate From(Storage destination)
    {
        return destination == null ? null : destination.GetComponent<ConstructionMandate>();
    }
}
