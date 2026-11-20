import { CircleCheckIcon, ExternalLink, ListMusic, ListPlus } from "lucide-react";
import { Link } from "@void/react";
import { Button } from "@/components/ui";
import { css } from "styled-system/css";
import type { SongPublishResponse } from "@/lib/publish/types";

/**
 * Verified success view of the new-song flow.
 *
 * Shown only after `publishAndVerify` re-read the catalog and the exact chart
 * snapshot from the API. The checklist tells the booth operator what has to
 * happen before the song shows up in the Unity build: the game refreshes the
 * cloud catalog at the Select screen.
 */

const panel = css({
  bg: "green.solid.bg",
  borderColor: "green.solid.border",
  borderRadius: "16px",
  borderStyle: "solid",
  borderWidth: "1px",
  color: "green.solid.fg",
  display: "grid",
  gap: "3",
  justifyItems: "start",
  margin: "4",
  maxWidth: "40rem",
  padding: "4",
});

const headline = css({
  alignItems: "center",
  display: "flex",
  fontSize: "md",
  fontWeight: "semibold",
  gap: "2",
  margin: "0",
});

const facts = css({
  display: "grid",
  fontSize: "xs",
  gap: "1",
  gridTemplateColumns: "auto minmax(0, 1fr)",
  margin: "0",
});

const factKey = css({
  color: "fg.muted",
  fontSize: "2xs",
  margin: "0",
});

const factValue = css({
  fontSize: "2xs",
  margin: "0",
  overflowWrap: "anywhere",
});

const checklist = css({
  display: "grid",
  fontSize: "2xs",
  gap: "2",
  margin: "0",
  paddingInlineStart: "1rem",
});

const catalogUrl = css({
  fontSize: "2xs",
  overflowWrap: "anywhere",
});

const actions = css({
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
});

const raisedButton = css({
  bg: "gray.8",
  color: "fg.default",
  borderRadius: "8px",
  borderColor: "transparent",
  _hover: { bg: "gray.9" },
});

const accent = css({ colorPalette: "blue" });

/** Caption heading of the post-publish checklist (was an inline style). */
const nextStepTitle = css({
  color: "fg.default",
  fontSize: "xs",
  fontWeight: "semibold",
  margin: "0 0 1",
});

/** The icon inside a button. */
const buttonIcon = css({
  color: "currentColor",
  marginRight: "1",
});

export interface PublishSuccessViewProps {
  response: SongPublishResponse;
  noteCount: number;
  /** Base URL of the API the song was published to. */
  apiBaseUrl: string;
  /** Start the flow over with empty fields. */
  onRegisterAnother: () => void;
}

export function PublishSuccessView({
  response,
  noteCount,
  apiBaseUrl,
  onRegisterAnother,
}: PublishSuccessViewProps) {
  const catalogHref = `${apiBaseUrl}/api/v1/catalog`;

  return (
    <section className={panel} data-testid="new-song-success">
      <p className={headline}>
        <CircleCheckIcon size={18} aria-hidden="true" />
        公開しました
      </p>

      <dl className={facts}>
        <dt className={factKey}>曲ID</dt>
        <dd className={factValue}>{response.songId}</dd>

        <dt className={factKey}>譜面バージョン</dt>
        <dd className={factValue}>v{response.chartVersion}</dd>

        <dt className={factKey}>ノート数</dt>
        <dd className={factValue}>{noteCount}</dd>

        <dt className={factKey}>カタログバージョン</dt>
        <dd className={factValue}>{response.catalogVersion}</dd>
      </dl>

      <div>
        <p className={nextStepTitle}>ゲームで見つかるまで</p>
        <ol className={checklist}>
          <li>
            UnityはSelect画面でクラウドカタログを再取得します。ゲームをSelect画面まで進めるか、開き直してください。
          </li>
          <li>反映まで数秒かかります（ゲームは5〜10秒ごとにポーリングします）。</li>
          <li>
            カタログAPIで公開を確認できます: <span className={catalogUrl}>{catalogHref}</span>
          </li>
        </ol>
      </div>

      <div className={actions}>
        <a href={catalogHref} target="_blank" rel="noreferrer">
          <Button className={raisedButton} size="md">
            <ExternalLink className={buttonIcon} size={14} aria-hidden="true" />
            カタログで確認
          </Button>
        </a>
        <Link href="/">
          <Button className={raisedButton} size="md">
            <ListMusic className={buttonIcon} size={14} aria-hidden="true" />
            Studioのカタログ
          </Button>
        </Link>
        <Button className={accent} size="md" onClick={onRegisterAnother}>
          <ListPlus className={buttonIcon} size={14} aria-hidden="true" />
          新しい曲を登録
        </Button>
      </div>
    </section>
  );
}
