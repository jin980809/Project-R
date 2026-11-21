// 스킬별 pixelfx 이펙트 대응표. export.mjs가 읽는다.
//
// 부위(cast, trail, projectile, impact, muzzle)마다
//   id      pixelfx 이펙트 ("melee/spin-slash")
//   opts    이펙트 옵션 (palette는 PAL 이름 "fire" 또는 색 배열). radius·reach 같은 크기 옵션은 픽셀 단위다.
//   from/to 잘라 쓸 구간(초). 비우면 처음/끝
//   frames  내보낼 프레임 수
//   speed   재생 배속 (fps = frames / ((to - from) / speed))
//   anchor  기준점: "bbox"(그림 가운데) "origin"(이펙트 발밑) "waist"(캐릭터 허리) "axis"(캐릭터 자리 x, 그림이 몰린 높이 y: 조준 쪽으로 돌리는 베기) "left" "right" 또는 [dx, dy]
//   fade / fadeIn  끝/앞 몇 비율을 흐리게
//   maxHalf [가로, 세로] 기준점에서 이보다 먼 그림(긴 꼬리, 멀리 튄 불꽃)은 잘라낸다(픽셀)
//   scatter { count, radius, squash, center } 같은 그림을 원 안 여러 자리에 시간차로 겹친다(넓은 장판)
//   trail   돌진 길: 이펙트 원점(+dx)에서 스킬 사거리만큼 잘라 늘리지 않고 그린다
//   zoom    정수배 확대(되도록 1)
//   ground  바닥에 깔리는 그림(캐릭터 뒤에 그린다). cast·impact 부위에 쓰고 meta의 cast_ground·impact_ground가 된다(없으면 0).
//   intro / outro  장판(cast)의 나타나는 그림·사라지는 그림(intro_NN.png, outro_NN.png). 반복 그림과 같은 칸 크기로 잘린다.
//           { from, to, frames, speed } 같은 이펙트의 구간을 그대로 쓴다(불이 번져 나가는 시작, 타서 꺼지는 끝)
//           { mode: "grow" | "dissolve" | "shrink", time } 반복 그림을 커지며 나타나게/점점이 흩어지게/오그라들게 바꿔 만든다
// meta    meta.txt에 쓸 값 (방향: *_dir 1 조준 각도로 돌림, 2 좌우만 뒤집음. 바닥 여부). fps와 크기 값은 export.mjs가 스킬 반경에서 계산해 채운다.
//         크기는 언제나 그림 1픽셀 = 1/32유닛(마을·던전과 같은 밀도)이 되도록 정해진다.
// bind    스킬 에셋이 없는 묶음(무기 기본 공격, 몬스터)의 반경 값 { kind, radius, explosionRadius, range }
// keep    대응표에 없어도 지우지 않을 예전 부위

const White = ["#2a2f48", "#4a5478", "#7d8ab8", "#b8c4e8", "#e8eeff", "#ffffff"];
const SteelArrow = ["#2e3648", "#6e7a90", "#d0d8e6", "#ffffff"];
// 한 스킬 안의 부위(날아가는 그림·섬광·적중)는 같은 색 묶음을 쓴다. 기본 불꽃(sparks)이 주황이라 색 묶음에 맞춘 불꽃을 따로 준다.
const Lightning = ["#120a33", "#2a1b78", "#4630c0", "#5a62f0", "#58a6ff", "#9ed8ff", "#e4f6ff", "#ffffff"];
const WhiteSparks = ["#ffffff", "#e8eeff", "#b8c4e8", "#7d8ab8"];
const IceSparks = ["#ffffff", "#d8f4ff", "#9bdcff", "#58b4ec"];
const VoltSparks = ["#ffffff", "#fff08a", "#ffd23c", "#e8a218"];
const HolyAccent = ["#2a1c08", "#9a6c18", "#ffd84a", "#fff09a", "#fffbe6"];
const GaleLeaves = ["#123818", "#2f7a2a", "#6cb83a", "#c8f070", "#163418", "#357530", "#7cc048", "#e4ffb0", "#0c1a0e", "#225224", "#4f9a3a", "#b4e070"];

function oneShot(id, from, to, frames, speed, more) {
  return { id: id, from: from, to: to, frames: frames, speed: speed, fade: 0.3, ...more };
}

// 반복 이펙트를 한 번 재생으로 쓴다(앞뒤를 흐려 나타났다 사라지게).
function pulse(id, from, to, frames, speed, more) {
  return { id: id, from: from, to: to, frames: frames, speed: speed, fadeIn: 0.15, fade: 0.35, ...more };
}

// 장판: 지속 시간 동안 반복된다.
function zone(id, from, to, frames, speed, more) {
  return { id: id, from: from, to: to, frames: frames, speed: speed, ...more };
}

// 날아가는 그림: 머리(오른쪽 끝)를 기준으로 꼬리를 짧게 자른다.
function projectile(id, to, frames, speed, maxHalf, more) {
  return { id: id, from: 0, to: to, frames: frames, speed: speed, anchor: "right", maxHalf: maxHalf, ...more };
}

function arrow(palette, seed) {
  const opts = {};
  if (palette !== undefined) {
    opts.wood = palette;
  }
  if (seed !== undefined) {
    opts.seed = seed;
  }
  return projectile("projectiles/arrow", 0.6, 6, 1, [22, 6], { opts: opts });
}

function hitSpark(palette) {
  return oneShot("impacts/hit-spark", 0, 0.3, 6, 1, { opts: { palette: palette }, anchor: "left", maxHalf: [30, 16] });
}

// 마법 기본 공격 적중: 작은 불꽃만 짧게 튄다
function magicSpark(palette) {
  return oneShot("impacts/hit-spark", 0, 0.24, 5, 1, { opts: { palette: palette }, anchor: "left", maxHalf: [16, 10], fade: 0.4 });
}

function smallExplosion(seed) {
  return oneShot("explosions/small-explosion", 0, 0.95, 12, 1.2, { opts: { seed: seed }, anchor: [0, -24], fade: 0.4, maxHalf: [76, 76] });
}

// 강화 시전: 몸에서 짧게 터지는 빛(앞부분만 써서 작게)
function powerBurst(palette) {
  return oneShot("arcane/mana-burst", 0, 0.9, 10, 1.4, { opts: { palette: palette }, anchor: "bbox", maxHalf: [56, 56] });
}

// 치명·집중 강화: 반짝이는 고리
function focusBurst(palette) {
  return oneShot("pickups/collect-burst", 0, 1.0, 10, 1.3, { opts: { palette: palette }, anchor: "bbox" });
}

// 돌진 길: 찌르기 줄기를 사거리만큼 깐다
function dashTrail(palette, alpha) {
  return { id: "melee/thrust", from: 0.03, to: 0.45, frames: 8, speed: 1, fade: 0.35, smear: 0.82, opts: { palette: palette, reach: 190 }, trail: true, alpha: alpha };
}

// 스킬끼리 같은 pixelfx 이펙트를 쓰지 않는다(무기 기본 공격·강화 평타·몬스터는 스킬이 아니어서 예외).
// 장판(Zone)은 나타나는 그림(intro) → 반복 그림 → 사라지는 그림(outro)으로 재생한다.

// 장판 나타나기·사라지기: 같은 이펙트의 구간이 없으면 반복 그림을 커지게/흩어지게 바꿔 만든다.
const Grow = { mode: "grow", time: 0.45 };
const Dissolve = { mode: "dissolve", time: 0.5 };

export default [
  // 1 검사
  { group: "1_sword", name: "SwordSpin", parts: { cast: oneShot("melee/spin-slash", 0.04, 0.72, 12, 1.1, { opts: { palette: "blood", radius: 62 }, anchor: "waist" }) } },
  // 십자 베기: 위에서 본 X자 두 번 베기(교차점 섬광, 불티, 충격 고리)
  { group: "1_sword", name: "SwordCross", parts: { cast: oneShot("local/cross-slash", 0, 0.7, 14, 1, { anchor: "origin", fade: 0.2 }) }, meta: { cast_dir: "1" } },
  // 방패 밀치기: 낮게 쓸고 나가는 납작한 금빛 초승달을 길로 깔고, 끝에서 묵직한 타격
  {
    group: "1_sword", name: "SwordBash",
    parts: {
      trail: { id: "melee/spear-sweep", from: 0.1, to: 0.7, frames: 8, speed: 1, fade: 0.35, opts: { palette: "gold" }, trail: true, alpha: 0.85 },
      impact: oneShot("impacts/blunt-hit", 0, 0.7, 10, 1, { opts: { palette: "gold" }, anchor: "bbox", maxHalf: [40, 32] }),
    },
  },
  { group: "1_sword", name: "SwordGuard", parts: { cast: oneShot("scifi/emp-blast", 0.1, 1.9, 14, 1.6, { opts: { palette: "gold", radius: 96 }, anchor: "origin" }) } },
  // 돌진 베기: 얼음 바람을 길로 깔고, 끝에서 X자 두 번 베기
  {
    group: "1_sword", name: "SwordCharge",
    parts: {
      trail: { id: "ice/frost-breath", from: 0.05, to: 0.9, frames: 8, speed: 1, fade: 0.35, trail: true, alpha: 0.9 },
      impact: oneShot("melee/double-slash", 0.05, 0.7, 10, 1, { opts: { palette: "ice", sparks: IceSparks }, anchor: "axis", maxHalf: [60, 50] }),
    },
    meta: { impact_dir: "1" },
  },
  { group: "1_sword", name: "SwordRoar", parts: { cast: powerBurst("blood") } },

  // 2 창술사
  // 관통 돌진: 지나간 길을 따라 금빛 번개가 이어 달리며 바닥에 불티를 튀긴다(창 기본 공격의 찌르기와 다른 그림)
  { group: "2_spear", name: "SpearPierce", parts: { trail: { id: "lightning/chain-lightning", from: 0.3, to: 1.4, frames: 10, speed: 1.6, fade: 0.35, opts: { palette: "volt", sparks: VoltSparks }, trail: true, alpha: 0.95 } } },
  {
    group: "2_spear", name: "SpearThrow",
    parts: {
      projectile: projectile("local/thunder-javelin", 0.6, 9, 1, [52, 9], { opts: { palette: Lightning } }),
      // 맞은 자리: 갈래 번개가 사방 바닥을 찌르고 충격 고리가 퍼진다
      impact: oneShot("lightning/thunderclap", 0.12, 1.3, 12, 1.2, { opts: { palette: Lightning }, anchor: "origin", maxHalf: [96, 72] }),
    },
  },
  // 휩쓸기: 조준 방향 부채꼴(SkillDefinition.arcDegrees)을 치므로 위에서 본 큰 베기 호를 조준 쪽으로 돌린다(옆에서 본 낮은 휩쓸기 그림은 방향이 맞지 않았다).
  // 휩쓸기: 큰 낫질처럼 넓은 초승달이 쓸고 지나가며 빛 가루를 흘린다(검·도끼·둔기 기본 공격의 베기와 다른 그림)
  { group: "2_spear", name: "SpearSweep", parts: { cast: oneShot("melee/scythe-reap", 0.2, 1.6, 10, 1.4, { opts: { palette: ["#100a04", "#24160a", "#5e3a0c", "#a8680f", "#e8a218", "#ffd23c", "#fff08a", "#ffffff"] }, anchor: "bbox", fade: 0.35 }) }, meta: { cast_dir: "1" } },
  { group: "2_spear", name: "SpearGale", parts: { cast: pulse("nature/leaf-storm", 0, 2.4, 14, 2.4, { opts: { leaves: GaleLeaves }, anchor: "bbox" }) } },
  // 도약 찍기: 착지 판정이 원이므로 위에서 본 원형 착지 충격(옆에서 본 heavy-landing은 납작한 회색 먼지로 보였다)
  { group: "2_spear", name: "SpearLeap", parts: { impact: oneShot("local/leap-crater", 0, 1.2, 14, 1.1, { anchor: "origin", fade: 0.25, ground: true }) } },
  { group: "2_spear", name: "SpearStorm", parts: { cast: zone("lightning/lightning-strike", 0, 2.2, 18, 1, { opts: { palette: Lightning }, anchor: "origin", scatter: { count: 3, radius: 30, squash: 0.6 }, ground: true, intro: Grow, outro: Dissolve }) } },

  // 3 궁수
  { group: "3_bow", name: "BowFan", parts: { projectile: arrow(), impact: hitSpark(undefined) }, meta: { impact_dir: "1" } },
  // 폭발 화살: 맞은 자리에서 불티가 꽃불처럼 사방으로 터진다(몬스터 폭발과 다른 그림)
  { group: "3_bow", name: "BowBlast", parts: { projectile: projectile("local/ember-arrow", 0.4, 8, 1, [34, 12], {}), impact: oneShot("explosions/firework-burst", 0.45, 2.0, 12, 1.4, { opts: { palette: ["#3a1008", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#fff3a6"] }, anchor: "bbox", fade: 0.35, maxHalf: [64, 64] }) } },
  // 회피 도약: 떠난 자리에 연기 뭉치가 퐁 터진다
  { group: "3_bow", name: "BowLeap", parts: { cast: oneShot("spawn/poof", 0, 1.2, 10, 1.4, { anchor: "bbox", fade: 0.3 }) } },
  { group: "3_bow", name: "BowFocus", parts: { cast: focusBurst("gold") } },
  { group: "3_bow", name: "BowRain", parts: { cast: zone("arcane/star-fall", 0.1, 2.2, 16, 1, { opts: { palette: "poison" }, anchor: "bbox", ground: true, intro: Grow, outro: Dissolve }) } },
  { group: "3_bow", name: "BowBless", parts: { cast: oneShot("nature/healing-bloom", 0.1, 3.0, 14, 2.2, { anchor: "origin", ground: true }) } },

  // 4 마법사
  {
    group: "4_staff", name: "StaffFireball",
    parts: {
      projectile: projectile("fire/fireball", 1.2, 12, 2, [28, 12], { opts: { radius: 6 } }),
      // 맞은 자리: 흰 불꽃 돔이 솟고 불덩이가 피어오른다
      impact: oneShot("explosions/big-explosion", 0, 1.8, 14, 1.6, { anchor: [0, -30], fade: 0.4, maxHalf: [84, 84] }),
    },
  },
  {
    group: "4_staff", name: "StaffFlame",
    parts: { cast: zone("fire/fire-ring", 0.75, 1.6, 12, 1, { opts: { reach: 60 }, anchor: "origin", ground: true, intro: { from: 0.2, to: 0.75, frames: 8 }, outro: { from: 1.6, to: 2.8, frames: 12 } }) },
  },
  {
    group: "4_staff", name: "StaffIce",
    parts: {
      projectile: projectile("ice/ice-lance", 0.8, 8, 1, [26, 8]),
      impact: oneShot("ice/snowflake-burst", 0.1, 1.6, 12, 1.6, { anchor: "bbox", maxHalf: [36, 36] }),
    },
  },
  // 서리 폭발: 판정이 원이므로 위에서 본 원형 그림(옆에서 본 frost-nova는 납작한 얼음 줄로 보였다)
  { group: "4_staff", name: "StaffNova", parts: { cast: oneShot("local/frost-ring", 0, 1.6, 16, 1.2, { anchor: "origin", fade: 0.25, ground: true }) } },
  {
    group: "4_staff", name: "StaffStorm",
    parts: {
      // 연쇄 번개: 지직거리는 번개 구슬이 날아간다
      projectile: projectile("lightning/ball-lightning", 1.0, 10, 1, [20, 16], { anchor: "bbox", opts: { palette: "arcane" } }),
      impact: oneShot("lightning/zap-hit", 0.05, 0.7, 9, 1, { opts: { palette: "arcane" }, anchor: "bbox", maxHalf: [32, 24] }),
    },
    meta: { impact_dir: "1" },
  },
  // 점멸: 떠나는 자리에서 빛줄기로 오그라들어 사라지고, 도착한 자리에서 빛이 모여 나타난다
  {
    group: "4_staff", name: "StaffBlink",
    parts: {
      cast: oneShot("arcane/blink-out", 0.2, 1.6, 12, 1.6, { anchor: "bbox", maxHalf: [56, 48] }),
      impact: oneShot("arcane/blink-in", 0, 1.2, 12, 1.6, { anchor: "bbox", maxHalf: [56, 48] }),
    },
  },

  // 5 광전사
  // 피의 분노: 몸에서 붉은 불기둥이 치솟는다
  { group: "5_axe", name: "AxeRage", parts: { cast: oneShot("fire/flame-pillar", 0.4, 2.4, 14, 1.8, { opts: { palette: "blood", height: 90 }, anchor: "origin", fade: 0.35 }) } },
  // 광폭 회전: 위에서 본 두 도끼날 회오리
  { group: "5_axe", name: "AxeWhirl", parts: { cast: oneShot("local/whirl-ring", 0, 0.8, 12, 1.1, { anchor: "origin", fade: 0.2, ground: true }) } },
  {
    group: "5_axe", name: "AxeThrow",
    parts: { projectile: { id: "projectiles/energy-disc", from: 0, to: 0.5, frames: 6, speed: 1, anchor: "origin", maxHalf: [16, 16], opts: { palette: "metal" } } },
    meta: { proj_spin: "0" },
  },
  // 처형: 핏빛 잔상으로 달려들어 내려찍는다
  {
    group: "5_axe", name: "AxeExecute",
    parts: { trail: { id: "local/blood-rush", from: 0, to: 0.6, frames: 9, speed: 1, trail: true }, impact: oneShot("melee/heavy-cleave", 0.3, 0.9, 10, 1, { opts: { palette: "blood" }, anchor: "bbox", fade: 0.4, maxHalf: [64, 48] }) },
    meta: { impact_dir: "2" },
  },
  { group: "5_axe", name: "AxeSplit", parts: { cast: oneShot("melee/hammer-smash", 0.15, 0.95, 12, 1.2, { opts: { palette: "fire" }, anchor: "origin", fade: 0.35, ground: true }) }, meta: { cast_dir: "2" } },
  { group: "5_axe", name: "AxeEndure", parts: { cast: pulse("holy/divine-shield", 0, 1.2, 12, 1.2, { opts: { palette: "fire" }, anchor: "bbox" }) } },

  // 6 파괴자
  { group: "6_blunt", name: "BluntSlam", parts: { cast: oneShot("impacts/ground-slam", 0, 1.0, 12, 1.1, { anchor: "origin", fade: 0.35, maxHalf: [80, 72], ground: true }) } },
  {
    group: "6_blunt", name: "BluntQuake",
    parts: { cast: zone("nature/quake", 0.6, 2.2, 14, 1.2, { opts: { reach: 72 }, anchor: "origin", ground: true, intro: { from: 0.12, to: 0.6, frames: 6 }, outro: { from: 2.2, to: 3.05, frames: 9 } }) },
  },
  // 도약 강타: 착지 자리가 패이며 먼지 고리가 두 번 구른다
  { group: "6_blunt", name: "BluntLeap", parts: { impact: oneShot("impacts/heavy-landing", 0, 1.3, 12, 1.1, { opts: { palette: "earth" }, anchor: "origin", fade: 0.3, ground: true }) } },
  // 파쇄 돌진: 바닥을 갈아엎는 고랑, 끝에서 치명타 섬광
  {
    group: "6_blunt", name: "BluntRam",
    parts: { trail: { id: "local/ground-furrow", from: 0, to: 0.75, frames: 10, speed: 1, trail: true }, impact: oneShot("impacts/crit-hit", 0, 0.65, 10, 1, { anchor: "bbox", maxHalf: [48, 40] }) },
    meta: { impact_dir: "1" },
  },
  { group: "6_blunt", name: "BluntWall", parts: { cast: pulse("scifi/force-field", 0, 1.2, 12, 1, { opts: { palette: "gold", radius: 30 }, anchor: "bbox" }) } },
  { group: "6_blunt", name: "BluntVigor", parts: { cast: oneShot("holy/heal", 0, 1.9, 14, 1.4, { opts: { palette: "leaf" }, anchor: "origin", ground: true }) } },

  // 7 저격수
  { group: "7_cross", name: "CrossPierce", parts: { projectile: projectile("local/pierce-bolt", 0.36, 6, 1, [36, 10], {}) } },
  // 집중 조준: 손끝에 기운이 모여 구슬이 된다
  { group: "7_cross", name: "CrossAim", parts: { cast: oneShot("arcane/spell-charge", 0, 1.6, 12, 1.4, { opts: { palette: "volt" }, anchor: "bbox", fade: 0.35 }) } },
  { group: "7_cross", name: "CrossBurst", parts: { projectile: projectile("local/steel-bolt", 0.3, 6, 1, [20, 8], {}) } },
  // 폭발 볼트: 작은 로켓 볼트(긴 연기는 잘라낸다), 맞으면 화염 기둥이 솟으며 터진다
  {
    group: "7_cross", name: "CrossBomb",
    parts: {
      projectile: projectile("projectiles/rocket", 1.2, 8, 1, [30, 7], { opts: { seed: 6 } }),
      impact: oneShot("explosions/barrel-explosion", 1.35, 3.0, 12, 1.4, { anchor: [0, -24], fade: 0.4, maxHalf: [80, 80] }),
    },
  },
  {
    group: "7_cross", name: "CrossTrap",
    parts: { cast: zone("ice/ice-spikes", 0.7, 1.4, 8, 1, { anchor: "bbox", maxHalf: [64, 50], ground: true, intro: { from: 0.25, to: 0.7, frames: 6 }, outro: { from: 1.45, to: 2.6, frames: 12 } }) },
  },
  // 후방 구르기: 떠난 자리에 연막이 퍼진다
  { group: "7_cross", name: "CrossRoll", parts: { cast: oneShot("explosions/smoke-bomb", 1.0, 3.6, 12, 2.2, { anchor: "bbox", fade: 0.35, maxHalf: [60, 44] }) } },

  // 8 총잡이
  {
    group: "8_gun", name: "GunSpray",
    parts: {
      // 난사: 달궈진 주황 탄, 연사 총구 불꽃, 맞은 자리에서 채찍처럼 탁 터지는 섬광(총 기본 공격과 다른 그림)
      projectile: projectile("local/spray-round", 0.2, 6, 1, [20, 6], {}),
      // 연사 총구 불꽃은 총구가 발밑에서 26픽셀 위에 있고 한 발이 아주 짧아, 첫 발만 느리게 잘라 쓴다
      muzzle: oneShot("projectiles/muzzle-smg", 0, 0.06, 4, 0.25, { anchor: [0, -26], fade: 0.3, maxHalf: [30, 14] }),
      impact: oneShot("melee/whip-crack", 0.3, 0.85, 7, 1, { anchor: "bbox", maxHalf: [28, 20] }),
    },
    meta: { impact_dir: "1", muzzle_dir: "1" },
  },
  // 속사 태세: 몸에 번개가 모여 터진다
  { group: "8_gun", name: "GunRapid", parts: { cast: oneShot("lightning/charge-up", 0.4, 2.5, 14, 1.8, { opts: { palette: "volt" }, anchor: "bbox", fade: 0.3, maxHalf: [64, 56] }) } },
  {
    group: "8_gun", name: "GunShotgun",
    parts: {
      projectile: projectile("local/buckshot", 0.2, 4, 1, [16, 8], {}),
      muzzle: oneShot("projectiles/muzzle-shotgun", 0, 0.35, 6, 1, { anchor: "left", fade: 0.4, maxHalf: [36, 20] }),
    },
    meta: { muzzle_dir: "1" },
  },
  // 수류탄: 도화선이 타는 둥근 수류탄이 굴러가고, 맞으면 섬광과 파편
  {
    group: "8_gun", name: "GunGrenade",
    parts: {
      projectile: { id: "local/grenade-shell", from: 0, to: 0.3, frames: 6, speed: 1, anchor: "bbox" },
      impact: oneShot("explosions/grenade", 1.63, 3.3, 12, 1.2, { anchor: "bbox", fade: 0.4, maxHalf: [72, 56] }),
    },
    meta: { proj_spin: "540" },
  },
  // 화약 지대: 불씨가 번득이는 검은 화약 웅덩이
  {
    group: "8_gun", name: "GunPowder",
    parts: {
      cast: zone("nature/toxic-puddle", 0, 4, 18, 1.4, {
        opts: { palette: ["#06070a", "#100a08", "#1a100a", "#2a1408", "#4a1c0a", "#a8401a", "#ff8a2a", "#ffe08a"], sheen: ["#3a1408", "#7a2a10", "#c85a1a", "#ffb040"], width: 56 },
        anchor: "bbox", ground: true, intro: Grow, outro: Dissolve,
      }),
    },
  },
  // 구르며 쏘기: 구르기 시작 자리에서 탄이 튕겨 불꽃이 흩어진다
  { group: "8_gun", name: "GunRoll", parts: { cast: oneShot("impacts/ricochet", 0, 0.8, 8, 1, { anchor: "bbox", fade: 0.35, maxHalf: [48, 24] }) }, meta: { cast_dir: "1" } },
  // 레일건: 날아가는 그림은 숨기고 총구에서 사거리(속도 40 x 0.45초 = 18유닛 = 576픽셀)만큼 빛줄기를 깐다
  {
    group: "8_gun", name: "GunRail",
    parts: {
      trail: { id: "local/rail-beam", from: 0, to: 0.55, frames: 12, speed: 1, anchor: "bbox" },
    },
    meta: { proj_hidden: "1" },
  },

  // 9 주술사
  {
    group: "9_wand", name: "WandCurse",
    parts: {
      // 저주탄: 소용돌이치는 작은 공허 구슬(지팡이·완드 기본 공격의 마력탄과 다른 그림)
      projectile: { id: "shadow/void-orb", from: 0, to: 1.6, frames: 10, speed: 2, anchor: "bbox", maxHalf: [20, 20] },
      // 맞은 자리: 검은 별이 박히며 검보랏빛으로 터진다
      impact: oneShot("shadow/dark-explosion", 0.3, 2.2, 12, 1.6, { anchor: "bbox", fade: 0.4, maxHalf: [56, 56] }),
    },
  },
  // 블랙홀: 기울어진 강착 원반이 구멍 앞뒤로 휘어 감기는 입체 블랙홀. 커지며 나타나 오그라들며 사라진다.
  { group: "9_wand", name: "WandHex", parts: { cast: zone("shadow/black-hole", 0, 4, 20, 1.6, { anchor: "bbox", ground: true, intro: { mode: "grow", time: 0.5 }, outro: { mode: "shrink", time: 0.45 } }) } },
  // 독 구름: 한 뭉치에서 뭉게뭉게 퍼지고(0~1.2초), 숨쉬다가(1.2~3.2초 반복), 오그라들며 흩어진다(3.2~4초)
  {
    group: "9_wand", name: "WandMiasma",
    parts: { cast: zone("local/toxic-cloud", 1.2, 3.2, 16, 1, { anchor: "origin", ground: true, intro: { from: 0, to: 1.2, frames: 14 }, outro: { from: 3.2, to: 4.0, frames: 10 } }) },
  },
  {
    group: "9_wand", name: "WandVenom",
    parts: {
      projectile: projectile("local/venom-glob", 0.4, 8, 1, [24, 10], {}),
      impact: oneShot("nature/acid-splash", 0.25, 2.4, 12, 1.6, { anchor: "origin" }),
    },
  },
  {
    group: "9_wand", name: "WandSpirit",
    parts: { trail: { id: "local/spirit-trail", from: 0, to: 0.75, frames: 10, speed: 1, trail: true }, impact: oneShot("shadow/shadow-tendrils", 0.1, 2.8, 14, 2, { anchor: "origin" }) },
  },
  { group: "9_wand", name: "WandDrain", parts: { cast: oneShot("explosions/implosion", 0, 1.5, 14, 1.4, { anchor: "bbox", maxHalf: [52, 52] }) } },

  // 10 사제
  { group: "10_priest", name: "PriestBless", parts: { cast: pulse("holy/blessing", 0, 3, 14, 3, { anchor: "origin", square: true }) } },
  {
    group: "10_priest", name: "PriestBolt",
    parts: {
      projectile: { id: "holy/sunburst", from: 0, to: 4, frames: 8, speed: 4, anchor: "bbox", maxHalf: [18, 18] },
      impact: oneShot("explosions/plasma-explosion", 0, 0.8, 9, 1.3, { opts: { palette: "holy", accent: HolyAccent }, anchor: "bbox", maxHalf: [48, 48] }),
    },
  },
  { group: "10_priest", name: "PriestHeal", parts: { cast: oneShot("holy/heal-aoe", 0.2, 2.6, 14, 2, { anchor: "origin", ground: true }) } },
  { group: "10_priest", name: "PriestJudge", parts: { cast: oneShot("holy/smite", 0.3, 1.8, 14, 1.4, { anchor: "origin" }) } },
  { group: "10_priest", name: "PriestRevive", parts: { cast: oneShot("holy/feather-burst", 0, 2.2, 14, 1.8, { anchor: "bbox" }) } },
  { group: "10_priest", name: "PriestSanctum", parts: { cast: zone("arcane/rune-circle", 0, 4, 16, 1, { opts: { palette: "holy" }, anchor: "origin", ground: true, intro: { mode: "grow", time: 0.5 }, outro: Dissolve }) } },

  // 무기 기본 공격(WeaponDefinition.vfx). 근접은 cast(휘두르기), 원거리는 projectile·muzzle·impact
  { group: "0_weapon", name: "Weapon_Sword", bind: { kind: -1 }, parts: { cast: oneShot("melee/sword-slash", 0.03, 0.4, 8, 1.3, { opts: { palette: White, sparks: WhiteSparks, radius: 34 }, anchor: "axis", fade: 0.4 }) } },
  { group: "0_weapon", name: "Weapon_Spear", bind: { kind: -1 }, parts: { cast: oneShot("melee/thrust", 0.03, 0.4, 8, 1.3, { opts: { palette: White, reach: 76 }, anchor: "axis", fade: 0.4, maxHalf: [96, 40] }) } },
  { group: "0_weapon", name: "Weapon_Axe", bind: { kind: -1 }, parts: { cast: oneShot("melee/sword-slash", 0.03, 0.42, 8, 1.2, { opts: { palette: "ember", radius: 42, seed: 5 }, anchor: "axis", fade: 0.4 }) } },
  { group: "0_weapon", name: "Weapon_Blunt", bind: { kind: -1 }, parts: { cast: oneShot("melee/sword-slash", 0.03, 0.42, 8, 1.2, { opts: { palette: "stone", radius: 34, seed: 7 }, anchor: "axis", fade: 0.4 }) } },
  { group: "0_weapon", name: "Weapon_Bow", bind: { kind: -1 }, parts: { projectile: arrow(), impact: hitSpark(undefined) }, meta: { impact_dir: "1" } },
  { group: "0_weapon", name: "Weapon_Crossbow", bind: { kind: -1 }, parts: { projectile: arrow(SteelArrow, 4), impact: hitSpark("metal") }, meta: { impact_dir: "1" } },
  {
    group: "0_weapon", name: "Weapon_Staff", bind: { kind: -1 },
    // 기본 공격: 작은 파란 마력탄, 맞으면 작은 불꽃만 튄다(큰 구슬·폭발은 스킬 몫)
    parts: {
      projectile: projectile("projectiles/plasma-shot", 0.8, 8, 1, [11, 6], { anchor: [-5, 0], opts: { palette: "electric", size: 3, seed: 3, light: 0.35 } }),
      impact: magicSpark("electric"),
    },
    meta: { impact_dir: "1" },
  },
  {
    group: "0_weapon", name: "Weapon_Gun", bind: { kind: -1 },
    parts: {
      projectile: projectile("projectiles/bullet-tracer", 0.6, 4, 1, [14, 4], { opts: { length: 12, seed: 2 } }),
      muzzle: oneShot("projectiles/muzzle-pistol", 0, 0.25, 4, 1, { anchor: "left", fade: 0.4, maxHalf: [16, 12] }),
      impact: oneShot("impacts/bullet-impact", 0, 0.35, 5, 1, { anchor: "right", maxHalf: [20, 14] }),
    },
    meta: { impact_dir: "1", muzzle_dir: "1" },
  },
  {
    group: "0_weapon", name: "Weapon_Wand", bind: { kind: -1 },
    parts: {
      // 기본 공격: 작은 보라 마력탄, 맞으면 작은 불꽃만 튄다
      projectile: projectile("projectiles/plasma-shot", 0.8, 8, 1, [12, 7], { anchor: [-5, 0], opts: { palette: "arcane", size: 3, seed: 6, light: 0.35 } }),
      impact: magicSpark("arcane"),
    },
    meta: { impact_dir: "1" },
  },

  // 전사 무기 강화 평타(WeaponDefinition.powerVfx): 날아가는 검기와 맞은 자리 불꽃. 그림 픽셀 그대로 그린다.
  { group: "0_weapon", name: "Power_Sword", bind: { kind: -1 }, parts: { projectile: projectile("local/sword-wave", 0.4, 6, 1, [22, 26], {}), impact: hitSpark(undefined) }, meta: { impact_dir: "1" } },
  { group: "0_weapon", name: "Power_Spear", bind: { kind: -1 }, parts: { projectile: projectile("local/sword-wave", 0.4, 6, 1, [18, 20], { opts: { palette: ["#3a2a10", "#6a4a18", "#a87a24", "#e0b040", "#ffe08a", "#fff6d0", "#ffffff"], height: 15, thick: 5, seed: 4 } }), impact: hitSpark(undefined) }, meta: { impact_dir: "1" } },
  { group: "0_weapon", name: "Power_Axe", bind: { kind: -1 }, parts: { projectile: projectile("local/sword-wave", 0.4, 6, 1, [24, 28], { opts: { palette: "ember", height: 22, thick: 8, seed: 5 } }), impact: hitSpark("ember") }, meta: { impact_dir: "1" } },
  { group: "0_weapon", name: "Power_Blunt", bind: { kind: -1 }, parts: { projectile: projectile("local/sword-wave", 0.4, 6, 1, [24, 28], { opts: { palette: "stone", height: 22, thick: 8, seed: 7 } }), impact: hitSpark("metal") }, meta: { impact_dir: "1" } },

  // 마법사 무기 강화 평타(WeaponDefinition.powerVfx): 큰 마력 구슬과 폭발(반경은 bind의 explosionRadius = powerRadius)
  {
    group: "0_weapon", name: "Power_Staff", bind: { kind: -1, explosionRadius: 1.8 },
    parts: {
      projectile: projectile("local/power-orb", 0.5, 8, 1, [40, 24], {}),
      impact: oneShot("explosions/plasma-explosion", 0, 0.8, 10, 1.3, { opts: { accent: ["#0a1838", "#1a3c8c", "#3a7ce0", "#8cc8ff", "#e0f4ff"] }, anchor: "bbox", maxHalf: [64, 64] }),
    },
  },
  {
    group: "0_weapon", name: "Power_Wand", bind: { kind: -1, explosionRadius: 1.5 },
    parts: {
      projectile: projectile("local/power-orb", 0.5, 8, 1, [38, 22], { opts: { palette: ["#120818", "#2a1240", "#4a1e70", "#7a34b0", "#a85ae0", "#d08cff", "#f0d4ff", "#ffffff"], size: 8, seed: 3 } }),
      // plasma-explosion은 palette(7색 배열)와 accent로 색을 정한다("arcane" 이름은 먹지 않아 흰·분홍으로 보였다)
      impact: oneShot("explosions/plasma-explosion", 0, 0.8, 10, 1.3, { opts: { palette: ["#120818", "#2e1250", "#5a2496", "#8a44d0", "#b880ff", "#e4ccff", "#ffffff"], accent: ["#1c0a30", "#4a1a8c", "#8a3ce0", "#c48cff", "#f0d8ff"] }, anchor: "bbox", maxHalf: [56, 56] }),
    },
  },

  // 차징 스킬(직업마다 하나). 크기는 다 모았을 때 기준이고, 덜 모으면 게임에서 작게 그린다.
  // 발도 일섬: 머리카락처럼 가는 섬광 선이 그어지고, 선을 따라 베인 자리가 차례로 터진다
  { group: "11_charge", name: "SwordIai", parts: { trail: { id: "local/iai-flash", from: 0, to: 0.6, frames: 12, speed: 1, trail: true } } },
  // 용창 투척: 불의 용이 감아 도는 창
  { group: "11_charge", name: "SpearDragon", parts: { projectile: projectile("local/dragon-lance", 0.5, 8, 1, [58, 16], {}) } },
  // 분쇄 일격: 내려찍는 순간 땅이 터지며 흙이 V자로 솟고 흙먼지가 피어오른다
  { group: "11_charge", name: "AxeCrush", parts: { cast: oneShot("explosions/dirt-explosion", 0, 1.8, 14, 1.3, { anchor: "origin", fade: 0.35, ground: true }) } },
  // 강궁: 연둣빛 바람 띠가 나선으로 감긴 굵은 화살
  { group: "11_charge", name: "BowPower", parts: { projectile: projectile("local/gale-arrow", 0.4, 8, 1, [42, 12], {}) } },
  {
    group: "11_charge", name: "CrossSnipe",
    parts: { projectile: projectile("projectiles/laser-bolt", 0.5, 8, 1, [40, 8], {}) },
  },
  // 메테오: 운석이 0.54초에 땅에 닿는다(SkillDefinition.impactDelay 0.55와 맞춘 재생 속도 1.15)
  { group: "11_charge", name: "StaffMeteor", parts: { cast: oneShot("fire/meteor", 0, 2.2, 20, 1.15, { anchor: "origin", fade: 0.3 }) } },
  // 영혼 폭발: 혼불이 날아가 맞은 자리에서 큰 영혼이 솟아 터지고 유령들이 사방으로 날아간다
  {
    group: "11_charge", name: "WandSoul",
    parts: {
      projectile: projectile("local/soul-wisp", 0.6, 9, 1, [30, 14], {}),
      impact: oneShot("local/soul-burst", 0, 1.6, 18, 1, { anchor: "origin", fade: 0.15 }),
    },
  },
  // 성광: 위에서 본 금빛 빛 고리, 도는 햇살, 솟는 빛기둥(서리 폭발과 다른 그림)
  { group: "11_charge", name: "PriestRadiance", parts: { cast: oneShot("local/holy-radiance", 0, 1.4, 16, 1.2, { anchor: "origin", fade: 0.2, ground: true }) } },

  // 몬스터 폭발(폭탄 버섯 등)·보스 내려찍기(SkillFxPlayer). 그림 픽셀 그대로 그린다.
  { group: "0_monster", name: "Monster_Explosion", bind: { kind: -1 }, parts: { impact: oneShot("explosions/small-explosion", 0, 1.0, 12, 1.2, { opts: { seed: 2 }, anchor: [0, -24], fade: 0.4, maxHalf: [64, 72] }) } },
  // 던전 공용: 다음 층 포탈(반복)과 특수방 순간이동(마법진에서 빛기둥이 솟는다)
  { group: "0_world", name: "World_Portal", bind: { kind: -1 }, parts: { impact: zone("arcane/portal", 0, 2.4, 16, 1, { anchor: "origin" }) } },
  { group: "0_world", name: "World_Teleport", bind: { kind: -1 }, parts: { impact: oneShot("arcane/magic-circle-cast", 1.0, 3.9, 16, 2.4, { anchor: "origin", fade: 0.3 }) } },
  { group: "0_monster", name: "Monster_BossImpact", bind: { kind: -1 }, parts: { impact: oneShot("impacts/ground-slam", 0, 1.0, 12, 1.1, { opts: { palette: "ember", seed: 8 }, anchor: "origin", fade: 0.35, maxHalf: [96, 80] }) } },
];
