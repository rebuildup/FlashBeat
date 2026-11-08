import { Group, Layer, Line, Rect, Stage, Text } from "react-konva";
import type { KonvaEventObject } from "konva/lib/Node";
import { useEffect, useMemo, useRef, useState, type RefObject } from "react";
import { css } from "styled-system/css";
import { holdEndBeat, isHold } from "@/lib/chart/long-note";
import { snapBeat, snapBeatExclusive, type SnapSetting } from "@/lib/chart/snap";
import { noteBeat } from "@/lib/chart/timing";
import type { Chart, ChartNote } from "@/lib/chart/types";
import { useSnapDivision } from "./snap-store";

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

/** Narrowest lane that stays playable. Below this the stage scrolls. */
export const MIN_LANE_WIDTH = 56;

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

/**
 * Body of a hold.
 *
 * A hold is drawn as a bar that runs from its start to its end. It uses the
 * same surface as a tap, but a darker one, so the two kinds stay apart at a
 * glance: the tap is the short bright block, the hold is the long body.
 */
const HOLD_FILL = "#8a8a8a";
const HOLD_FILL_SELECTED = "#3fa9f5";
const HOLD_STROKE = "rgba(0,0,0,0.25)";
const HOLD_STROKE_SELECTED = "#9ad3fa";

/**
 * Handle of the end of a hold.
 *
 * The handle is what the operator drags to change the length of the hold. It
 * sits at the end of the body and is drawn in the accent blue, so it is the
 * only part of the hold that reacts to the mouse.
 */
const HOLD_HANDLE_FILL = "#3fa9f5";
const HOLD_HANDLE_STROKE = "#9ad3fa";

/** Size of the resize handle in pixels. */
export const HOLD_HANDLE_SIZE = 12;

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
  /**
   * Grid the notes are placed on (issue #180).
   *
   * `off` places the notes freely, a division snaps them onto that step of
   * the beat. When the prop is left out the grid follows the division the
   * toolbar picked (the shared snap store), so the editor itself does not
   * have to carry the state.
   */
  snapDivision?: SnapSetting;
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
  /**
   * Called with the index of a hold and the beat its end was dragged to.
   *
   * `null` when the hold is not resized. The end of a hold is the only part
   * of it that moves on its own; the body follows.
   */
  onResizeHold?(index: number, endBeat: number): void;
}

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

/**
 * Where a note lands on the grid.
 *
 * The grid is measured in beats: one beat is `BEAT_HEIGHT` pixels tall and
 * the grid of the chart decides how many steps fit into a beat. A note
 * lands on the nearest step of the grid, so the pointer only says where
 * the note was dropped and the grid says where it belongs.
 *
 * The beat is rounded onto the steps of the resolution as well, because a
 * note carries its own resolution (`LPB`): a beat the resolution cannot
 * express would round the grid away again when the note is written.
 *
 * The function is exported so the placement can be reasoned about (and
 * tested) without a canvas.
 */
export function snappedBeat(
  pixels: number,
  beatHeight: number,
  division: SnapSetting,
  resolution = 0,
): number {
  if (beatHeight <= 0 || !Number.isFinite(pixels)) {
    return 0;
  }
  const beat = Math.max(0, snapBeat(pixels / beatHeight, division));
  if (resolution <= 0) {
    return beat;
  }
  return Math.max(0, Math.round(beat * resolution) / resolution);
}

/**
 * Where the end of a hold lands on the grid.
 *
 * The end never ends before the hold starts, so the beat of the start is
 * the lower bound of the grid.
 */
export function snappedHoldEndBeat(
  pixels: number,
  beatHeight: number,
  division: SnapSetting,
  startBeat: number,
  resolution = 0,
): number {
  if (beatHeight <= 0 || !Number.isFinite(pixels)) {
    return startBeat;
  }
  const endBeat = snapBeatExclusive(pixels / beatHeight, division);
  const onGrid =
    resolution <= 0 ? endBeat : Math.max(0, Math.round(endBeat * resolution) / resolution);
  return Math.max(startBeat, onGrid);
}

export function NoteGrid({
  chart,
  resolution,
  snapDivision: snapDivisionProp,
  zoom,
  selectedIndex,
  playheadSec,
  onSelect,
  onAddNote,
  onMoveNote,
  onResizeHold,
}: NoteGridProps) {
  const [panelRef, panelWidth, panelHeight] = usePanelSize();
  const beatHeight = BEAT_HEIGHT * zoom;

  /**
   * The grid the notes are placed on.
   *
   * The prop wins when it is given; otherwise the grid follows the division
   * the toolbar picked. The tempo of the chart is not part of the rounding:
   * the grid is measured in beats, so a change of the BPM moves the times
   * the grid stands for without moving the notes themselves.
   */
  const storeDivision = useSnapDivision();
  const snapDivision = snapDivisionProp ?? storeDivision;

  const lastBeat = useMemo(() => {
    let last = 8;
    for (const note of chart.notes) {
      last = Math.max(last, Math.ceil(noteBeat(note)) + 2);
      // A hold reaches past its start, so the grid has to show its end too.
      if (isHold(note)) {
        last = Math.max(last, Math.ceil(holdEndBeat(note)) + 2);
      }
    }
    return last;
  }, [chart.notes]);

  // Lanes stretch to the panel; the stage scrolls once the floor would make
  // a lane narrower than the playable minimum.
  const laneWidth = Math.max(MIN_LANE_WIDTH, Math.floor(panelWidth / 8));
  // Tap notes stay compact rectangles centered in the lane, never bars that
  // read across the lane boundary.
  const noteWidth = Math.min(laneWidth - 12, 72);
  // Lanes fill the panel down to the last visible beat, so the work area is
  // never empty panel below the chart.
  const stageHeight = Math.max(lastBeat + 2, Math.ceil(panelHeight / beatHeight)) * beatHeight;
  const stageWidth = laneWidth * 8;

  const playheadBeat = (playheadSec * chart.BPM) / 60;
  const selectedNote = selectedIndex !== null ? chart.notes[selectedIndex] : undefined;

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
    const beat = snappedBeat(pointer.y, beatHeight, snapDivision, resolution);

    onSelect(null);
    onAddNote(block, beat);
  };

  /**
   * Touch placement (issue #216).
   *
   * The same gesture as the mouse: a tap on an empty lane places the note
   * the toolbar selected. Notes are dragged and resized by their own touch
   * handlers, so this only reacts when the touch misses every note.
   */
  const handleStageTouchStart = (event: KonvaEventObject<TouchEvent>) => {
    const stage = event.target.getStage();
    if (event.target !== stage) {
      return;
    }

    const pointer = stage?.getPointerPosition();
    if (!pointer) {
      return;
    }

    const block = Math.min(7, Math.max(0, Math.floor(pointer.x / laneWidth)));
    const beat = snappedBeat(pointer.y, beatHeight, snapDivision, resolution);

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
    const beat = snappedBeat(node.y() + NOTE_HEIGHT / 2, beatHeight, snapDivision, resolution);

    onMoveNote(index, block, beat);
  };

  /**
   * The end of a hold was dragged.
   *
   * The handle sits at the end of the body, so its center tells where the
   * hold ends. The beat is measured from the pointer and clamped to the
   * start of the hold, so a hold never ends before it starts. The end lands
   * on the grid, just like the start of the note.
   */
  const handleHoldResizeEnd = (event: KonvaEventObject<DragEvent>, note: ChartNote) => {
    event.cancelBubble = true;

    const node = event.target;
    const pointer = node.getStage()?.getPointerPosition();
    // The handle is dragged freely, so the pointer is the source of truth.
    // When it is missing the node itself carries the position.
    const y = pointer ? pointer.y : node.y();
    const endBeat = snappedHoldEndBeat(y, beatHeight, snapDivision, noteBeat(note), resolution);

    onResizeHold?.(chart.notes.indexOf(note), endBeat);
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
          onTouchStart={handleStageTouchStart}
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

            {/*
              The selected note is the anchor of every edit: the whole lane
              and the beat line are tinted, so "what is selected" is visible
              from anywhere on the work area (issue #216).
            */}
            {selectedNote ? (
              <>
                <Rect
                  x={selectedNote.block * laneWidth}
                  y={0}
                  width={laneWidth}
                  height={stageHeight}
                  fill="rgba(63,169,245,0.10)"
                  listening={false}
                />
                <Line
                  points={[
                    0,
                    noteBeat(selectedNote) * beatHeight,
                    stageWidth,
                    noteBeat(selectedNote) * beatHeight,
                  ]}
                  stroke="rgba(63,169,245,0.45)"
                  strokeWidth={1}
                  dash={[6, 4]}
                  listening={false}
                />
              </>
            ) : null}

            {Array.from({ length: 8 }, (_, lane) => (
              <Text
                key={`lane-label-${lane}`}
                x={lane * laneWidth + 6}
                y={6}
                text={String(lane + 1)}
                fontSize={11}
                fontStyle="bold"
                fill={selectedNote?.block === lane ? NOTE_FILL_SELECTED : LANE_LABEL}
                listening={false}
              />
            ))}

            {chart.notes.map((note, index) => {
              const beat = noteBeat(note);
              const selected = index === selectedIndex;
              const noteX = note.block * laneWidth + (laneWidth - noteWidth) / 2;
              const noteY = beat * beatHeight - NOTE_HEIGHT / 2;

              // A hold is a body that runs from its start to its end, with a
              // handle at the end the operator drags to change the length.
              if (isHold(note)) {
                const endBeat = holdEndBeat(note);
                const endY = endBeat * beatHeight - NOTE_HEIGHT / 2;
                const bodyHeight = Math.max(NOTE_HEIGHT, endY - noteY + NOTE_HEIGHT);

                return (
                  <Group key={index}>
                    <Rect
                      x={noteX}
                      y={noteY}
                      width={noteWidth}
                      height={bodyHeight}
                      cornerRadius={4}
                      fill={selected ? HOLD_FILL_SELECTED : HOLD_FILL}
                      stroke={selected ? HOLD_STROKE_SELECTED : HOLD_STROKE}
                      strokeWidth={selected ? 2 : 1}
                      shadowColor={selected ? HOLD_FILL_SELECTED : undefined}
                      shadowBlur={selected ? 14 : 0}
                      shadowOpacity={selected ? 0.9 : 0}
                      onMouseDown={(event) => handleNoteMouseDown(event, index)}
                      onTouchStart={(event) =>
                        handleNoteMouseDown(event as unknown as KonvaEventObject<MouseEvent>, index)
                      }
                    />

                    {/* The head of the hold: the place the key goes down. */}
                    <Rect
                      x={noteX}
                      y={noteY}
                      width={noteWidth}
                      height={NOTE_HEIGHT}
                      cornerRadius={4}
                      fill={selected ? NOTE_FILL_SELECTED : NOTE_FILL}
                      stroke={selected ? NOTE_STROKE_SELECTED : "rgba(0,0,0,0.25)"}
                      strokeWidth={selected ? 2 : 1}
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

                    {/* The handle of the end: it changes the length. */}
                    <Rect
                      x={noteX + noteWidth - HOLD_HANDLE_SIZE / 2}
                      y={endY + NOTE_HEIGHT / 2 - HOLD_HANDLE_SIZE / 2}
                      width={HOLD_HANDLE_SIZE}
                      height={HOLD_HANDLE_SIZE}
                      cornerRadius={3}
                      fill={HOLD_HANDLE_FILL}
                      stroke={HOLD_HANDLE_STROKE}
                      strokeWidth={1.5}
                      draggable
                      onMouseDown={(event) => {
                        event.cancelBubble = true;
                        onSelect(index);
                      }}
                      onMouseEnter={(event) => {
                        const stage = event.target.getStage();
                        if (stage) stage.container().style.cursor = "ew-resize";
                      }}
                      onMouseLeave={(event) => {
                        const stage = event.target.getStage();
                        if (stage) stage.container().style.cursor = "crosshair";
                      }}
                      onTouchStart={(event) => {
                        event.cancelBubble = true;
                        onSelect(index);
                      }}
                      onDragEnd={(event) => handleHoldResizeEnd(event, note)}
                    />
                  </Group>
                );
              }

              return (
                <Rect
                  key={index}
                  x={noteX}
                  y={noteY}
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
