# ADR-0036: Web Studio の認証は Void managed auth (Better Auth) + D1

- Status: Accepted
- Date: 2026-11-24
- Resolves: Issue #799 (Web Studio 認証・所有権・脅威モデル基盤)
- Related: Issue #800 (アカウント単位の制作〜公開フロー), Issue #794 (権利監査)

## Context

現行の Web Studio は `FLASHBEAT_ADMIN_TOKEN` の bearer token 入力だけで、
一般ユーザーが自分の曲・譜面を作成・保存・編集・公開する製品要件
(Issue #799/#800) を満たせない。認証・セッション・パスワード保管を
自前で実装することは、Issue が明示的に禁じる「実績ある認証ライブラリを
使用する。独自の認証暗号・セッション管理を一から作らない」に反する。

Void 1.6.2 は Better Auth をネイティブ統合している:

- `auth.ts` の `defineAuth` で設定し、`/api/auth/*` ハンドラと
  全リクエストへのセッション解決ミドルウェアが自動配線される
- `requireAuth(c)` / `getSession()` / `getUser()` (`void/auth`)、
  `auth` クライアント (`void/client`) を提供する
- `void db generate` が auth テーブル (user / session / account /
  verification、プラグイン分を含む) を `db/migrations/` に生成する
- パスワードハッシュは scrypt、セッションは D1 の `session` テーブル

## Decision

**Void managed auth (= Better Auth) を採用し、ユーザー/セッション/
アカウントは既存の FlashBeat D1 (`flashbeat`) に同居させる。**

- `web/auth.ts` で `defineAuth` を有効化。email + password を既定で
  有効化し、`rateLimit.storage: "database"` でレート制限カウンタを
  D1 に永続化する (Worker インスタンス間で共有するため)。
- `compatibility_flags`: Better Auth は `node:crypto` /
  `node:async_hooks` に依存する。Void のデプロイ経路は必要 flag
  (`nodejs_compat` / `nodejs_als`) を wrangler 生成時に自動解決し、
  `void.config.ts` への手動追加は不要。dev (workerd) で auth
  エンドポイントとセッション認証を実機検証した
  (`web/tests/authz.test.ts`)。万が一 flag が欠けた場合は
  `void deploy` の設定検査が拒否するため静かに壊れない。
- `BETTER_AUTH_SECRET` は Void が deploy 時に自動生成・再利用する。
  ローカル dev は Void の dev 既定値で動く。本番値は Infisical →
  Worker secret の既存経路 (`script/with-secrets`) に乗せる。
- Workspace / Membership / 監査ログ / レート制限カウンタは
  Better Auth の管理外として `db/schema.ts` に定義し、
  `void db generate` の通常マイグレーションで管理する。

## Consequences

- 認証テーブルが既存の festival D1 に追加される。D1 は単一 DB 運用
  (`void.config.ts` の `d1_databases`) を維持し、分離はテーブル名で行う。
- ユーザー ID は Better Auth の不変 ID (text)。`songs.createdByUserId` /
  `memberships.userId` はこの ID を保持し、auth テーブルへの外部キーは
  張らない (スキーマモジュールが分かれるため。整合は API 層で担保)。
- セッション Cookie は HttpOnly / Secure / SameSite=Lax (Better Auth
  既定)。クロスオリジンの unsafe メソッドでは Void がセッション Cookie
  を剥がすため、CSRF はフレームワーク側で塞がる (脅威モデル §4.1)。
- auth エンドポイントのレート制限は Better Auth 既定 + database
  storage。エッジ側の追加レート制限は運用フォローアップ。
- 所有権・公開フローの詳細は `docs/architecture/web-auth-threat-model.md`、
  実装契約は `web/routes/api/v1/_authz.ts` と `/api/v1/me/*` を参照。
  #800 はこの上にユーザー公開フローを構築する。
- 代替案 (Auth0 / Clerk / 自前 JWT) は追加の外部依存・コスト・
  D1 との整合コストがあり、Void 統合の実績ある経路を優先して却下。

## Verification

- `void db generate` が auth テーブルを含むマイグレーションを生成すること
- `node --test tests/` の `authz.test.ts` が sign-up → セッション →
  `/api/v1/me` の往復と負例契約を実 Worker + 実 D1 で検証すること
- `vp build` が auth 有効の Worker をビルドできること
  (deploy 時の flag 解決はレビュー時に確認)

## Follow-ups

- ソーシャルログイン (Google/GitHub) は `auth.providers` +
  `AUTH_<PROVIDER>_CLIENT_*` secret が揃った時点で有効化する。
  初回は email/password のみ。
- Cloudflare エッジのレート制限・WAF ルールは運用タスクとして別途設定。
- #800 でユーザー公開 API・draft→publish 昇格・権利情報の記録を実装し、
  `audit_log` へ公開操作を追記する。
- `void db status --remote` / `void db migrate --remote` でリモート適用を
  確認し、既存曲・譜面・ランキングが保たれることを検証する。

## Known limitations

- Better Auth のメール送信 (パスワードリセット・メール確認) は未設定。
  プロバイダ統合は必要になった時点で `void/email` 経路を評価する。
- ソーシャルログインは credential 未設定のため email/password のみ。
