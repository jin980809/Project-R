# ArtGen (Python + pycairo)

Layer Lab 2D Minimal 패키지 샘플 맵 스타일(갈색 외곽선, 차분한 색, 위 밝은 띠·아래 어두운 띠, 3/4 시점)로
로비 마을 소품과 스킬 이펙트를 그린다. VillageGen/SkillFxGen(PowerShell) 결과를 대체하므로 그 스크립트는 다시 돌리지 않는다.

    pip install pycairo pillow numpy
    python village.py <출력폴더>   # Prop_*.png (크기·피벗 기존과 같음)
    python anims.py <출력폴더>     # Anim_*.png (탑 입구 소용돌이·횃불·깃발, 굴뚝 연기, 가로등 불빛)
    python fx.py <출력폴더>        # <Fx>_00..07.png (명암 그림, 게임에서 SkillFxGradient 셰이더가 스킬 색을 입힌다)

- 결과물 위치: Assets/4.Sprite/Village, Assets/4.Sprite/Fx/Skill
- Anim_* 겹침 위치(소품 그림 픽셀 좌표)는 overlays.py와 TowerBootstrap.BuildVillage의 AddPropLoop 호출을 맞춘다.
