using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MaterialMandate;

// "Designate use targets" button placed between the material selector and the priority panel of the build menu.
public static class MandateButton
{
    private static GameObject root;
    private static GameObject clearButton;
    private static TMPro.TextMeshProUGUI label;

    public static bool IsCreated => root != null;

    public static void Refresh()
    {
        if (label == null || root == null) return;
        var count = MandateSelection.Count;
        label.text = (MandateSelection.PickMode ? STRINGS.UI.BUTTON_ACTIVE : STRINGS.UI.BUTTON) + " (" + count + ")";
        var image = root.transform.Find("Main").GetComponent<Image>();
        image.color = MandateSelection.PickMode ? new Color(0.35f, 0.65f, 0.35f) : new Color(0.25f, 0.28f, 0.45f);
        clearButton.SetActive(count > 0);
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

        root = new GameObject("MaterialMandateButton", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        var row = root.GetComponent<HorizontalLayoutGroup>();
        row.spacing = 4f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;
        var rowLayout = root.GetComponent<LayoutElement>();
        rowLayout.minHeight = 28f;
        rowLayout.preferredHeight = 28f;
        rowLayout.flexibleWidth = 1f;

        var font = screen.productDescriptionText != null ? screen.productDescriptionText.font : null;
        label = CreateButton("Main", OnClick, new Color(0.25f, 0.28f, 0.45f), font, flexible: true);
        var clearLabel = CreateButton("Clear", OnClear, new Color(0.55f, 0.3f, 0.3f), font, flexible: false);
        clearLabel.text = STRINGS.UI.CLEAR;
        clearButton = clearLabel.transform.parent.gameObject;

        Place(panel);
        Refresh();
    }

    private static TMPro.TextMeshProUGUI CreateButton(string name, UnityEngine.Events.UnityAction onClick, Color color,
        TMPro.TMP_FontAsset font, bool flexible)
    {
        var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        button.transform.SetParent(root.transform, false);
        button.GetComponent<Image>().color = color;
        button.GetComponent<Button>().onClick.AddListener(onClick);
        var layout = button.GetComponent<LayoutElement>();
        if (flexible) layout.flexibleWidth = 1f;
        else layout.preferredWidth = 56f;

        var textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(button.transform, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = textObject.AddComponent<TMPro.TextMeshProUGUI>();
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.fontSize = 14f;
        if (font != null) text.font = font;
        text.raycastTarget = false;
        return text;
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

    public static void Toggle() => OnClick();

    private static void OnClick()
    {
        MandateSelection.PickMode = !MandateSelection.PickMode;
        Refresh();
    }

    private static void OnClear()
    {
        MandateSelection.ClearTargets();
        Refresh();
    }

    public static void ExitPickMode()
    {
        MandateSelection.PickMode = false;
        Refresh();
    }

    public static void ClearGroup() => OnClear();
}

[HarmonyPatch(typeof(ProductInfoScreen), nameof(ProductInfoScreen.ConfigureScreen), typeof(BuildingDef), typeof(string))]
public static class ProductInfoScreenConfigurePatch
{
    public static void Postfix(ProductInfoScreen __instance) => MandateButton.EnsureCreated(__instance);
}
