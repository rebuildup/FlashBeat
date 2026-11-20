# Web Studio 認証・所有権・脅威モデル (Issue #799)

本書は `web/` (Void + Cloudflare Workers + D1) の認証・認可基盤の脅威モデルである。
実装より先に定義し、各脅威に対する対策とそれを検証する契約テストを対応付ける。
後続 Issue (#800 公開フロー、#801 ルーティング、#802 ビジュアル) は本書の契約の上に構築する。

## 1. 資産とアクター

### 資産

- ユーザーの曲メタデータ・譜面 (下書き・公開済みスナップショット)
- 公開カタログ (`songs` / `chart_versions`) とスコア・ランキング
- 認証セッション (Better Auth のセッショントークン)
- Workspace / Membership (チーム管理の単位)
- 監査ログ・レート制限カウンタ

### アクター

- **未認証の外部者**: 誰でも到達できる。読み取り API は公開情報のみを返す。
- **一般ユーザー**: 自分の Workspace の曲・譜面を作成・編集・(権限に応じて)公開する。
- **Workspace メンバー**: Membership の role (`owner` / `admin` / `member`) に応じた操作。
- **スタッフ**: 従来の `FLASHBEAT_ADMIN_TOKEN` を持つ運用者。管理 API を使う。
- **別ユーザー / 別 Workspace**: IDOR・権限昇格を試みる敵対的アクター。

## 2. 認証方式の決定

**採用: Void managed auth (= Better Auth) + Cloudflare D1。**

- 独自の認証暗号・セッション管理は実装しない。パスワードハッシュは Void 同梱の
  scrypt 実装、セッションは Better Auth が D1 の `session` テーブルで管理する。
- 有効化は `web/auth.ts` (`defineAuth`)。Void が `/api/auth/*` ハンドラと
  全リクエストへのセッション解決ミドルウェアを自動配線し、`requireAuth(c)` /
  `getSession()` / `void/client` の `auth` クライアントを提供する。
- `void db generate` が auth テーブル (user / session / account / verification)
  を `db/migrations/` に含める。プラグインや `user.additionalFields` を
  追加した場合も同じ経路で追従する。
- **compatibility_flags**: Better Auth は `node:crypto` / `node:async_hooks`
  に依存する。Void のデプロイ経路は必要 flag を自動解決する
  (`nodejs_compat` / `nodejs_als`、Void 1.6.2 `dist/index.mjs` の
  wrangler 生成部)。`void.config.ts` への手動追加は不要であり、
  dev (workerd) でも auth の動作を実機検証済み (tests/authz.test.ts)。
  万が一の deploy 時 flag 欠落は `void deploy` の設定検査が拒否するため、
  静かに壊れる経路はない。決定の記録は ADR-0036。
- **レート制限**: auth エンドポイントは Better Auth のレート制限が有効
  (デプロイ環境では `cf-connecting-ip` をキーに既定値が有効化される)。
  永続化のため `rateLimit.storage: "database"` を指定し、インスタンス間で
  カウンタを共有する (要 auth テーブルの `rateLimit` マイグレーション)。
- **セッション失効**: サインアウト・パスワード変更・管理操作による失効は
  `session` テーブルの削除/期限切れとして即時反映される。権限 (Membership)
  の変更も毎リクエスト DB を読むため、キャッシュを挟まず即時反映される。

## 3. 所有権モデル

```
user (Better Auth, 不変ID) ──< membership >── workspace ──< songs
        │                                          │
        └──< audit_log (actor)                     └── ownerWorkspaceId
songs.state: 'draft' | 'published'   (公開状態)
chart_versions: (songId, version) 不変スナップショット + createdByUserId
```

- **正本はセッション + DB の関係**。表示名・メールアドレス・クライアントが
  名乗る ownerId は一切信用しない。全ての変更 API は
  `requireAuth` → Membership 参照 → 対象リソースの `ownerWorkspaceId`
  照合の順でサーバー側検査する (`routes/api/v1/_authz.ts`)。
- **個人 Workspace**: 初回の認証済みアクセス時に遅延プロビジョニングする
  (`kind='personal'`, role='owner')。冪等。
- **チーム Workspace**: `POST /api/v1/me/workspaces` で作成、作成者が
  `owner`。Membership による複数人管理に拡張可能。
- **role と権限**: `owner` / `admin` は曲の編集・公開・メンバー管理、
  `member` は自分が編集権を持つ曲の下書き編集のみ。role の付与・剥奪は
  `owner` のみ (`admin` は `member` の追加・削除まで)。自分自身の
  owner ロール剥奪・最後の owner の削除は禁止 (ロックアウト防止)。
- **既存データ**: 現行の公開曲・譜面・ランキングは `ownerWorkspaceId = NULL`
  / `createdByUserId = NULL` の「スタッフ管理曲」として残す。所有者不明の
  既存曲を一般ユーザーへ帰属させない (#800 の要求と同じルール)。
  `state` の既定は `'published'` で既存行は全て公開曲のまま。

## 4. 脅威と対策

### 4.1 認証・認可

| 脅威 | 対策 | 実装 |
|---|---|---|
| 未認証の変更 API 呼び出し | 全ての `/api/v1/me/*` と今後の変更 API でセッション必須。401 をボディ読取前に返す | `_authz.ts` `requireUser` |
| 他人の下書き・非公開譜面の取得 (IDOR) | リソースは `ownerWorkspaceId` で所有者の Workspace に紐付き、Membership role を毎回 DB 照合。非所有は存在を秘匿して 404 | `_authz.ts` `findOwnedSong` |
| Workspace/Song ID 差し替えによる権限昇格 | 権限は URL/ボディの ID ではなくセッションの userId から導出した Membership で決める。ボディの `workspaceId` は所属検査を通ったものだけ採用 | `_authz.ts` `requireWorkspaceMembership` |
| role 自己昇格 | role 変更は Membership 管理 API のみ。owner 以外は role を付与できず、自分の role を直接書き換える経路は存在しない | `members.ts` |
| CSRF | セッションクッキーは SameSite=Lax + HttpOnly。Void の auth ミドルウェアはクロスオリジンの unsafe メソッドで Origin が trustedOrigins に無い場合セッション Cookie を剥がして解決するため、他オリジンからの POST/PUT は未認証扱いになる。追加で `auth.ts` の `trustedOrigins` は自オリジンのみ | Void `sessionHeaders` (better-auth-shared) |
| セッション固定 | Better Auth はサインイン時に新しいセッショントークンを発行しセッション ID を再生成する。独自のセッション機構は持たない | Better Auth |
| 失効済みセッションの再利用 | セッションは毎リクエスト `session` テーブルを参照。サインアウト・期限切れ・削除は次のリクエストから 401 | Void auth middleware |
| 権限変更の即時反映 | Membership はリクエストごとに DB 参照 (キャッシュなし)。セッション失効と同じ即時性 | `_authz.ts` |
| クッキー | HttpOnly / Secure (https baseURL で自動) / SameSite=Lax。トークンはフロントエンドの永続ストレージ (localStorage 等) に置かない。クライアントは Cookie 認証のみ | Better Auth 既定 |

### 4.2 データサイズ・リソース攻撃

| 脅威 | 対策 |
|---|---|
| 巨大ボディ | JSON パース前に `Content-Length` を検査し、宣言が無い/過少でも実読み込みバイト数を上限 (1 MiB) で打ち切る。超過は 413。`_authz.ts` `readBoundedJson` |
| フィールド数・配列長・入れ子 | パース後にオブジェクトグラフを走査し、深さ (16)・ノード数 (100,000)・文字列長 (8,192) を検査。超過は 422/413。`assertJsonLimits` |
| 極端な譜面 | 下書きは編集中で壊れた譜面を許容するため厳密検証は公開時 (#800) に行い、ここではサイズ・構造の上限のみ。公開経路は既存の契約スキーマ + 意味検証 (`_publish-model.ts`) |
| 曲数・API 呼び出し | 利用者単位のレート制限を `rate_limit_hits` (固定ウィンドウ、原子的インクリメント) で実施。下書き保存 60 回/分、Workspace/メンバー変更 30 回/分。auth エンドポイントは Better Auth 側の制限 (§2) |
| 同時大量リクエスト | Cloudflare 側のレート制限・Bot 対策は運用で別途設定 (フォローアップ)。D1 の書き込みは `db.batch` の暗黙トランザクションで原子化し、同時更新の競合は一意制約/冪等 upsert で処理 |

### 4.3 インジェクション

| 脅威 | 対策 |
|---|---|
| SQL インジェクション | 全クエリは Drizzle のパラメータ化クエリまたは `sql` テンプレート (バインド変数)。生文字列連結はしない。入力値はパターン検証を通す |
| XSS | API は JSON のみを返し、文字列はエンコードせずそのまま返す (React のテキスト描画がエスケープする前提)。`dangerouslySetInnerHTML` は使わない。保存値を HTML として解釈する経路は存在しない |
| SSRF / 任意 URL 取得 | 本基盤は外部 URL を取得しない。`youtubeId` は 11 文字の ID パターンのみ受け付け、URL 文字列は受け付けない (公開メタデータは #800 で同じ規則を継承) |
| OS コマンド | Worker 内にシェル実行経路は存在しない。ユーザー入力をコマンドへ埋め込まない |
| 出力エンコード | JSON API のみ。`Content-Type: application/json`、文字列は JSON エスケープ。ヘッダー・CSV・HTML 出力は本基盤に存在しない |
| 情報漏洩 | エラー応答は `{ error, diagnostics? }` の定型で、SQL・スタック・他ユーザー情報を含めない。認証情報・下書き内容・PII はログに出さない (Void/Better Auth の既定ログもセッション値を出さない)。クライアントバンドルに秘密値を置かない (`BETTER_AUTH_SECRET` は `VITE_` ではない) |

### 4.4 運用

| 脅威 | 対策 |
|---|---|
| D1 マイグレーション | `void db generate` の差分 SQL のみ追加し、既存テーブルは `ALTER TABLE ADD COLUMN` (NULL 許容 or DEFAULT 付き) に留める。既存の曲・譜面・ランキング行は変更しない。ロールバックは「新テーブル/列を使わない旧 Worker へ戻す」ことで可能 (追加物は後方互換)。リモート適用はレビュー後に `void db migrate --remote` |
| 監査ログ | `audit_log` テーブルに Workspace 作成・メンバー追加/削除・曲の下書き作成/更新を記録 (actorUserId, action, target, detail JSON, createdAt)。公開・管理操作の記録は #800 で同じテーブルへ追加する |
| Cloudflare 側対策 | エッジのレート制限ルールはダッシュボード/Terraform 側の運用タスクとして PR に明記 (コードでは完結しない) |

## 5. 契約テスト (tests/authz.test.ts)

実 Worker + 実ローカル D1 で以下の負例を検証する (肯定例だけでは不十分):

- 未認証: `GET /api/v1/me`・`PUT /api/v1/me/songs/{id}` → 401
- 別ユーザー: B が A の曲を GET/PUT → 404 (存在秘匿)
- 別 Workspace: A が B の workspaceId をボディに指定 → 403/404
- role: `member` は publish 権限を持たない (canPublish=false を返す)、
  member はメンバー管理不可
- 大容量: Content-Length 超過 → 413、チャンク読取超過 → 413
- 不正 JSON / 深いネスト / 巨大ノード数 → 422/413
- XSS 文字列の保存→取得ラウンドトリップ (API は文字列をそのまま返す)
- SQL インジェクション文字列を含む songId/title → パターン 422 / そのまま保存
  (パラメータ化で効果を持たない)
- CSRF: 別 Origin からの PUT (Cookie 付き) → セッション無効で 401
- 並列更新: 同一曲への同時 PUT → 競合せず最終状態が一貫
- 失効: サインアウト後の `/api/v1/me` → 401、セッション行削除後も 401
- レート制限: 上限超過 → 429
- 下書きは公開カタログに出ない (`GET /api/v1/catalog` に現れない)
