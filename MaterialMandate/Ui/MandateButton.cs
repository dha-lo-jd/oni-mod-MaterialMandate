using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MaterialMandate;

// "Designate use targets" button placed between the material selector and the priority panel of the build menu.
public static class MandateButton
{
    private static GameObject root;
    private static TMPro.TextMeshProUGUI label;

    public static bool IsCreated => root != null;

    public static void Refresh()
    {
        if (label == null || root == null) return;
        var count = MandateSelection.Count;
        label.text = (MandateSelection.PickMode ? STRINGS.UI.BUTTON_ACTIVE : STRINGS.UI.BUTTON) + " (" + count + ")";
        var image = root.GetComponent<Image>();
        if (image != null)
            image.color = MandateSelection.PickMode ? new Color(0.35f, 0.65f, 0.35f) : new Color(0.25f, 0.28f, 0.45f);
    }

    public static void EnsureCreated(ProductInfoScreen screen)
    {
        var panel = screen.materialSelectionPanel;
        if (panel == null) return;
        if (root != null)
        {
            Place(panel);
            Refresh();
            return;
        }

        root = new GameObject("MaterialMandateButton", typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(LayoutElement));
        root.GetComponent<Image>().color = new Color(0.25f, 0.28f, 0.45f);
        var layout = root.GetComponent<LayoutElement>();
        layout.minHeight = 28f;
        layout.preferredHeight = 28f;
        layout.flexibleWidth = 1f;
        root.GetComponent<Button>().onClick.AddListener(OnClick);

        var textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(root.transform, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        label = textObject.AddComponent<TMPro.TextMeshProUGUI>();
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.fontSize = 14f;
        if (screen.productDescriptionText != null) label.font = screen.productDescriptionText.font;
        label.raycastTarget = false;

        Place(panel);
        Refresh();
    }

    private static void Place(MaterialSelectionPanel panel)
    {
        var parent = panel.transform;
        root.transform.SetParent(parent, false);
        var priorityParent = Traverse.Create(panel).Field("priorityScreenParent").GetValue<GameObject>();
        var index = priorityParent != null && priorityParent.transform.parent == parent
            ? priorityParent.transform.GetSiblingIndex()
            : parent.childCount;
        root.transform.SetSiblingIndex(index);
        root.SetActive(true);
    }

    private static void OnClick()
    {
        MandateSelection.PickMode = !MandateSelection.PickMode;
        Refresh();
    }
}

[HarmonyPatch(typeof(ProductInfoScreen), nameof(ProductInfoScreen.ConfigureScreen), typeof(BuildingDef), typeof(string))]
public static class ProductInfoScreenConfigurePatch
{
    public static void Postfix(ProductInfoScreen __instance) => MandateButton.EnsureCreated(__instance);
}
