// 스킬 아이콘 60개(24x24 픽셀)를 코드로 그린다. 스킬마다 다른 그림(무기·원소 모양 + 배경색)이 나오게 한다.
// 실행: node Tools/ArtGen/SkillIcons/skillicons.mjs [미리보기.png]
// 결과: Assets/4.Sprite/Fx/SkillIcon/<스킬 에셋 이름>.png. 그다음 EditorCmd refresh + build로 SkillDefinition.icon에 연결한다.
// 플레이어 상태이상 아이콘(Status_로 시작하는 이름)은 Assets/4.Sprite/Fx/StatusIcon/에 쓴다(HUD 디버프 칸).
import fs from "fs";
import zlib from "zlib";
import path from "path";
import { fileURLToPath } from "url";

const S = 24;
const here = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.resolve(here, "../../../Assets/4.Sprite/Fx/SkillIcon");
const statusDir = path.resolve(here, "../../../Assets/4.Sprite/Fx/StatusIcon");

// ------------------------------------------------------------ 색
function hex(h, a = 255) {
  h = h.replace("#", "");
  return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16), a];
}
function mix(c, d, t) {
  return [0, 1, 2].map(i => Math.round(c[i] + (d[i] - c[i]) * t)).concat([255]);
}
function alpha(c, a) {
  return [c[0], c[1], c[2], a];
}
const BLACK = hex("000000");
const WHITE = hex("ffffff");
const OUTLINE = hex("120c18");
// 재질: 어두움, 중간, 밝음
const M = {
  steel: ["5a6372", "a7b0bd", "eef2f6"],
  gold: ["8a5a10", "d9a521", "ffeb8a"],
  wood: ["4f2f17", "84522a", "b67d43"],
  red: ["7a1010", "d42a2a", "ff8a78"],
  fire: ["b0280c", "f66a18", "ffd848"],
  ice: ["2a64a8", "6cbaf4", "e2f7ff"],
  volt: ["a87410", "ffd23a", "fffbd8"],
  poison: ["2c6418", "66c834", "cff58a"],
  arcane: ["4c1c86", "9a58e8", "e6bcff"],
  holy: ["b88a1c", "ffe468", "ffffff"],
  heal: ["1a7432", "44c866", "c0ffc4"],
  stone: ["4c3e34", "85705e", "bca58e"],
  dark: ["160a22", "3c1c56", "8044b0"],
  bone: ["8c8070", "d8cfbc", "fffaf0"],
  leaf: ["1e5a1c", "4aa83a", "a6e070"],
  brass: ["7a5418", "c89a3a", "f4dc8a"],
  iron: ["2a2c34", "50545e", "8a909c"],
};
const C = {};
for (const k in M) C[k] = M[k].map(h => hex(h));

// ------------------------------------------------------------ 그리기 층
function layer() {
  return new Uint8ClampedArray(S * S * 4);
}
function put(L, x, y, c) {
  x = Math.round(x);
  y = Math.round(y);
  if (x < 0 || y < 0 || x >= S || y >= S) return;
  const i = (y * S + x) * 4;
  const a = c[3] / 255;
  if (a >= 1 || L[i + 3] === 0) {
    L[i] = c[0]; L[i + 1] = c[1]; L[i + 2] = c[2]; L[i + 3] = Math.max(c[3], L[i + 3]);
  } else {
    L[i] = L[i] + (c[0] - L[i]) * a;
    L[i + 1] = L[i + 1] + (c[1] - L[i + 1]) * a;
    L[i + 2] = L[i + 2] + (c[2] - L[i + 2]) * a;
    L[i + 3] = Math.max(L[i + 3], c[3]);
  }
}
// 굵기 w: 진행 방향에 수직인 축으로 한 칸씩 늘린다(대각선도 빈틈 없이).
function line(L, x0, y0, x1, y1, c, w = 1) {
  const dx = x1 - x0, dy = y1 - y0;
  const n = Math.ceil(Math.max(Math.abs(dx), Math.abs(dy)) * 2) + 1;
  const ox = Math.abs(dx) > Math.abs(dy) ? 0 : 1;
  const oy = 1 - ox;
  const seen = new Set();
  for (let s = 0; s <= n; s++) {
    const t = s / n;
    const x = Math.round(x0 + dx * t), y = Math.round(y0 + dy * t);
    for (let k = 0; k < w; k++) {
      const off = k - Math.floor((w - 1) / 2);
      const key = (x + ox * off) + "," + (y + oy * off);
      if (!seen.has(key)) {
        seen.add(key);
        put(L, x + ox * off, y + oy * off, c);
      }
    }
  }
}
function dotted(L, pts, c, every = 2) {
  for (let i = 0; i < pts.length; i++) if (i % every === 0) put(L, pts[i][0], pts[i][1], c);
}
function rect(L, x, y, w, h, c) {
  for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) put(L, x + i, y + j, c);
}
function disc(L, cx, cy, r, c) {
  for (let y = Math.floor(cy - r - 1); y <= cy + r + 1; y++)
    for (let x = Math.floor(cx - r - 1); x <= cx + r + 1; x++)
      if ((x - cx) ** 2 + (y - cy) ** 2 <= r * r + 0.3) put(L, x, y, c);
}
function ellipse(L, cx, cy, rx, ry, c) {
  for (let y = Math.floor(cy - ry - 1); y <= cy + ry + 1; y++)
    for (let x = Math.floor(cx - rx - 1); x <= cx + rx + 1; x++)
      if (((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.05) put(L, x, y, c);
}
function ring(L, cx, cy, r, th, c) {
  for (let y = Math.floor(cy - r - 1); y <= cy + r + 1; y++)
    for (let x = Math.floor(cx - r - 1); x <= cx + r + 1; x++) {
      const d = Math.sqrt((x - cx) ** 2 + (y - cy) ** 2);
      if (d <= r + 0.5 && d > r - th + 0.5) put(L, x, y, c);
    }
}
function ellipseRing(L, cx, cy, rx, ry, c) {
  arcPts(cx, cy, rx, ry, 0, 360, 1).forEach(p => put(L, p[0], p[1], c));
}
// 각도는 도(0 = 오른쪽, 90 = 아래)
function arcPts(cx, cy, rx, ry, a0, a1, step = 1) {
  const pts = [];
  let last = "";
  const n = Math.ceil(Math.abs(a1 - a0) * Math.max(rx, ry) / 40) + 2;
  for (let s = 0; s <= n; s++) {
    const a = (a0 + (a1 - a0) * s / n) * Math.PI / 180;
    const p = [Math.round(cx + Math.cos(a) * rx), Math.round(cy + Math.sin(a) * ry)];
    const key = p.join(",");
    if (key !== last) {
      pts.push(p);
      last = key;
    }
  }
  return pts;
}
function arc(L, cx, cy, r, a0, a1, c, w = 1) {
  for (let k = 0; k < w; k++) arcPts(cx, cy, r - k, r - k, a0, a1).forEach(p => put(L, p[0], p[1], c));
}
function poly(L, pts, c) {
  let minX = 99, minY = 99, maxX = -99, maxY = -99;
  for (const p of pts) {
    minX = Math.min(minX, p[0]); maxX = Math.max(maxX, p[0]);
    minY = Math.min(minY, p[1]); maxY = Math.max(maxY, p[1]);
  }
  for (let y = Math.floor(minY); y <= Math.ceil(maxY); y++)
    for (let x = Math.floor(minX); x <= Math.ceil(maxX); x++) {
      let inside = false;
      for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
        const xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
        if ((yi > y) !== (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
      }
      if (inside) put(L, x, y, c);
    }
}
// 선분 (x0,y0)->(x1,y1) 기준 좌표계: u = 진행 방향, v = 왼쪽(화면 기준 수직)
function frame(x0, y0, x1, y1) {
  const dx = x1 - x0, dy = y1 - y0, len = Math.hypot(dx, dy);
  const d = [dx / len, dy / len], n = [d[1], -d[0]];
  return { len, at: (u, v) => [x0 + d[0] * u + n[0] * v, y0 + d[1] * u + n[1] * v], d, n };
}

// ------------------------------------------------------------ 그림 조각
// 아이콘 하나 = 배경(bg) + 아래 효과(U, 테두리 없음) + 본 그림(G, 검은 테두리) + 위 효과(F, 테두리 없음)
function sword(G, x0, y0, x1, y1, o = {}) {
  const f = frame(x0, y0, x1, y1);
  const blade = o.blade || C.steel, guard = o.guard || C.gold;
  const grip = 3.2, gw = o.guardHalf || 3;
  line(G, ...f.at(0, 0), ...f.at(grip, 0), C.wood[1], 1);
  put(G, ...f.at(0, 0), guard[1]);
  poly(G, [f.at(grip + 0.8, -1.4), f.at(f.len - 2.2, -1.4), f.at(f.len, 0), f.at(f.len - 2.2, 1.4), f.at(grip + 0.8, 1.4)], blade[1]);
  line(G, ...f.at(grip + 1.2, 0.5), ...f.at(f.len - 2, 0.5), blade[2], 1);
  line(G, ...f.at(grip + 0.5, -gw), ...f.at(grip + 0.5, gw), guard[1], 1);
  put(G, ...f.at(grip + 0.5, -gw), guard[2]);
}
function spear(G, x0, y0, x1, y1, o = {}) {
  const f = frame(x0, y0, x1, y1);
  const head = o.head || C.steel;
  line(G, ...f.at(0, 0), ...f.at(f.len - 4, 0), C.wood[1], 1);
  poly(G, [f.at(f.len - 5.5, 0), f.at(f.len - 3.5, -2.2), f.at(f.len + 0.5, 0), f.at(f.len - 3.5, 2.2)], head[1]);
  line(G, ...f.at(f.len - 4.5, 0.3), ...f.at(f.len - 1, 0.3), head[2], 1);
  if (o.tassel) put(G, ...f.at(f.len - 6, 1), C.red[1]);
}
function axe(G, x0, y0, x1, y1, side = 1, o = {}) {
  const f = frame(x0, y0, x1, y1);
  line(G, ...f.at(0, 0), ...f.at(f.len, 0), C.wood[1], 2);
  const head = o.head || C.steel;
  const L = f.len;
  const p = (u, v) => f.at(L - u, v * side);
  poly(G, [p(0.5, -1), p(6.5, -1), p(8.5, -6.5), p(4, -5), p(-1.5, -6.5)], head[1]);
  line(G, ...p(7.5, -6), ...p(-0.5, -6), head[2], 1);
  poly(G, [p(1, 1), p(5, 1), p(4, 3), p(2, 3)], head[0]);
}
function hammer(G, x0, y0, x1, y1, o = {}) {
  const f = frame(x0, y0, x1, y1);
  line(G, ...f.at(0, 0), ...f.at(f.len - 2, 0), C.wood[1], 2);
  const head = o.head || C.iron;
  const L = f.len;
  poly(G, [f.at(L - 5, -5), f.at(L + 0.5, -5), f.at(L + 0.5, 5), f.at(L - 5, 5)], head[1]);
  line(G, ...f.at(L - 4.5, -4.5), ...f.at(L - 4.5, 4.5), head[2], 1);
  line(G, ...f.at(L, -4.5), ...f.at(L, 4.5), head[0], 1);
}
function arrow(G, x0, y0, x1, y1, o = {}) {
  const f = frame(x0, y0, x1, y1);
  const head = o.head || C.steel;
  line(G, ...f.at(0, 0), ...f.at(f.len - 2, 0), o.shaft || C.wood[2], 1);
  poly(G, [f.at(f.len - 3, -2), f.at(f.len + 0.6, 0), f.at(f.len - 3, 2)], head[1]);
  put(G, ...f.at(f.len - 1, 0), head[2]);
  const fl = o.fletch || C.red;
  line(G, ...f.at(0, 0), ...f.at(-1.5, -1.8), fl[1], 1);
  line(G, ...f.at(0, 0), ...f.at(-1.5, 1.8), fl[1], 1);
  line(G, ...f.at(1.2, 0), ...f.at(-0.3, -1.8), fl[2], 1);
}
function bolt(G, x0, y0, x1, y1, o = {}) {
  const f = frame(x0, y0, x1, y1);
  const head = o.head || C.iron;
  line(G, ...f.at(0, 0), ...f.at(f.len - 2, 0), C.wood[1], 2);
  poly(G, [f.at(f.len - 3.5, -2.5), f.at(f.len + 0.8, 0), f.at(f.len - 3.5, 2.5)], head[1]);
  put(G, ...f.at(f.len - 1.5, 0.5), head[2]);
  line(G, ...f.at(0, -1), ...f.at(2, -2), C.steel[2], 1);
  line(G, ...f.at(0, 1), ...f.at(2, 2), C.steel[2], 1);
}
function lightning(G, pts, c = C.volt, w = 2) {
  for (let i = 0; i + 1 < pts.length; i++) line(G, ...pts[i], ...pts[i + 1], c[1], w);
  for (let i = 0; i + 1 < pts.length; i++) line(G, ...pts[i], ...pts[i + 1], c[2], 1);
}
function flame(G, cx, by, h, o = {}) {
  const c = o.c || C.fire;
  const w = h * 0.42;
  const tear = (s, col) => {
    const r = w * s, top = by - h * s;
    disc(G, cx, by - r, r, col);
    poly(G, [[cx - r * 0.95, by - r * 1.2], [cx + (o.lean || 0) * s, top], [cx + r * 0.95, by - r * 1.2]], col);
  };
  tear(1, c[0]);
  tear(0.72, c[1]);
  tear(0.42, c[2]);
}
function shard(G, x0, y0, x1, y1, c = C.ice, wid = 2.2) {
  const f = frame(x0, y0, x1, y1);
  poly(G, [f.at(-0.5, 0), f.at(f.len * 0.3, -wid), f.at(f.len + 0.5, 0), f.at(f.len * 0.3, wid)], c[1]);
  line(G, ...f.at(f.len * 0.25, -wid * 0.45), ...f.at(f.len - 1, 0), c[2], 1);
}
function snowflake(G, cx, cy, r, c = C.ice) {
  for (let k = 0; k < 3; k++) {
    const a = k * Math.PI / 3 + Math.PI / 2;
    const dx = Math.cos(a) * r, dy = Math.sin(a) * r;
    line(G, cx - dx, cy - dy, cx + dx, cy + dy, c[1], 1);
    for (const s of [-1, 1]) {
      const bx = cx + dx * 0.6 * s, by = cy + dy * 0.6 * s;
      const b1 = a + 0.7, b2 = a - 0.7;
      line(G, bx, by, bx + Math.cos(b1) * 2.4 * s, by + Math.sin(b1) * 2.4 * s, c[2], 1);
      line(G, bx, by, bx + Math.cos(b2) * 2.4 * s, by + Math.sin(b2) * 2.4 * s, c[2], 1);
    }
  }
  disc(G, cx, cy, 1.2, c[2]);
}
function shield(G, cx, cy, w, h, c = C.steel, emblem = null) {
  const x0 = cx - w / 2, x1 = cx + w / 2, y0 = cy - h / 2, y1 = cy + h / 2;
  poly(G, [[x0, y0], [x1, y0], [x1, cy + h * 0.1], [cx, y1], [x0, cy + h * 0.1]], c[1]);
  line(G, x0 + 1, y0 + 1, x1 - 1, y0 + 1, c[2], 1);
  line(G, x0 + 1, y0 + 1, x0 + 1, cy + h * 0.05, c[2], 1);
  line(G, x1 - 1, y0 + 2, x1 - 1, cy + h * 0.08, c[0], 1);
  if (emblem) emblem(cx, cy);
}
function heart(G, cx, cy, s, c = C.red) {
  disc(G, cx - s * 0.5, cy - s * 0.25, s * 0.55, c[1]);
  disc(G, cx + s * 0.5, cy - s * 0.25, s * 0.55, c[1]);
  poly(G, [[cx - s * 1.05, cy - s * 0.1], [cx + s * 1.05, cy - s * 0.1], [cx, cy + s * 1.0]], c[1]);
  disc(G, cx - s * 0.6, cy - s * 0.4, s * 0.22, c[2]);
}
function plus(G, cx, cy, len, th, c) {
  rect(G, cx - th / 2, cy - len / 2, th, len, c);
  rect(G, cx - len / 2, cy - th / 2, len, th, c);
}
function drop(G, cx, cy, r, c = C.red) {
  disc(G, cx, cy, r, c[1]);
  poly(G, [[cx - r * 0.85, cy - r * 0.5], [cx, cy - r * 2.3], [cx + r * 0.85, cy - r * 0.5]], c[1]);
  disc(G, cx - r * 0.35, cy - r * 0.2, Math.max(0.6, r * 0.3), c[2]);
}
function skull(G, cx, cy, s = 1) {
  disc(G, cx, cy - 0.5 * s, 3.6 * s, C.bone[1]);
  rect(G, cx - 2 * s, cy + 2 * s, 4 * s + 1, 2 * s, C.bone[1]);
  rect(G, cx - 2 * s, cy - 1 * s, 2 * s, 2 * s, C.iron[0]);
  rect(G, cx + 1 * s, cy - 1 * s, 2 * s, 2 * s, C.iron[0]);
  put(G, cx, cy + 1.5 * s, C.iron[0]);
  put(G, cx - 1.5 * s, cy - 3 * s, C.bone[2]);
}
function cloud(G, cx, cy, c) {
  disc(G, cx - 4, cy + 1, 3.2, c[1]);
  disc(G, cx + 4, cy + 1, 3.2, c[1]);
  disc(G, cx, cy - 1.5, 4.2, c[1]);
  rect(G, cx - 5, cy + 1, 10, 3.5, c[1]);
  disc(G, cx - 1.5, cy - 3, 1.4, c[2]);
  disc(G, cx - 4.8, cy - 0.5, 1, c[2]);
}
function orb(G, cx, cy, r, c) {
  disc(G, cx, cy, r, c[1]);
  disc(G, cx + r * 0.2, cy + r * 0.2, r * 0.6, c[0]);
  disc(G, cx, cy, r * 0.55, c[1]);
  disc(G, cx - r * 0.35, cy - r * 0.35, Math.max(0.7, r * 0.3), c[2]);
}
function star4(F, cx, cy, r, c) {
  line(F, cx - r, cy, cx + r, cy, c, 1);
  line(F, cx, cy - r, cx, cy + r, c, 1);
  put(F, cx, cy, WHITE);
  if (r >= 3) {
    put(F, cx - 1, cy - 1, alpha(c, 160)); put(F, cx + 1, cy - 1, alpha(c, 160));
    put(F, cx - 1, cy + 1, alpha(c, 160)); put(F, cx + 1, cy + 1, alpha(c, 160));
  }
}
function burst(G, cx, cy, r, c = C.fire, spikes = 8) {
  const pts = [];
  for (let i = 0; i < spikes * 2; i++) {
    const a = i * Math.PI / spikes + 0.2;
    const rr = i % 2 === 0 ? r : r * 0.5;
    pts.push([cx + Math.cos(a) * rr, cy + Math.sin(a) * rr]);
  }
  poly(G, pts, c[1]);
  disc(G, cx, cy, r * 0.42, c[2]);
}
function chevron(F, cx, cy, s, c) {
  line(F, cx - s, cy + s, cx, cy, c, 2);
  line(F, cx, cy, cx + s, cy + s, c, 2);
}
function arrowHead(G, x, y, dirDeg, s, c) {
  const a = dirDeg * Math.PI / 180;
  const d = [Math.cos(a), Math.sin(a)], n = [-d[1], d[0]];
  poly(G, [[x + d[0] * s, y + d[1] * s], [x - d[0] * s * 0.6 + n[0] * s, y - d[1] * s * 0.6 + n[1] * s],
    [x - d[0] * s * 0.6 - n[0] * s, y - d[1] * s * 0.6 - n[1] * s]], c);
}
function speedLines(U, x0, x1, ys, c) {
  ys.forEach((y, i) => line(U, x0 + (i % 2) * 2, y, x1 - (i % 2), y, c, 1));
}
function ghost(G, cx, cy, c) {
  disc(G, cx, cy - 2, 4.5, c[1]);
  rect(G, cx - 4.5, cy - 2, 10, 6, c[1]);
  for (let i = 0; i < 3; i++) poly(G, [[cx - 4.5 + i * 3.3, cy + 3], [cx - 2.9 + i * 3.3, cy + 6], [cx - 1.3 + i * 3.3, cy + 3]], c[1]);
  rect(G, cx - 2.5, cy - 3, 2, 2, C.dark[0]);
  rect(G, cx + 1, cy - 3, 2, 2, C.dark[0]);
  disc(G, cx - 2, cy - 5, 1, c[2]);
}
function pistol(G, x, y, flip = 1, c = C.iron) {
  const P = (u, v) => [x + u * flip, y + v];
  poly(G, [P(0, 0), P(11, 0), P(11, 3), P(4, 3), P(4.5, 8), P(1, 8), P(0, 3)], c[1]);
  line(G, ...P(1, 1), ...P(10, 1), c[2], 1);
  put(G, ...P(3, 4), c[0]);
  rect(G, Math.min(P(1.5, 5)[0], P(3.5, 5)[0]), y + 5, 2, 2, C.wood[1]);
}
function bomb(G, cx, cy, r, F) {
  disc(G, cx, cy, r, C.iron[0]);
  disc(G, cx - r * 0.35, cy - r * 0.35, r * 0.35, C.iron[2]);
  rect(G, cx + r * 0.3, cy - r - 1, 2, 2, C.iron[1]);
  line(G, cx + r * 0.6, cy - r - 1, cx + r * 0.9 + 1, cy - r - 3, C.wood[2], 1);
  if (F) star4(F, cx + r * 0.9 + 2, cy - r - 4, 2, C.volt[1]);
}
function wing(G, cx, cy, side, c = C.bone) {
  const P = (u, v) => [cx + u * side, cy + v];
  poly(G, [P(0, 0), P(7, -6), P(8, -2), P(7, 1), P(5, 4), P(1, 3)], c[1]);
  line(G, ...P(2, 0), ...P(7, -4), c[2], 1);
  line(G, ...P(2, 2), ...P(6, 1), c[0], 1);
}
function crack(F, x, y, c, len = 1) {
  line(F, x, y, x - 4 * len, y + 2, c, 1);
  line(F, x - 4 * len, y + 2, x - 7 * len, y + 1, c, 1);
  line(F, x, y, x + 4 * len, y + 2, c, 1);
  line(F, x + 4 * len, y + 2, x + 8 * len, y + 1, c, 1);
  line(F, x, y, x + 1, y + 3, c, 1);
}
function spiral(L, cx, cy, r, turns, c, w = 1, a0 = 0) {
  const n = 200;
  for (let i = 0; i <= n; i++) {
    const t = i / n;
    const a = a0 + t * turns * Math.PI * 2;
    const rr = r * t;
    for (let k = 0; k < w; k++) put(L, cx + Math.cos(a) * (rr + k * 0.6), cy + Math.sin(a) * (rr + k * 0.6), c);
  }
}

// ------------------------------------------------------------ 스킬별 그림
const BG = {};
const DRAW = {};
function def(name, bg, fn) {
  BG[name] = bg;
  DRAW[name] = fn;
}
const glow = (c, a) => alpha(c, a);

// 전사: 검사
def("SwordSpin", "a83434", (U, G, F) => {
  arc(F, 12, 12, 10, 200, 320, C.steel[2], 2);
  arrowHead(F, 18, 4, 20, 3, C.steel[2]);
  arc(F, 12, 12, 10, 20, 140, C.steel[2], 2);
  arrowHead(F, 6, 20, 200, 3, C.steel[2]);
  sword(G, 7, 17, 17, 7);
  drop(F, 4, 8, 1.2);
  drop(F, 19, 20, 1.2);
});
def("SwordCross", "8c2a3a", (U, G, F) => {
  sword(G, 4, 20, 20, 4);
  sword(G, 20, 20, 4, 4);
  line(U, 2, 2, 22, 22, glow(WHITE, 70), 1);
  line(U, 22, 2, 2, 22, glow(WHITE, 70), 1);
  star4(F, 12, 12, 3, C.steel[2]);
});
def("SwordBash", "4a5a78", (U, G, F) => {
  speedLines(U, 1, 6, [7, 11, 15, 19], glow(WHITE, 150));
  shield(G, 12, 12, 12, 16, C.steel, (x, y) => { rect(G, x - 1, y - 4, 2, 8, C.steel[0]); });
  burst(F, 20, 6, 3.5, C.volt, 6);
});
def("SwordGuard", "2a58a8", (U, G, F) => {
  disc(U, 12, 12, 10, glow(C.holy[1], 60));
  shield(G, 12, 13, 12, 15, C.gold, (x, y) => { plus(G, x, y - 1, 7, 2, C.red[1]); });
  arc(F, 12, 13, 10, 200, 250, C.holy[2], 1);
  arc(F, 12, 13, 10, 290, 340, C.holy[2], 1);
});
def("SwordCharge", "b04622", (U, G, F) => {
  speedLines(U, 1, 9, [6, 9, 15, 18], glow(WHITE, 170));
  sword(G, 5, 12, 22, 12, { guardHalf: 3.5 });
  put(F, 3, 12, WHITE);
});
def("SwordRoar", "c23a1e", (U, G, F) => {
  sword(G, 12, 21, 12, 3, { guardHalf: 3.5 });
  for (const r of [4, 7]) {
    arc(F, 12, 10, r + 3, 140, 220, C.volt[1], 1);
    arc(F, 12, 10, r + 3, -40, 40, C.volt[1], 1);
  }
});
// 전사: 창기사
def("SpearPierce", "6a5a24", (U, G, F) => {
  speedLines(U, 1, 8, [5, 8], glow(WHITE, 140));
  spear(G, 2, 20, 22, 4, { tassel: true });
  drop(F, 17, 15, 1.3);
  drop(F, 20, 12, 1);
  line(U, 7, 22, 22, 9, glow(WHITE, 90), 1);
});
def("SpearThrow", "4a2e9a", (U, G, F) => {
  spear(G, 4, 20, 21, 3, { head: C.volt });
  lightning(F, [[3, 13], [7, 12], [8, 15], [12, 10]], C.volt, 1);
  lightning(F, [[11, 21], [13, 17], [16, 18], [19, 13]], C.volt, 1);
  star4(F, 20, 4, 2, C.volt[2]);
});
def("SpearSweep", "1f6e7e", (U, G, F) => {
  arc(U, 12, 6, 14, 25, 155, glow(hex("90ecff"), 200), 3);
  arc(F, 12, 6, 14, 25, 155, hex("e8fcff"), 1);
  spear(G, 2, 11, 22, 5);
});
def("SpearGale", "1f7a4c", (U, G, F) => {
  spiral(G, 13, 11, 8, 1.6, C.leaf[2], 2, 0);
  line(F, 2, 17, 10, 17, C.leaf[2], 1);
  line(F, 4, 20, 14, 20, glow(C.leaf[2], 180), 1);
  chevron(F, 18, 18, 3, WHITE);
});
def("SpearLeap", "7a5a2a", (U, G, F) => {
  dotted(U, arcPts(12, 14, 9, 11, 180, 270), glow(WHITE, 200), 2);
  spear(G, 12, 1, 12, 18);
  line(G, 13, 1, 13, 13, C.wood[2], 1);
  line(F, 4, 21, 20, 21, C.stone[0], 1);
  crack(F, 13, 19, C.stone[2], 0.8);
});
def("SpearStorm", "2c2670", (U, G, F) => {
  cloud(G, 12, 5, C.iron);
  lightning(F, [[7, 8], [5, 13], [8, 14], [5, 20]], C.volt, 2);
  lightning(F, [[17, 8], [19, 12], [16, 13], [18, 17]], C.volt, 1);
  spear(G, 12, 22, 12, 10);
});
// 사수: 레인저
def("BowFan", "2a7234", (U, G, F) => {
  arrow(G, 3, 21, 9, 4, { head: C.steel });
  arrow(G, 3, 21, 16, 7, { head: C.steel });
  arrow(G, 3, 21, 20, 15, { head: C.steel });
  arc(U, 3, 21, 18, -78, -14, glow(WHITE, 90), 1);
});
def("BowBlast", "a84418", (U, G, F) => {
  arrow(G, 3, 21, 13, 11);
  burst(G, 16, 8, 6.5, C.fire, 8);
  star4(F, 20, 3, 1, C.volt[2]);
});
def("BowLeap", "2a6274", (U, G, F) => {
  disc(U, 4, 20, 2.2, glow(C.stone[2], 200));
  disc(U, 7, 21, 1.5, glow(C.stone[2], 160));
  arc(G, 12, 19, 9, 195, 335, C.steel[2], 2);
  arrowHead(G, 20, 16, 70, 3, C.steel[2]);
  poly(G, [[6, 13], [9, 4], [13, 2], [12, 7], [15, 6], [12, 11]], C.bone[1]);
  line(G, 7, 12, 12, 3, C.bone[2], 1);
  line(G, 9, 11, 13, 7, C.bone[0], 1);
});
def("BowFocus", "3e5c1e", (U, G, F) => {
  ring(U, 12, 12, 10, 1, glow(C.red[2], 200));
  line(F, 12, 1, 12, 4, C.red[2], 1); line(F, 12, 20, 12, 23, C.red[2], 1);
  line(F, 1, 12, 4, 12, C.red[2], 1); line(F, 20, 12, 23, 12, C.red[2], 1);
  ellipse(G, 12, 12, 7.5, 4.2, C.bone[2]);
  disc(G, 12, 12, 3.2, C.leaf[1]);
  disc(G, 12, 12, 1.4, C.iron[0]);
  put(G, 11, 11, WHITE);
});
def("BowRain", "2e5a22", (U, G, F) => {
  cloud(U, 12, 3, [null, glow(C.poison[0], 200), glow(C.poison[1], 200)]);
  for (const x of [5, 12, 19]) arrow(G, x - 2, 5 + (x % 3), x, 19 - (x % 4), { head: C.poison, fletch: C.poison });
  disc(F, 8, 21, 0.8, C.poison[2]);
  disc(F, 16, 21, 0.8, C.poison[2]);
});
def("BowBless", "2a8444", (U, G, F) => {
  disc(U, 12, 12, 9, glow(C.heal[2], 50));
  poly(G, [[4, 20], [6, 9], [13, 3], [20, 4], [18, 12], [11, 18]], C.leaf[1]);
  line(G, 5, 19, 17, 6, C.leaf[0], 1);
  line(G, 9, 11, 7, 11, C.leaf[2], 1);
  plus(F, 18, 17, 6, 2, C.heal[2]);
});
// 마법사: 원소술사
def("StaffFireball", "8e1e10", (U, G, F) => {
  line(U, 3, 21, 12, 12, glow(C.fire[1], 160), 4);
  line(U, 5, 21, 12, 14, glow(C.fire[2], 120), 1);
  disc(G, 15, 9, 6, C.fire[0]);
  disc(G, 15.5, 8.5, 4.3, C.fire[1]);
  disc(G, 16, 8, 2.3, C.fire[2]);
  put(F, 17, 7, WHITE);
});
def("StaffFlame", "7e1c0c", (U, G, F) => {
  ellipse(U, 12, 20, 10, 2.5, glow(C.fire[1], 120));
  flame(G, 6, 21, 10, { lean: -1 });
  flame(G, 18, 21, 11, { lean: 1 });
  flame(G, 12, 21, 16);
});
def("StaffIce", "1e4e86", (U, G, F) => {
  shard(G, 2, 16, 14, 4);
  shard(G, 5, 21, 19, 7);
  shard(G, 11, 23, 22, 12);
  star4(F, 14, 4, 1, C.ice[2]);
});
def("StaffNova", "1e62a8", (U, G, F) => {
  ring(U, 12, 12, 11, 2, glow(C.ice[2], 140));
  disc(U, 12, 12, 8, glow(C.ice[1], 90));
  snowflake(G, 12, 12, 8.5);
});
def("StaffStorm", "3c2e8a", (U, G, F) => {
  disc(U, 12, 12, 7, glow(C.volt[1], 80));
  lightning(G, [[12, 9], [10, 5], [13, 4], [11, 0]], C.volt, 1);
  lightning(G, [[15, 12], [19, 10], [20, 14], [23, 12]], C.volt, 1);
  lightning(G, [[11, 15], [9, 19], [12, 20], [10, 23]], C.volt, 1);
  lightning(G, [[9, 12], [5, 14], [4, 10], [1, 12]], C.volt, 1);
  orb(G, 12, 12, 3.6, C.volt);
});
def("StaffBlink", "5a2a92", (U, G, F) => {
  dotted(U, arcPts(6, 16, 4, 4, 0, 360), glow(C.arcane[2], 220), 2);
  line(G, 8, 14, 15, 9, C.arcane[2], 2);
  arrowHead(G, 16, 8, -35, 3.2, C.arcane[2]);
  star4(F, 19, 5, 3, C.arcane[2]);
  star4(F, 21, 13, 1, WHITE);
  star4(F, 13, 4, 1, WHITE);
});
// 전사: 광전사(도끼)
def("AxeRage", "7e0e18", (U, G, F) => {
  disc(U, 11, 13, 9, glow(C.red[1], 70));
  drop(G, 10, 15, 5.2);
  chevron(F, 19, 9, 3, C.volt[1]);
  chevron(F, 19, 14, 3, C.volt[1]);
});
def("AxeWhirl", "92161a", (U, G, F) => {
  arc(F, 12, 12, 11, 230, 330, C.red[2], 2);
  arrowHead(F, 20, 5, 30, 3, C.red[2]);
  arc(F, 12, 12, 11, 50, 150, C.red[2], 2);
  arrowHead(F, 4, 19, 210, 3, C.red[2]);
  axe(G, 5, 19, 13, 11, 1);
  axe(G, 19, 5, 11, 13, 1);
});
def("AxeThrow", "6e3016", (U, G, F) => {
  arc(U, 13, 12, 10, 100, 170, glow(WHITE, 150), 1);
  arc(U, 13, 12, 10, 190, 260, glow(WHITE, 150), 1);
  speedLines(U, 1, 5, [9, 13, 17], glow(WHITE, 120));
  axe(G, 8, 17, 18, 7, -1);
  drop(F, 20, 20, 1.2);
});
def("AxeExecute", "4e0e12", (U, G, F) => {
  axe(G, 4, 21, 17, 5, 1, { head: C.steel });
  skull(F, 18, 18, 1);
});
def("AxeSplit", "6a3e16", (U, G, F) => {
  rect(U, 1, 19, 22, 4, glow(C.stone[0], 220));
  axe(G, 12, 2, 12, 18, -1);
  crack(F, 12, 19, C.stone[2], 1);
  disc(F, 4, 15, 1, C.stone[2]);
  disc(F, 20, 14, 1, C.stone[2]);
});
def("AxeEndure", "561a1a", (U, G, F) => {
  disc(U, 12, 12, 10, glow(C.red[1], 60));
  shield(G, 12, 12, 14, 18, C.iron, (x, y) => { drop(G, x, y + 1, 2.4); });
  line(F, 7, 5, 9, 9, C.iron[0], 1);
});
// 전사: 둔기(검사·창기사·광전사로 나뉨)
def("BluntSlam", "6e4e2c", (U, G, F) => {
  hammer(G, 12, 2, 12, 15);
  rect(U, 1, 20, 22, 3, glow(C.stone[0], 220));
  burst(F, 12, 19, 4, C.volt, 6);
  disc(F, 4, 16, 1, C.stone[2]);
  disc(F, 20, 17, 1, C.stone[2]);
});
def("BluntQuake", "5e3e1e", (U, G, F) => {
  poly(G, [[1, 14], [8, 12], [13, 15], [23, 13], [23, 23], [1, 23]], C.stone);
  poly(G, [[1, 14], [8, 12], [13, 15], [23, 13], [23, 23], [1, 23]], C.stone[1]);
  line(G, 2, 15, 22, 14, C.stone[2], 1);
  line(F, 9, 14, 11, 18, C.iron[0], 1); line(F, 11, 18, 9, 22, C.iron[0], 1);
  line(F, 16, 14, 17, 19, C.iron[0], 1);
  disc(G, 5, 7, 1.8, C.stone[1]); disc(G, 18, 5, 1.3, C.stone[1]); disc(G, 13, 2, 1, C.stone[2]);
  arc(U, 12, 13, 9, 200, 340, glow(WHITE, 120), 1);
});
def("BluntLeap", "7c5e3e", (U, G, F) => {
  ellipseRing(U, 12, 20, 10, 2.5, glow(C.volt[2], 200));
  ellipseRing(U, 12, 20, 6, 1.5, glow(C.volt[2], 230));
  hammer(G, 5, 4, 14, 15);
  line(F, 18, 2, 18, 7, WHITE, 1); line(F, 21, 4, 21, 9, WHITE, 1);
});
def("BluntRam", "6c3e26", (U, G, F) => {
  speedLines(U, 1, 8, [6, 9, 15, 18], glow(WHITE, 170));
  hammer(G, 4, 12, 17, 12);
  disc(F, 21, 5, 1.2, C.stone[2]);
  disc(F, 22, 19, 1, C.stone[2]);
  star4(F, 21, 12, 2, C.volt[2]);
});
def("BluntWall", "4e4e60", (U, G, F) => {
  rect(G, 3, 6, 18, 15, C.stone[1]);
  for (const x of [3, 9, 15]) rect(G, x, 3, 4, 3, C.stone[1]);
  for (let r = 0; r < 4; r++) {
    line(G, 3, 9 + r * 4, 20, 9 + r * 4, C.stone[0], 1);
    const off = (r % 2) * 3;
    for (let x = 6 + off; x < 20; x += 6) line(G, x, 6 + r * 4, x, 8 + r * 4, C.stone[0], 1);
  }
  line(G, 4, 7, 19, 7, C.stone[2], 1);
});
def("BluntVigor", "3e5e2e", (U, G, F) => {
  poly(G, [[2, 21], [9, 8], [13, 13], [16, 10], [22, 21]], C.stone[1]);
  poly(G, [[7, 12], [9, 8], [11, 11]], C.stone[2]);
  plus(F, 17, 6, 7, 3, C.heal[2]);
  plus(F, 17, 6, 5, 1, WHITE);
});
// 사수: 저격수(석궁)
def("CrossPierce", "1e5e60", (U, G, F) => {
  ring(G, 13, 12, 6, 2, C.red);
  ring(G, 13, 12, 6, 2, C.red[1]);
  disc(G, 13, 12, 1.5, C.bone[2]);
  bolt(G, 1, 12, 23, 12);
  drop(F, 17, 20, 1.2);
});
def("CrossAim", "283a50", (U, G, F) => {
  ring(G, 12, 12, 8, 2, C.iron[1]);
  line(G, 12, 2, 12, 8, C.iron[1], 1); line(G, 12, 16, 12, 22, C.iron[1], 1);
  line(G, 2, 12, 8, 12, C.iron[1], 1); line(G, 16, 12, 22, 12, C.iron[1], 1);
  disc(F, 12, 12, 1.4, C.red[2]);
  put(F, 12, 12, WHITE);
});
def("CrossBurst", "1e4e60", (U, G, F) => {
  bolt(G, 2, 18, 15, 5);
  bolt(G, 5, 21, 18, 8);
  bolt(G, 8, 24, 21, 11);
});
def("CrossBomb", "7c3e1c", (U, G, F) => {
  bolt(G, 2, 21, 12, 11);
  bomb(G, 16, 9, 4.5, F);
});
def("CrossTrap", "1e4e70", (U, G, F) => {
  // 벌린 덫(앞에서 본 모습): 아래로 둥근 턱 두 개와 위로 솟은 이빨, 얼음 조각
  ellipse(U, 12, 19, 10, 2.5, glow(C.ice[2], 120));
  arc(G, 12, 10, 9, 15, 165, C.iron[1], 2);
  arc(G, 12, 10, 6, 25, 155, C.iron[0], 1);
  for (let k = 0; k < 7; k++) {
    const a = (25 + k * 21.6) * Math.PI / 180;
    const bx = 12 + Math.cos(a) * 7.5, by = 10 + Math.sin(a) * 7.5;
    poly(G, [[bx - 1.1, by], [bx + (12 - bx) * 0.35, by - 4.5], [bx + 1.1, by]], C.steel[2]);
  }
  rect(G, 10, 18, 5, 3, C.iron[1]);
  shard(F, 3, 9, 6, 3, C.ice, 1.3);
  shard(F, 21, 9, 18, 3, C.ice, 1.3);
  star4(F, 12, 4, 2, C.ice[2]);
});
def("CrossRoll", "2a4e50", (U, G, F) => {
  arc(G, 12, 11, 7, -90, 160, C.steel[2], 2);
  arrowHead(G, 5, 14, 100, 3, C.steel[2]);
  disc(U, 17, 20, 2.2, glow(C.stone[2], 200));
  disc(U, 21, 19, 1.5, glow(C.stone[2], 160));
  bolt(F, 9, 12, 16, 12);
});
// 사수: 건슬링어
def("GunSpray", "7a4818", (U, G, F) => {
  pistol(G, 2, 9);
  for (const [x, y] of [[16, 4], [19, 8], [20, 12], [18, 16], [15, 19], [21, 3], [22, 17]]) {
    line(F, x - 1, y, x + 1, y, C.volt[1], 1);
  }
  put(F, 14, 10, WHITE);
});
def("GunRapid", "7e3a10", (U, G, F) => {
  for (const x of [5, 10, 15]) {
    rect(G, x - 1, 10, 4, 10, C.brass[1]);
    disc(G, x + 0.5, 9, 1.6, C.fire[0]);
    rect(G, x - 1, 18, 4, 2, C.brass[0]);
    line(G, x - 1, 11, x - 1, 17, C.brass[2], 1);
  }
  chevron(F, 19, 6, 3, C.volt[1]);
  chevron(F, 19, 11, 3, C.volt[1]);
});
def("GunShotgun", "5e3016", (U, G, F) => {
  poly(G, [[1, 20], [5, 15], [8, 17], [4, 22]], C.wood[1]);
  poly(G, [[5, 15], [15, 7], [17, 9], [8, 17]], C.iron[1]);
  line(G, 6, 15, 15, 8, C.iron[2], 1);
  poly(G, [[8, 15], [12, 12], [13, 14], [9, 17]], C.wood[2]);
  rect(G, 6, 17, 2, 3, C.iron[0]);
  for (const [x, y] of [[17, 4], [19, 6], [20, 9], [17, 8], [21, 12], [16, 2], [22, 5]]) put(F, x, y, C.volt[2]);
  arc(U, 15, 9, 7, -80, 20, glow(C.volt[1], 120), 1);
});
def("GunGrenade", "4a5820", (U, G, F) => {
  ellipse(G, 12, 14, 6, 7.5, C.poison[0]);
  for (const y of [11, 15]) line(G, 7, y, 17, y, hex("1e3a10"), 1);
  line(G, 12, 8, 12, 21, hex("1e3a10"), 1);
  disc(G, 10, 11, 1, C.leaf[2]);
  rect(G, 10, 4, 5, 3, C.iron[1]);
  ring(G, 18, 5, 2.5, 1, C.steel[2]);
});
def("GunPowder", "6c2610", (U, G, F) => {
  flame(U, 12, 11, 11, { c: [glow(C.fire[0], 255), glow(C.fire[1], 255), glow(C.fire[2], 255)] });
  rect(G, 5, 10, 14, 12, C.wood[1]);
  line(G, 5, 13, 18, 13, C.iron[1], 1); line(G, 5, 19, 18, 19, C.iron[1], 1);
  line(G, 6, 11, 6, 21, C.wood[2], 1);
  disc(G, 12, 16, 1.6, C.iron[0]);
});
def("GunRoll", "5a3e1e", (U, G, F) => {
  arc(G, 12, 12, 8, -60, 230, C.steel[2], 2);
  arrowHead(G, 16, 5, 30, 3, C.steel[2]);
  rect(G, 10, 9, 4, 7, C.brass[1]);
  disc(G, 11.5, 8.5, 1.6, C.fire[0]);
  disc(U, 4, 20, 2, glow(C.stone[2], 180));
});
// 마법사: 주술사(완드)
def("WandCurse", "3a1a5a", (U, G, F) => {
  for (const [x, y, r] of [[5, 19, 2.6], [11, 12, 3.4], [18, 5, 4]]) {
    line(U, x - 4, y + 4, x, y, glow(C.arcane[1], 140), 2);
    orb(G, x, y, r, C.arcane);
    put(G, x - 0.8, y + 0.4, C.dark[0]); put(G, x + 0.8, y + 0.4, C.dark[0]);
  }
});
def("WandHex", "1c0c2c", (U, G, F) => {
  disc(U, 12, 12, 11, glow(C.arcane[0], 160));
  spiral(F, 12, 12, 10, 1.2, C.arcane[2], 1, 0);
  spiral(F, 12, 12, 10, 1.2, C.arcane[1], 1, Math.PI);
  disc(G, 12, 12, 3.4, BLACK);
  ring(F, 12, 12, 4, 1, C.arcane[2]);
});
def("WandMiasma", "264a1a", (U, G, F) => {
  cloud(G, 12, 11, C.poison);
  skull(F, 12, 13, 1);
  disc(F, 6, 20, 1, C.poison[2]); disc(F, 17, 21, 1.2, C.poison[2]);
});
def("WandVenom", "2e5418", (U, G, F) => {
  disc(G, 12, 14, 6.5, C.steel[2]);
  disc(G, 12, 15, 5.5, C.poison[1]);
  rect(G, 6, 13, 13, 2, C.poison[2]);
  rect(G, 10, 4, 5, 5, C.steel[2]);
  rect(G, 10, 2, 5, 2, C.wood[1]);
  disc(G, 9.5, 12, 1, WHITE);
  drop(F, 19, 22, 1.2, C.poison);
  drop(F, 5, 22, 1, C.poison);
});
def("WandSpirit", "442a6a", (U, G, F) => {
  speedLines(U, 1, 9, [8, 12, 16], glow(C.arcane[2], 180));
  ghost(G, 15, 12, [null, hex("e8deff"), WHITE]);
});
def("WandDrain", "4a1640", (U, G, F) => {
  heart(G, 12, 13, 5);
  arc(F, 12, 12, 10, 200, 270, C.arcane[2], 1);
  arc(F, 12, 12, 10, 20, 90, C.arcane[2], 1);
  disc(F, 5, 6, 1.4, C.arcane[2]);
  disc(F, 19, 18, 1.4, C.arcane[2]);
  disc(F, 20, 5, 1, C.arcane[1]);
});
// 마법사: 사제
def("PriestHeal", "26703e", (U, G, F) => {
  disc(U, 12, 12, 10, glow(C.heal[2], 70));
  plus(G, 12, 12, 16, 6, C.heal[1]);
  plus(G, 12, 12, 14, 2, C.heal[2]);
  star4(F, 20, 4, 2, WHITE);
});
def("PriestSanctum", "7c6820", (U, G, F) => {
  ellipse(U, 12, 18, 10, 4, glow(C.holy[1], 110));
  ellipseRing(G, 12, 18, 10, 4, C.holy[2]);
  rect(G, 11, 3, 3, 13, C.holy[1]);
  rect(G, 7, 6, 11, 3, C.holy[1]);
  line(G, 12, 4, 12, 14, WHITE, 1);
  put(F, 4, 9, C.holy[2]); put(F, 20, 10, C.holy[2]);
});
def("PriestBolt", "6e6220", (U, G, F) => {
  line(U, 3, 21, 12, 12, glow(C.holy[1], 140), 3);
  disc(U, 14, 10, 7, glow(C.holy[2], 90));
  orb(G, 14, 10, 4.5, C.holy);
  star4(F, 14, 10, 4, WHITE);
});
def("PriestJudge", "7e5e10", (U, G, F) => {
  rect(U, 7, 0, 10, 20, glow(C.holy[1], 120));
  rect(G, 9, 0, 6, 19, C.holy[1]);
  rect(G, 11, 0, 2, 19, WHITE);
  ellipse(U, 12, 20, 10, 2.5, glow(C.holy[2], 160));
  line(F, 2, 15, 6, 18, C.holy[2], 1);
  line(F, 22, 15, 18, 18, C.holy[2], 1);
});
def("PriestBless", "8a6e1e", (U, G, F) => {
  ellipseRing(F, 12, 3, 6, 2, C.holy[1]);
  ellipseRing(F, 12, 3, 5, 1.2, C.holy[2]);
  wing(G, 10, 13, -1, C.bone);
  wing(G, 14, 13, 1, C.bone);
  rect(G, 11, 9, 2, 11, C.gold[1]);
  arrowHead(G, 12, 8, -90, 3.5, C.gold[2]);
});
def("PriestRevive", "56647e", (U, G, F) => {
  disc(U, 12, 8, 8, glow(C.holy[2], 60));
  poly(G, [[5, 22], [5, 13], [7, 10], [17, 10], [19, 13], [19, 22]], C.stone[1]);
  line(G, 6, 13, 6, 21, C.stone[2], 1);
  rect(G, 11, 12, 2, 8, C.stone[0]);
  rect(G, 9, 14, 6, 2, C.stone[0]);
  rect(G, 11, 4, 2, 4, C.holy[2]);
  arrowHead(G, 12, 3, -90, 3, C.holy[2]);
  star4(F, 5, 5, 2, C.holy[2]);
  star4(F, 19, 6, 2, C.holy[2]);
});

// ------------------------------------------------------------ 차징 스킬(누르고 있다 떼기). 오른쪽 아래에 금빛 충전 눈금 세 칸을 공통으로 그린다.
function chargeMark(F) {
  rect(F, 16, 20, 2, 2, C.gold[1]);
  rect(F, 18.5, 18, 2, 4, C.gold[1]);
  rect(F, 21, 16, 2, 6, C.gold[2]);
}
// 검사 발도 일섬: 칼집에서 뽑아 길게 가르는 한 줄기 섬광
def("SwordIai", "24345a", (U, G, F) => {
  line(U, 1, 15, 23, 6, glow(hex("bfe6ff"), 150), 3);
  line(F, 1, 15, 23, 6, WHITE, 1);
  sword(G, 4, 20, 17, 8, { blade: C.ice });
  speedLines(U, 1, 9, [5, 8], glow(WHITE, 120));
  star4(F, 21, 6, 2.5, C.ice[2]);
  chargeMark(F);
});
// 창기사 용창 투척: 불길을 두른 창
def("SpearDragon", "6e2410", (U, G, F) => {
  line(U, 2, 21, 13, 10, glow(C.fire[1], 170), 4);
  line(U, 4, 21, 13, 12, glow(C.fire[2], 130), 1);
  spear(G, 3, 20, 21, 3, { head: C.fire, tassel: true });
  flame(F, 6, 20, 6, { c: C.fire, lean: -2 });
  chargeMark(F);
});
// 광전사 분쇄 일격: 내려찍은 도끼와 갈라지는 땅
def("AxeCrush", "5a2410", (U, G, F) => {
  burst(U, 12, 19, 7, C.fire, 7);
  axe(G, 19, 3, 10, 16, 1, { head: C.steel });
  crack(F, 9, 20, C.dark[0], 1);
  crack(F, 15, 21, C.dark[0], 1);
  line(F, 2, 22, 22, 22, C.stone[1], 1);
  chargeMark(F);
});
// 레인저 강궁: 바람을 두른 굵은 화살
def("BowPower", "1c5a56", (U, G, F) => {
  arc(U, 12, 12, 9, 200, 340, glow(hex("b8fff0"), 150), 2);
  arc(U, 12, 12, 9, 20, 160, glow(hex("b8fff0"), 150), 2);
  arrow(G, 2, 21, 21, 3, { head: C.ice, fletch: C.leaf });
  line(U, 2, 21, 21, 3, glow(WHITE, 90), 3);
  chargeMark(F);
});
// 저격수 저격: 조준선 가운데를 꿰뚫는 볼트
def("CrossSnipe", "3e1418", (U, G, F) => {
  ring(G, 12, 11, 7, 1, C.red[1]);
  line(G, 12, 2, 12, 6, C.red[1], 1);
  line(G, 12, 16, 12, 20, C.red[1], 1);
  line(G, 3, 11, 7, 11, C.red[1], 1);
  line(G, 17, 11, 21, 11, C.red[1], 1);
  bolt(G, 3, 19, 20, 4);
  put(F, 12, 11, WHITE);
  chargeMark(F);
});
// 건슬링어 레일건: 총구에서 뻗는 전기 광선
def("GunRail", "15305e", (U, G, F) => {
  line(U, 9, 9, 23, 9, glow(hex("7ad8ff"), 190), 5);
  line(F, 9, 9, 23, 9, hex("e8faff"), 1);
  pistol(G, 1, 7, 1, C.iron);
  lightning(F, [[11, 6], [14, 8], [16, 5], [19, 7]], C.ice, 1);
  lightning(F, [[12, 12], [15, 10], [18, 13]], C.ice, 1);
  chargeMark(F);
});
// 원소술사 메테오: 꼬리를 끌며 떨어지는 불타는 운석
def("StaffMeteor", "4e1408", (U, G, F) => {
  line(U, 22, 2, 12, 12, glow(C.fire[1], 180), 6);
  line(U, 21, 2, 13, 10, glow(C.fire[2], 140), 2);
  disc(G, 10, 14, 5.5, C.stone[0]);
  disc(G, 10, 14, 4.5, C.fire[0]);
  disc(G, 9.5, 13.5, 3, C.fire[1]);
  disc(G, 9, 13, 1.5, C.fire[2]);
  star4(F, 4, 21, 2, C.fire[2]);
  chargeMark(F);
});
// 주술사 영혼 폭발: 영혼이 뭉친 보랏빛 구슬
def("WandSoul", "301250", (U, G, F) => {
  disc(U, 12, 12, 9, glow(C.arcane[1], 110));
  orb(G, 12, 12, 6, C.arcane);
  ghost(F, 6, 7, C.bone);
  spiral(F, 12, 12, 4, 1.2, C.arcane[2], 1);
  chargeMark(F);
});
// 사제 성광: 몸 둘레로 퍼지는 빛
def("PriestRadiance", "7a5a10", (U, G, F) => {
  disc(U, 12, 12, 10, glow(C.holy[1], 90));
  burst(G, 12, 12, 9, C.holy, 10);
  ring(F, 12, 12, 10, 1, C.holy[2]);
  plus(F, 12, 12, 3, 1, WHITE);
  chargeMark(F);
});

// ------------------------------------------------------------ 플레이어 상태이상(HUD 디버프 칸). 순서는 PlayerController 상태 비트 순서.
// 화상: 타오르는 불꽃
def("Status_Burn", "8a2a10", (U, G, F) => {
  disc(U, 12, 16, 7, glow(C.fire[1], 90));
  flame(G, 12, 21, 16, { lean: 1 });
  star4(F, 18, 6, 1.5, C.fire[2]);
});
// 출혈: 떨어지는 핏방울 셋
def("Status_Bleed", "5a1020", (U, G, F) => {
  drop(G, 12, 15, 4.2, C.red);
  drop(G, 5.5, 9, 2.2, C.red);
  drop(G, 18.5, 19, 2.2, C.red);
});
// 둔화: 모래시계
def("Status_Slow", "1e3a6a", (U, G, F) => {
  rect(G, 6, 3, 12, 2, C.wood[1]);
  rect(G, 6, 19, 12, 2, C.wood[1]);
  poly(G, [[7.5, 5], [16.5, 5], [12.5, 12], [11.5, 12]], C.ice[2]);
  poly(G, [[11.5, 12], [12.5, 12], [16.5, 19], [7.5, 19]], C.ice[2]);
  poly(G, [[9.5, 7], [14.5, 7], [12, 11]], C.gold[1]);
  poly(G, [[12, 15], [15.5, 19], [8.5, 19]], C.gold[1]);
  line(F, 12, 11, 12, 15, C.gold[2], 1);
});
// 약화: 아래로 꺾인 화살표와 금 간 검
def("Status_Weaken", "3e2052", (U, G, F) => {
  sword(G, 5, 19, 15, 5);
  crack(F, 10, 12, C.dark[0], 2);
  line(G, 18, 5, 18, 17, C.arcane[1], 2);
  arrowHead(G, 18, 19, 90, 4, C.arcane[1]);
});
// 기절: 머리 위를 도는 별과 소용돌이
def("Status_Stun", "5a4a10", (U, G, F) => {
  spiral(G, 12, 13, 8, 2.2, C.volt[1], 2);
  star4(F, 5, 6, 2.5, C.volt[2]);
  star4(F, 19, 5, 2, C.volt[2]);
  star4(F, 19, 19, 2, C.volt[2]);
});

// ------------------------------------------------------------ 합치기
function background(hue) {
  const base = hex(hue);
  const L = layer();
  for (let y = 0; y < S; y++)
    for (let x = 0; x < S; x++) {
      const corner = (x === 0 || x === S - 1) && (y === 0 || y === S - 1);
      if (corner) continue;
      const edge = x === 0 || y === 0 || x === S - 1 || y === S - 1;
      let c;
      if (edge) c = mix(base, BLACK, 0.7);
      else {
        const t = y / S;
        c = mix(base, BLACK, 0.25 + t * 0.4);
        const d = Math.hypot(x - 11.5, y - 10.5);
        if (d < 8) c = mix(c, base, 0.35);
        if (y === 1) c = mix(base, WHITE, 0.15);
      }
      put(L, x, y, c);
    }
  return L;
}
function over(dst, src) {
  for (let i = 0; i < S * S; i++) {
    const a = src[i * 4 + 3] / 255;
    if (a === 0) continue;
    for (let k = 0; k < 3; k++) dst[i * 4 + k] = dst[i * 4 + k] + (src[i * 4 + k] - dst[i * 4 + k]) * a;
    dst[i * 4 + 3] = Math.max(dst[i * 4 + 3], src[i * 4 + 3]);
  }
}
function outline(G) {
  const O = layer();
  for (let y = 0; y < S; y++)
    for (let x = 0; x < S; x++) {
      if (G[(y * S + x) * 4 + 3] > 0) continue;
      let near = false;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = x + dx, ny = y + dy;
        if (nx >= 0 && ny >= 0 && nx < S && ny < S && G[(ny * S + nx) * 4 + 3] > 0) near = true;
      }
      if (near) put(O, x, y, OUTLINE);
    }
  return O;
}
function render(name) {
  const img = background(BG[name]);
  const U = layer(), G = layer(), F = layer();
  DRAW[name](U, G, F);
  over(img, U);
  over(img, outline(G));
  over(img, G);
  over(img, F);
  return img;
}

// ------------------------------------------------------------ PNG
const crcTable = new Uint32Array(256).map((_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});
function crc32(buf) {
  let c = 0xffffffff;
  for (const b of buf) c = crcTable[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}
function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const td = Buffer.concat([Buffer.from(type), data]);
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(td));
  return Buffer.concat([len, td, crc]);
}
function png(w, h, rgba) {
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) {
    raw[y * (w * 4 + 1)] = 0;
    Buffer.from(rgba.buffer, rgba.byteOffset + y * w * 4, w * 4).copy(raw, y * (w * 4 + 1) + 1);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk("IHDR", ihdr),
    chunk("IDAT", zlib.deflateSync(raw)), chunk("IEND", Buffer.alloc(0))]);
}

// ------------------------------------------------------------ 실행
fs.mkdirSync(outDir, { recursive: true });
fs.mkdirSync(statusDir, { recursive: true });
const names = Object.keys(DRAW);
const images = names.map(render);
names.forEach((n, i) => {
  let dir = outDir;
  if (n.startsWith("Status_")) {
    dir = statusDir;
  }
  fs.writeFileSync(path.join(dir, n + ".png"), png(S, S, images[i]));
});
console.log("wrote " + names.length + " icons to " + outDir);

// 미리보기: 6개씩 한 줄(계열 직업 순서), 4배, 사이 4픽셀
const preview = process.argv[2];
if (preview) {
  const scale = 4, cols = 6, gap = 4;
  const rows = Math.ceil(names.length / cols);
  const W = cols * (S * scale + gap) + gap, H = rows * (S * scale + gap) + gap;
  const sheet = new Uint8ClampedArray(W * H * 4);
  for (let i = 0; i < W * H; i++) { sheet[i * 4] = 40; sheet[i * 4 + 1] = 40; sheet[i * 4 + 2] = 46; sheet[i * 4 + 3] = 255; }
  images.forEach((img, k) => {
    const ox = gap + (k % cols) * (S * scale + gap), oy = gap + Math.floor(k / cols) * (S * scale + gap);
    for (let y = 0; y < S * scale; y++)
      for (let x = 0; x < S * scale; x++) {
        const si = (Math.floor(y / scale) * S + Math.floor(x / scale)) * 4;
        const a = img[si + 3] / 255;
        const di = ((oy + y) * W + ox + x) * 4;
        for (let c = 0; c < 3; c++) sheet[di + c] = sheet[di + c] + (img[si + c] - sheet[di + c]) * a;
      }
  });
  fs.writeFileSync(preview, png(W, H, sheet));
  console.log("preview " + preview);
}
