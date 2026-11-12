using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MaterialMandate;

// While in pick mode: dims the world a little and lights up everything that can be designated (loose debris and containers).
// The designated ones are lit more strongly. A quasi overlay, drawn with a translucent layer behind the HUD and the
// highlight colour the game itself uses for hovering (additive tint of the animation).
public static class PickModeOverlay
{
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color32 Candidate = new Color32(0, 120, 25, 0);
    private static readonly Color32 Designated = new Color32(110, 255, 110, 0);
    private static readonly Color32 None = new Color32(0, 0, 0, 0);

    private static GameObject dim;
    private static Driver driver;
    private static readonly Dictionary<KBatchedAnimController, Color32> lit = new Dictionary<KBatchedAnimController, Color32>();

    public static bool IsShown => driver != null;

    public static void Show()
    {
        if (driver != null) return;
        CreateDim();
        driver = new GameObject("MaterialMandatePickModeOverlay").AddComponent<Driver>();
        Refresh();
    }

    public static void Hide()
    {
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

    // Recomputes what is lit; called periodically because debris keeps appearing, moving and merging.
    public static void Refresh()
    {
        var wanted = new Dictionary<KBatchedAnimController, Color32>();
        foreach (var pickup in Components.Pickupables.Items)
            if (MandateSelection.IsDesignatable(pickup))
                Want(wanted, pickup.gameObject, MandateSelection.Pickups.Contains(pickup) ? Designated : Candidate);

        foreach (var storage in Object.FindObjectsOfType<Storage>())
            if (MandateSelection.IsDesignatable(storage))
                Want(wanted, storage.gameObject, MandateSelection.Storages.Contains(storage) ? Designated : Candidate);

        foreach (var pair in lit)
            if (pair.Key != null && !wanted.ContainsKey(pair.Key))
                pair.Key.HighlightColour = None;

        foreach (var pair in wanted)
            pair.Key.HighlightColour = pair.Value;

        lit.Clear();
        foreach (var pair in wanted) lit[pair.Key] = pair.Value;
    }

    private static void Want(Dictionary<KBatchedAnimController, Color32> wanted, GameObject target, Color32 color)
    {
        var controller = target.GetComponent<KBatchedAnimController>();
        if (controller != null) wanted[controller] = color;
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
