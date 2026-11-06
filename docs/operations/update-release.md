# Win64 リリース運用手順 (Issue #35)

展示会場の Windows 機に Unity 本体 (`FlashBeat.exe`) のパッチを配布する手順。
通常起動は `FlashBeat.exe` 直接。専用ランチャー・自動更新・ロールバック UI は作らない (`tools/launcher/**` は Issue #36)。

## 取得先

- GitHub Releases: `https://github.com/rebuildup/FlashBeat/releases`
- アセット命名: `FlashBeat-<version>.zip` + `FlashBeat-<version>.zip.sha256`
  - 例: `FlashBeat-0.1.0-win64-743c88d.zip` (version は `<semver>-win64-<build commit SHA 短縮形>`)
- 展示運営者はブラウザでダウンロードするだけ。GH/CLI/PowerShell・管理者権限はプレイヤーに要求しない。

## ZIP の中身

Unity Win64 ビルド出力フォルダ**一式** (exe 単体ではない):

- `FlashBeat.exe` / `UnityPlayer.dll` / `UnityCrashHandler64.exe`
- `FlashBeat_Data/` (Managed DLL 群・リソース一式)
- `MonoBleedingEdge/`, `D3D12/`, `DirectML.dll`, `dstorage*.dll`
- 除外: `FlashBeat_BurstDebugInformation_DoNotShip/` (Unity の指示により ship 対象外)

## SHA-256 検証手順

```powershell
# ZIP と .sha256 を同じフォルダに置いてから
Get-Content .\FlashBeat-<version>.zip.sha256   # 例: "abc123...  FlashBeat-0.1.0-win64-743c88d.zip"
Get-FileHash .\FlashBeat-<version>.zip -Algorithm SHA256
# 2 つのハッシュが一致すること。不一致ならダウンロード破損 → 再ダウンロード。
```

## パッチ版の発行手順

1. Unity 6000.5.6f1 で Win64 ビルド (`Build → Build Windows 64` または `BuildScript.BuildStandaloneWindows64`)。
2. 実機 smoke (起動→曲選択→プレイ→リザルト) を確認。**実機証拠がなければリリース完了としない** (Issue #22 がゲート)。
3. パッケージ化:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File tools/release/package-win64.ps1 `
       -SourceDir <Unity出力フォルダ> -OutDir <distフォルダ> -Version <semver>-win64-<buildSHA>
   ```

   スクリプトが ZIP + `.sha256` sidecar を作り、内容検査 (exe/Data/Managed DLL の存在・数, arch integrity) を自動実行する。
4. ドラフト Release 作成 (タグは実在しない新タグ。draft の間はタグは push されない):

   ```powershell
   gh release create v<version> --draft --target <build commit SHA> `
       --title "FlashBeat <version>" `
       --notes "SHA-256: <hash>`nSource commit: <full SHA>`n実機検証: <証跡リンク or 未証明>"
   gh release upload v<version> FlashBeat-<version>.zip FlashBeat-<version>.zip.sha256
   ```

5. **公開 (publish) はオーナー手動承認**。権限境界を勝手に越えない:

   ```powershell
   # オーナーが #22 の実機 smoke 合格後に実行
   gh release edit v<version> --draft=false
   ```

6. 不具合が出た場合は既存 Release を差し替えず、**修正版を新しいドラフト Release として発行**する
   (version の build SHA 部分が変わるのでタグ衝突しない)。旧版は archive に残して proven artifact を保持する。

## 更新の種別 (Stage 区別)

| 種別 | 対象 | 配布方法 |
|---|---|---|
| 本体更新 (S0/S1/S2/S3 の Unity 側変更) | exe / FlashBeat_Data | 本手順の ZIP + Release |
| コンテンツのみの更新 (楽曲カタログ・譜面) | Cloudflare 側 | Unity 本体 ZIP とは独立に即時反映 (ZIP 再発行不要) |

## 既存バイナリ互換

- パッチ ZIP はフォルダ全置換。レジストリ書き込み・常駐プロセス・既存の起動経路変更なし。
- 本体が依存する backend API **v1 の契約を壊す変更はリリースしない**。
  破壊的変更が必要な場合は v2 を追加し、v1 を deprecated 期間中維持してから削除する。
- 旧 Unity 本体が新 API を呼んでも壊れないこと (未知フィールド追加は可、必須フィールドの意味変更は不可)。
