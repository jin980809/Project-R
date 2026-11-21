// 관통 볼트(CrossPierce) 투사체용 프로젝트 전용 이펙트: 송곳 촉 볼트가 공기를 뚫으며 오른쪽으로 날아간다.
// 길고 뾰족한 원뿔 촉 둘레로 공기 소용돌이 고리가 차례로 생겨 뒤로 밀려나며 커지고 흐려진다.
// 촉 끝이 (0, 0)이고, export.mjs가 "local/pierce-bolt"로 읽는다. 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Pierce bolt",
  desc: "A long bolt with a drill-like conical head boring right through the air: vortex rings form round its tip one after another and slide back along the shaft, swelling and fading.",
  tags: ["projectile", "bolt", "pierce", "crossbow", "directional", "loop"],
  size: [68, 24],
  origin: [60, 12],
  duration: 0.36,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#0e1420", "#2c3a54", "#56708e", "#90b0cc", "#d0e4f4"], length: 34, seed: 2 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 5);
  const L = o.length;
  const ph = t / meta.duration;

  // 공기 소용돌이 고리: 촉에서 생겨 뒤로 밀려나며 커진다(세로로 긴 타원 고리)
  for (let i = 0; i < 3; i++) {
    const k = (ph + i / 3) % 1;
    const x = -6 - k * (L - 4);
    const ry = 3 + k * 5;
    const rx = 1.5 + k;
    p.shade(x - rx - 1, -ry - 1, x + rx + 1, ry + 1, (px, py, out) => {
      const d = Math.hypot((px - x) / rx, py / ry);
      if (d > 1 || d < 0.6) {
        return;
      }
      out.a = (1 - k) * 0.9;
      return C[3];
    });
  }
  // 대: 가늘고 길다
  p.line(-L, 0, -9, 0, C[2]);
  p.line(-L, -1, -L + 4, -1, C[3]);
  p.line(-L, 1, -L + 4, 1, C[3]);
  // 송곳 촉: 나선 홈이 앞으로 돈다
  p.tri(-10, -3, -10, 3, 0, 0, C[1]);
  p.tri(-9, -2, -9, 2, -1, 0, C[3]);
  const groove = Math.floor(ph * 12) % 3;
  p.px(-8 + groove * 2, -1 + (groove % 2), C[4]);
  p.px(-1, 0, WHITE);
}
