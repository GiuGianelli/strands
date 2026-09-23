using UnityEditor;

namespace Strands.Editor
{
    // Keep the original PNGs; limit imported texture memory for the Web build.
    public sealed class CardArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            bool menu = assetPath.StartsWith("Assets/Resources/Menu/");
            if (!menu && !assetPath.StartsWith("Assets/Resources/Cards/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = menu ? 2048 : 1024;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
