# Material Mandate

An Oxygen Not Included mod that lets a construction order take its materials from **the exact debris and containers
you point at**, instead of whatever matching material happens to be nearest.

## How to use

1. Open the build menu, pick a building and its material as usual.
2. Press **Designate use targets**, between the material list and the priority panel.
3. Click debris on the ground and storage containers to add them to the use targets. The button turns green and
   shows how many targets are registered.
4. Leave the designation mode by pressing the button again, **Esc** or right-click, then place the building.

Every construction order placed while use targets are registered is bound to them. **Clear** empties the list, and
it is also emptied when you leave the build tool.

## Rules

- **Debris and containers**: a debris pile is used as that single object. A container offers everything inside it
  that matches the selected material.
- **Only the selected material counts**: targets of another material are ignored. For buildings that need several
  materials, a target is used for the slot that accepts it.
- **Targets first, then normal delivery**: the order takes nothing from elsewhere until the targets run out. After
  that, the rest is delivered from the general stock as in the unmodded game.
- **Only as much as needed**: a target larger than the required amount is split, and the remainder stays where it is.
- **Never blocks construction**: a target that is unreachable or no longer exists is dropped, and the order falls back
  to normal delivery.
- **Shared targets**: when several orders use the same target, it goes to whichever delivery starts first. Orders
  left short are completed by normal delivery.
- **Not saved**: use targets are not stored in the save file. After loading, pending orders use normal delivery.

## Translations

Text is translated through `translations/<language code>.po` (English and Japanese are included).

## Compatibility

- ONI build: **744825 or later**
- Required DLC: none
- Patches `FetchChore.FindFetchTarget`, `FetchAreaChore.StatesInstance.Begin`, `BuildingDef.TryPlace`,
  `ProductInfoScreen.ConfigureScreen` and the left click of `BuildTool` (Harmony).

## Local build setup

1. Copy `MaterialMandate/Local.Build.props.example` to `MaterialMandate/Local.Build.props` (git-ignored).
2. Set `GameManagedPath` (`OxygenNotIncluded_Data\Managed`) and `ModInstallPath` (e.g. `...\mods\local\MaterialMandate`).
   Alternatively set the environment variables `ONI_GAME_MANAGED_PATH` / `ONI_MOD_INSTALL_PATH`.
3. `dotnet build` — Debug builds copy the DLL, pdb, `mod.yaml`, `mod_info.yaml` and `translations/*.po` to
   `ModInstallPath`. Release builds create a distribution folder in `release/MaterialMandate`.

The design notes are in `SPEC.md` (Japanese).
