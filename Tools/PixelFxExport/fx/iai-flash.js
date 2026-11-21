// 발도 일섬(SwordIai) 돌진 길용 프로젝트 전용 이펙트: 출발 자리(0, 0)에서 오른쪽으로 length 픽셀.
// 0~0.07초 머리가 빛나는 가는 흰 선이 단숨에 끝까지 그어지고, 잠깐 머리카락처럼 가는 선으로 남았다가
// 0.16초부터 선을 따라 왼쪽부터 차례로 베인 자리가 X자로 터지며 불티가 튄다. 마지막에 선이 위아래로 갈라져 사라진다.
// 게임은 이 그림을 돌진 거리만큼 깐다(SkillVfx.trail). export.mjs가 "local/iai-flash"로 읽는다.
// 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다.
import { fit, rng, seg, ease, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Iai flash",
  desc: "An iaido draw-cut along a dash: a razor-thin white line streaks to the end in an instant, hangs, then the cuts burst open along it one after another as small X flashes with sparks, and the line splits apart and fades.",
  tags: ["melee", "sword", "slash", "dash", "trail", "directional", "one-shot"],
  size: [232, 56],
  origin: [12, 28],
  duration: 0.6,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#141a34", "#3e5ca8", "#6a92e0", "#a6c8ff", "#dcecff", "#ffffff"], sparks: ["#ffffff", "#dcecff", "#a6c8ff"], length: 192, cuts: 5, seed: 6 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 6);
  const L = o.length;
  const S = o.seed;
  const n = o.cuts;
  const SP = fit(o.sparks, 3);

  // 1) 그어지는 선: 머리가 앞으로 달리고, 지나간 자리는 가는 선으로 남는다
  const head = L * ease.outQuart(seg(t, 0, 0.07));
  const split = seg(t, 0.32, 0.6);
  if (split < 1) {
    const gap = split * 6;
    const fade = 1 - split;
    if (gap < 0.5) {
      p.line(0, 0, head, 0, WHITE, fade);
      p.line(0, 1, head, 1, C[3], fade);
    } else {
      // 위아래로 갈라지는 두 줄
      p.line(0, -gap, head, -gap, C[4], fade);
      p.line(0, gap, head, gap, C[3], fade);
    }
    // 바람 자국: 선 위아래의 짧은 줄
    if (t < 0.25) {
      for (let i = 0; i < 10; i++) {
        const g = rng(S, i, 2);
        const x = g.range(0, head);
        const y = g.range(-6, 6);
        p.line(x, y, x + g.range(8, 18), y, C[2], 1 - seg(t, 0.1, 0.25));
      }
    }
  }
  // 달리는 머리의 빛
  if (t < 0.1) {
    const k = 1 - seg(t, 0.06, 0.1);
    p.circle(head, 0, 2.5, WHITE, k);
    p.line(head - 10, 0, head + 6, 0, WHITE, k);
    p.glow(head, 0, 12, C[3], 0.6 * k);
  }

  // 2) 베인 자리가 차례로 터진다: 작은 X자 섬광 + 불티
  for (let i = 0; i < n; i++) {
    const x = L * (i + 0.6) / (n + 0.2);
    const born = 0.16 + i * 0.035;
    const k = seg(t, born, born + 0.22);
    if (k > 0 && k < 1) {
      const s = 4 + 9 * ease.outCubic(k);
      const fade = 1 - seg(k, 0.4, 1);
      p.line(x - s, -s * 0.8, x + s, s * 0.8, WHITE, fade);
      p.line(x - s, s * 0.8, x + s, -s * 0.8, C[4], fade);
      if (k < 0.3) {
        p.circle(x, 0, 3, WHITE);
      }
      p.glow(x, 0, 14, C[3], 0.45 * fade);
      for (let j = 0; j < 6; j++) {
        const g = rng(S, i, j);
        const a = g.range(-Math.PI, Math.PI);
        const d = 3 + k * g.range(10, 20);
        const sx = x + Math.cos(a) * d;
        const sy = Math.sin(a) * d * 0.7;
        p.line(sx, sy, sx - Math.cos(a) * 2, sy - Math.sin(a) * 2, SP[j % 3], fade);
      }
    }
  }
}
