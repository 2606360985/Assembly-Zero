using UnityEditor;

namespace AssemblyZero.Unity.Editor
{
    public sealed class FounderArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.Contains("/Resources/Founder/Art/"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;
            importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.wrapMode=UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
