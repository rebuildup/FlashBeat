import { Layer, Line, Rect, Stage, Text } from "react-konva";
import type { KonvaEventObject } from "konva/lib/Node";
import { useEffect, useMemo, useRef, useState, type RefObject } from "react";
import { css } from "styled-system/css";
import { noteBeat } from "@/lib/chart/timing";
import type { Chart } from "@/lib/chart/types";
import { gridMetrics, LANE_COUNT } from "./grid-metrics";

/**
 * The eight lane grid.
 *
 * The lanes are the protagonist of the editor: the stage stretches to the
 * width of its panel (measured with a ResizeObserver) so the work area is
 * lanes, not empty panel. Bars are numbered and the selected note is marked
 * with the accent blue, everything else stays quiet.
 *
 * Colors follow design/flashbeat-ui.pen v5: the work area is a recessed well
 * (#131313, k0), grid lines climb the k scale, tap notes are the light
 * "primary" surface (#EDEDED) exactly like the white tap bars in GameScene,
 * and the accent blue (#3FA9F5) marks the current playhead and the selected
 * note. No gradients, no teal.
 */

/** Height of a note in pixels. */
export const NOTE_HEIGHT = 16;

/** Height of one beat in pixels at zoom level 1. */
export const BEAT_HEIGHT = 44;

const LANE_BACKGROUND = "#131313";
const LANE_BACKGROUND_ALT = "#161616";
const GRID_LINE = "#222222";
const GRID_LINE_STRONG = "#2d2d2d";
const NOTE_FILL = "#ededed";
const NOTE_FILL_SELECTED = "#3fa9f5";
const NOTE_STROKE_SELECTED = "#9ad3fa";
const PLAYHEAD = "#3fa9f5";
const LANE_LABEL = "#8c8c8c";
const BAR_LABEL = "#8c8c8c";

const scrollArea = css({
  flex: "1",
  minHeight: "0",
  overflow: "auto",
  display: "flex",
  justifyContent: "flex-start",
  width: "full",
  // The grid takes the focus so the note shortcuts have a visible anchor.
  "&:focus-visible": {
    outline: "2px solid #3fa9f5",
    outlineOffset: "-2px",
  },
});

export interface NoteGridProps {
  chart: Chart;
  /** Lines per beat used for new notes and for the snap grid. */
  resolution: number;
  /** Zoom factor, 1 is the default height of a beat. */
  zoom: number;
  /** Index of the selected note, `null` when nothing is selected. */
  selectedIndex: number | null;
  /** Playhead position in seconds. */
  playheadSec: number;
  /** Called with the index of the clicked note, `null` when the click missed. */
  onSelect(index: number | null): void;
  /** Called with the lane and the beat of a new note. */
  onAddNote(block: number, beat: number): void;
  /** Called with the index and the new position of a dragged note. */
  onMoveNote(index: number, block: number, beat: number): void;
}

/**
 * Geometry of the stage.
 *
 * The eight lanes stretch to the panel. Below the playable lane width the
 * stage keeps that width and the grid scrolls sideways, so a narrow viewport
 * never squeezes the lanes into an unusable strip (issue #158, finding #1).
 * The rules themselves live in `grid-metrics.ts`.
 */

/** Measures the panel the stage lives in, so the lanes fill the work area. */
function usePanelSize(): [RefObject<HTMLDivElement | null>, number, number] {
  const ref = useRef<HTMLDivElement | null>(null);
  const [size, setSize] = useState<[number, number]>([0, 0]);

  useEffect(() => {
    const element = ref.current;
    if (!element) {
      return;
    }

    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        setSize([entry.contentRect.width, entry.contentRect.height]);
      }
    });
    observer.observe(element);
    setSize([element.clientWidth, element.clientHeight]);
    return () => observer.disconnect();
  }, []);

  return [ref, size[0], size[1]];
}

export function NoteGrid({
  chart,
  resolution,
  zoom,
  selectedIndex,
  playheadSec,
  onSelect,
  onAddNote,
  onMoveNote,
}: NoteGridProps) {
  const [panelRef, panelWidth, panelHeight] = usePanelSize();
  const beatHeight = BEAT_HEIGHT * zoom;

  const lastBeat = useMemo(() => {
    let last = 8;
    for (const note of chart.notes) {
      last = Math.max(last, Math.ceil(noteBeat(note)) + 2);
    }
    return last;
  }, [chart.notes]);

  // Lanes stretch to the panel; the stage scrolls once the floor would make
  // a lane narrower than the playable minimum.
  const { laneWidth, noteWidth, stageWidth, stageHeight } = gridMetrics({
    panelWidth,
    panelHeight,
    beatHeight,
    lastBeat,
  });

  const playheadBeat = (playheadSec * chart.BPM) / 60;

  const handleStageMouseDown = (event: KonvaEventObject<MouseEvent>) => {
    const stage = event.target.getStage();
    if (event.target !== stage) {
      return;
    }

    const pointer = stage?.getPointerPosition();
    if (!pointer) {
      return;
    }

    const block = Math.min(LANE_COUNT - 1, Math.max(0, Math.floor(pointer.x / laneWidth)));
    const beat = Math.max(0, pointer.y / beatHeight);

    onSelect(null);
    onAddNote(block, beat);
  };

  const handleNoteMouseDown = (event: KonvaEventObject<MouseEvent>, index: number) => {
    event.cancelBubble = true;
    onSelect(index);
  };

  const handleDragEnd = (event: KonvaEventObject<DragEvent>, index: number) => {
    const node = event.target;
    const block = Math.min(
      LANE_COUNT - 1,
      Math.max(0, Math.floor((node.x() + noteWidth / 2) / laneWidth)),
    );
    const beat = Math.max(0, (node.y() + NOTE_HEIGHT / 2) / beatHeight);

    onMoveNote(index, block, beat);
  };

  const beatLines = [];
  const lastLineBeat = stageHeight / beatHeight;
  for (let beat = 0; beat <= lastLineBeat; beat += 1) {
    const y = beat * beatHeight;
    const isBar = beat % 4 === 0;
    beatLines.push(
      <Line
        key={`beat-${beat}`}
        points={[0, y, stageWidth, y]}
        stroke={isBar ? GRID_LINE_STRONG : GRID_LINE}
        strokeWidth={isBar ? 1.5 : 1}
        listening={false}
      />,
    );

    if (isBar && beat > 0) {
      beatLines.push(
        <Text
          key={`bar-${beat}`}
          x={4}
          y={y + 3}
          text={String(beat / 4 + 1)}
          fontSize={10}
          fill={BAR_LABEL}
          listening={false}
        />,
      );
    }

    for (let sub = 1; sub < resolution; sub += 1) {
      beatLines.push(
        <Line
          key={`sub-${beat}-${sub}`}
          points={[
            0,
            y + (beatHeight / resolution) * sub,
            stageWidth,
            y + (beatHeight / resolution) * sub,
          ]}
          stroke={GRID_LINE}
          strokeWidth={0.5}
          listening={false}
        />,
      );
    }
  }

  return (
    <div
      aria-label="Note grid"
      className={scrollArea}
      ref={panelRef}
      role="group"
      tabIndex={0}
      data-testid="note-grid"
    >
      <div style={{ minWidth: stageWidth, flex: 1 }}>
        <Stage
          width={stageWidth}
          height={stageHeight}
          onMouseDown={handleStageMouseDown}
          style={{ cursor: "crosshair" }}
        >
          <Layer>
            {Array.from({ length: LANE_COUNT }, (_, lane) => (
              <Rect
                key={`lane-${lane}`}
                x={lane * laneWidth}
                y={0}
                width={laneWidth}
                height={stageHeight}
                fill={lane % 2 === 0 ? LANE_BACKGROUND : LANE_BACKGROUND_ALT}
                listening={false}
              />
            ))}

            {beatLines}

            {Array.from({ length: LANE_COUNT }, (_, lane) => (
              <Text
                key={`lane-label-${lane}`}
                x={lane * laneWidth + 6}
                y={6}
                text={String(lane + 1)}
                fontSize={11}
                fontStyle="bold"
                fill={LANE_LABEL}
                listening={false}
              />
            ))}

            {chart.notes.map((note, index) => {
              const beat = noteBeat(note);
              const selected = index === selectedIndex;
              return (
                <Rect
                  key={index}
                  x={note.block * laneWidth + (laneWidth - noteWidth) / 2}
                  y={beat * beatHeight - NOTE_HEIGHT / 2}
                  width={noteWidth}
                  height={NOTE_HEIGHT}
                  cornerRadius={4}
                  fill={selected ? NOTE_FILL_SELECTED : NOTE_FILL}
                  stroke={selected ? NOTE_STROKE_SELECTED : "rgba(0,0,0,0.25)"}
                  strokeWidth={selected ? 2 : 1}
                  shadowColor={selected ? NOTE_FILL_SELECTED : undefined}
                  shadowBlur={selected ? 14 : 0}
                  shadowOpacity={selected ? 0.9 : 0}
                  draggable
                  onMouseDown={(event) => handleNoteMouseDown(event, index)}
                  onMouseEnter={(event) => {
                    const stage = event.target.getStage();
                    if (stage) stage.container().style.cursor = "grab";
                  }}
                  onMouseLeave={(event) => {
                    const stage = event.target.getStage();
                    if (stage) stage.container().style.cursor = "crosshair";
                  }}
                  onTouchStart={(event) =>
                    handleNoteMouseDown(event as unknown as KonvaEventObject<MouseEvent>, index)
                  }
                  onDragEnd={(event) => handleDragEnd(event, index)}
                />
              );
            })}

            <Line
              points={[0, playheadBeat * beatHeight, stageWidth, playheadBeat * beatHeight]}
              stroke={PLAYHEAD}
              strokeWidth={2}
              listening={false}
            />
          </Layer>
        </Stage>
      </div>
    </div>
  );
}
