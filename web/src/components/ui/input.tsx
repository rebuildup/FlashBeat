import { Field } from "@ark-ui/react/field";
import {
  useRef,
  useState,
  type ComponentProps,
  type CompositionEvent,
  type FocusEvent,
  type KeyboardEvent,
} from "react";
import { styled } from "styled-system/jsx";
import { input } from "styled-system/recipes";

export type InputProps = ComponentProps<typeof Input>;
export const Input = styled(Field.Input, input);

/**
 * Draft input (issue #163).
 *
 * 数値入力を「入力途中の文字列 (draft)」と「確定値 (committed value)」に分離する。
 * typing 中は画面に draft をそのまま出し、parse が成功した値だけ `onCommit`
 * に流す。blur / Enter で正規化して commit し、parse できないテキスト
 * (空文字・負号途中・無効文字) は確定値の表示へ戻す (revert)。
 *
 * これにより Ctrl/⌘+A → Delete が途中で確定値に戻されず、先頭 0 や
 * 「12.」のような途中状態も保持される。IME 変換中 (composition 中) は
 * commit しないので、変換確定前の値が確定値として飛ばされない。
 *
 * `type="number"` は DOM が部分入力を保持できないため使わない。
 * 代わりに `inputMode` を渡すこと。
 */
export interface DraftInputProps<T> extends Omit<
  ComponentProps<typeof Input>,
  "value" | "defaultValue" | "onChange" | "type"
> {
  /** 確定値 (committed value)。 */
  value: T;
  /** 確定値 → 表示テキスト。既定は `String`。未確定 (NaN など) は "" を返すこと。 */
  format?: (value: T) => string;
  /** 入力テキスト → 確定値。`null` は「まだ確定できない」(blur で revert)。 */
  parse: (text: string) => T | null;
  /** parse が成功した値の受け先 (typing 中の有効値と blur / Enter の確定値)。 */
  onCommit: (value: T) => void;
}

function defaultFormat<T>(value: T): string {
  return String(value);
}

export function DraftInput<T>(props: DraftInputProps<T>) {
  const {
    value,
    format = defaultFormat,
    parse,
    onCommit,
    onBlur,
    onKeyDown,
    onCompositionStart,
    onCompositionEnd,
    ...rest
  } = props;

  // null は「非編集」: 確定値を format したものを表示する。
  const [draft, setDraft] = useState<string | null>(null);
  const composingRef = useRef(false);

  // blur / Enter の時点で古い closure を使わないよう、最新の値を ref 経由で読む。
  const latestRef = useRef({ format, parse, onCommit });
  latestRef.current = { format, parse, onCommit };

  const commitText = (text: string): boolean => {
    const parsed = latestRef.current.parse(text);
    if (parsed === null) {
      return false;
    }
    latestRef.current.onCommit(parsed);
    return true;
  };

  const handleBlur = (event: FocusEvent<HTMLInputElement>) => {
    if (draft !== null) {
      commitText(event.currentTarget.value);
      setDraft(null);
    }
    onBlur?.(event);
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    // IME の変換確定 (keyCode 229 / isComposing) は submit 扱いにしない。
    const composing = event.nativeEvent.isComposing || event.keyCode === 229;
    if (event.key === "Enter" && !composing && draft !== null) {
      commitText(event.currentTarget.value);
      setDraft(null);
    }
    onKeyDown?.(event);
  };

  const handleCompositionStart = (event: CompositionEvent<HTMLInputElement>) => {
    composingRef.current = true;
    onCompositionStart?.(event);
  };

  const handleCompositionEnd = (event: CompositionEvent<HTMLInputElement>) => {
    composingRef.current = false;
    const text = event.currentTarget.value;
    setDraft(text);
    commitText(text);
    onCompositionEnd?.(event);
  };

  return (
    <Input
      {...rest}
      value={draft ?? latestRef.current.format(value)}
      onChange={(event) => {
        const text = event.currentTarget.value;
        setDraft(text);
        if (!composingRef.current) {
          commitText(text);
        }
      }}
      onBlur={handleBlur}
      onKeyDown={handleKeyDown}
      onCompositionStart={handleCompositionStart}
      onCompositionEnd={handleCompositionEnd}
    />
  );
}

/** 全角数字 (U+FF10–FF19) を半角に正規化する。 */
export function normalizeFullWidthDigits(text: string): string {
  return text.replace(/[０-９]/g, (char) => String.fromCharCode(char.charCodeAt(0) - 0xfee0));
}

/**
 * 整数として解釈する (issue #163)。全角数字は半角化して受け、
 * 先頭 0 は blur 時の format で落ちる。空文字・負号・小数は `null`。
 */
export function parseIntegerOrNull(text: string): number | null {
  const normalized = normalizeFullWidthDigits(text.trim());
  if (!/^\d+$/.test(normalized)) {
    return null;
  }
  const value = Number(normalized);
  return Number.isSafeInteger(value) ? value : null;
}

/**
 * 整数または小数として解釈する。途中の「12.」も 12 に正規化される。
 * 空文字・負号・無効文字は `null`。
 */
export function parseNumberOrNull(text: string): number | null {
  const normalized = normalizeFullWidthDigits(text.trim()).replace("．", ".");
  if (!/^\d+\.?\d*$|^\.\d+$/.test(normalized)) {
    return null;
  }
  const value = Number(normalized);
  return Number.isFinite(value) ? value : null;
}
