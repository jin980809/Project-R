// 영혼 폭발(WandSoul) 투사체용 프로젝트 전용 이펙트: 보랏빛 혼불 유령이 오른쪽으로 날아간다.
// 둥근 머리에 눈 두 개, 뒤로 물결치며 갈라지는 꼬리, 꼬리 끝에서 흩어지는 넋 조각. 늘 같은 자리에 있어 잘라도 빠지지 않는다.
// 머리 가운데가 (0, 0)이고, export.mjs가 "local/soul-wisp"로 읽는다. 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, rng, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Soul wisp",
  desc: "A violet ghost-flame flying right: a round glowing head with two hollow eyes, a forked tail rippling behind it and soul flecks peeling off the tail tips.",
  tags: ["projectile", "soul", "ghost", "spirit", "directional", "loop"],
  size: [56, 32],
  origin: [44, 16],
  duration: 0.6,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#120818", "#2e1250", "#5a2496", "#8a44d0", "#c49cff", "#ecdcff"], seed: 4 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 6);
  const ph = t / meta.duration;

  // 흩어지는 넋 조각
  for (let i = 0; i < 6; i++) {
    const g = rng(o.seed, i);
    const k = (ph + i / 6) % 1;
    p.px(-18 - k * 18, g.range(-5, 5) + Math.sin((k + i) * TAU) * 2, C[3 + (i % 3)], 1 - k);
  }
  // 갈라진 꼬리 두 가닥: 뒤로 갈수록 가늘고 물결친다
  for (let j = 0; j < 2; j++) {
    const side = j * 2 - 1;
    for (let x = -22; x <= -2; x++) {
      const u = (-x) / 22;
      const y = side * u * 4 + Math.sin(x * 0.45 + ph * TAU * 2 + j * 1.7) * u * 3;
      const r = 3.6 * (1 - u) + 0.6;
      let c = C[3];
      if (u < 0.45) {
        c = C[4];
      }
      p.circle(x, y, r, c, 1 - u * 0.5);
    }
  }
  // 머리: 짙은 테두리, 밝은 얼굴, 위쪽 반짝임
  p.circle(0, 0, 7, C[1]);
  p.circle(0, 0, 6, C[4]);
  p.circle(1, -1, 4, C[5]);
  // 눈 두 개(퀭하게 빈 눈), 깜빡이듯 크기가 바뀐다
  let eyeH = 2;
  if (Math.floor(ph * 6) % 6 === 0) {
    eyeH = 1;
  }
  p.rect(1, -2, 2, eyeH, C[0]);
  p.rect(4, -2, 2, eyeH, C[0]);
  p.px(2, 2, C[2]);
  p.px(3, 2, C[2]);
  p.px(-1, -4, WHITE);
  p.glow(0, 0, 14, C[3], 0.45);
}
