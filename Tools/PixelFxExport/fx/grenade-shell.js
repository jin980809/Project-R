// 수류탄(GunGrenade) 투사체용 프로젝트 전용 이펙트: 날아가는 둥근 수류탄. 국방색 몸통에 쇠 띠와 손잡이,
// 꼭지의 도화선이 지글지글 타며 불티를 튀긴다. 게임은 이 그림을 proj_spin으로 돌려 굴러가게 보인다.
// 가운데(0, 0)가 수류탄 가운데이고, export.mjs가 "local/grenade-shell"로 읽는다. 이펙트 규칙(Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, rng, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Grenade shell",
  desc: "A round grenade in flight: an olive body with a steel band and lever, its fuse fizzing at the cap and spitting sparks.",
  tags: ["projectile", "grenade", "bomb", "loop"],
  size: [32, 32],
  origin: [16, 16],
  duration: 0.3,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#141a0e", "#2e3a1c", "#4e6230", "#7a9046", "#a8b86e"], metal: ["#3a4250", "#8a96a8", "#d8e0ee"], fire: ["#c9281f", "#ffae35", "#fff8d6"], seed: 3 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 5);
  const M = fit(o.metal, 3);
  const F = fit(o.fire, 3);
  const ph = t / meta.duration;

  // 몸통: 짙은 테두리, 아래쪽 그림자, 왼쪽 위 반짝임
  p.circle(0, 1, 7, C[0]);
  p.circle(0, 1, 6, C[2]);
  p.circle(1, 2, 4, C[1]);
  p.circle(-2, -1, 3, C[3]);
  p.px(-3, -2, C[4]);
  // 쇠 띠와 손잡이
  p.line(-6, 1, 6, 1, M[1]);
  p.rect(-2, -8, 4, 3, M[0]);
  p.line(2, -7, 5, -3, M[2]);
  // 도화선 불꽃
  const flick = Math.floor(ph * 6) % 2;
  p.circle(0, -10, 1 + flick, F[1]);
  p.px(0, -10, F[2]);
  for (let i = 0; i < 4; i++) {
    const g = rng(o.seed, i);
    const k = (ph + i / 4) % 1;
    const a = g.range(-2.8, -0.3);
    p.px(Math.cos(a) * k * 7, -10 + Math.sin(a) * k * 6, F[i % 3]);
  }
  p.glow(0, -10, 6, F[1], 0.5);
  p.px(0, -11, WHITE);
}
