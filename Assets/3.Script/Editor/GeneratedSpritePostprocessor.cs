using UnityEditor;
using UnityEngine;

// Tools/MonsterGen으로 만든 스프라이트의 임포트 설정을 통일한다.
// 몬스터 프레임은 발밑(아래에서 8픽셀)을 피벗으로 잡아 Y 정렬과 위치 계산이 맞도록 한다.
public class GeneratedSpritePostprocessor : AssetPostprocessor
{
    private const string MonsterRoot = "Assets/4.Sprite/Monster/";
    private const string FxRoot = "Assets/4.Sprite/Fx/";
    private const float BaselinePixels = 8f;

    private void OnPreprocessTexture()
    {
        bool isMonster = assetPath.StartsWith(MonsterRoot);
        bool isFx = assetPath.StartsWith(FxRoot);
        if (isMonster || isFx)
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
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
            }
            importer.SetTextureSettings(settings);
        }
    }
}
