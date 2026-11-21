// 산탄(GunShotgun) 투사체용 프로젝트 전용 이펙트: 달군 산탄 알 세 개가 짧은 주황 줄을 끌며 오른쪽으로 날아간다.
// 알마다 흰 심과 주황 테가 깜박이고 뒤 줄이 붉게 식는다. 맨 앞 알 끝이 (0, 0)이고,
// export.mjs가 "local/buckshot"으로 읽는다. 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Buckshot",
  desc: "A cluster of three hot pellets flying right, each a white core in an orange rim trailing a short streak that cools to red.",
  tags: ["projectile", "bullet", "shotgun", "directional", "loop"],
  size: [36, 20],
  origin: [30, 10],
  duration: 0.2,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#5a1408", "#c8401a", "#ff9a3a", "#ffe08a"], seed: 9 },
};

const PELLETS = [[0, 0], [-5, -3], [-4, 3]];

export function draw(p, t, o) {
  const C = fit(o.palette, 4);
  const ph = t / meta.duration;
  for (let i = 0; i < PELLETS.length; i++) {
    const [x, y] = PELLETS[i];
    const len = 9 + ((i + Math.floor(ph * 4)) % 2) * 2;
    p.line(x - len, y, x - 5, y, C[0]);
    p.line(x - 6, y, x - 2, y, C[1]);
    p.circle(x - 1, y, 1.6, C[2]);
    p.px(x - 1, y, WHITE);
    p.px(x, y, C[3]);
  }
  p.glow(-2, 0, 7, C[2], 0.35);
}
