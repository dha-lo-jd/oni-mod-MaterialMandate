# CLAUDE.md

ONI mod. Build: `dotnet build` (Debug copies to `ModInstallPath` from `Local.Build.props`).

## 実機検証
ONI 実機で検証するには Gravitas.TestHarness を使う。テストは `[HarnessTest]` を付けた static メソッドとして書く（`MaterialMandate/HarnessTests.cs`、`#if DEBUG`）。
実行: `python C:/Users/dha_1/ONI-Modding/GravitasTestHarness/Gravitas.TestProbe/probe.py run --project . --summary --tests '{"tags":["materialmandate"]}'`
テストは複製人間のいるセーブが要る（運搬と建築を実際に行わせるため）。`harness.json` の既定のセーブが `Live Human Testing.sav`（11/13 に直した）。
ゲームを起動し本番の mods.json を書き換えるので、実行前にユーザーに確認する。詳細は harness repo の `README.md` と `protocol.md`。

クリックはハーネスの入力再現（`Play.Click`）で行う。ゲームが実マウスに対して呼ぶ入り口を通り、カメラとマウス位置をセルに合わせる。
テストが見ていないもの: 暗転とハイライトの見た目、ボタンの配置、キー入力。

## 運搬の絞り込み
`Patches/FetchPatches.cs`。運搬元を選ぶ `FetchChore.FindFetchTarget` と、運搬計画を立てる `FetchAreaChore.StatesInstance.Begin` の 2 か所に手を入れている。`Begin` は付近の同素材を集めた合計から運搬量を決めて運搬タスクに伝えるので、対象外の物体は**集める段階で**外す（あとから積み荷を減らすと、約束した量より少なく届いて建築が素材不足のまま完成する）。テストの場面は複製人間の立ち位置で配置が変わるので、配置に依存する不具合は `Scene.Create` の引数で配置を固定して再現する。

## 対象指定モードのハイライト
`Ui/PickModeOverlay.cs`。0.25 秒ごとに、カメラに映っている範囲のセルだけを走査する（コロニー全体は走査しない）。候補は選択中の素材（`BuildTool` の `selectedElements`）に合うものだけ。クリックで指定できるのも同じ範囲（`MandateSelection.Toggle`）で、光るものと指定できるものは一致させる。素材を選び直すと `BuildTool.Activate` が呼び直されるので、そこで使用対象群を空にする。仕様は `SPEC.md`。
