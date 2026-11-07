# Plan: Web 入力・操作系の実操作対応 (Issue #163)

**Issue**: #163 | **Branch**: `163` | **Base**: `release-0-1-11` @ `2f2b6ba`

## 調査で確認した根本原因 (コード根拠)

1. **Ark Select が開かない**: 全 5 箇所の `<Select.Root>` 呼出し (`editor-toolbar.tsx` Grid/View, `song-metadata-form.tsx` Level, `topbar.tsx` Sort/Level) が `Select.Control` の中に `ValueText` + `Indicator` を直接置き、`Select.Trigger` を描画していない。`@ark-ui/react 5.40.0` の実装 (`node_modules/@ark-ui/react/dist/components/select/select-control.js` / `select-trigger.js`) では Control は `getControlProps()` (div, click/keyboard なし) で、Trigger (`getTriggerProps()`, `role="combobox"` button, onClick/onKeyDown) だけがインタラクティブ。→ クリックもキーボードも何も起きない。
2. **BPM が空にできない**: `editor-toolbar.tsx` が controlled input で `Number.parseInt` 成功時のみ `onBpmChange` を呼ぶため、空文字 draft が確定値に反映されず表示が即座に戻る。Ctrl+A→Delete が「効かない」ように見える。
3. **Duration が 0 になる**: `song-metadata-form.tsx` が `Number(event.currentTarget.value)` を直接 commit するため `Number("") = 0` が即確定する。
4. **テキスト/ID/URL 入力**は controlled string で既に成立 (IME・Ctrl+A はブラウザ標準動作で壊れない)。数値のみ draft モデルが必要。

## Design

### T1: DraftInput (`web/src/components/ui/input.tsx`)

- `DraftInput<T>`: `value: T` (確定値) + `parse: (text) => T | null` + `onCommit: (value: T) => void` + `format?: (value: T) => string`。
- 内部に `draft: string | null` state (null = 非編集)。表示は `draft ?? format(value)`。
- typing 中: draft を保持し、**parse 成功時のみ** `onCommit` (既存のライブ更新 UX を維持)。composition 中 (`compositionstart`〜`compositionend`) は commit しない。
- blur / Enter: 現在のテキストを parse → 成功なら commit、失敗なら draft を捨てて確定値を再表示 (revert)。Enter 後は focus を維持。
- `parseIntegerOrNull`: 全角数字 (U+FF10–FF19) を半角化してから `/^\d+$/` 判定。負号・小数途中は null (revert)。
- `type="number"` は使わず `inputMode` を使う (DOM の number input が部分入力を保持できないため)。

### T2: Ark Select (`web/src/components/ui/select.tsx`)

- `Control` を composite 化: `Select.Control` の中に bare な `Select.Trigger` を自動で置き children を包む。既存呼出し (`<Select.Control><ValueText/><Indicator/></Select.Control>`) は JSX を変えずにクリック/キーボード可能になる = 他 Worker の画面の将来の呼出しも壊さない。
- bare trigger は utilities layer の css で recipe の trigger slot (border/padding/height) を無効化し、見た目は従来どおり Control 側の className が担う。
- focus ring は recipe trigger の `focusVisibleRing: "inside"` に任せる (ボタンに focus が乗るため)。
- `Trigger` も従来どおり export (明示的に使う呼出し側を許容)。

### T3/T4: テスト

- jsdom + `createRoot` + `act` (`song-thumbnail.test.tsx` の既存パターン)。 Zag/Ark が jsdom で必要とする stub (ResizeObserver 等) はテスト内で最小限定義。
- ブラウザE2E: `web/tests/e2e/browser/run.ts` — playwright を dynamic import し、無ければ SKIPPED (exit 0)。`vp dev` をダミートークン付きで起動し、`/`, `/chart`, `/publish` で Ctrl/⌘+A→Delete→再入力・paste・IME (CDP `Input.imeSetComposition`)・キーボード・再選択を検証。`--layout` で 3 解像度の overflow チェック + スクリーンショット。

## Risks

- Zag の select が jsdom で動かない場合 → primitive テストは DOM 属性レベル (role/aria-expanded/handler) に落とし、実操作の証明は Playwright E2E に集約する。
- `styled-system/` は生成物のため、Panda 関係の変更はしない (css prop / 既存 token のみ)。
