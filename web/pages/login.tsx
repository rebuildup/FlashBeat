import { useState } from "react";
import type { FormEvent } from "react";
import { Link } from "@void/react";
import { auth } from "void/client/react";
import { Button, Field, Input } from "@/components/ui";
import { css } from "styled-system/css";
import { useClientVisit } from "@/app/client-visit";
import {
  pageActions,
  pageBackLink,
  pageHeader,
  pageHeaderIdentity,
  pageSubtitle,
  pageTitle,
  panelBody,
  panelFrame,
  primaryCta,
} from "@/app/panel";
import type { Props } from "./login.server";
import "@/styles/app.css";

const card = css({
  display: "flex",
  flex: "1",
  flexDirection: "column",
  minHeight: "0",
});

const form = css({
  display: "flex",
  flexDirection: "column",
  gap: "4",
  maxWidth: "24rem",
  width: "100%",
});

const modeRow = css({
  display: "flex",
  gap: "2",
});

const modeButton = css({
  bg: "transparent",
  borderColor: "gray.outline.border",
  borderRadius: "l2",
  borderWidth: "1px",
  color: "fg.muted",
  cursor: "pointer",
  flex: "1",
  fontSize: "sm",
  minHeight: "10",
  _hover: { color: "fg.default" },
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "colorPalette.solid.bg",
    outlineOffset: "1px",
  },
  "&[data-active=true]": {
    bg: "colorPalette.subtle.bg",
    borderColor: "colorPalette.subtle.border",
    color: "colorPalette.subtle.fg",
  },
});

const errorBox = css({
  bg: "red.subtle.bg",
  borderRadius: "l2",
  color: "red.subtle.fg",
  colorPalette: "red",
  fontSize: "sm",
  padding: "3",
});

const accountBox = css({
  color: "fg.muted",
  display: "flex",
  flexDirection: "column",
  fontSize: "sm",
  gap: "2",
});

type Mode = "signin" | "signup";

/**
 * Account page of the Studio (issue #800): email + password sign in and
 * sign up through Void managed auth (`auth` client of `void/client`).
 * The session cookie is set by `/api/auth/*`; every `/api/v1/me/*` route
 * then resolves the caller server-side.
 */
export default function LoginPage(_props: Props) {
  const visit = useClientVisit();
  const session = auth.useSession();
  const [mode, setMode] = useState<Mode>("signin");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);

  const user = session.data?.user ?? null;

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setPending(true);
    try {
      if (mode === "signup") {
        const result = await auth.signUp.email({
          email: email.trim(),
          password,
          name: name.trim() || email.trim().split("@")[0],
        });
        if (result.error) {
          setError(result.error.message ?? "アカウントを作成できませんでした。");
          return;
        }
      } else {
        const result = await auth.signIn.email({ email: email.trim(), password });
        if (result.error) {
          setError("メールアドレスまたはパスワードが違います。");
          return;
        }
      }
      visit("/my-songs");
    } catch {
      setError("認証サーバーに接続できませんでした。時間をおいて再試行してください。");
    } finally {
      setPending(false);
    }
  }

  async function onSignOut() {
    setError("");
    setPending(true);
    try {
      await auth.signOut();
      visit("/");
    } finally {
      setPending(false);
    }
  }

  return (
    <>
      <header className={pageHeader}>
        <Link className={pageBackLink} href="/">
          ← 曲を選ぶ
        </Link>
        <div className={pageHeaderIdentity}>
          <h1 className={pageTitle}>アカウント</h1>
          <p className={pageSubtitle} data-testid="login-subtitle">
            {user ? "ログイン中のアカウント" : "ログインまたはアカウント作成"}
          </p>
        </div>
        <div className={pageActions}>
          {user ? (
            <Link className={primaryCta} href="/my-songs" data-testid="go-my-songs">
              マイソングへ
            </Link>
          ) : null}
        </div>
      </header>

      <div className={card}>
        <div className={panelBody}>
          <div className={panelFrame} style={{ flex: "0 0 auto" }}>
            <div className={panelBody} style={{ display: "flex", justifyContent: "center" }}>
              {user ? (
                <div className={accountBox} data-testid="signed-in">
                  <p>
                    {user.name}（{user.email}）でログインしています。
                  </p>
                  <div className={pageActions}>
                    <Button variant="outline" size="sm" disabled={pending} onClick={onSignOut}>
                      ログアウト
                    </Button>
                  </div>
                </div>
              ) : (
                <form className={form} onSubmit={onSubmit} data-testid="auth-form">
                  <div className={modeRow} role="tablist" aria-label="認証モード">
                    <button
                      type="button"
                      className={modeButton}
                      data-active={mode === "signin"}
                      onClick={() => setMode("signin")}
                    >
                      ログイン
                    </button>
                    <button
                      type="button"
                      className={modeButton}
                      data-active={mode === "signup"}
                      onClick={() => setMode("signup")}
                    >
                      新規登録
                    </button>
                  </div>

                  {mode === "signup" ? (
                    <Field.Root>
                      <Field.Label>表示名</Field.Label>
                      <Input
                        value={name}
                        onChange={(event) => setName(event.currentTarget.value)}
                        autoComplete="name"
                        maxLength={80}
                      />
                    </Field.Root>
                  ) : null}

                  <Field.Root>
                    <Field.Label>メールアドレス</Field.Label>
                    <Input
                      type="email"
                      required
                      value={email}
                      onChange={(event) => setEmail(event.currentTarget.value)}
                      autoComplete="email"
                    />
                  </Field.Root>

                  <Field.Root>
                    <Field.Label>パスワード</Field.Label>
                    <Input
                      type="password"
                      required
                      minLength={8}
                      value={password}
                      onChange={(event) => setPassword(event.currentTarget.value)}
                      autoComplete={mode === "signup" ? "new-password" : "current-password"}
                    />
                  </Field.Root>

                  {error ? (
                    <p className={errorBox} role="alert" data-testid="auth-error">
                      {error}
                    </p>
                  ) : null}

                  <Button
                    type="submit"
                    className={primaryCta}
                    disabled={pending}
                    data-testid="auth-submit"
                  >
                    {pending ? "送信中…" : mode === "signup" ? "アカウントを作成" : "ログイン"}
                  </Button>
                </form>
              )}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
