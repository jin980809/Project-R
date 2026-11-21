using UnityEditor;
using UnityEngine;

// Tools/MonsterGen, Tools/ArtGen으로 만든 스프라이트의 임포트 설정을 통일한다.
// 몬스터 프레임은 발밑(아래에서 8픽셀)을 피벗으로 잡아 Y 정렬과 위치 계산이 맞도록 한다.
public class GeneratedSpritePostprocessor : AssetPostprocessor
{
    private const string MonsterRoot = "Assets/4.Sprite/Monster/";
    private const string FxRoot = "Assets/4.Sprite/Fx/";
    private const string SkillVfxRoot = "Assets/4.Sprite/Fx/SkillVfx/";
    // 스킬 아이콘(Tools/ArtGen/SkillIcons, 24픽셀): 점 필터로 2배 확대해 쓴다.
    private const string SkillIconRoot = "Assets/4.Sprite/Fx/SkillIcon/";
    private const string StatusIconRoot = "Assets/4.Sprite/Fx/StatusIcon/";
    private const string DungeonRoot = "Assets/4.Sprite/Dungeon/";
    private const string VillageRoot = "Assets/4.Sprite/Village/";
    // 픽셀아트 마을: 32픽셀 = 1유닛, 점 필터, 무압축. 바닥·겹침 애니메이션은 가운데, 나머지는 발밑(아래에서 3픽셀) 피벗.
    private const string VillagePxRoot = "Assets/4.Sprite/VillagePx/";
    // 픽셀아트 던전: 마을과 같은 규칙. 방 그림·문·계단 바닥 표시·횃불은 가운데, 북쪽 아치(Gate)는 아래 가운데.
    private const string DungeonPxRoot = "Assets/4.Sprite/DungeonPx/";
    private const float VillagePxPixelsPerUnit = 32f;
    private const float VillagePxBaselinePixels = 3f;
    // 주민 의상: 패키지 부위처럼 100픽셀 = 1유닛, 가운데 피벗.
    private const string CostumeRoot = "Assets/4.Sprite/Costume/";
    // 말풍선(Bubble)은 9-slice 테두리를 준다.
    private const string NpcRoot = "Assets/4.Sprite/Npc/";
    private const float BaselinePixels = 8f;
    // 던전 장애물(Prop_)은 그림자 가운데(아래에서 14픽셀)를 피벗으로 해 Y 정렬이 발밑 기준이 되게 한다.
    private const float PropBaselinePixels = 14f;

    private void OnPreprocessTexture()
    {
        if (assetPath.StartsWith(VillagePxRoot) || assetPath.StartsWith(DungeonPxRoot))
        {
            ImportVillagePx();
            return;
        }
        bool isMonster = assetPath.StartsWith(MonsterRoot) || assetPath.StartsWith(NpcRoot);
        bool isFx = assetPath.StartsWith(FxRoot) || assetPath.StartsWith(CostumeRoot);
        // 던전과 예전 마을 그림은 같은 규칙(Prop_ 발밑 피벗, 바닥·벽·잔디·울타리는 이어 붙임)을 쓴다.
        bool isDungeon = assetPath.StartsWith(DungeonRoot) || assetPath.StartsWith(VillageRoot);
        if (isMonster || isFx || isDungeon)
        {
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (isMonster && System.IO.Path.GetFileName(assetPath).StartsWith("Bubble"))
            {
                // 꼬리(왼쪽 아래)는 테두리 안에 있어 늘어나지 않는다.
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spriteBorder = new Vector4(34f, 30f, 22f, 22f);
            }
            else if (isMonster)
            {
                int width;
                int height;
                importer.GetSourceTextureWidthAndHeight(out width, out height);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, BaselinePixels / height);
            }
            else if (isDungeon)
            {
                string fileName = System.IO.Path.GetFileName(assetPath);
                int width;
                int height;
                importer.GetSourceTextureWidthAndHeight(out width, out height);
                // 마을 바닥(1800x1000 네 장)은 압축해 메모리를 줄인다.
                if (fileName.StartsWith("Ground_V2"))
                {
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.maxTextureSize = 2048;
                }
                if (fileName.StartsWith("Prop_"))
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(0.5f, PropBaselinePixels / height);
                }
                else if (fileName.StartsWith("Gate"))
                {
                    // 북쪽 아치는 벽 밑동(아래 가장자리)에 세운다.
                    settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                }
                else
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Center;
                }
                // 바닥과 벽은 타일로 이어 붙인다.
                if (fileName.StartsWith("Floor") || fileName.StartsWith("Wall") || fileName.StartsWith("Ground_Grass") || fileName.StartsWith("Prop_Fence"))
                {
                    // settings가 importer 값을 덮어쓰므로 settings 쪽에 넣는다.
                    settings.wrapMode = TextureWrapMode.Repeat;
                }
            }
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
            }
            // 스킬별 컬러 이펙트 프레임(수백 장)은 압축해 메모리를 줄인다.
            if (assetPath.StartsWith(SkillVfxRoot))
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 512;
                // pixelfx로 내보낸 픽셀아트 프레임(Tools/PixelFxExport)은 점 필터로 픽셀을 살린다.
                // 돌진 길(trail_)도 이제 사거리만큼 픽셀 그대로 그린 그림이라 같은 점 필터를 쓴다.
                importer.filterMode = FilterMode.Point;
                settings.filterMode = FilterMode.Point;
            }
            if (assetPath.StartsWith(SkillIconRoot) || assetPath.StartsWith(StatusIconRoot))
            {
                importer.filterMode = FilterMode.Point;
                settings.filterMode = FilterMode.Point;
            }
            importer.SetTextureSettings(settings);
        }
    }

    private void ImportVillagePx()
    {
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = VillagePxPixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.filterMode = FilterMode.Point;
        string fileName = System.IO.Path.GetFileName(assetPath);
        if (fileName.StartsWith("Ground") || fileName.StartsWith("Anim_") || fileName.StartsWith("Room_") || fileName.StartsWith("Door")
            || fileName.StartsWith("Decal_") || fileName.StartsWith("Torch") || fileName.StartsWith("Fx_") || fileName.StartsWith("Wall_"))
        {
            settings.spriteAlignment = (int)SpriteAlignment.Center;
        }
        else if (fileName.StartsWith("Gate"))
        {
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        }
        else
        {
            int width;
            int height;
            importer.GetSourceTextureWidthAndHeight(out width, out height);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, VillagePxBaselinePixels / height);
        }
        // 울타리는 옆으로 이어 붙인다(Tiled 그리기).
        if (fileName.StartsWith("Fence"))
        {
            settings.wrapMode = TextureWrapMode.Repeat;
        }
        importer.SetTextureSettings(settings);
    }
}
