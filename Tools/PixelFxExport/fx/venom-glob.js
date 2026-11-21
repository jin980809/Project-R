// 독 폭탄(WandVenom) 투사체용 프로젝트 전용 이펙트: 출렁이는 독 덩어리가 오른쪽으로 날아간다.
// 짙은 테두리 안에서 연두색 독이 출렁이고 거품이 비치며, 뒤로 독 방울이 떨어져 나간다.
// 덩어리 앞 끝이 (0, 0) 근처이고, export.mjs가 "local/venom-glob"으로 읽는다. 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, rng, TAU } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Venom glob",
  desc: "A wobbling glob of poison flying right: green venom sloshing inside a dark skin with bubbles showing through, droplets peeling off behind.",
  tags: ["projectile", "poison", "nature", "directional", "loop"],
  size: [48, 28],
  origin: [40, 14],
  duration: 0.4,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#0c1408", "#1e3a10", "#3e6a18", "#6cae22", "#a6e03a", "#e4ff86"], seed: 6 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 6);
  const ph = t / meta.duration;

  // 떨어져 나가는 독 방울
  for (let i = 0; i < 5; i++) {
    const g = rng(o.seed, i);
    const k = (ph + i / 5) % 1;
    p.circle(-10 - k * 20, g.range(-3, 3) + k * 3, 1.5 - k, C[3], 1 - k);
  }
  // 몸통: 앞이 둥글고 뒤로 늘어진 물방울, 출렁여 모양이 바뀐다
  const wob = Math.sin(ph * TAU * 2);
  p.shade(-16, -8, 2, 8, (x, y) => {
    const rx = 6 + wob * 0.6;
    const ry = 5.5 - wob * 0.6;
    const cx = -6;
    let d = Math.hypot((x - cx) / rx, y / ry);
    if (x < cx) {
      d = Math.hypot((x - cx) / (rx + 5), y / (ry * (1 - (cx - x) / 14)));
    }
    if (d > 1) {
      return;
    }
    if (d > 0.8) {
      return C[1];
    }
    if (y < -1.5 && x > cx - 2) {
      return C[4];
    }
    return C[3];
  });
  // 안에 비치는 거품과 반짝임
  const b = (ph * 3) % 1;
  p.px(-8 + b * 2, 2 - b * 4, C[5]);
  p.px(-4, -3, C[5]);
  p.px(-11, 1, C[2]);
  p.glow(-6, 0, 10, C[3], 0.35);
}
