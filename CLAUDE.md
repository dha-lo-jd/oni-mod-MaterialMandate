# CLAUDE.md

ONI mod. Build: `dotnet build` (Debug copies to `ModInstallPath` from `Local.Build.props`).

## 実機検証
ONI 実機で検証するには Gravitas.TestHarness を使う。テストは `[HarnessTest]` を付けた static メソッドとして書く（`MaterialMandate/HarnessTests.cs`、`#if DEBUG`）。
実行: `python C:/Users/dha_1/RiderProjects/GravitasTestHarness/Gravitas.TestProbe/probe.py run --project . --save "Live Human Testing.sav" --tests '{"tags":["materialmandate"]}'`
テストは複製人間のいるセーブが要る（運搬と建築を実際に行わせるため）。`harness.json` の既定のセーブは無人なので、`--save` を付ける。
ゲームを起動し本番の mods.json を書き換えるので、実行前にユーザーに確認する。詳細は harness repo の `README.md` と `protocol.md`。

テストが見ていないもの: 暗転とハイライトの見た目、ボタンの配置、実際のマウス操作（テストは `BuildTool` のクリック処理を直接呼ぶ）。

## 対象指定モードのハイライト
`Ui/PickModeOverlay.cs`。0.25 秒ごとに、カメラに映っている範囲のセルだけを走査する（コロニー全体は走査しない）。候補は選択中の素材（`BuildTool` の `selectedElements`）に合うものだけで、指定済みの対象は素材に関係なく光らせる。仕様は `SPEC.md`。
