// 강궁(BowPower) 투사체용 프로젝트 전용 이펙트: 바람을 두른 굵은 화살이 오른쪽으로 날아간다.
// 연둣빛 바람 띠 두 가닥이 화살대를 나선으로 감아 돌고, 화살촉 앞에 공기 고리가 깨지며, 뒤로 바람 줄기가 흩어진다.
// 화살촉 끝이 (0, 0)이고, export.mjs가 "local/gale-arrow"로 읽는다. 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, rng, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Gale arrow",
  desc: "A heavy arrow flying right wrapped in two spiralling bands of green wind, an air ring cracking off its head and wind streaks peeling away behind.",
  tags: ["projectile", "arrow", "wind", "directional", "loop"],
  size: [80, 28],
  origin: [70, 14],
  duration: 0.4,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#06201c", "#16685a", "#28a07e", "#4ad8a8", "#9cf4d4", "#e0fff4"], length: 34, seed: 5 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 6);
  const L = o.length;
  const ph = t / meta.duration;

  // 1) 바람 줄기: 뒤로 흩어지는 짧은 선(주기에 맞춰 이어진다)
  for (let i = 0; i < 6; i++) {
    const g = rng(o.seed, i);
    const k = (ph + i / 6) % 1;
    const x = -L - k * 26;
    const y = g.range(-6, 6);
    p.line(x, y, x + 6 - k * 3, y, C[3 + (i % 2)], 1 - k);
  }

  // 2) 화살대와 깃: 짙은 테두리 + 연한 나무색 대신 밝은 바람색 대
  p.line(-L, -1, -4, -1, C[0]);
  p.line(-L, 1, -4, 1, C[0]);
  p.line(-L, 0, -4, 0, C[5]);
  p.tri(-L - 2, 0, -L + 6, 0, -L - 1, -4, C[2]);
  p.tri(-L - 2, 0, -L + 6, 0, -L - 1, 4, C[2]);

  // 3) 화살촉: 넓은 강철 촉
  p.tri(-7, -4, -7, 4, 0, 0, C[0]);
  p.tri(-6, -3, -6, 3, -1, 0, WHITE);
  p.px(-4, 0, C[4]);

  // 4) 나선 바람 띠: 앞으로 흘러가며 위로 넘어가는 쪽만 밝다
  for (let j = 0; j < 2; j++) {
    for (let x = -L; x <= -2; x++) {
      const a = x * 0.32 - ph * TAU * 2 + j * Math.PI;
      const r = 4.5;
      const y = Math.sin(a) * r;
      const front = Math.cos(a) > 0;
      let c = C[2];
      if (front) {
        c = C[4];
      }
      if (front || ((x + j) & 1) === 0) {
        p.px(x, y, c);
      }
    }
  }

  // 5) 촉 앞 공기 고리
  const ring = (ph * 2) % 1;
  p.arc(1 + ring * 4, 0, 3 + ring * 4, 1, -1.2, 1.2, C[4], 1 - ring);
  p.glow(-2, 0, 10, C[3], 0.4);
}
