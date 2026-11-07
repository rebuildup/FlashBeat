import { createListCollection } from "@ark-ui/react/collection";
import { Portal } from "@ark-ui/react/portal";
import { CirclePlay, Gauge, Hash, Music, Timer, User } from "lucide-react";
import { Field, Input, Select } from "@/components/ui";
import { css } from "styled-system/css";
import type {
  SongMetadataDraft,
  SongMetadataField,
  SongMetadataIssue,
} from "@/lib/publish/validation";

/**
 * Metadata form of the publish screen.
 *
 * The fields are the ones the publish contract asks for. Every rule is
 * checked while the operator types, and a broken field is marked, so a
 * rejected publish never comes as a surprise.
 */

const form = css({
  display: "grid",
  gap: "3",
});

/** The icon of a field label. */
const labelIcon = css({
  color: "fg.muted",
  flexShrink: 0,
  marginRight: "1",
  verticalAlign: "-2px",
});

const selectRoot = css({
  width: "full",
});

const selectControl = css({
  alignItems: "center",
  borderColor: "gray.surface.border",
  borderRadius: "l1",
  borderWidth: "1px",
  display: "flex",
  gap: "2",
  height: "sizes.10",
  justifyContent: "space-between",
  paddingInline: "3",
  textAlign: "left",
  width: "full",
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "colorPalette.solid.bg",
    outlineOffset: "1px",
  },
});

interface LevelItem {
  label: string;
  value: string;
  group: string;
}

/** Level bands of the contract range `1` to `99`. */
const LEVEL_GROUPS: { label: string; from: number; to: number }[] = [
  { label: "Easy", from: 1, to: 9 },
  { label: "Normal", from: 10, to: 19 },
  { label: "Hard", from: 20, to: 29 },
  { label: "Expert", from: 30, to: 49 },
  { label: "Master", from: 50, to: 99 },
];

const LEVEL_ITEMS: LevelItem[] = LEVEL_GROUPS.flatMap((group) =>
  Array.from({ length: group.to - group.from + 1 }, (_, index) => {
    const level = group.from + index;
    return { label: `Level ${level}`, value: String(level), group: group.label };
  }),
);

const levelCollection = createListCollection<LevelItem>({ items: LEVEL_ITEMS });

export interface SongMetadataFormProps {
  /** Current values of the fields. */
  draft: SongMetadataDraft;
  /** Called with the fields that changed. */
  onChange: (patch: Partial<SongMetadataDraft>) => void;
  /** Called when a field loses the focus, so its error becomes visible. */
  onFieldBlur: (field: SongMetadataField) => void;
  /** Problems that are visible right now. */
  issues: SongMetadataIssue[];
}

export function SongMetadataForm({ draft, onChange, onFieldBlur, issues }: SongMetadataFormProps) {
  const errorByField: Record<SongMetadataField, string | undefined> = {
    songId: undefined,
    title: undefined,
    artist: undefined,
    youtube: undefined,
    level: undefined,
    durationSec: undefined,
  };

  for (const issue of issues) {
    errorByField[issue.field] = issue.message;
  }

  const durationValue = Number.isFinite(draft.durationSec) ? String(draft.durationSec) : "";

  return (
    <div className={form}>
      <Field.Root invalid={errorByField.songId !== undefined}>
        <Field.Label>
          <Hash className={labelIcon} size={14} aria-hidden="true" />
          Song id
        </Field.Label>
        <Input
          value={draft.songId}
          placeholder="who"
          onChange={(event) => onChange({ songId: event.currentTarget.value })}
          onBlur={() => onFieldBlur("songId")}
        />
        {errorByField.songId ? (
          <Field.ErrorText>{errorByField.songId}</Field.ErrorText>
        ) : (
          <Field.HelperText>
            Lowercase letters, digits and dashes.
          </Field.HelperText>
        )}
      </Field.Root>

      <Field.Root invalid={errorByField.title !== undefined}>
        <Field.Label>
          <Music className={labelIcon} size={14} aria-hidden="true" />
          Title
        </Field.Label>
        <Input
          value={draft.title}
          placeholder="Who"
          onChange={(event) => onChange({ title: event.currentTarget.value })}
          onBlur={() => onFieldBlur("title")}
        />
        {errorByField.title ? (
          <Field.ErrorText>{errorByField.title}</Field.ErrorText>
        ) : (
          <Field.HelperText>Shown in the catalog and in the game.</Field.HelperText>
        )}
      </Field.Root>

      <Field.Root invalid={errorByField.artist !== undefined}>
        <Field.Label>
          <User className={labelIcon} size={14} aria-hidden="true" />
          Artist
        </Field.Label>
        <Input
          value={draft.artist}
          placeholder="Azari"
          onChange={(event) => onChange({ artist: event.currentTarget.value })}
          onBlur={() => onFieldBlur("artist")}
        />
        {errorByField.artist ? (
          <Field.ErrorText>{errorByField.artist}</Field.ErrorText>
        ) : (
          <Field.HelperText>May be empty.</Field.HelperText>
        )}
      </Field.Root>

      <Field.Root invalid={errorByField.youtube !== undefined}>
        <Field.Label>
          <CirclePlay className={labelIcon} size={14} aria-hidden="true" />
          YouTube video
        </Field.Label>
        <Input
          value={draft.youtube}
          placeholder="https://www.youtube.com/watch?v=dQw4w9WgXcQ"
          onChange={(event) => onChange({ youtube: event.currentTarget.value })}
          onBlur={() => onFieldBlur("youtube")}
        />
        {errorByField.youtube ? (
          <Field.ErrorText>{errorByField.youtube}</Field.ErrorText>
        ) : (
          <Field.HelperText>A YouTube URL or an 11 character video id.</Field.HelperText>
        )}
      </Field.Root>

      <Field.Root invalid={errorByField.level !== undefined}>
        <Field.Label>
          <Gauge className={labelIcon} size={14} aria-hidden="true" />
          Level
        </Field.Label>
        <Select.Root
          className={selectRoot}
          collection={levelCollection}
          value={[String(draft.level)]}
          onValueChange={(details) => {
            const [next] = details.value;
            if (next !== undefined) {
              onChange({ level: Number(next) });
              onFieldBlur("level");
            }
          }}
        >
          <Select.Control className={selectControl}>
            <Select.ValueText placeholder="Level" />
            <Select.Indicator />
          </Select.Control>
          <Portal>
            <Select.Positioner>
              <Select.Content>
                <Select.List>
                  {LEVEL_GROUPS.map((group) => (
                    <Select.ItemGroup key={group.label}>
                      <Select.ItemGroupLabel>{group.label}</Select.ItemGroupLabel>
                      {LEVEL_ITEMS.filter((item) => item.group === group.label).map((item) => (
                        <Select.Item key={item.value} item={item}>
                          <Select.ItemText>{item.label}</Select.ItemText>
                          <Select.ItemIndicator />
                        </Select.Item>
                      ))}
                    </Select.ItemGroup>
                  ))}
                </Select.List>
              </Select.Content>
            </Select.Positioner>
          </Portal>
        </Select.Root>
        {errorByField.level ? (
          <Field.ErrorText>{errorByField.level}</Field.ErrorText>
        ) : (
          <Field.HelperText>Difficulty of the song, 1 to 99.</Field.HelperText>
        )}
      </Field.Root>

      <Field.Root invalid={errorByField.durationSec !== undefined}>
        <Field.Label>
          <Timer className={labelIcon} size={14} aria-hidden="true" />
          Duration
        </Field.Label>
        <Input
          type="number"
          min={1}
          max={1800}
          value={durationValue}
          placeholder="128"
          onChange={(event) => onChange({ durationSec: Number(event.currentTarget.value) })}
          onBlur={() => onFieldBlur("durationSec")}
        />
        {errorByField.durationSec ? (
          <Field.ErrorText>{errorByField.durationSec}</Field.ErrorText>
        ) : (
          <Field.HelperText>Length of the song in seconds.</Field.HelperText>
        )}
      </Field.Root>
    </div>
  );
}
