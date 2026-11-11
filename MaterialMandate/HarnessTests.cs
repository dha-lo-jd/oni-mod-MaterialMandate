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

    private sealed class Scene
    {
        private Pickupable target;
        private Pickupable decoy;
        private GameObject site;
        private int siteCell;

        // The construction site object is destroyed when the building completes and a foundation tile appears.
        public bool IsBuilt => site == null && Grid.Objects[siteCell, (int)ObjectLayer.FoundationTile] != null;

        public float TargetMass => Mass(target);
        public float DecoyMass => Mass(decoy);

        private static float Mass(Pickupable p) => p == null ? 0f : p.GetComponent<PrimaryElement>().Mass;

        public static Scene Create(float targetMass, float decoyMass)
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
            scene.target = Spawn(targetCell, targetMass);

            var def = Assets.GetBuildingDef("Tile");
            var elements = new List<Tag> { Material.CreateTag() };
            scene.site = def.Instantiate(Grid.CellToPosCBC(scene.siteCell, Grid.SceneLayer.Building), Orientation.Neutral,
                elements);

            MandateSelection.Clear();
            MandateSelection.Pickups.Add(scene.target);
            MandateSelection.ApplyTo(scene.site);
            MandateSelection.Clear();
            Check.NotNull(scene.site.GetComponent<ConstructionMandate>(), "mandate was not attached");
            return scene;
        }

        public void Cleanup()
        {
            foreach (var chunk in new[] { target, decoy })
                if (chunk != null)
                    Util.KDestroyGameObject(chunk.gameObject);
            var built = Grid.Objects[siteCell, (int)ObjectLayer.FoundationTile];
            if (built != null) Util.KDestroyGameObject(built);
            if (site != null) Util.KDestroyGameObject(site);
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
