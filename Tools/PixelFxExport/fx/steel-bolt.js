// 연발 볼트(CrossBurst) 투사체용 프로젝트 전용 이펙트: 짧고 굵은 석궁 볼트가 오른쪽으로 날아간다.
// 네모난 강철 촉, 짧은 대, 위아래 두 장의 날개 깃. 뒤로 짧은 속도 줄 세 가닥이 흐른다.
// 촉 끝이 (0, 0)이고, export.mjs가 "local/steel-bolt"로 읽는다. 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Steel bolt",
  desc: "A short, stubby crossbow quarrel flying right: a squared steel head, a short shaft, two stiff vanes and three quick speed lines behind it.",
  tags: ["projectile", "bolt", "crossbow", "directional", "loop"],
  size: [44, 20],
  origin: [36, 10],
  duration: 0.3,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#141820", "#3a4250", "#6e7a90", "#a8b4c8", "#d8e0ee"], wood: ["#4a2e1a", "#7a5030"], seed: 4 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 5);
  const Wd = fit(o.wood, 2);
  const ph = t / meta.duration;

  // 속도 줄: 셋이 차례로 흐른다
  for (let i = 0; i < 3; i++) {
    const k = (ph + i / 3) % 1;
    const x = -18 - k * 12;
    const y = (i - 1) * 3;
    p.line(x, y, x + 5, y, C[2], 1 - k);
  }
  // 대
  p.line(-16, 0, -5, 0, Wd[1]);
  p.line(-16, 1, -5, 1, Wd[0]);
  // 날개 깃: 뻣뻣한 두 장
  p.tri(-17, 0, -11, 0, -17, -4, C[3]);
  p.tri(-17, 1, -11, 1, -17, 5, C[2]);
  // 네모난 촉
  p.rect(-6, -2, 4, 5, C[1]);
  p.tri(-2, -2, -2, 3, 1, 0.5, C[2]);
  p.px(-5, -1, C[4]);
  p.px(-1, 0, WHITE);
}
