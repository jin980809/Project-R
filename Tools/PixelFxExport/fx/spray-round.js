// 난사(GunSpray) 투사체용 프로젝트 전용 이펙트: 달궈진 주황 탄두가 오른쪽으로 날아간다.
// 하얗게 달아오른 탄두 앞머리, 뒤로 길게 식어 가는 주황·빨강 꼬리, 꼬리 끝에서 흩어지는 짧은 연기 점.
// 탄두 끝이 (0, 0)이고, export.mjs가 "local/spray-round"로 읽는다. 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, rng, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Spray round",
  desc: "A red-hot slug flying right: a white-hot nose, a long tail cooling from orange to red behind it, and short smoke flecks drifting off the tail end.",
  tags: ["projectile", "bullet", "gun", "fire", "directional", "loop"],
  size: [40, 12],
  origin: [34, 6],
  duration: 0.2,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#3a1008", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#fff3a6"], smoke: "#5a5060", length: 18, seed: 9 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 7);
  const L = o.length;
  const ph = t / meta.duration;

  // 꼬리 끝 연기 점
  for (let i = 0; i < 3; i++) {
    const g = rng(o.seed, i);
    const k = (ph + i / 3) % 1;
    p.px(-L - 2 - k * 8, g.range(-1.5, 1.5), o.smoke, 1 - k);
  }
  // 식어 가는 꼬리: 뒤로 갈수록 어둡고 가늘다
  for (let x = -L; x <= -3; x++) {
    const u = (-x - 3) / (L - 3);
    let c = C[4];
    if (u > 0.3) {
      c = C[3];
    }
    if (u > 0.6) {
      c = C[2];
    }
    p.px(x, 0, c, 1 - u * 0.4);
    if (u < 0.45) {
      p.px(x, -1, C[3], 0.8);
      p.px(x, 1, C[2], 0.8);
    }
  }
  // 탄두: 짙은 테두리에 하얗게 달아오른 앞머리
  p.rect(-4, -2, 4, 4, C[1]);
  p.rect(-3, -1, 3, 2, C[5]);
  p.px(-1, 0, WHITE);
  p.px(0, 0, C[6]);
  p.glow(-2, 0, 6, C[4], 0.45);
}
