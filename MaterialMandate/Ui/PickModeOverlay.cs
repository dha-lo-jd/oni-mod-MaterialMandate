using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MaterialMandate;

// While in pick mode: dims the world a little and lights up what can be designated for the selected material (loose
// debris of that material and containers holding some). The designated ones are lit more strongly. A quasi overlay, drawn with a translucent layer behind the HUD and the
// highlight colour the game itself uses for hovering (additive tint of the animation).
public static class PickModeOverlay
{
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color32 Candidate = new Color32(0, 120, 25, 0);
    private static readonly Color32 Designated = new Color32(110, 255, 110, 0);
    private static readonly Color32 None = new Color32(0, 0, 0, 0);

    private static GameObject dim;
    private static Driver driver;
    // Cells added around the camera view, so that an object scrolling in is already lit.
    private const int ViewPadding = 2;

    private static Dictionary<KBatchedAnimController, Color32> lit = new Dictionary<KBatchedAnimController, Color32>();
    private static Dictionary<KBatchedAnimController, Color32> wanted = new Dictionary<KBatchedAnimController, Color32>();
    private static readonly HashSet<GameObject> seenBuildings = new HashSet<GameObject>();

    public static bool IsShown => driver != null;

    // The building preview follows the cursor; hidden while picking so that it is clear the click designates a target
    // instead of placing the building. The build tool re-creates the preview on activation, so this is applied again then.
    public static void ApplyPreview()
    {
        var tool = BuildTool.Instance;
        if (tool == null) return;
        var visible = !MandateSelection.PickMode;
        var preview = tool.visualizer;
        if (preview != null && preview.activeSelf != visible) preview.SetActive(visible);
        // Tile-like buildings are also drawn by the block tile renderer, which UpdateVis refreshes on every mouse move
        // (skipped in pick mode, see BuildToolUpdateVisPatch); clear what is already there.
        if (!visible) HarmonyLib.Traverse.Create(tool).Method("ClearTilePreview").GetValue();
        var remaining = ResourceRemainingDisplayScreen.instance;
        if (remaining != null && !visible) remaining.DeactivateDisplay();
        else if (remaining != null && preview != null) remaining.ActivateDisplay(preview);
    }

    public static void Show()
    {
        if (driver != null) return;
        CreateDim();
        driver = new GameObject("MaterialMandatePickModeOverlay").AddComponent<Driver>();
        ApplyPreview();
        Refresh();
    }

    public static void Hide()
    {
        ApplyPreview();
        if (driver != null) Object.Destroy(driver.gameObject);
        driver = null;
        if (dim != null) Object.Destroy(dim);
        dim = null;
        foreach (var pair in lit)
            if (pair.Key != null)
                pair.Key.HighlightColour = None;
        lit.Clear();
    }

    private static void CreateDim()
    {
        var parent = GameScreenManager.Instance == null ? null : GameScreenManager.Instance.ssCameraCanvas;
        if (parent == null) return;
        dim = new GameObject("MaterialMandateDim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(parent.transform, false);
        dim.transform.SetAsFirstSibling(); // behind the HUD, above the world
        var rect = dim.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = dim.GetComponent<Image>();
        image.color = DimColor;
        image.raycastTarget = false;
    }

    public static void RefreshIfShown()
    {
        if (driver != null) Refresh();
    }

    // Recomputes what is lit; called periodically because debris keeps appearing, moving and merging, the camera moves
    // and the selected material can change.
    //
    // Designated targets are always lit. Candidates are lit only when they are of the selected material (a container:
    // when it holds some) and only inside the camera view, so the cost follows the screen, not the size of the colony.
    // What is lit is exactly what a click can designate or release (MandateSelection.Toggle).
    public static void Refresh()
    {
        wanted.Clear();
        foreach (var pickup in MandateSelection.Pickups)
            if (pickup != null)
                Want(pickup.gameObject, Designated);
        foreach (var storage in MandateSelection.Storages)
            if (storage != null)
                Want(storage.gameObject, Designated);

        AddVisibleCandidates(MandateSelection.SelectedMaterials());

        foreach (var pair in lit)
            if (pair.Key != null && !wanted.ContainsKey(pair.Key))
                pair.Key.HighlightColour = None;

        foreach (var pair in wanted)
            pair.Key.HighlightColour = pair.Value;

        // The new set becomes the lit one; the old dictionary is reused for the next refresh.
        var previous = lit;
        lit = wanted;
        wanted = previous;
    }

    private static void AddVisibleCandidates(IList<Tag> materials)
    {
        if (Camera.main == null) return;
        Grid.GetVisibleExtents(out int minX, out int minY, out int maxX, out int maxY);
        minX = Mathf.Max(0, minX - ViewPadding);
        minY = Mathf.Max(0, minY - ViewPadding);
        maxX = Mathf.Min(Grid.WidthInCells - 1, maxX + ViewPadding);
        maxY = Mathf.Min(Grid.HeightInCells - 1, maxY + ViewPadding);

        seenBuildings.Clear();
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var cell = Grid.XYToCell(x, y);

                // Loose objects of a cell form a linked list starting at the one registered on the grid.
                var top = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
                if (top != null)
                {
                    var first = top.GetComponent<Pickupable>();
                    for (var entry = first == null ? null : first.objectLayerListItem; entry != null; entry = entry.nextItem)
                    {
                        var pickup = entry.pickupable;
                        if (MandateSelection.IsDesignatable(pickup) && MandateSelection.MatchesMaterial(pickup, materials))
                            Want(pickup.gameObject, Candidate);
                    }
                }

                // A building covers several cells; look at each one once.
                var building = Grid.Objects[cell, (int)ObjectLayer.Building];
                if (building != null && seenBuildings.Add(building))
                {
                    var storage = building.GetComponent<Storage>();
                    if (MandateSelection.IsDesignatable(storage) && MandateSelection.MatchesMaterial(storage, materials))
                        Want(building, Candidate);
                }
            }
        }

        seenBuildings.Clear();
    }

    // The first colour asked for an object wins, so a designated target stays designated.
    private static void Want(GameObject target, Color32 color)
    {
        var controller = target.GetComponent<KBatchedAnimController>();
        if (controller != null && !wanted.ContainsKey(controller)) wanted[controller] = color;
    }

    // What the overlay lights the object with: 0 = not lit, 1 = candidate, 2 = designated (read by the in-game tests).
    internal static int LitLevel(GameObject target)
    {
        var controller = target == null ? null : target.GetComponent<KBatchedAnimController>();
        if (controller == null || !lit.TryGetValue(controller, out var color)) return 0;
        return color.Equals(Designated) ? 2 : 1;
    }

    private sealed class Driver : MonoBehaviour
    {
        private float next;

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.25f;
            Refresh();
        }
    }
}
