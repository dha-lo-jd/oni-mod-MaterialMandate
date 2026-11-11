#if DEBUG
// In-game tests run by Gravitas.TestHarness:
//   python Gravitas.TestProbe/probe.py run --project <this repo> --save "Live Human Testing.sav" --tests '{"tags":["materialmandate"]}'
// Needs HarnessDllPath in Local.Build.props (Check / Wait come from the harness assembly).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gravitas.TestHarness;
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

    // Targets (300 kg) outweigh the need (200 kg for a Tile): the nearer decoy chunk must stay untouched.
    [HarnessTest(Name = "materialmandate.e1_target_used_exclusively", Tags = "materialmandate,e2e", TimeoutSeconds = 600)]
    public static IEnumerator TargetUsedExclusively()
    {
        var scene = Scene.Create(targetMass: 300f, decoyMass: 200f);
        yield return Wait.Seconds(300f, 3);
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
    }

    // Targets (40 kg) are smaller than the need (200 kg): all of it goes in, the rest comes from the decoy.
    [HarnessTest(Name = "materialmandate.e2_falls_back_when_exhausted", Tags = "materialmandate,e2e", TimeoutSeconds = 600)]
    public static IEnumerator FallsBackWhenExhausted()
    {
        var scene = Scene.Create(targetMass: 40f, decoyMass: 300f);
        yield return Wait.Seconds(300f, 3);
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
    }

    // The whole player flow: build menu -> pick-mode button -> click debris -> leave pick mode -> place the building.
    [HarnessTest(Name = "materialmandate.e0_ui_flow", Tags = "materialmandate,e2e", TimeoutSeconds = 600)]
    public static IEnumerator UiFlow()
    {
        var scene = Scene.Create(targetMass: 300f, decoyMass: 200f, viaUi: true);
        yield return null;
        scene.RunUiFlow();
        yield return Wait.Seconds(300f, 3);
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
    }

    // Same as e0, but the use target is a storage locker holding the 300 kg: the stored chunk is delivered, the decoy is not.
    [HarnessTest(Name = "materialmandate.e0b_container_ui_flow", Tags = "materialmandate,e2e", TimeoutSeconds = 600)]
    public static IEnumerator ContainerUiFlow()
    {
        var scene = Scene.Create(targetMass: 300f, decoyMass: 200f, viaUi: true, container: true);
        yield return null;
        scene.RunUiFlow();
        yield return Wait.Seconds(300f, 3);
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
    }

    private sealed class Scene
    {
        private GameObject locker;
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
            var minion = Components.LiveMinionIdentities[0];
            var home = Grid.PosToCell(minion.transform.GetPosition());
            var cells = FindFloorRun(home);
            Check.True(cells.Count >= 3, "not enough open floor next to the duplicant (" + cells.Count + " cells)");

            var scene = new Scene();
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
        }

        private static Pickupable Spawn(int cell, float mass)
        {
            var element = ElementLoader.FindElementByHash(Material);
            var go = element.substance.SpawnResource(Grid.CellToPosCBC(cell, Grid.SceneLayer.Ore), mass,
                element.defaultValues.temperature, byte.MaxValue, 0, prevent_merge: true);
            return go.GetComponent<Pickupable>();
        }

        // Free standable cells in the duplicant's row that are connected to it, nearest first.
        private static List<int> FindFloorRun(int home)
        {
            var result = new List<int>();
            foreach (var direction in new[] { 1, -1 })
            {
                var cell = home;
                for (var i = 0; i < 25; i++)
                {
                    cell = direction > 0 ? Grid.CellRight(cell) : Grid.CellLeft(cell);
                    if (!IsFreeFloor(cell)) break;
                    if (i >= 1) result.Add(cell);
                }
            }

            return result.OrderBy(c => Mathf.Abs(Grid.CellToXY(c).x - Grid.CellToXY(home).x)).ToList();
        }

        private static bool IsFreeFloor(int cell)
        {
            if (!Grid.IsValidCell(cell) || Grid.Solid[cell]) return false;
            var below = Grid.CellBelow(cell);
            if (!Grid.IsValidCell(below) || !Grid.Solid[below]) return false;
            if (Grid.Element[cell].IsLiquid) return false;
            return Grid.Objects[cell, (int)ObjectLayer.Building] == null &&
                   Grid.Objects[cell, (int)ObjectLayer.FoundationTile] == null &&
                   Grid.Objects[cell, (int)ObjectLayer.Pickupables] == null;
        }
    }
}
#endif
