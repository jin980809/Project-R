// 처형(AxeExecute) 돌진 길용 프로젝트 전용 이펙트: 출발 자리(0, 0)에서 오른쪽으로 length 픽셀.
// 붉은 잔상 갈매기 무늬(>)가 차례로 앞으로 쏘아지며 짙은 핏빛 띠를 남기고, 핏방울이 튀어 바닥에 떨어진다.
// 게임은 이 그림을 돌진 거리만큼 깐다(SkillVfx.trail). export.mjs가 "local/blood-rush"로 읽는다.
// 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다.
import { fit, rng, seg, ease } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Blood rush",
  desc: "A brutal lunge: crimson chevron after-images shoot forward one after another over a dark blood-red streak, droplets fly off and spatter on the floor.",
  tags: ["melee", "dash", "trail", "blood", "directional", "one-shot"],
  size: [120, 48],
  origin: [12, 24],
  duration: 0.6,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#1a0406", "#4a0a10", "#86141c", "#c82a2a", "#ff6a52", "#ffd0b8"], length: 80, seed: 9 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 6);
  const L = o.length;
  const S = o.seed;
  const head = L * ease.outCubic(seg(t, 0, 0.22));
  const fade = 1 - seg(t, 0.3, 0.6);

  // 1) 핏빛 띠: 머리 쪽이 굵고 밝다
  if (fade > 0 && head > 1) {
    p.shade(0, -7, head, 7, (x, y, out) => {
      out.a = fade;
      const k = x / Math.max(1, head);
      const w = 1 + 5 * k;
      const d = Math.abs(y) / w;
      if (d > 1) {
        return;
      }
      if (d < 0.3 && k > 0.6) {
        return C[4];
      }
      if (d < 0.6) {
        return C[3];
      }
      return C[1];
    });
  }

  // 2) 잔상 갈매기 무늬: 네 개가 시간차로 앞으로 달린다
  for (let i = 0; i < 4; i++) {
    const born = i * 0.04;
    const k = seg(t, born, born + 0.26);
    if (k > 0 && k < 1) {
      const x = L * ease.outCubic(k) - 4;
      const s = 7 - i;
      const a = 1 - seg(k, 0.6, 1);
      let c = C[4];
      if (i > 1) {
        c = C[3];
      }
      p.line(x - s, -s, x, 0, c, a);
      p.line(x - s, s, x, 0, c, a);
      p.line(x - s - 1, -s, x - 1, 0, C[2], a);
      p.line(x - s - 1, s, x - 1, 0, C[2], a);
    }
  }

  // 3) 핏방울: 위아래로 튀었다가 떨어져 바닥에 번진다
  for (let i = 0; i < 14; i++) {
    const g = rng(S, i, 3);
    const x0 = g.range(8, L);
    const born = 0.22 * (x0 / L);
    const age = t - born;
    const life = g.range(0.22, 0.36);
    if (age > 0) {
      const side = i % 2 === 0 ? -1 : 1;
      if (age < life) {
        const k = age / life;
        p.circle(x0 + k * 6, side * (3 + k * g.range(6, 12)) - Math.sin(k * Math.PI) * 6, 1.2, C[3]);
      } else if (fade > 0) {
        p.ellipse(x0 + 6, side * g.range(9, 15), 2, 1, C[2], fade);
      }
    }
  }
}
