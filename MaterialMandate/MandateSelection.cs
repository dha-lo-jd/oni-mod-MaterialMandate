using System.Collections.Generic;
using UnityEngine;

namespace MaterialMandate;

// The use-target group being assembled in the build menu. Applied to every construction order placed while it exists.
internal static class MandateSelection
{
    public static bool PickMode;
    public static readonly List<Pickupable> Pickups = new List<Pickupable>();
    public static readonly List<Storage> Storages = new List<Storage>();

    public static int Count
    {
        get
        {
            Prune();
            return Pickups.Count + Storages.Count;
        }
    }

    public static void Clear()
    {
        PickMode = false;
        Pickups.Clear();
        Storages.Clear();
    }

    // Empties the group but leaves pick mode as it is.
    public static void ClearTargets()
    {
        Pickups.Clear();
        Storages.Clear();
    }

    private static void Prune()
    {
        Pickups.RemoveAll(p => p == null);
        Storages.RemoveAll(s => s == null);
    }

    // Toggles the loose object (preferred) or the container at the cell. Returns a short description of what happened.
    public static string Toggle(int cell)
    {
        if (!Grid.IsValidCell(cell)) return null;

        var item = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
        var pickup = item != null ? item.GetComponent<Pickupable>() : null;
        if (pickup != null)
        {
            var added = !Pickups.Remove(pickup);
            if (added) Pickups.Add(pickup);
            return (added ? "+ " : "- ") + pickup.GetProperName();
        }

        var building = Grid.Objects[cell, (int)ObjectLayer.Building];
        var storage = building != null ? building.GetComponent<Storage>() : null;
        if (storage != null)
        {
            var added = !Storages.Remove(storage);
            if (added) Storages.Add(storage);
            return (added ? "+ " : "- ") + storage.GetProperName();
        }

        return null;
    }

    public static void ApplyTo(GameObject constructable)
    {
        if (constructable == null || constructable.GetComponent<Constructable>() == null) return;
        if (Count == 0) return;
        var mandate = constructable.GetComponent<ConstructionMandate>();
        if (mandate == null) mandate = constructable.AddComponent<ConstructionMandate>();
        mandate.Pickups.Clear();
        mandate.Storages.Clear();
        mandate.Pickups.AddRange(Pickups);
        mandate.Storages.AddRange(Storages);
    }
}
