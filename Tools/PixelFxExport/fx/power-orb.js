// 마법사 강화 평타(지팡이·완드)용 프로젝트 전용 이펙트: 큰 마력 구슬이 오른쪽으로 날아간다.
// 둥글게 음영을 넣은 구슬 둘레를 기울어진 고리 두 개가 돌고, 뒤로 혜성 꼬리가 흐르며 작은 빛 조각이 구슬을 맴돈다.
// export.mjs가 "local/power-orb"로 읽는다. 구슬 앞쪽 끝이 (0, 0)이고, 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, cycle, fly, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Power orb",
  desc: "A big shaded orb of mana flying right: two tilted rings spin round it, a comet tail of shrinking beads streams behind, and motes orbit and shed off.",
  tags: ["arcane", "orb", "magic", "projectile", "directional", "loop"],
  size: [96, 56],
  origin: [74, 28],
  duration: 0.5,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#0a1030", "#16285e", "#2446a0", "#3a78e0", "#62b4ff", "#a8e0ff", "#e8f8ff", "#ffffff"], size: 9, seed: 6 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 8), D = meta.duration, S = o.size, ph = t / D;
  const cx = -S - 1;

  // 혜성 꼬리: 뒤로 갈수록 작고 어두운 구슬이 이어진다(앞으로 흐르듯 위치가 밀린다)
  const shift = ph * 6;
  for (let i = 0; i < 9; i++) {
    const d = i * 6 + shift;
    const k = d / 54;
    if (k >= 1) {
      continue;
    }
    const r = S * (1 - k) * 0.85;
    const c = k < 0.25 ? C[5] : k < 0.5 ? C[4] : k < 0.75 ? C[3] : C[2];
    p.disc(cx - d - 3, Math.sin((ph + i * 0.13) * TAU) * k * 2, Math.max(0.6, r), c);
  }
  p.glowLine(cx - 50, 0, cx, 0, 7, C[3], 0.3);

  // 뒤쪽 고리 절반(구슬 뒤로 지나가는 쪽)
  const rings = [[S + 5, 0.35, 0.6], [S + 3, 0.5, -1.1]];
  for (let j = 0; j < rings.length; j++) {
    const [rr, squash, tilt] = rings[j];
    const spin = ph * TAU * (j === 0 ? 1 : -1);
    for (let a = 0; a < TAU; a += 0.08) {
      const ex = Math.cos(a + spin) * rr;
      const ey = Math.sin(a + spin) * rr * squash;
      const front = Math.sin(a + spin) > 0;
      if (!front) {
        const x = cx + ex * Math.cos(tilt) - ey * Math.sin(tilt);
        const y = ex * Math.sin(tilt) + ey * Math.cos(tilt);
        p.px(x, y, C[3]);
      }
    }
  }

  // 구슬: 짙은 테두리, 아래 오른쪽은 어둡고 위 왼쪽은 하얗게
  const pulse = Math.sin(ph * TAU * 2) * 0.6;
  const R = S + pulse;
  p.glow(cx, 0, R * 2.6, C[4], 0.45);
  p.shade(cx - R - 2, -R - 2, cx + R + 2, R + 2, (x, y) => {
    const dx = (x - cx) / R;
    const dy = y / R;
    const dd = dx * dx + dy * dy;
    if (dd > 1.25) {
      return;
    }
    if (dd > 1) {
      return C[1];
    }
    const nz = Math.sqrt(1 - dd);
    const light = -dx * 0.5 - dy * 0.55 + nz * 0.7;
    if (light > 0.95) {
      return WHITE;
    }
    if (light > 0.75) {
      return C[6];
    }
    if (light > 0.5) {
      return C[5];
    }
    if (light > 0.25) {
      return C[4];
    }
    return C[3];
  });
  // 구슬 속 소용돌이 빛줄기
  for (let a = 0; a < TAU * 0.6; a += 0.12) {
    const r = R * 0.55 * (a / (TAU * 0.6));
    p.px(cx + Math.cos(a + ph * TAU) * r, Math.sin(a + ph * TAU) * r, C[7]);
  }

  // 앞쪽 고리 절반(구슬 앞으로 지나가는 쪽)
  for (let j = 0; j < rings.length; j++) {
    const [rr, squash, tilt] = rings[j];
    const spin = ph * TAU * (j === 0 ? 1 : -1);
    for (let a = 0; a < TAU; a += 0.08) {
      const ex = Math.cos(a + spin) * rr;
      const ey = Math.sin(a + spin) * rr * squash;
      if (Math.sin(a + spin) > 0) {
        const x = cx + ex * Math.cos(tilt) - ey * Math.sin(tilt);
        const y = ex * Math.sin(tilt) + ey * Math.cos(tilt);
        p.px(x, y, C[6]);
      }
    }
  }

  // 구슬을 맴돌다 떨어져 나가는 빛 조각
  cycle(t, D, 8, o.seed, (g) => g.range(0.2, 0.35), (age, life, r) => {
    const a = r.range(0, TAU);
    const [x, y] = fly(cx + Math.cos(a) * (S + 3), Math.sin(a) * (S + 3), -r.range(80, 160), Math.sin(a) * r.range(10, 30), age, 0, 1);
    const k = age / life;
    p.px(x, y, k < 0.4 ? WHITE : C[5]);
  });
}
