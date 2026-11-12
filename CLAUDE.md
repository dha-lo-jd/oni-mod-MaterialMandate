# CLAUDE.md

ONI mod. Build: `dotnet build` (Debug copies to `ModInstallPath` from `Local.Build.props`).

## 実機検証
ONI 実機で検証するには Gravitas.TestHarness を使う。テストは `[HarnessTest]` を付けた static メソッドとして書く（`MaterialMandate/HarnessTests.cs`、`#if DEBUG`）。
実行: `python C:/Users/dha_1/RiderProjects/GravitasTestHarness/Gravitas.TestProbe/probe.py run --project . --tests '{"tags":["materialmandate"]}'`
ゲームを起動し本番の mods.json を書き換えるので、実行前にユーザーに確認する。詳細は harness repo の `README.md` と `protocol.md`。
