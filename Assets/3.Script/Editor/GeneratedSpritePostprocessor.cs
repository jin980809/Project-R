using UnityEditor;
using UnityEngine;

// Tools/MonsterGen으로 만든 스프라이트의 임포트 설정을 통일한다.
// 몬스터 프레임은 발밑(아래에서 8픽셀)을 피벗으로 잡아 Y 정렬과 위치 계산이 맞도록 한다.
public class GeneratedSpritePostprocessor : AssetPostprocessor
{
    private const string MonsterRoot = "Assets/4.Sprite/Monster/";
    private const string FxRoot = "Assets/4.Sprite/Fx/";
    private const string DungeonRoot = "Assets/4.Sprite/Dungeon/";
    private const string VillageRoot = "Assets/4.Sprite/Village/";
    private const float BaselinePixels = 8f;
    // 던전 장애물(Prop_)은 그림자 가운데(아래에서 14픽셀)를 피벗으로 해 Y 정렬이 발밑 기준이 되게 한다.
    private const float PropBaselinePixels = 14f;

    private void OnPreprocessTexture()
    {
        bool isMonster = assetPath.StartsWith(MonsterRoot);
        bool isFx = assetPath.StartsWith(FxRoot);
        // 던전과 마을 그림은 같은 규칙(Prop_ 발밑 피벗, 바닥·벽·잔디·울타리는 이어 붙임)을 쓴다.
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
            if (isMonster)
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
            importer.SetTextureSettings(settings);
        }
    }
}
