import { Portal } from "@ark-ui/react/portal";
import { createListCollection } from "@ark-ui/react/collection";
import { Button, Field, Input, Select } from "@/components/ui";
import { useRef } from "react";
import { css } from "styled-system/css";
import type { Chart } from "@/lib/chart/types";

/**
 * Toolbar of the chart editor.
 *
 * Everything is a Park UI control styled with the Panda tokens of the Web
 * Studio, so the editor looks like the rest of the app.
 */

const bar = css({
  alignItems: "flex-end",
  borderBottomColor: "gray.surface.border",
  borderBottomWidth: "1px",
  display: "flex",
  flexWrap: "wrap",
  gap: "3",
  justifyContent: "space-between",
  paddingBottom: "3",
  paddingTop: "3",
  px: "4",
});

const group = css({
  alignItems: "flex-end",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
});

const field = css({ width: "9rem" });
const wideField = css({ width: "12rem" });
const selectRoot = css({ width: "8rem" });

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
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "colorPalette.solid.bg",
    outlineOffset: "1px",
  },
});

const fileInput = css({ display: "none" });

const counter = css({
  color: "fg.muted",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
  paddingBottom: "2",
});

const accent = css({ colorPalette: "teal" });

const RESOLUTIONS = [
  { label: "1/4", value: "4" },
  { label: "1/8", value: "8" },
  { label: "1/16", value: "16" },
];

const ZOOMS = [
  { label: "0.5×", value: "0.5" },
  { label: "1×", value: "1" },
  { label: "2×", value: "2" },
];

const resolutionCollection = createListCollection<{ label: string; value: string }>({
  items: RESOLUTIONS,
});

const zoomCollection = createListCollection<{ label: string; value: string }>({ items: ZOOMS });

export interface EditorToolbarProps {
  chart: Chart | null;
  noteCount: number;
  resolution: number;
  zoom: number;
  canDelete: boolean;
  onNameChange(name: string): void;
  onBpmChange(bpm: number): void;
  onResolutionChange(resolution: number): void;
  onZoomChange(zoom: number): void;
  onImportFile(file: File): void;
  onExport(): void;
  onDeleteSelected(): void;
}

export function EditorToolbar({
  chart,
  noteCount,
  resolution,
  zoom,
  canDelete,
  onNameChange,
  onBpmChange,
  onResolutionChange,
  onZoomChange,
  onImportFile,
  onExport,
  onDeleteSelected,
}: EditorToolbarProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  return (
    <header className={bar}>
      <div className={group}>
        <input
          ref={fileInputRef}
          className={fileInput}
          type="file"
          accept="application/json,.json"
          data-testid="import-input"
          onChange={(event) => {
            const file = event.currentTarget.files?.[0];
            if (file) {
              onImportFile(file);
            }
            event.currentTarget.value = "";
          }}
        />
        <Button
          size="md"
          variant="outline"
          onClick={() => fileInputRef.current?.click()}
          data-testid="import-button"
        >
          Import chart
        </Button>
        <Button
          className={accent}
          size="md"
          onClick={onExport}
          disabled={!chart}
          data-testid="export-button"
        >
          Export
        </Button>
        <Button
          size="md"
          variant="outline"
          onClick={onDeleteSelected}
          disabled={!canDelete}
          data-testid="delete-button"
        >
          Delete note
        </Button>
        <span className={counter} data-testid="note-count">
          {noteCount} notes
        </span>
      </div>

      <div className={group}>
        <Field.Root className={wideField}>
          <Field.Label>Song name</Field.Label>
          <Input
            value={chart?.name ?? ""}
            disabled={!chart}
            onChange={(event) => onNameChange(event.currentTarget.value)}
          />
        </Field.Root>

        <Field.Root className={field}>
          <Field.Label>BPM</Field.Label>
          <Input
            value={chart ? String(chart.BPM) : ""}
            disabled={!chart}
            inputMode="numeric"
            onChange={(event) => {
              const bpm = Number.parseInt(event.currentTarget.value, 10);
              if (Number.isFinite(bpm)) {
                onBpmChange(bpm);
              }
            }}
          />
        </Field.Root>

        <Select.Root
          className={selectRoot}
          collection={resolutionCollection}
          value={[String(resolution)]}
          onValueChange={(details) => {
            const [next] = details.value;
            if (next) {
              onResolutionChange(Number.parseInt(next, 10));
            }
          }}
        >
          <Select.Label>Grid</Select.Label>
          <Select.Control className={selectControl}>
            <Select.ValueText placeholder="1/4" />
            <Select.Indicator />
          </Select.Control>
          <Portal>
            <Select.Positioner>
              <Select.Content>
                <Select.List>
                  {RESOLUTIONS.map((item) => (
                    <Select.Item key={item.value} item={item}>
                      <Select.ItemText>{item.label}</Select.ItemText>
                      <Select.ItemIndicator />
                    </Select.Item>
                  ))}
                </Select.List>
              </Select.Content>
            </Select.Positioner>
          </Portal>
        </Select.Root>

        <Select.Root
          className={selectRoot}
          collection={zoomCollection}
          value={[String(zoom)]}
          onValueChange={(details) => {
            const [next] = details.value;
            if (next) {
              onZoomChange(Number.parseFloat(next));
            }
          }}
        >
          <Select.Label>View</Select.Label>
          <Select.Control className={selectControl}>
            <Select.ValueText placeholder="Zoom 1" />
            <Select.Indicator />
          </Select.Control>
          <Portal>
            <Select.Positioner>
              <Select.Content>
                <Select.List>
                  {ZOOMS.map((item) => (
                    <Select.Item key={item.value} item={item}>
                      <Select.ItemText>{item.label}</Select.ItemText>
                      <Select.ItemIndicator />
                    </Select.Item>
                  ))}
                </Select.List>
              </Select.Content>
            </Select.Positioner>
          </Portal>
        </Select.Root>
      </div>
    </header>
  );
}
