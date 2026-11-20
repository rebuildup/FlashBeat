import { useEffect, useState } from "react";
import { Portal } from "@ark-ui/react/portal";
import { createListCollection } from "@ark-ui/react/collection";
import { CloudUpload, X } from "lucide-react";
import { Button, Dialog, Field, Input, Select } from "@/components/ui";
import { css } from "styled-system/css";
import type { MeDraftDetail, PublishDraftBody } from "@/lib/me/client";

/**
 * Publish dialog of the per-account flow (issue #800).
 *
 * The staff publish screen asks for `FLASHBEAT_ADMIN_TOKEN`; this dialog
 * asks for the one thing the staff flow cannot know — the rights
 * attestation. `rightsConfirmed` must be checked explicitly and
 * `materialSource` / `rightsStatus` are mandatory, because the server
 * refuses a publish without them (422).
 */

const RIGHTS_OPTIONS = [
  { value: "original", label: "自作・オリジナル" },
  { value: "licensed", label: "ライセンス取得済み" },
  { value: "permission", label: "権利者の許可あり" },
  { value: "other", label: "その他" },
] as const;

const rightsCollection = createListCollection({
  items: RIGHTS_OPTIONS.map((option) => ({ ...option })),
});

const grid = css({
  display: "grid",
  gap: "3",
  gridTemplateColumns: { base: "1fr", md: "1fr 1fr" },
});

const fullRow = css({ gridColumn: "1 / -1" });

const checkboxRow = css({
  alignItems: "flex-start",
  color: "fg.default",
  display: "flex",
  fontSize: "sm",
  gap: "2",
  lineHeight: "1.5",
});

const checkbox = css({
  accentColor: "colorPalette.solid.bg",
  flexShrink: 0,
  height: "1rem",
  marginTop: "0.2em",
  width: "1rem",
});

const errorText = css({
  color: "red.subtle.fg",
  colorPalette: "red",
  fontSize: "xs",
  margin: 0,
});

const selectControl = css({
  alignItems: "center",
  bg: "transparent",
  borderColor: "gray.outline.border",
  borderRadius: "l2",
  borderWidth: "1px",
  cursor: "pointer",
  display: "flex",
  gap: "2",
  height: "sizes.10",
  justifyContent: "space-between",
  paddingInline: "3",
  textAlign: "left",
  width: "full",
});

const buttonIcon = css({
  color: "currentColor",
  flexShrink: 0,
});

export interface PublishDraftDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** The draft being published (detail incl. chart). */
  draft: MeDraftDetail | null;
  publishing: boolean;
  error: string;
  onPublish: (body: PublishDraftBody) => void;
}

export function PublishDraftDialog({
  open,
  onOpenChange,
  draft,
  publishing,
  error,
  onPublish,
}: PublishDraftDialogProps) {
  const [title, setTitle] = useState("");
  const [artist, setArtist] = useState("");
  const [youtubeId, setYoutubeId] = useState("");
  const [level, setLevel] = useState("5");
  const [durationSec, setDurationSec] = useState("30");
  const [materialSource, setMaterialSource] = useState("");
  const [rightsStatus, setRightsStatus] = useState<string>("original");
  const [license, setLicense] = useState("");
  const [rightsConfirmed, setRightsConfirmed] = useState(false);

  // Re-seed the form every time the dialog opens on a (possibly new)
  // draft, so a previous song's values never leak into the next publish.
  useEffect(() => {
    if (!open || !draft) return;
    setTitle(draft.title);
    setArtist(draft.artist);
    setYoutubeId(draft.youtubeId ?? "");
    setLevel(draft.level === null ? "5" : String(draft.level));
    setDurationSec(draft.durationSec === null ? "30" : String(draft.durationSec));
    setMaterialSource(draft.materialSource ?? "");
    setRightsStatus(draft.rightsStatus ?? "original");
    setLicense(draft.license ?? "");
    setRightsConfirmed(false);
  }, [open, draft]);

  function submit() {
    if (!draft?.chart) return;
    onPublish({
      title: title.trim(),
      artist: artist.trim(),
      youtubeId: youtubeId.trim(),
      level: Number(level),
      durationSec: Number(durationSec),
      chart: draft.chart,
      rightsConfirmed: true,
      materialSource: materialSource.trim(),
      rightsStatus,
      license: license.trim().length > 0 ? license.trim() : null,
    });
  }

  const canSubmit =
    !publishing &&
    rightsConfirmed &&
    materialSource.trim().length > 0 &&
    title.trim().length > 0 &&
    youtubeId.trim().length === 11 &&
    Number.isInteger(Number(level)) &&
    Number(level) >= 1 &&
    Number(level) <= 99 &&
    Number(durationSec) > 0 &&
    draft?.chart != null;

  return (
    <Dialog.Root open={open} onOpenChange={(details) => onOpenChange(details.open)}>
      <Portal>
        <Dialog.Backdrop />
        <Dialog.Positioner>
          <Dialog.Content>
            <Dialog.Header>
              <Dialog.Title>「{draft?.title || draft?.songId || ""}」を公開</Dialog.Title>
              <Dialog.Description>
                曲ID {draft?.songId ?? "—"} ·{" "}
                {draft?.publishedVersion
                  ? `更新（v${draft.publishedVersion} → 次の版）`
                  : "新規公開"}
                。公開するとカタログに表示されます。
              </Dialog.Description>
            </Dialog.Header>

            <Dialog.Body>
              <div className={grid}>
                <Field.Root className={fullRow}>
                  <Field.Label>曲名</Field.Label>
                  <Input
                    value={title}
                    onChange={(e) => setTitle(e.currentTarget.value)}
                    maxLength={160}
                  />
                </Field.Root>
                <Field.Root>
                  <Field.Label>アーティスト</Field.Label>
                  <Input
                    value={artist}
                    onChange={(e) => setArtist(e.currentTarget.value)}
                    maxLength={160}
                  />
                </Field.Root>
                <Field.Root>
                  <Field.Label>YouTube動画ID</Field.Label>
                  <Input
                    value={youtubeId}
                    onChange={(e) => setYoutubeId(e.currentTarget.value)}
                    maxLength={11}
                  />
                </Field.Root>
                <Field.Root>
                  <Field.Label>レベル（1-99）</Field.Label>
                  <Input
                    type="number"
                    min={1}
                    max={99}
                    value={level}
                    onChange={(e) => setLevel(e.currentTarget.value)}
                  />
                </Field.Root>
                <Field.Root>
                  <Field.Label>再生時間（秒）</Field.Label>
                  <Input
                    type="number"
                    min={1}
                    max={1800}
                    step="0.1"
                    value={durationSec}
                    onChange={(e) => setDurationSec(e.currentTarget.value)}
                  />
                </Field.Root>

                <Field.Root className={fullRow}>
                  <Field.Label>素材の出所</Field.Label>
                  <Input
                    value={materialSource}
                    onChange={(e) => setMaterialSource(e.currentTarget.value)}
                    maxLength={200}
                    placeholder="例: 自作、〇〇氏から許諾"
                  />
                  <Field.HelperText>
                    この曲・譜面の素材がどこから来たかを記録します。
                  </Field.HelperText>
                </Field.Root>

                <Field.Root>
                  <Field.Label>権利状態</Field.Label>
                  <Select.Root
                    collection={rightsCollection}
                    value={[rightsStatus]}
                    onValueChange={(details) => {
                      const [next] = details.value;
                      if (next) setRightsStatus(next);
                    }}
                  >
                    <Select.Control className={selectControl}>
                      <Select.ValueText placeholder="選択してください" />
                      <Select.Indicator />
                    </Select.Control>
                    <Portal>
                      <Select.Positioner>
                        <Select.Content>
                          <Select.List>
                            {RIGHTS_OPTIONS.map((option) => (
                              <Select.Item key={option.value} item={option}>
                                <Select.ItemText>{option.label}</Select.ItemText>
                                <Select.ItemIndicator />
                              </Select.Item>
                            ))}
                          </Select.List>
                        </Select.Content>
                      </Select.Positioner>
                    </Portal>
                  </Select.Root>
                </Field.Root>

                <Field.Root>
                  <Field.Label>利用許可（任意）</Field.Label>
                  <Input
                    value={license}
                    onChange={(e) => setLicense(e.currentTarget.value)}
                    maxLength={500}
                    placeholder="例: 改変・再配布の条件"
                  />
                </Field.Root>

                <label className={`${checkboxRow} ${fullRow}`}>
                  <input
                    type="checkbox"
                    className={checkbox}
                    checked={rightsConfirmed}
                    onChange={(e) => setRightsConfirmed(e.currentTarget.checked)}
                    data-testid="rights-confirmed"
                  />
                  <span>この曲と譜面の素材について、利用する権利があることを確認しました。</span>
                </label>
              </div>

              {error ? (
                <p className={errorText} role="alert" data-testid="publish-error">
                  {error}
                </p>
              ) : null}
            </Dialog.Body>

            <Dialog.Footer>
              <Dialog.ActionTrigger asChild>
                <Button variant="outline" size="md" disabled={publishing}>
                  <X className={buttonIcon} size={14} aria-hidden="true" />
                  キャンセル
                </Button>
              </Dialog.ActionTrigger>
              <Button
                size="md"
                className={css({ colorPalette: "blue" })}
                loading={publishing}
                loadingText="公開中"
                disabled={!canSubmit}
                onClick={submit}
                data-testid="publish-submit"
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
