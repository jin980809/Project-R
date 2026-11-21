// 용창 투척(SpearDragon) 투사체용 프로젝트 전용 이펙트: 불의 용이 감아 도는 창이 오른쪽으로 날아간다.
// 붉게 달군 창촉과 검은 창대를 따라 불꽃 몸통이 S자로 꿈틀거리며 감기고, 창촉 위에 뿔 달린 용 머리가 입을 벌리며,
// 뒤로는 불꽃 꼬리와 불티가 흩날린다. 창촉 끝이 (0, 0)이고, export.mjs가 "local/dragon-lance"로 읽는다.
// 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, rng, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Dragon lance",
  desc: "A spear flying right with a serpent of fire coiling along it: a red-hot head, a horned dragon's head of flame gaping over the tip, a writhing S-shaped body and a tail of flame and embers.",
  tags: ["projectile", "spear", "fire", "dragon", "directional", "loop"],
  size: [104, 40],
  origin: [92, 20],
  duration: 0.5,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#2a0a06", "#5a1408", "#9a2a0c", "#d8501a", "#ff8a2a", "#ffc24a", "#fff0a0"], length: 52, seed: 3 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 7);
  const L = o.length;
  const ph = t / meta.duration;

  // 1) 불티: 뒤로 흩날린다
  for (let i = 0; i < 10; i++) {
    const g = rng(o.seed, i);
    const k = (ph + i / 10) % 1;
    p.dot(-L * 0.6 - k * 34, g.range(-9, 9) + Math.sin(k * 6 + i) * 2, C[4 + (i % 3)], 1 - k);
  }

  // 2) 창대와 창촉
  p.line(-L, 0, -9, 0, C[0]);
  p.line(-L, 1, -9, 1, C[1]);
  p.tri(-10, -3, -10, 4, 0, 0, C[1]);
  p.tri(-9, -2, -9, 2, -1, 0, C[5]);
  p.px(-5, 0, WHITE);

  // 3) 불꽃 몸통: 창을 따라 S자로 꿈틀대며 뒤로 갈수록 가늘다
  const pts = [];
  for (let i = 0; i <= 24; i++) {
    const k = i / 24;
    const x = -6 - k * (L + 6);
    const y = Math.sin(k * 7 - ph * TAU) * (3 + 5 * k) - 3;
    pts.push([x, y, 4.5 - 3.5 * k]);
  }
  for (let i = pts.length - 1; i >= 0; i--) {
    const [x, y, r] = pts[i];
    p.circle(x, y, r + 1, C[2]);
  }
  for (let i = pts.length - 1; i >= 0; i--) {
    const [x, y, r] = pts[i];
    let c = C[4];
    if (i < 8) {
      c = C[5];
    }
    p.circle(x, y - 0.5, r, c);
    if (r > 2.2) {
      p.px(x, y - r + 1, C[6]);
    }
  }

  // 4) 용 머리: 창촉 위, 뿔 두 개와 벌린 입, 눈
  const hx = -6;
  const hy = pts[0][1] - 1;
  p.circle(hx, hy, 5, C[2]);
  p.circle(hx, hy, 4, C[5]);
  p.line(hx - 3, hy - 4, hx - 7, hy - 8, C[3]);
  p.line(hx - 1, hy - 4, hx - 3, hy - 9, C[3]);
  const jaw = 1 + Math.sin(ph * TAU * 2) * 1.2;
  p.tri(hx + 2, hy - 1, hx + 9, hy - 2, hx + 3, hy + 1, C[4]);
  p.tri(hx + 2, hy + 1, hx + 8, hy + 2 + jaw, hx + 3, hy + 3, C[3]);
  p.px(hx + 1, hy - 2, WHITE);
  p.glow(hx, hy, 16, C[4], 0.55);
}
