#if DEBUG
// In-game tests run by Gravitas.TestHarness:
//   python Gravitas.TestProbe/probe.py run --project <this repo> --save "Live Human Testing.sav" --tests '{"tags":["materialmandate"]}'
// Needs HarnessDllPath in Local.Build.props (Check / Wait come from the harness assembly).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gravitas.TestHarness;
using Klei.AI;
using UnityEngine;

namespace MaterialMandate;

public static class HarnessDebug
{
    // Opens the build menu product screen for a building, as when picking it from the build menu.
    public static string ShowProduct(string prefabId)
    {
        var def = Assets.GetBuildingDef(prefabId);
        var screen = PlanScreen.Instance.ProductInfoScreen;
        screen.ClearProduct(false);
        screen.Show();
        screen.ConfigureScreen(def);
        return MandateButton.IsCreated ? "button" : "no button";
    }

    // For screenshots: drops a few chunks and a locker next to the first duplicant and centres the camera on them.
    public static string SpawnDemo()
    {
        var minion = Components.LiveMinionIdentities[0];
        var home = Grid.PosToCell(minion.transform.GetPosition());
        var element = ElementLoader.FindElementByHash(SimHashes.SandStone);
        foreach (var dx in new[] { -4, -2, 3 })
        {
            var cell = Grid.OffsetCell(home, dx, 0);
            element.substance.SpawnResource(Grid.CellToPosCBC(cell, Grid.SceneLayer.Ore), 200f,
                element.defaultValues.temperature, byte.MaxValue, 0, prevent_merge: true);
        }

        var lockerCell = Grid.OffsetCell(home, 5, 0);
        var locker = Assets.GetBuildingDef("StorageLocker").Build(lockerCell, Orientation.Neutral, null,
            new List<Tag> { SimHashes.Steel.CreateTag() }, 293f, false);
        locker.GetComponent<Storage>().Store(element.substance.SpawnResource(Grid.CellToPosCBC(lockerCell, Grid.SceneLayer.Ore),
            300f, element.defaultValues.temperature, byte.MaxValue, 0, prevent_merge: true), true);
        CameraController.Instance.SnapTo(minion.transform.GetPosition());
        return "spawned";
    }

    public static string DumpPanel()
    {
        var panel = PlanScreen.Instance.ProductInfoScreen.materialSelectionPanel;
        var parent = HarmonyLib.Traverse.Create(panel).Field("priorityScreenParent").GetValue<GameObject>();
        var lines = new List<string>();
        foreach (Transform child in panel.transform)
        {
            var rect = child.GetComponent<RectTransform>();
            lines.Add(child.GetSiblingIndex() + " " + child.name + " active=" + child.gameObject.activeSelf + " h=" +
                      (rect == null ? 0 : rect.rect.height) + " y=" + (rect == null ? 0 : rect.anchoredPosition.y) +
                      (child.gameObject == parent ? " [priorityParent]" : ""));
        }
        return string.Join(" | ", lines);
    }

    // Like ShowProduct, but also activates the BuildTool as the build menu does (shows the priority panel).
    public static string ActivateBuild(string prefabId, bool pickMode)
    {
        ShowProduct(prefabId);
        var def = Assets.GetBuildingDef(prefabId);
        BuildTool.Instance.Activate(def, new List<Tag> { SimHashes.SandStone.CreateTag() });
        if (pickMode != MandateSelection.PickMode) MandateButton.Toggle();
        return "active tool: " + PlayerController.Instance.ActiveTool.GetType().Name;
    }
}

internal static class HarnessTests
{
    private const SimHashes Material = SimHashes.SandStone;

    [HarnessTest(Name = "materialmandate.mod_loaded", Tags = "materialmandate")]
    public static void ModLoaded()
    {
        Check.True(MaterialMandateMod.modPath != null, "MaterialMandateMod.OnLoad has not run");
    }

    [HarnessTest(Name = "materialmandate.button_created", Tags = "materialmandate")]
    public static void ButtonCreated()
    {
        Check.Equal("button", HarnessDebug.ShowProduct("Tile"));
    }

    // Esc / right click leave pick mode without closing the build menu or dropping the build tool.
    [HarnessTest(Name = "materialmandate.ui_pick_mode_exit_keys", Tags = "materialmandate")]
    public static void PickModeExitKeys()
    {
        foreach (var action in new[] { Action.Escape, Action.MouseRight })
        {
            HarnessDebug.ActivateBuild("Tile", pickMode: true);
            Check.True(MandateSelection.PickMode, "pick mode did not start");

            var down = new KButtonEvent(null, InputEventType.KeyDown, action);
            PlanScreen.Instance.OnKeyDown(down);
            ToolMenu.Instance.OnKeyDown(down);
            Check.True(down.Consumed, action + " key down was not consumed");
            Check.False(MandateSelection.PickMode, action + " did not leave pick mode");

            var up = new KButtonEvent(null, InputEventType.KeyUp, action);
            PlanScreen.Instance.OnKeyUp(up);
            ToolMenu.Instance.OnKeyUp(up);
            Check.True(up.Consumed, action + " key up was not swallowed");
            Check.True(PlayerController.Instance.ActiveTool == BuildTool.Instance, action + " dropped the build tool");

            BuildTool.Instance.Deactivate();
        }
    }

    // Pick mode dims the world and lights up what can be designated for the selected material, inside the camera view;
    // leaving it restores everything.
    [HarnessTest(Name = "materialmandate.ui_pick_mode_overlay", Tags = "materialmandate")]
    public static IEnumerator PickModeOverlayTest()
    {
        var minion = Components.LiveMinionIdentities[0];
        var home = Grid.PosToCell(minion.transform.GetPosition());
        var cells = Scene.FindFloorRun(home);
        Check.True(cells.Count >= 4, "not enough open floor next to the duplicant (" + cells.Count + " cells)");

        var baseline = new HashSet<Pickupable>(Components.Pickupables.Items.Where(p => p != null));
        var def = Assets.GetBuildingDef("Tile");
        var sandstone = new List<Tag> { SimHashes.SandStone.CreateTag() };
        var granite = new List<Tag> { SimHashes.Granite.CreateTag() };
        var lockerDef = Assets.GetBuildingDef("StorageLocker");
        var steel = new List<Tag> { SimHashes.Steel.CreateTag() };
        GameObject fullLocker = null;
        GameObject emptyLocker = null;
        var view = CameraController.Instance.transform.GetPosition();

        try
        {
            var same = Scene.Spawn(cells[0], 50f).gameObject;
            var other = Scene.Spawn(cells[1], 50f, SimHashes.Granite).gameObject;
            fullLocker = lockerDef.Build(cells[2], Orientation.Neutral, null, steel, 293f, false);
            emptyLocker = lockerDef.Build(cells[3], Orientation.Neutral, null, steel, 293f, false);
            foreach (var locker in new[] { fullLocker, emptyLocker })
                locker.GetComponent<Storage>().SetOnlyFetchMarkedItems(true); // keep the lockers from being filled
            fullLocker.GetComponent<Storage>().Store(Scene.Spawn(cells[2], 50f).gameObject, true);
            yield return null;
            yield return null;

            CameraController.Instance.SnapTo(minion.transform.GetPosition());
            yield return null;
            var controller = same.GetComponent<KBatchedAnimController>();
            Check.True(controller.HighlightColour.g == 0, "already highlighted before pick mode");

            HarnessDebug.ActivateBuild("Tile", pickMode: true); // selects Sandstone
            Check.True(PickModeOverlay.IsShown, "overlay was not shown");
            PickModeOverlay.Refresh();
            Check.True(controller.HighlightColour.g > 0, "debris of the selected material was not highlighted");
            Check.Equal(1, PickModeOverlay.LitLevel(same), "debris of the selected material");
            Check.Equal(0, PickModeOverlay.LitLevel(other), "debris of another material");
            Check.Equal(1, PickModeOverlay.LitLevel(fullLocker), "container holding the selected material");
            Check.Equal(0, PickModeOverlay.LitLevel(emptyLocker), "empty container");

            // A designated target stays lit whatever the material.
            MandateSelection.Pickups.Add(other.GetComponent<Pickupable>());
            PickModeOverlay.Refresh();
            Check.Equal(2, PickModeOverlay.LitLevel(other), "designated debris of another material");
            MandateSelection.ClearTargets();

            // The highlight follows the material selection.
            BuildTool.Instance.Activate(def, granite);
            PickModeOverlay.Refresh();
            Check.Equal(0, PickModeOverlay.LitLevel(same), "Sandstone debris with Granite selected");
            Check.Equal(1, PickModeOverlay.LitLevel(other), "Granite debris with Granite selected");
            Check.Equal(0, PickModeOverlay.LitLevel(fullLocker), "Sandstone container with Granite selected");
            Check.True(controller.HighlightColour.g == 0, "highlight of the unselected material was not removed");
            BuildTool.Instance.Activate(def, sandstone);

            // Only what the camera shows is lit.
            Grid.CellToXY(home, out var homeX, out var homeY);
            var farX = homeX < Grid.WidthInCells / 2 ? Grid.WidthInCells - 4 : 4;
            CameraController.Instance.SnapTo(Grid.CellToPosCCC(Grid.XYToCell(farX, homeY), Grid.SceneLayer.Move));
            yield return null;
            PickModeOverlay.Refresh();
            Check.Equal(0, PickModeOverlay.LitLevel(same), "debris outside the camera view");
            CameraController.Instance.SnapTo(minion.transform.GetPosition());
            yield return null;
            PickModeOverlay.Refresh();
            Check.Equal(1, PickModeOverlay.LitLevel(same), "debris back in the camera view");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 100; i++) PickModeOverlay.Refresh();
            watch.Stop();
            Debug.Log("[MaterialMandate] overlay refresh: " + (watch.Elapsed.TotalMilliseconds / 100.0).ToString("0.000") + " ms per call");

            MandateButton.ExitPickMode();
            Check.False(PickModeOverlay.IsShown, "overlay was not hidden");
            Check.True(controller.HighlightColour.g == 0, "highlight was not removed");
            Check.Equal(0, PickModeOverlay.LitLevel(same), "lit level after leaving pick mode");
        }
        finally
        {
            MandateSelection.Clear();
            if (PlayerController.Instance.ActiveTool == BuildTool.Instance) BuildTool.Instance.Deactivate();
            CameraController.Instance.SnapTo(view);
            foreach (var locker in new[] { fullLocker, emptyLocker })
                if (locker != null)
                    Util.KDestroyGameObject(locker);
            foreach (var extra in Components.Pickupables.Items.Where(p => p != null && !baseline.Contains(p)).ToList())
                if (extra.GetComponent<MinionIdentity>() == null)
                    Util.KDestroyGameObject(extra.gameObject);
        }

        yield return Wait.Seconds(1f, 3); // let the grid settle before the next test picks its floor
    }

    // The clear button empties the group and leaves the pick mode untouched.
    [HarnessTest(Name = "materialmandate.ui_clear_group", Tags = "materialmandate")]
    public static void ClearGroup()
    {
        HarnessDebug.ActivateBuild("Tile", pickMode: true);
        MandateSelection.Pickups.Add(Components.Pickupables.Items.First(p => p != null));
        MandateButton.Refresh();
        Check.Equal(1, MandateSelection.Count, "group was not filled");

        MandateButton.ClearGroup();
        Check.Equal(0, MandateSelection.Count, "group was not cleared");
        Check.True(MandateSelection.PickMode, "clearing the group must not leave pick mode");

        BuildTool.Instance.Deactivate();
        Check.Equal(0, MandateSelection.Count, "group survived leaving the build tool");
    }

    // Targets (300 kg) outweigh the need (200 kg for a Tile): the nearer decoy chunk must stay untouched.
    [HarnessTest(Name = "materialmandate.e1_target_used_exclusively", Tags = "materialmandate,e2e", TimeoutSeconds = 600)]
    public static IEnumerator TargetUsedExclusively()
    {
        var scene = Scene.Create(targetMass: 300f, decoyMass: 200f);
        yield return scene.WaitUntilBuilt();
        try
        {
            Check.True(scene.IsBuilt, "tile was not built");
            Check.Near(200f, scene.DecoyMass, 0.5, "decoy chunk was used");
            Check.Near(100f, scene.TargetMass, 1.0, "target chunk should have supplied exactly 200 kg");
        }
        finally
        {
            scene.Cleanup();
        }

        yield return Wait.Seconds(3f, 3); // let the grid settle before the next test picks its floor
    }

    // Targets (40 kg) are smaller than the need (200 kg): all of it goes in, the rest comes from the decoy.
    [HarnessTest(Name = "materialmandate.e2_falls_back_when_exhausted", Tags = "materialmandate,e2e", TimeoutSeconds = 600)]
    public static IEnumerator FallsBackWhenExhausted()
    {
        var scene = Scene.Create(targetMass: 40f, decoyMass: 300f);
        yield return scene.WaitUntilBuilt();
        try
        {
            Check.True(scene.IsBuilt, "tile was not built");
            Check.Near(0f, scene.TargetMass, 0.5, "target chunk should be used up");
            Check.Near(140f, scene.DecoyMass, 1.0, "decoy chunk should supply the remaining 160 kg");
        }
        finally
        {
            scene.Cleanup();
        }

        yield return Wait.Seconds(3f, 3); // let the grid settle before the next test picks its floor
    }

    // The whole player flow: build menu -> pick-mode button -> click debris -> leave pick mode -> place the building.
    [HarnessTest(Name = "materialmandate.e0_ui_flow", Tags = "materialmandate,e2e", TimeoutSeconds = 600)]
    public static IEnumerator UiFlow()
    {
        var scene = Scene.Create(targetMass: 300f, decoyMass: 200f, viaUi: true);
        yield return null;
        scene.RunUiFlow();
        yield return scene.WaitUntilBuilt();
        try
        {
            Check.True(scene.IsBuilt, "tile was not built");
            Check.Near(200f, scene.DecoyMass, 0.5, "decoy chunk was used");
            Check.Near(100f, scene.TargetMass, 1.0, "target chunk should have supplied exactly 200 kg");
        }
        finally
        {
            scene.Cleanup();
        }

        yield return Wait.Seconds(3f, 3); // let the grid settle before the next test picks its floor
    }

    // Same as e0, but the use target is a storage locker holding the 300 kg: the stored chunk is delivered, the decoy is not.
    [HarnessTest(Name = "materialmandate.e0b_container_ui_flow", Tags = "materialmandate,e2e", TimeoutSeconds = 600)]
    public static IEnumerator ContainerUiFlow()
    {
        var scene = Scene.Create(targetMass: 300f, decoyMass: 200f, viaUi: true, container: true);
        yield return null;
        scene.RunUiFlow();
        yield return scene.WaitUntilBuilt();
        try
        {
            Check.True(scene.IsBuilt, "tile was not built");
            Check.Near(200f, scene.DecoyMass, 0.5, "decoy chunk was used; " + scene.Diagnose());
            Check.Near(100f, scene.TargetMass, 1.0, "stored chunk should have supplied exactly 200 kg");
        }
        finally
        {
            scene.Cleanup();
        }

        yield return Wait.Seconds(3f, 3); // let the grid settle before the next test picks its floor
    }

    private sealed class Scene
    {
        private GameObject locker;
        private HashSet<Pickupable> baseline;
        private int targetCell;

        private Pickupable target;
        private Pickupable decoy;
        private GameObject site;
        private int siteCell;

        // The construction site object is destroyed when the building completes and a foundation tile appears.
        public bool IsBuilt => site == null && Grid.Objects[siteCell, (int)ObjectLayer.FoundationTile] != null;

        public float TargetMass => Mass(target);
        public float DecoyMass => Mass(decoy);

        private static float Mass(Pickupable p) => p == null ? 0f : p.GetComponent<PrimaryElement>().Mass;

        public static Scene Create(float targetMass, float decoyMass, bool viaUi = false, bool container = false)
        {
            KeepAlive();
            var baseline = new HashSet<Pickupable>(Components.Pickupables.Items.Where(p => p != null));
            var minion = Components.LiveMinionIdentities[0];
            var home = Grid.PosToCell(minion.transform.GetPosition());
            var cells = FindFloorRun(home);
            Check.True(cells.Count >= 3, "not enough open floor next to the duplicant (" + cells.Count + " cells)");

            var scene = new Scene { baseline = baseline };
            var decoyCell = cells.First();
            var targetCell = cells.Last();
            scene.siteCell = cells[cells.Count / 2];
            scene.decoy = Spawn(decoyCell, decoyMass);
            scene.targetCell = targetCell;
            if (container)
            {
                var lockerDef = Assets.GetBuildingDef("StorageLocker");
                scene.locker = lockerDef.Build(targetCell, Orientation.Neutral, null,
                    new List<Tag> { SimHashes.Steel.CreateTag() }, 293f, false);
                scene.target = Spawn(targetCell, targetMass);
                var lockerStorage = scene.locker.GetComponent<Storage>();
                lockerStorage.Store(scene.target.gameObject, true);
                lockerStorage.SetOnlyFetchMarkedItems(true); // otherwise the locker sweeps the decoy chunk into itself
            }
            else
            {
                scene.target = Spawn(targetCell, targetMass);
            }

            var def = Assets.GetBuildingDef("Tile");
            var elements = new List<Tag> { Material.CreateTag() };
            MandateSelection.Clear();

            if (viaUi)
            {
                scene.def = def;
                scene.elements = elements;
                return scene;
            }

            {
                scene.site = def.Instantiate(Grid.CellToPosCBC(scene.siteCell, Grid.SceneLayer.Building),
                    Orientation.Neutral, elements);
                MandateSelection.Pickups.Add(scene.target);
                MandateSelection.ApplyTo(scene.site);
                MandateSelection.Clear();
            }

            Check.NotNull(scene.site.GetComponent<ConstructionMandate>(), "mandate was not attached");
            return scene;
        }

        private BuildingDef def;
        private List<Tag> elements;

        // Drives the real tool flow. Called a frame after spawning so the debris is registered on the grid.
        public void RunUiFlow()
        {
                var screen = PlanScreen.Instance.ProductInfoScreen;
                screen.ClearProduct(false);
                screen.Show();
                screen.ConfigureScreen(def);
                BuildTool.Instance.Activate(def, elements);
                Check.True(PlayerController.Instance.ActiveTool == BuildTool.Instance, "BuildTool is not active");

                MandateButton.Toggle();
                Check.True(MandateSelection.PickMode, "pick mode did not start");
                Click(targetCell);
                Check.Equal(1, MandateSelection.Count, "debris was not designated");
                var existing = Grid.Objects[targetCell, (int)def.ObjectLayer];
                Check.True(existing == null || existing.GetComponent<Constructable>() == null,
                    "a building was placed in pick mode");
                MandateButton.Toggle();
                Check.False(MandateSelection.PickMode, "pick mode did not end");

                Click(this.siteCell);
                var placed = Grid.Objects[this.siteCell, (int)def.ObjectLayer];
                Check.NotNull(placed, "building was not placed");
                this.site = placed;
                BuildTool.Instance.Deactivate();
            Check.NotNull(site.GetComponent<ConstructionMandate>(), "mandate was not attached");
        }

        private static void Click(int cell)
        {
            var pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Move);
            BuildTool.Instance.OnLeftClickDown(pos);
            BuildTool.Instance.OnLeftClickUp(pos);
        }

        // Advances the game in short steps until the tile is complete (at most ~300 s of game time).
        public IEnumerator WaitUntilBuilt()
        {
            for (var i = 0; i < 15 && !IsBuilt; i++)
                yield return Wait.Seconds(20f, 3);
        }

        // Every test advances game time; keep the duplicants fed so a long run does not starve them.
        private static void KeepAlive()
        {
            foreach (var minion in Components.LiveMinionIdentities.Items)
            {
                var amounts = minion.GetAmounts();
                foreach (var amount in new[] { Db.Get().Amounts.Calories, Db.Get().Amounts.Breath, Db.Get().Amounts.HitPoints, Db.Get().Amounts.Stamina })
                {
                    var instance = amounts.Get(amount);
                    if (instance != null) instance.SetValue(instance.GetMax());
                }
            }
        }

        public string Diagnose()
        {
            if (target == null) return "target gone";
            var minion = Components.LiveMinionIdentities[0].GetComponent<KPrefabID>().InstanceID;
            var storage = target.storage;
            var automatable = storage == null ? null : storage.automatable;
            var mandate = site == null ? null : site.GetComponent<ConstructionMandate>();
            var nav = Components.LiveMinionIdentities[0].GetComponent<Navigator>();
            var found = Grid.Objects[siteCell, (int)ObjectLayer.FoundationTile];
            return "siteNull=" + (site == null) + " foundation=" + (found != null ? found.name : "none") +
                   " decoyNull=" + (decoy == null) + " alive=" + Components.LiveMinionIdentities.Count +
                   " gameTime=" + GameClock.Instance.GetTime() + " cachedCell=" + target.cachedCell + " storageCell=" + Grid.PosToCell(storage) + " navCost=" +
                   target.GetNavigationCost(nav, target.cachedCell) + " decoyCost=" + decoy.GetNavigationCost(nav, decoy.cachedCell) +
                   " mass=" + Mass(target) + " unreserved=" + target.UnreservedFetchAmount + " reserved=" + target.ReservedAmount +
                   " couldPickUp=" + target.CouldBePickedUpByMinion(minion) +
                   " storage=" + (storage != null) + " automationOnly=" + (automatable != null && automatable.GetAutomationOnly()) +
                   " lockerPrio=" + (storage == null ? -1 : storage.masterPriority.priority_value) +
                   " mandateStorages=" + (mandate == null ? -1 : mandate.Storages.Count) +
                   " matches=" + (mandate != null && mandate.Matches(target));
        }

        public void Cleanup()
        {
            foreach (var chunk in new[] { target, decoy })
                if (chunk != null)
                    Util.KDestroyGameObject(chunk.gameObject);
            var built = Grid.Objects[siteCell, (int)ObjectLayer.FoundationTile];
            if (built != null) Util.KDestroyGameObject(built);
            if (site != null) Util.KDestroyGameObject(site);
            if (locker != null) Util.KDestroyGameObject(locker);
            // Anything the test added (e.g. what the locker dropped when it was removed) must not feed the next test.
            foreach (var extra in Components.Pickupables.Items.Where(p => p != null && !baseline.Contains(p)).ToList())
                if (extra.GetComponent<MinionIdentity>() == null)
                    Util.KDestroyGameObject(extra.gameObject);
        }

        public static Pickupable Spawn(int cell, float mass, SimHashes material = Material)
        {
            var element = ElementLoader.FindElementByHash(material);
            var go = element.substance.SpawnResource(Grid.CellToPosCBC(cell, Grid.SceneLayer.Ore), mass,
                element.defaultValues.temperature, byte.MaxValue, 0, prevent_merge: true);
            return go.GetComponent<Pickupable>();
        }

        // The nearest free standable cells the duplicant can walk to (any row nearby, so leftovers of earlier tests do not matter).
        public static List<int> FindFloorRun(int home)
        {
            var navigator = Components.LiveMinionIdentities[0].GetComponent<Navigator>();
            var origin = Grid.CellToXY(home);
            var result = new List<int>();
            for (var dy = -6; dy <= 6; dy++)
            for (var dx = -25; dx <= 25; dx++)
            {
                if (Mathf.Abs(dx) < 2 && dy == 0) continue; // keep clear of the duplicant itself
                var cell = Grid.XYToCell(origin.x + dx, origin.y + dy);
                if (!IsFreeFloor(cell)) continue;
                if (navigator.GetNavigationCost(cell) < 0) continue;
                result.Add(cell);
            }

            return result.OrderBy(c => Mathf.Abs(Grid.CellToXY(c).x - origin.x) + 2 * Mathf.Abs(Grid.CellToXY(c).y - origin.y))
                .Take(9).ToList();
        }

        private static bool IsFreeFloor(int cell)
        {
            if (!Grid.IsValidCell(cell) || Grid.Solid[cell]) return false;
            var below = Grid.CellBelow(cell);
            if (!Grid.IsValidCell(below) || !Grid.Solid[below]) return false;
            var above = Grid.CellAbove(cell);
            if (!Grid.IsValidCell(above) || Grid.Solid[above]) return false; // headroom (also for the 1x2 locker)
            if (Grid.Element[cell].IsLiquid) return false;
            return Grid.Objects[cell, (int)ObjectLayer.Building] == null &&
                   Grid.Objects[cell, (int)ObjectLayer.FoundationTile] == null &&
                   Grid.Objects[cell, (int)ObjectLayer.Pickupables] == null;
        }
    }
}
#endif
