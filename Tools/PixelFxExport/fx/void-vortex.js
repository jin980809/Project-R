// 블랙홀(WandHex)용 프로젝트 전용 이펙트: 위에서 내려다본 소용돌이. 나선 팔이 빠르게 돌며 가운데 검은 구멍으로 빨려 들고,
// 빛 고리가 바깥에서 안으로 좁혀 들며, 부스러기가 나선을 따라 끌려 들어간다. export.mjs가 "local/void-vortex"로 읽는다.
// 가운데가 (0, 0)이고, 이펙트 규칙(Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다. 바닥 원근으로 세로를 조금 눌렀다.
import { fit, cycle, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Void vortex",
  desc: "A whirlpool of void seen from above: four spiral arms whip round into a black core rimmed with violet light, rings of light collapse inward and debris is dragged in along the spiral.",
  tags: ["shadow", "void", "black-hole", "vortex", "gravity", "area", "loop"],
  size: [168, 136],
  origin: [84, 68],
  duration: 1.2,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#0a0612", "#1c0f33", "#35195e", "#5a2a9a", "#8a4ad8", "#b884ff", "#e2c8ff", "#ffffff"], radius: 72, squash: 0.78, seed: 9 },
};

// 나선 위의 한 점: 반지름 r, 바깥에서 안으로 갈수록 더 감긴다.
function spiralPoint(r, R, turn, arm, arms, sq) {
  const a = turn + (arm / arms) * TAU + (1 - r / R) * 2.6;
  return [Math.cos(a) * r, Math.sin(a) * r * sq];
}

export function draw(p, t, o) {
  const C = fit(o.palette, 8), D = meta.duration, R = o.radius, SQ = o.squash, ph = t / D;
  // 한 반복에 나선이 반 바퀴(4개 팔이 대칭이라 이어진다)
  const turn = ph * (TAU / 4) * 2;

  // 바깥 그림자 원판: 바닥이 빨려 드는 느낌의 어두운 원
  p.shade(-R - 2, -R * SQ - 2, R + 2, R * SQ + 2, (x, y) => {
    const d = Math.hypot(x, y / SQ);
    if (d > R) {
      return;
    }
    if (d > R * 0.82) {
      return ((Math.floor(x) + Math.floor(y)) & 1) === 0 ? C[1] : undefined;
    }
    return C[1];
  });

  // 나선 팔 네 개: 바깥은 어둡고 가늘게, 안쪽은 밝고 굵게
  const arms = 4;
  for (let arm = 0; arm < arms; arm++) {
    for (let r = R * 0.95; r > 9; r -= 0.7) {
      const [x, y] = spiralPoint(r, R, turn, arm, arms, SQ);
      const k = 1 - r / R;
      let c = C[3];
      if (k > 0.7) {
        c = C[7];
      } else if (k > 0.5) {
        c = C[6];
      } else if (k > 0.25) {
        c = C[5];
      } else if (k > 0.1) {
        c = C[4];
      }
      // 팔 두께: 안쪽일수록 굵다(바깥 1칸, 안쪽 3칸)
      const w = 0.6 + k * 1.6;
      p.disc(x, y, w, c);
      // 팔 바깥쪽 가장자리에 어두운 그림자 한 줄(팔이 또렷하게 보이게)
      const [x2, y2] = spiralPoint(r + 2.2, R, turn - 0.12, arm, arms, SQ);
      p.px(x2, y2, C[2]);
    }
  }

  // 안으로 좁혀 드는 빛 고리 두 개(반복마다 바깥에서 새로 시작)
  for (let j = 0; j < 2; j++) {
    const q = (ph * 2 + j * 0.5) % 1;
    const r = R * (1 - q) * 0.9 + 12 * q;
    const c = q > 0.6 ? C[6] : C[4];
    p.shade(-r - 1, -r * SQ - 1, r + 1, r * SQ + 1, (x, y) => {
      const d = Math.hypot(x, y / SQ);
      if (Math.abs(d - r) < 0.75) {
        return c;
      }
    });
  }

  // 나선을 따라 끌려 드는 부스러기(안으로 갈수록 빨라지고 밝아진다)
  cycle(t, D, 18, o.seed, (g) => g.range(0.5, 0.9), (age, life, rr) => {
    const k = age / life;
    const r = R * (1 - k * k) * rr.range(0.8, 1);
    if (r < 10) {
      return;
    }
    const a = rr.range(0, TAU) - k * k * 4.5;
    const x = Math.cos(a) * r;
    const y = Math.sin(a) * r * SQ;
    const tail = 1 + k * 3;
    const tx = Math.cos(a + 0.5) * tail;
    const ty = Math.sin(a + 0.5) * tail * SQ;
    p.line(x, y, x + tx, y + ty, k > 0.6 ? C[6] : C[4]);
  });

  // 강착 원반: 구멍 둘레를 빠르게 도는 밝고 어두운 띠
  p.shade(-26, -22, 26, 22, (x, y) => {
    const d = Math.hypot(x, y / SQ);
    if (d < 10 || d > 22) {
      return;
    }
    const a = Math.atan2(y / SQ, x) - ph * TAU * 2 - (22 - d) * 0.18;
    const band = (Math.sin(a * 3) + 1) * 0.5;
    const k = 1 - (d - 10) / 12;
    const v2 = band * 0.6 + k * 0.6;
    if (v2 > 0.95) {
      return C[7];
    }
    if (v2 > 0.75) {
      return C[6];
    }
    if (v2 > 0.5) {
      return C[5];
    }
    if (v2 > 0.3) {
      return C[4];
    }
  });

  // 가운데: 밝게 빛나는 테두리와 검은 구멍
  const pulse = 1 + Math.round(Math.sin(ph * TAU * 2));
  p.glow(0, 0, 30, C[4], 0.35);
  p.shade(-13, -11, 13, 11, (x, y) => {
    const d = Math.hypot(x, y / SQ);
    if (d < 7 + pulse * 0.5) {
      return C[0];
    }
    if (d < 9 + pulse * 0.5) {
      return WHITE;
    }
    if (d < 11 + pulse * 0.5) {
      return C[6];
    }
  });
}
