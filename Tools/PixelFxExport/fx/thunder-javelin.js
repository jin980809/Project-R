// 뇌창 투척(SpearThrow)용 프로젝트 전용 이펙트: 번개로 된 투창이 오른쪽으로 날아간다.
// pixelfx 라이브러리에 번개 창 그림이 없어 여기서 그린다. export.mjs가 "local/thunder-javelin"으로 읽는다.
// 창촉 끝이 (0, 0)이고, 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, cycle, fly, rng, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Thunder javelin",
  desc: "A javelin of lightning flying right: a white-hot shaft with a long barbed head, arcs crackling round it, a jagged bolt tail and sparks shed behind.",
  tags: ["lightning", "spear", "javelin", "projectile", "directional", "loop"],
  size: [150, 48],
  origin: [120, 24],
  duration: 0.6,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#120a33", "#2a1b78", "#4630c0", "#5a62f0", "#58a6ff", "#9ed8ff", "#e4f6ff", "#ffffff"], length: 40, seed: 7 },
};

// 지그재그 번개 한 줄: x0에서 x1까지 seg칸으로 나눠 위아래로 흔든다. 같은 g면 같은 모양.
function zigzag(p, g, x0, x1, y, amp, seg, c, a) {
  const pts = [[x0, y]];
  for (let i = 1; i < seg; i++) {
    pts.push([x0 + ((x1 - x0) * i) / seg, y + g.range(-amp, amp)]);
  }
  pts.push([x1, y]);
  p.polyline(pts, c, a);
}

export function draw(p, t, o) {
  const C = fit(o.palette, 8), D = meta.duration, S = o.seed, L = o.length, ph = t / D;
  const step = Math.floor(t * 15 + 1e-6);

  // 꼬리: 창 뒤로 갈수록 가늘고 어두워지는 들쭉날쭉한 번개 두 줄
  for (let j = 0; j < 2; j++) {
    const g = rng(step, j, S);
    const pts = [[-L + 2, 0]];
    let y = 0;
    for (let i = 1; i <= 7; i++) {
      y = y * 0.4 + g.range(-3.2, 3.2);
      pts.push([-L + 2 - i * 5, y]);
    }
    for (let i = 0; i < pts.length - 1; i++) {
      const k = i / (pts.length - 1);
      let c = C[4];
      if (k < 0.3) {
        c = C[6];
      } else if (k < 0.6) {
        c = C[5];
      }
      const alpha = (1 - k) * (j === 0 ? 1 : 0.55);
      p.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], c, alpha);
    }
  }
  p.glowLine(-L - 30, 0, -L + 4, 0, 8, C[3], 0.25);

  // 떨어져 나가는 불꽃: 짧은 줄로 뒤로 흩어지며 사라진다
  cycle(t, D, 12, S + 1, (g) => g.range(0.2, 0.35), (age, life, r) => {
    const [x, y] = fly(r.range(-L, -6), r.range(-2, 2), -r.range(140, 220), r.sign() * r.range(20, 60), age, 0, 2);
    const k = age / life;
    const tail = 2 + 3 * (1 - k);
    p.line(x, y, x + tail, y, k < 0.4 ? WHITE : C[5], 1 - k);
  });

  // 창 둘레를 감는 번개 두 줄(두 프레임마다 모양이 바뀐다)
  for (let j = 0; j < 2; j++) {
    zigzag(p, rng(step, j + 5, S), -L + 1, -9, 0, 4.2, 7, j === 0 ? C[6] : C[5], 0.9);
  }

  // 자루: 가운데 흰 심, 위는 밝게, 아래는 짙게
  for (let x = -L; x <= -10; x++) {
    p.px(x, -1, C[6]);
    p.px(x, 0, WHITE);
    p.px(x, 1, C[4]);
  }
  // 자루 끝의 짧은 갈래(깃)
  p.line(-L, 0, -L - 4, -3, C[5]);
  p.line(-L, 0, -L - 4, 3, C[4]);

  // 창촉: 긴 마름모. 윗면 흰색, 가운데 밝은 파랑, 아랫면 짙은 파랑. 뒤쪽에 갈고리가 하나씩 튀어나온다.
  p.shade(-15, -6, 1, 6, (x, y) => {
    let half = 0;
    if (x >= -6) {
      half = 4 * (-x / 6);
    } else {
      half = 4 * ((x + 14) / 8);
    }
    if (x > 0.5 || x < -14.5) {
      return;
    }
    if (Math.abs(y) > half + 0.4) {
      // 창촉 테두리: 짙은 남색 한 줄로 모양을 또렷하게
      if (Math.abs(y) < half + 1.4) {
        return C[2];
      }
      return;
    }
    if (x > -1.5) {
      return WHITE;
    }
    const v = y / Math.max(0.5, half);
    if (v < -0.6) {
      return WHITE;
    }
    if (v < 0.05) {
      return C[6];
    }
    if (v < 0.5) {
      return C[5];
    }
    return C[3];
  });
  p.line(-14, -1, -17, -4, C[6]);
  p.line(-14, 1, -17, 4, C[4]);

  // 창을 따라 앞으로 달리는 빛 한 점
  const pulse = -L + ((ph * 3) % 1) * (L - 2);
  p.px(pulse, -1, WHITE);
  p.px(pulse + 1, -1, WHITE);

  // 빛: 자루를 따라 은은하게, 창촉 끝은 뜨겁게
  p.glowLine(-L, 0, -12, 0, 6, C[4], 0.22);
  p.glow(pulse, 0, 6, C[6], 0.3);
  p.glow(-1, 0, 5, C[6], 0.3);
  p.glow(-L * 0.5, 0, 22, C[3], 0.18 + 0.06 * Math.sin(ph * TAU * 3));
}
