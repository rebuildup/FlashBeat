import { CircleCheckIcon } from "lucide-react";
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
  bg: "#3a3a3a",
  color: "#ffffff",
  borderRadius: "8px",
  borderColor: "transparent",
  _hover: { bg: "#454545" },
});

const accent = css({ colorPalette: "blue" });

export interface PublishSuccessViewProps {
  response: SongPublishResponse;
  noteCount: number;
  /** Base URL of the API the song was published to. */
  apiBaseUrl: string;
  /** Start the flow over with empty fields. */
  onRegisterAnother: () => void;
}

export function PublishSuccessView({ response, noteCount, apiBaseUrl, onRegisterAnother }: PublishSuccessViewProps) {
  const catalogHref = `${apiBaseUrl}/api/v1/catalog`;

  return (
    <section className={panel} data-testid="new-song-success">
      <p className={headline}>
        <CircleCheckIcon size={18} />
        公開しました / Published
      </p>

      <dl className={facts}>
        <dt className={factKey}>曲ID / Song id</dt>
        <dd className={factValue}>{response.songId}</dd>

        <dt className={factKey}>譜面バージョン / Chart version</dt>
        <dd className={factValue}>v{response.chartVersion}</dd>

        <dt className={factKey}>ノート数 / Note count</dt>
        <dd className={factValue}>{noteCount}</dd>

        <dt className={factKey}>カタログバージョン / Catalog version</dt>
        <dd className={factValue}>{response.catalogVersion}</dd>
      </dl>

      <div>
        <p style={{ fontSize: "2xs", fontWeight: "semibold", margin: "0 0 1" }}>
          ゲームで見つかるまで / Until it appears in the game
        </p>
        <ol className={checklist}>
          <li>
            UnityはSelect画面でクラウドカタログを再取得します。ゲームをSelect画面まで進めるか、開き直してください。
            / Unity refreshes the cloud catalog at the Select screen — open or reopen it.
          </li>
          <li>
            反映まで数秒かかります（ゲームは5〜10秒ごとにポーリングします）。 / It takes a few
            seconds; the game polls every 5–10 s.
          </li>
          <li>
            カタログAPIで公開を確認できます: <span className={catalogUrl}>{catalogHref}</span>
          </li>
        </ol>
      </div>

      <div className={actions}>
        <a href={catalogHref} target="_blank" rel="noreferrer">
          <Button className={raisedButton} size="md">
            カタログで確認 / Check the catalog
          </Button>
        </a>
        <a href="/">
          <Button className={raisedButton} size="md">
            Studioのカタログ / Studio catalog
          </Button>
        </a>
        <Button className={accent} size="md" onClick={onRegisterAnother}>
          新しい曲を登録 / Register another song
        </Button>
      </div>
    </section>
  );
}
