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
              <Dialog.Title>Publish “{summary.title}”</Dialog.Title>
              <Dialog.Description>
                Metadata and chart snapshot are written in one request.
              </Dialog.Description>
            </Dialog.Header>

            <Dialog.Body>
              <dl className={summaryList}>
                <dt className={summaryKey}>Song id</dt>
                <dd className={summaryValue}>{summary.songId}</dd>

                <dt className={summaryKey}>Artist</dt>
                <dd className={summaryValue}>{summary.artist || "—"}</dd>

                <dt className={summaryKey}>Video</dt>
                <dd className={summaryValue}>{summary.youtubeId}</dd>

                <dt className={summaryKey}>Level</dt>
                <dd className={summaryValue}>{summary.level}</dd>

                <dt className={summaryKey}>Duration</dt>
                <dd className={summaryValue}>{summary.durationSec} seconds</dd>

                <dt className={summaryKey}>Chart</dt>
                <dd className={summaryValue}>
                  {summary.noteCount} notes at {summary.bpm} BPM
                </dd>
              </dl>

              <Field.Root>
                <Field.Label>Staff token</Field.Label>
                <Input
                  type="password"
                  autoComplete="off"
                  placeholder="FLASHBEAT_ADMIN_TOKEN"
                  value={token}
                  onChange={(event) => onTokenChange(event.currentTarget.value)}
                />
                <Field.HelperText>
                  Entered by the operator at publish time. It is held in this browser tab only and
                  is never written to disk or to the bundle.
                </Field.HelperText>
              </Field.Root>
            </Dialog.Body>

            <Dialog.Footer>
              <Dialog.ActionTrigger asChild>
                <Button variant="outline" size="md" disabled={publishing}>
                  <X className={buttonIcon} size={14} aria-hidden="true" />
                  Cancel
                </Button>
              </Dialog.ActionTrigger>
              <Button
                className={accent}
                size="md"
                loading={publishing}
                loadingText="Publishing"
                disabled={token.trim().length === 0}
                onClick={onPublish}
              >
                <CloudUpload className={buttonIcon} size={14} aria-hidden="true" />
                Publish
              </Button>
            </Dialog.Footer>
          </Dialog.Content>
        </Dialog.Positioner>
      </Portal>
    </Dialog.Root>
  );
}
