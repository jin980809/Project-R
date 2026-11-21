// 파쇄 돌진(BluntRam) 돌진 길용 프로젝트 전용 이펙트: 출발 자리(0, 0)에서 오른쪽으로 length 픽셀.
// 앞으로 달리는 머리가 바닥을 갈아엎어 깊은 고랑을 파고, 고랑 양옆으로 흙덩이가 튀어 올랐다 떨어지며
// 흙먼지가 피어오르고, 고랑 가장자리에 금이 간다. 고랑은 남았다가 흐려진다.
// 게임은 이 그림을 돌진 거리만큼 깐다(SkillVfx.trail). export.mjs가 "local/ground-furrow"로 읽는다.
// 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다.
import { fit, rng, seg, ease } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Ground furrow",
  desc: "A charge ploughs the floor: a churning head tears a deep furrow, clods of earth fly up on both sides and thud down, dust billows along it and cracks run off its edges before it fades.",
  tags: ["earth", "dash", "trail", "rock", "directional", "one-shot"],
  size: [168, 64],
  origin: [12, 32],
  duration: 0.75,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#1a120c", "#3a2618", "#5e3e26", "#86603c", "#b08a5c", "#d8bc8c"], length: 128, seed: 3 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 6);
  const L = o.length;
  const S = o.seed;
  const head = L * ease.outCubic(seg(t, 0, 0.32));
  const fade = 1 - seg(t, 0.45, 0.75);

  // 1) 고랑: 머리 뒤로 남는 어두운 홈(가운데가 깊고, 위쪽 가장자리는 밝게 깎인 면)
  if (head > 1 && fade > 0) {
    p.shade(0, -8, head, 8, (x, y, out) => {
      out.a = fade;
      const w = 2.5 + 3 * Math.sin(Math.min(1, x / 20) * Math.PI / 2) + Math.sin(x * 0.7) * 0.6;
      const d = Math.abs(y) / w;
      if (d > 1) {
        return;
      }
      if (y < 0 && d > 0.7) {
        return C[3];
      }
      if (d < 0.45) {
        return C[0];
      }
      return C[1];
    });
    // 갈라진 금: 고랑 가장자리에서 비스듬히 뻗는다
    for (let i = 0; i < 7; i++) {
      const g = rng(S, i, 4);
      const x = g.range(10, L - 6);
      if (x < head) {
        const side = i % 2 === 0 ? -1 : 1;
        let px = x;
        let py = side * 5;
        for (let j = 0; j < 3; j++) {
          const nx = px + g.range(-3, 5);
          const ny = py + side * g.range(2, 4);
          p.line(px, py, nx, ny, C[0], fade);
          px = nx;
          py = ny;
        }
      }
    }
  }

  // 2) 갈아엎는 머리: 흙이 둥글게 솟구친다
  if (t < 0.4) {
    const k = 1 - seg(t, 0.3, 0.4);
    p.circle(head - 2, -2, 6, C[3], k);
    p.circle(head, -4, 4, C[4], k);
    p.px(head + 2, -6, C[5]);
  }

  // 3) 튀는 흙덩이: 지나간 자리에서 양옆으로 포물선을 그리며 떨어진다(y는 위로 뜬 높이 + 옆으로 밀린 거리)
  for (let i = 0; i < 26; i++) {
    const g = rng(S, i, 7);
    const x0 = g.range(4, L);
    const born = 0.32 * (x0 / L);
    const age = t - born;
    const life = g.range(0.3, 0.5);
    if (age > 0 && age < life) {
      const k = age / life;
      const side = i % 2 === 0 ? -1 : 1;
      const out = side * (6 + k * g.range(10, 18));
      const up = Math.sin(k * Math.PI) * g.range(10, 20);
      const x = x0 + k * g.range(2, 8);
      const y = out * 0.6 - up;
      const r = g.range(1, 2.2);
      p.circle(x, y, r, C[2 + (i % 3)]);
      // 바닥 그림자
      p.px(x, out * 0.6 + 2, C[0]);
    }
  }

  // 4) 흙먼지: 고랑을 따라 피어오르며 흐려진다
  for (let i = 0; i < 16; i++) {
    const g = rng(S, i, 9);
    const x0 = g.range(0, L);
    const born = 0.32 * (x0 / L) + 0.04;
    const k = seg(t, born, born + 0.45);
    if (k > 0 && k < 1) {
      const r = 3 + k * 6;
      p.disc(x0 + k * 4, -k * 8 + g.range(-6, 6), r, C[4], (1 - k) * 0.5);
    }
  }
}
