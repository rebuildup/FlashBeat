/**
 * Void managed auth (Better Auth) の設定 (Issue #799, ADR-0036)。
 *
 * `defineAuth` を置くだけで Void が `/api/auth/*` ハンドラと全リクエストへの
 * セッション解決ミドルウェアを配線し、`requireAuth` / `getSession` /
 * `void/client` の `auth` を有効にする。email + password は Void の既定で
 * 有効 (scrypt ハッシュ、D1 の `session` テーブルでセッション管理)。
 *
 * レート制限は `storage: "database"` で D1 に永続化し、Worker インスタンス間で
 * カウンタを共有する。`void db generate` が rate limit 用テーブルを
 * auth テーブルと一緒にマイグレーションへ含める。
 *
 * ソーシャルログインは `void.config.ts` の `auth.providers` と
 * `AUTH_<PROVIDER>_CLIENT_*` secret が揃った時点で有効化する (未設定の
 * プロバイダを列挙すると起動時に credential エラーになるため、現状は
 * email のみ)。
 */
import { defineAuth } from "void/auth";

export default defineAuth(({ defaults }) => ({
  ...defaults,
  rateLimit: {
    ...defaults.rateLimit,
    // インスタンスローカルのメモリではなく D1 に保存し、全インスタンスで
    // 同じカウンタを共有する。テーブルは `void db generate` が生成する。
    storage: "database",
  },
}));
