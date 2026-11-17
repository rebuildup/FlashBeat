import { Portal } from "@ark-ui/react/portal";
import { CloudUpload, X } from "lucide-react";
import { Button, Dialog, Field, Input } from "@/components/ui";
import { css } from "styled-system/css";

/**
 * Confirmation dialog of the publish screen.
 *
 * The dialog is the last step before the request leaves the browser. It shows
 * exactly what is going to be sent and it takes the staff token of the
 * operator. The token lives in the memory of the browser tab for this screen
 * only: it is never written to disk, never put in the URL and never part of
 * the bundle.
 */

const summaryList = css({
  display: "grid",
  gap: "1",
  gridTemplateColumns: "auto minmax(0, 1fr)",
  margin: "0",
  marginBottom: "4",
});

const summaryKey = css({
  color: "fg.muted",
  fontSize: "xs",
  margin: "0",
});

const summaryValue = css({
  color: "fg.default",
  fontSize: "xs",
  margin: "0",
  overflowWrap: "anywhere",
});

const accent = css({ colorPalette: "blue" });

/** The icon of a dialog footer button. */
const buttonIcon = css({
  color: "currentColor",
  flexShrink: 0,
});

/** What the publish request will contain. */
export interface PublishSummary {
  songId: string;
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  durationSec: number;
  bpm: number;
  noteCount: number;
}

export interface PublishDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Content of the request. */
  summary: PublishSummary;
  /** Staff token of the operator. */
  token: string;
  onTokenChange: (token: string) => void;
  /** `true` while the request is running. */
  publishing: boolean;
  /** Send the request. */
  onPublish: () => void;
}

export function PublishDialog({
  open,
  onOpenChange,
  summary,
  token,
  onTokenChange,
  publishing,
  onPublish,
}: PublishDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={(details) => onOpenChange(details.open)}>
      <Portal>
        <Dialog.Backdrop />
        <Dialog.Positioner>
          <Dialog.Content>
            <Dialog.Header>
              <Dialog.Title>「{summary.title}」を公開</Dialog.Title>
              <Dialog.Description>
                メタデータと譜面スナップショットを1つのリクエストで書き込みます。
              </Dialog.Description>
            </Dialog.Header>

            <Dialog.Body>
              <dl className={summaryList}>
                <dt className={summaryKey}>曲ID</dt>
                <dd className={summaryValue}>{summary.songId}</dd>

                <dt className={summaryKey}>アーティスト</dt>
                <dd className={summaryValue}>{summary.artist || "—"}</dd>

                <dt className={summaryKey}>動画</dt>
                <dd className={summaryValue}>{summary.youtubeId}</dd>

                <dt className={summaryKey}>レベル</dt>
                <dd className={summaryValue}>{summary.level}</dd>

                <dt className={summaryKey}>再生時間</dt>
                <dd className={summaryValue}>{summary.durationSec}秒</dd>

                <dt className={summaryKey}>譜面</dt>
                <dd className={summaryValue}>
                  {summary.noteCount}ノート · {summary.bpm} BPM
                </dd>
              </dl>

              <Field.Root>
                <Field.Label>スタッフトークン</Field.Label>
                <Input
                  type="password"
                  autoComplete="off"
                  placeholder="FLASHBEAT_ADMIN_TOKEN"
                  value={token}
                  onChange={(event) => onTokenChange(event.currentTarget.value)}
                />
                <Field.HelperText>
                  このブラウザタブ内にのみ保持され、ディスクやバンドルには書き込まれません。
                </Field.HelperText>
              </Field.Root>
            </Dialog.Body>

            <Dialog.Footer>
              <Dialog.ActionTrigger asChild>
                <Button variant="outline" size="md" disabled={publishing}>
                  <X className={buttonIcon} size={14} aria-hidden="true" />
                  キャンセル
                </Button>
              </Dialog.ActionTrigger>
              <Button
                className={accent}
                size="md"
                loading={publishing}
                loadingText="公開中"
                disabled={token.trim().length === 0}
                onClick={onPublish}
              >
                <CloudUpload className={buttonIcon} size={14} aria-hidden="true" />
                公開する
              </Button>
            </Dialog.Footer>
          </Dialog.Content>
        </Dialog.Positioner>
      </Portal>
    </Dialog.Root>
  );
}
