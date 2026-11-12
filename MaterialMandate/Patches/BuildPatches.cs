using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MaterialMandate.Patches;

// Tag a freshly placed construction order with the current use-target group.
[HarmonyPatch(typeof(BuildingDef), nameof(BuildingDef.TryPlace), typeof(GameObject), typeof(Vector3), typeof(Orientation),
    typeof(IList<Tag>), typeof(string), typeof(bool), typeof(int))]
public static class BuildingDefTryPlacePatch
{
    public static void Postfix(GameObject __result)
    {
        if (PlayerController.Instance != null && PlayerController.Instance.ActiveTool == BuildTool.Instance)
            MandateSelection.ApplyTo(__result);
    }
}

// Picking another material in the build menu re-activates the build tool with the new selection.
[HarmonyPatch(typeof(BuildTool), nameof(BuildTool.Activate), typeof(BuildingDef), typeof(IList<Tag>))]
public static class BuildToolActivatePatch
{
    public static void Postfix(IList<Tag> selected_elements)
    {
        if (MandateSelection.MaterialsSelected(selected_elements)) PickModeOverlay.RefreshIfShown();
        MandateButton.Refresh();
    }
}

// While in pick mode the left click designates targets instead of placing the building.
[HarmonyPatch(typeof(BuildTool), nameof(BuildTool.OnLeftClickDown))]
public static class BuildToolLeftClickDownPatch
{
    public static bool Prefix(Vector3 cursor_pos)
    {
        if (!MandateSelection.PickMode) return true;

        var text = MandateSelection.Toggle(Grid.PosToCell(cursor_pos));
        if (text != null && PopFXManager.Instance != null)
            PopFXManager.Instance.SpawnFX(Assets.GetSprite("action_move_to_storage"), text, null, 1.5f);
        MandateButton.Refresh();
        return false;
    }
}

[HarmonyPatch(typeof(BuildTool), nameof(BuildTool.OnLeftClickUp))]
public static class BuildToolLeftClickUpPatch
{
    public static bool Prefix() => !MandateSelection.PickMode;
}

[HarmonyPatch(typeof(BuildTool), "OnDeactivateTool")]
public static class BuildToolDeactivatePatch
{
    public static void Postfix()
    {
        MandateSelection.Clear();
        MandateButton.Refresh();
    }
}

// Esc / right click leave pick mode instead of closing the build menu. PlanScreen and ToolMenu handle these keys
// (right click on key up), so both are intercepted; the matching key up of an intercepted key down is swallowed too.
internal static class PickModeKeys
{
    private static bool swallowUp;

    private static bool IsExitKey(KButtonEvent e) => e.IsAction(Action.Escape) || e.IsAction(Action.MouseRight);

    private static bool Active =>
        MandateSelection.PickMode && PlayerController.Instance != null && PlayerController.Instance.ActiveTool == BuildTool.Instance;

    public static bool OnKeyDown(KButtonEvent e)
    {
        if (!Active || !IsExitKey(e)) return true;
        e.TryConsume(e.GetAction());
        MandateButton.ExitPickMode();
        swallowUp = true;
        return false;
    }

    public static bool OnKeyUp(KButtonEvent e)
    {
        if (!swallowUp || !IsExitKey(e)) return true;
        swallowUp = false;
        e.TryConsume(e.GetAction());
        return false;
    }
}

[HarmonyPatch(typeof(PlanScreen), nameof(PlanScreen.OnKeyDown))]
public static class PlanScreenKeyDownPatch
{
    public static bool Prefix(KButtonEvent e) => PickModeKeys.OnKeyDown(e);
}

[HarmonyPatch(typeof(PlanScreen), nameof(PlanScreen.OnKeyUp))]
public static class PlanScreenKeyUpPatch
{
    public static bool Prefix(KButtonEvent e) => PickModeKeys.OnKeyUp(e);
}

[HarmonyPatch(typeof(ToolMenu), nameof(ToolMenu.OnKeyDown))]
public static class ToolMenuKeyDownPatch
{
    public static bool Prefix(KButtonEvent e) => PickModeKeys.OnKeyDown(e);
}

[HarmonyPatch(typeof(ToolMenu), nameof(ToolMenu.OnKeyUp))]
public static class ToolMenuKeyUpPatch
{
    public static bool Prefix(KButtonEvent e) => PickModeKeys.OnKeyUp(e);
}
