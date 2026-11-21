// 폭발 화살(BowBlast) 투사체용 프로젝트 전용 이펙트: 촉에 화약 주머니를 단 화살이 오른쪽으로 날아간다.
// 둥근 화약 주머니의 도화선이 지글지글 타며 불티를 튀기고, 뒤로 가는 연기 줄이 남는다.
// 화살촉 끝이 (0, 0)이고, export.mjs가 "local/ember-arrow"로 읽는다. 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, rng, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Ember arrow",
  desc: "An arrow flying right with a round powder charge strapped behind its head: the fuse fizzes and spits sparks and a thin trail of smoke curls away behind.",
  tags: ["projectile", "arrow", "fire", "bomb", "directional", "loop"],
  size: [72, 28],
  origin: [62, 14],
  duration: 0.4,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#1a1210", "#3a2a22", "#6e5040", "#a87a50"], fire: ["#7a1f12", "#c9281f", "#ffae35", "#fff8d6"], smoke: ["#3a3640", "#5a5662", "#7e7a88"], length: 30, seed: 7 },
};

export function draw(p, t, o) {
  const W = fit(o.palette, 4);
  const F = fit(o.fire, 4);
  const K = fit(o.smoke, 3);
  const L = o.length;
  const ph = t / meta.duration;

  // 1) 연기 줄: 뒤로 흘러가며 부푼다
  for (let i = 0; i < 7; i++) {
    const k = (ph + i / 7) % 1;
    const x = -14 - k * 34;
    const y = -4 + Math.sin(k * 5 + i) * 2 - k * 3;
    p.circle(x, y, 1 + k * 2.5, K[i % 3], 1 - k);
  }

  // 2) 화살대·깃·촉
  p.line(-L, 0, -6, 0, W[3]);
  p.line(-L, 1, -6, 1, W[2]);
  p.tri(-L - 2, 0, -L + 5, 0, -L - 1, -3, F[1]);
  p.tri(-L - 2, 1, -L + 5, 1, -L - 1, 4, F[1]);
  p.tri(-6, -3, -6, 4, 0, 0, W[1]);
  p.tri(-5, -2, -5, 2, -1, 0, WHITE);

  // 3) 화약 주머니: 둥근 검은 주머니에 끈
  p.circle(-12, 0, 4.5, W[0]);
  p.circle(-12, 0, 3.5, W[1]);
  p.px(-13, -2, W[2]);
  p.line(-12, -4, -12, 4, W[3]);

  // 4) 도화선 불꽃: 위로 뻗은 심지 끝에서 깜박이며 불티가 튄다
  p.line(-13, -4, -15, -7, W[2]);
  const flick = Math.floor(ph * 8) % 2;
  p.circle(-15, -8, 1.5 + flick, F[2]);
  p.px(-15, -8, F[3]);
  for (let i = 0; i < 5; i++) {
    const g = rng(o.seed, i);
    const k = (ph * 2 + i / 5) % 1;
    const a = g.range(-2.6, -0.6);
    p.px(-15 + Math.cos(a) * k * 9 - k * 6, -8 + Math.sin(a) * k * 7, F[1 + (i % 3)]);
  }
  p.glow(-15, -8, 8, F[2], 0.6);
}
