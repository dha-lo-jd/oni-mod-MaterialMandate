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
