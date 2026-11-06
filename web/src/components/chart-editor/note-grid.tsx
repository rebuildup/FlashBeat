import { Layer, Line, Rect, Stage, Text } from "react-konva";
import type { KonvaEventObject } from "konva/lib/Node";
import { useEffect, useMemo, useRef, useState, type RefObject } from "react";
import { css } from "styled-system/css";
import { noteBeat } from "@/lib/chart/timing";
import type { Chart } from "@/lib/chart/types";

/**
 * The eight lane grid.
 *
 * The lanes are the protagonist of the editor: the stage stretches to the
 * width of its panel (measured with a ResizeObserver) so the work area is
 * lanes, not empty panel. Bars are numbered and the selected note is marked
 * with the teal accent, everything else stays quiet.
 */

/** Narrowest lane that stays playable. Below this the stage scrolls. */
export const MIN_LANE_WIDTH = 56;

/** Height of a note in pixels. */
export const NOTE_HEIGHT = 16;

/** Height of one beat in pixels at zoom level 1. */
export const BEAT_HEIGHT = 44;

const LANE_BACKGROUND = "#18191b";
const LANE_BACKGROUND_ALT = "#1d1e21";
const GRID_LINE = "#272a2d";
const GRID_LINE_STRONG = "#363a3f";
const NOTE_FILL = "#12a594";
const NOTE_FILL_SELECTED = "#0fd2b8";
const NOTE_STROKE = "#0d3d38";
const PLAYHEAD = "#0bd8b6";
const LANE_LABEL = "#777b84";
const BAR_LABEL = "#5a6169";

const scrollArea = css({
  flex: "1",
  minHeight: "0",
  overflow: "auto",
  display: "flex",
  justifyContent: "flex-start",
  width: "full",
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

/** Measures the panel the stage lives in, so the lanes can fill it. */
function usePanelWidth(): [RefObject<HTMLDivElement | null>, number] {
  const ref = useRef<HTMLDivElement | null>(null);
  const [width, setWidth] = useState(0);

  useEffect(() => {
    const element = ref.current;
    if (!element) {
      return;
    }

    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        setWidth(entry.contentRect.width);
      }
    });
    observer.observe(element);
    setWidth(element.clientWidth);
    return () => observer.disconnect();
  }, []);

  return [ref, width];
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
  const [panelRef, panelWidth] = usePanelWidth();
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
  const laneWidth = Math.max(MIN_LANE_WIDTH, Math.floor(panelWidth / 8));
  const noteWidth = laneWidth - 12;
  const stageHeight = (lastBeat + 2) * beatHeight;
  const stageWidth = laneWidth * 8;

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

    const block = Math.min(7, Math.max(0, Math.floor(pointer.x / laneWidth)));
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
    const block = Math.min(7, Math.max(0, Math.floor((node.x() + noteWidth / 2) / laneWidth)));
    const beat = Math.max(0, (node.y() + NOTE_HEIGHT / 2) / beatHeight);

    onMoveNote(index, block, beat);
  };

  const beatLines = [];
  for (let beat = 0; beat <= lastBeat + 2; beat += 1) {
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
    <div className={scrollArea} data-testid="note-grid">
      <div ref={panelRef} style={{ minWidth: stageWidth, flex: 1 }}>
        <Stage
          width={stageWidth}
          height={stageHeight}
          onMouseDown={handleStageMouseDown}
          style={{ cursor: "crosshair" }}
        >
          <Layer>
            {Array.from({ length: 8 }, (_, lane) => (
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

            {Array.from({ length: 8 }, (_, lane) => (
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
                  cornerRadius={3}
                  fill={selected ? NOTE_FILL_SELECTED : NOTE_FILL}
                  stroke={selected ? PLAYHEAD : NOTE_STROKE}
                  strokeWidth={selected ? 2 : 1}
                  shadowColor={selected ? PLAYHEAD : undefined}
                  shadowBlur={selected ? 8 : 0}
                  shadowOpacity={selected ? 0.55 : 0}
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
