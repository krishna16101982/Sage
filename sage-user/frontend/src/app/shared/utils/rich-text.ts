/**
 * The order form wording's simple formatting ("Edit wording by section"). A section is text with new lines and only these
 * tags: <b> <i> <u>, <span style="color:#rrggbb">, <span style="background-color:#rrggbb">, and the entities &amp; &lt;
 * &gt; &quot; &#39;. Anything else is plain text, so old plain wording reads the same. The User API reads the same
 * format (AppFunctions/RichText.cs) for the PDF.
 *
 * The text is never put into the page as HTML: it is parsed into runs and shown with Angular bindings or built with DOM
 * calls, so a tag cannot run script.
 */

/** A piece of text with one formatting. */
export interface RichRun {
  text: string;
  bold: boolean;
  italic: boolean;
  underline: boolean;
  /** #rrggbb or null. */
  color: string | null;
  background: string | null;
}

/** A section as lines of runs (a line may be empty). */
export type RichLines = RichRun[][];

const TOKEN = /<(\/?)(b|i|u)>|<span style="(color|background-color):\s*(#[0-9a-fA-F]{6});?">|(<\/span>)|&(amp|lt|gt|quot|#39);/gi;
const ENTITIES: Record<string, string> = { amp: '&', lt: '<', gt: '>', quot: '"', '#39': "'" };

export function parseRich(markup: string | null | undefined): RichLines {
  const lines: RichLines = [[]];
  const text = (markup ?? '').replace(/\r\n/g, '\n');
  let bold = 0;
  let italic = 0;
  let underline = 0;
  const spans: { prop: string; hex: string }[] = [];
  const last = (prop: string) => [...spans].reverse().find((s) => s.prop === prop)?.hex ?? null;
  const add = (piece: string) => {
    piece.split('\n').forEach((part, i) => {
      if (i > 0) lines.push([]);
      if (!part) return;
      lines[lines.length - 1].push({
        text: part, bold: bold > 0, italic: italic > 0, underline: underline > 0,
        color: last('color'), background: last('background-color'),
      });
    });
  };
  let at = 0;
  for (const m of text.matchAll(TOKEN)) {
    if (m.index! > at) add(text.slice(at, m.index));
    at = m.index! + m[0].length;
    if (m[2]) {
      const step = m[1] === '/' ? -1 : 1;
      const tag = m[2].toLowerCase();
      if (tag === 'b') bold = Math.max(0, bold + step);
      else if (tag === 'i') italic = Math.max(0, italic + step);
      else underline = Math.max(0, underline + step);
    } else if (m[3]) spans.push({ prop: m[3].toLowerCase(), hex: m[4].toLowerCase() });
    else if (m[5]) spans.pop();
    else add(ENTITIES[m[6].toLowerCase()]);
  }
  if (at < text.length) add(text.slice(at));
  return lines;
}

const sameFormat = (a: RichRun, b: RichRun) =>
  a.bold === b.bold && a.italic === b.italic && a.underline === b.underline && a.color === b.color && a.background === b.background;

/** Runs with the same formatting joined; empty runs dropped. */
function merge(line: RichRun[]): RichRun[] {
  const out: RichRun[] = [];
  for (const run of line) {
    if (!run.text) continue;
    const prev = out[out.length - 1];
    if (prev && sameFormat(prev, run)) out[out.length - 1] = { ...prev, text: prev.text + run.text };
    else out.push({ ...run });
  }
  return out;
}

const escape = (t: string) => t.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

export function serializeRich(lines: RichLines): string {
  return lines
    .map((line) =>
      merge(line)
        .map((r) => {
          let t = escape(r.text);
          if (r.underline) t = `<u>${t}</u>`;
          if (r.italic) t = `<i>${t}</i>`;
          if (r.bold) t = `<b>${t}</b>`;
          if (r.background) t = `<span style="background-color:${r.background}">${t}</span>`;
          if (r.color) t = `<span style="color:${r.color}">${t}</span>`;
          return t;
        })
        .join(''),
    )
    .join('\n');
}

/** Lines of runs, each line's ends trimmed and empty first / last lines dropped (what the API saves). */
export function cleanLines(markup: string | null | undefined): RichLines {
  const lines = parseRich(markup).map((line) => {
    const runs = merge(line);
    while (runs.length && !runs[0].text.trimStart()) runs.shift();
    if (runs.length) runs[0] = { ...runs[0], text: runs[0].text.trimStart() };
    while (runs.length && !runs[runs.length - 1].text.trimEnd()) runs.pop();
    if (runs.length) runs[runs.length - 1] = { ...runs[runs.length - 1], text: runs[runs.length - 1].text.trimEnd() };
    return runs;
  });
  while (lines.length && !lines[0].length) lines.shift();
  while (lines.length && !lines[lines.length - 1].length) lines.pop();
  return lines;
}

/** Same words and formatting (ignores how the tags were written and spaces at the ends of lines). */
export function sameRich(a: string | null | undefined, b: string | null | undefined): boolean {
  return serializeRich(cleanLines(a)) === serializeRich(cleanLines(b));
}

/** The words only. */
export function plainRich(markup: string | null | undefined): string {
  return parseRich(markup).map((l) => l.map((r) => r.text).join('')).join('\n');
}

// ---------------- the editor's page content <-> runs ----------------

const BLOCKS = new Set(['DIV', 'P', 'LI', 'UL', 'OL', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'BLOCKQUOTE', 'PRE', 'TR']);

/** Any CSS colour as #rrggbb, or null for none / transparent. */
function toHex(value: string | null | undefined): string | null {
  const v = (value ?? '').trim();
  if (!v || v === 'transparent' || v === 'inherit' || v === 'initial') return null;
  const rgb = v.match(/^rgba?\(\s*(\d+)[,\s]+(\d+)[,\s]+(\d+)(?:[,\s/]+([\d.]+))?\s*\)$/i);
  if (rgb) {
    if (rgb[4] !== undefined && Number(rgb[4]) === 0) return null;
    return '#' + [rgb[1], rgb[2], rgb[3]].map((n) => Number(n).toString(16).padStart(2, '0')).join('');
  }
  if (/^#[0-9a-f]{6}$/i.test(v)) return v.toLowerCase();
  if (/^#[0-9a-f]{3}$/i.test(v)) return ('#' + v.slice(1).split('').map((c) => c + c).join('')).toLowerCase();
  // Named colours: let the browser convert them.
  const ctx = document.createElement('canvas').getContext('2d');
  if (!ctx) return null;
  ctx.fillStyle = '#000000';
  ctx.fillStyle = v;
  return /^#[0-9a-f]{6}$/i.test(ctx.fillStyle) ? ctx.fillStyle.toLowerCase() : toHex(ctx.fillStyle);
}

/** Reads what the browser made of the editor (b / strong / font / spans with styles, divs and br for lines). */
export function readEditor(root: HTMLElement): RichLines {
  const lines: RichLines = [[]];
  const current = () => lines[lines.length - 1];
  const walk = (node: Node, fmt: Omit<RichRun, 'text'>) => {
    if (node.nodeType === Node.TEXT_NODE) {
      const text = (node.textContent ?? '').replace(/ /g, ' ').replace(/​/g, '');
      if (text) current().push({ ...fmt, text });
      return;
    }
    if (!(node instanceof HTMLElement)) return;
    if (node.tagName === 'BR') {
      lines.push([]);
      return;
    }
    const next = { ...fmt };
    const tag = node.tagName;
    if (tag === 'B' || tag === 'STRONG') next.bold = true;
    if (tag === 'I' || tag === 'EM') next.italic = true;
    if (tag === 'U') next.underline = true;
    if (tag === 'FONT' && node.getAttribute('color')) next.color = toHex(node.getAttribute('color')) ?? next.color;
    const st = node.style;
    if (st.fontWeight) next.bold = st.fontWeight === 'bold' || st.fontWeight === 'bolder' || Number(st.fontWeight) >= 600;
    if (st.fontStyle) next.italic = st.fontStyle === 'italic' || st.fontStyle === 'oblique';
    const deco = `${st.textDecoration} ${st.textDecorationLine}`;
    if (deco.includes('underline')) next.underline = true;
    if (st.color) next.color = toHex(st.color);
    if (st.backgroundColor) next.background = toHex(st.backgroundColor);
    const block = BLOCKS.has(tag);
    if (block && current().length) lines.push([]);
    node.childNodes.forEach((child) => walk(child, next));
    if (block && current().length) lines.push([]);
  };
  root.childNodes.forEach((child) => walk(child, { bold: false, italic: false, underline: false, color: null, background: null }));
  while (lines.length > 1 && !lines[lines.length - 1].length) lines.pop();
  return lines.map(merge);
}

/** Fills the editor from markup with DOM calls (never innerHTML). */
export function writeEditor(root: HTMLElement, markup: string | null | undefined): void {
  root.replaceChildren();
  const lines = parseRich(markup);
  lines.forEach((line, i) => {
    if (i > 0) root.appendChild(document.createElement('br'));
    for (const run of merge(line)) {
      let node: Node = document.createTextNode(run.text);
      const wrap = (tag: string, style?: [string, string]) => {
        const el = document.createElement(tag);
        if (style) el.style.setProperty(style[0], style[1]);
        el.appendChild(node);
        node = el;
      };
      if (run.underline) wrap('u');
      if (run.italic) wrap('i');
      if (run.bold) wrap('b');
      if (run.background) wrap('span', ['background-color', run.background]);
      if (run.color) wrap('span', ['color', run.color]);
      root.appendChild(node);
    }
  });
  // An empty last line needs a second <br> to show in a content-editable box.
  if (lines.length > 1 && !lines[lines.length - 1].length) root.appendChild(document.createElement('br'));
}
