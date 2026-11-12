using System.Collections.Generic;
using UnityEngine;

namespace MaterialMandate;

// The use-target group being assembled in the build menu. Applied to every construction order placed while it exists.
internal static class MandateSelection
{
    private static bool pickMode;

    public static bool PickMode
    {
        get => pickMode;
        set
        {
            if (pickMode == value) return;
            pickMode = value;
            if (value) PickModeOverlay.Show();
            else PickModeOverlay.Hide();
        }
    }
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

    // Loose solid things (not creatures) and storage buildings can be designated.
    public static bool IsDesignatable(Pickupable pickup)
    {
        if (pickup == null || pickup.storage != null || pickup.KPrefabID == null) return false;
        if (pickup.KPrefabID.HasTag(GameTags.Creature) || pickup.GetComponent<MinionIdentity>() != null) return false;
        var element = pickup.GetComponent<PrimaryElement>();
        return element != null && element.Mass > 0f;
    }

    public static bool IsDesignatable(Storage storage)
    {
        return storage != null && storage.GetComponent<BuildingComplete>() != null &&
               storage.fetchCategory != Storage.FetchCategory.Building;
    }

    private static readonly HarmonyLib.AccessTools.FieldRef<BuildTool, IList<Tag>> SelectedElements =
        HarmonyLib.AccessTools.FieldRefAccess<BuildTool, IList<Tag>>("selectedElements");

    // The materials chosen for the building being placed; null when the build tool is not in use.
    public static IList<Tag> SelectedMaterials()
    {
        var tool = BuildTool.Instance;
        if (tool == null || PlayerController.Instance == null || PlayerController.Instance.ActiveTool != tool) return null;
        return SelectedElements(tool);
    }

    // Whether the object is one of the given materials. Without materials everything matches.
    public static bool MatchesMaterial(Pickupable pickup, IList<Tag> materials)
    {
        if (materials == null || materials.Count == 0) return true;
        var id = pickup == null ? null : pickup.KPrefabID;
        if (id == null) return false;
        for (var i = 0; i < materials.Count; i++)
            if (id.HasTag(materials[i]))
                return true;
        return false;
    }

    // A container matches when it holds at least one object of the given materials.
    public static bool MatchesMaterial(Storage storage, IList<Tag> materials)
    {
        if (materials == null || materials.Count == 0) return true;
        if (storage == null) return false;
        var items = storage.items;
        for (var i = 0; i < items.Count; i++)
        {
            var pickup = items[i] == null ? null : items[i].GetComponent<Pickupable>();
            if (pickup != null && MatchesMaterial(pickup, materials)) return true;
        }
        return false;
    }

    // Toggles the loose object (preferred) or the container at the cell. Returns a short description of what happened.
    public static string Toggle(int cell)
    {
        if (!Grid.IsValidCell(cell)) return null;

        var item = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
        var pickup = item != null ? item.GetComponent<Pickupable>() : null;
        if (pickup != null && IsDesignatable(pickup))
        {
            var added = !Pickups.Remove(pickup);
            if (added) Pickups.Add(pickup);
            return (added ? "+ " : "- ") + pickup.GetProperName();
        }

        var building = Grid.Objects[cell, (int)ObjectLayer.Building];
        var storage = building != null ? building.GetComponent<Storage>() : null;
        if (storage != null && IsDesignatable(storage))
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
