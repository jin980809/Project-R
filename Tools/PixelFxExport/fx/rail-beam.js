// 레일건(GunRail)용 프로젝트 전용 이펙트: 총구(왼쪽 끝 0, 0)에서 오른쪽으로 뻗는 레이저 빛줄기.
// 0~0.04초 가는 조준선이 끝까지 그어지고, 0.04초에 굵은 빛줄기가 한 번에 뻗는다(흰 심 + 하늘색 띠 + 바깥 빛).
// 빛줄기를 감아 도는 전기 나선과 앞으로 달리는 빛 덩이가 보이고, 0.16초부터 가늘어지며 점점이 부서져 사라진다.
// 총구에는 섬광 고리, 끝에는 불꽃이 튄다. 게임은 이 그림을 투사체가 날아갈 거리만큼 깐다(SkillVfx.trail, projectileHidden).
// export.mjs가 "local/rail-beam"으로 읽는다. 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다.
import { fit, rng, seg, ease, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Rail beam",
  desc: "A railgun beam: a thin aiming line snaps out, then a thick white-cored cyan laser fires its full length with electric coils spiralling round it and pulses racing down it; it thins, flickers and breaks into motes.",
  tags: ["scifi", "laser", "beam", "lightning", "directional", "one-shot"],
  size: [600, 48],
  origin: [8, 24],
  duration: 0.55,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#06142a", "#0c2a5c", "#14489a", "#2478d8", "#4ab4ff", "#8ad8ff", "#d0f4ff", "#ffffff"], length: 576, seed: 4 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 8);
  const L = o.length;
  const S = o.seed;

  // 1) 조준선: 짧은 순간 가는 선이 끝까지 그어진다
  if (t < 0.06) {
    const reach = L * ease.outQuad(seg(t, 0, 0.04));
    p.line(0, 0, reach, 0, C[5]);
    p.circle(reach, 0, 1.5, WHITE);
  }

  // 2) 굵은 빛줄기: 뻗은 뒤 굵기가 줄며 사라진다
  const on = seg(t, 0.04, 0.06);
  const off = seg(t, 0.16, 0.42);
  const width = on * (1 - ease.inQuad(off));
  if (width > 0.02) {
    const half = 7 * width;
    // 빛줄기 몸: 가운데가 흰색, 바깥으로 하늘색 띠(가장자리는 지글지글 흔들린다)
    p.shade(0, -10, L, 10, (x, y) => {
      const wobble = Math.sin(x * 0.35 + t * 80) * 0.6;
      const d = Math.abs(y) / Math.max(0.5, half + wobble);
      if (d > 1) {
        return;
      }
      // 끝에서 몇 칸은 둥글게 줄인다
      const tip = Math.min(1, (L - x) / 6);
      if (d > tip) {
        return;
      }
      if (d < 0.3) {
        return WHITE;
      }
      if (d < 0.55) {
        return C[6];
      }
      if (d < 0.8) {
        return C[5];
      }
      return C[3];
    });
    p.glowLine(0, 0, L, 0, 10 + 8 * width, C[4], 0.45 * width);

    // 3) 전기 나선: 두 가닥이 빛줄기를 감아 돈다(앞쪽이 위로 넘어갈 때만 밝게)
    for (let j = 0; j < 2; j++) {
      for (let x = 2; x < L; x += 1) {
        const ph = x * 0.16 - t * 60 + j * Math.PI;
        const y = Math.sin(ph) * (half + 3);
        const front = Math.cos(ph) > 0;
        if (front || x % 2 === 0) {
          let c = C[4];
          if (front) {
            c = C[6];
          }
          p.dot(x, y, c, width);
        }
      }
    }

    // 4) 앞으로 달리는 빛 덩이
    for (let i = 0; i < 4; i++) {
      const x = ((t * 1800 + i * L / 4) % L);
      p.circle(x, 0, half * 0.9 + 1, WHITE, width);
    }

    // 5) 총구 섬광 고리와 끝의 불꽃
    p.circle(0, 0, 4 + 6 * width, WHITE, width);
    p.ring(0, 0, 6 + 10 * (1 - width), 1, C[5], width);
    p.glow(0, 0, 22, C[5], 0.6 * width);
    for (let i = 0; i < 8; i++) {
      const g = rng(S, i, Math.floor(t * 30));
      const a = g.range(-1.2, 1.2) + Math.PI;
      const d = g.range(3, 14);
      p.line(L, 0, L + Math.cos(a) * d * -1, Math.sin(a) * d, C[5 + (i % 3)], width);
    }
  }

  // 6) 부서지는 빛 점: 빛줄기 자리에서 위아래로 흩어지며 사라진다
  if (t > 0.18) {
    const k = seg(t, 0.18, 0.55);
    for (let i = 0; i < 60; i++) {
      const g = rng(S, i, 9);
      const x = g.range(4, L - 4);
      const dir = g.range(-1, 1);
      const y = dir * (2 + k * 10) + Math.sin(x * 0.1 + k * 6) * 1.5;
      if (g.next() > k * 0.9) {
        p.dot(x, y, C[4 + (i % 3)], 1 - k);
      }
    }
  }
}
