using UnityEditor;
using UnityEngine;

public static class LOTOGeneratorFbxImportConfigurator
{
    private const string GeneratorAssetPath = "Assets/FBX_inports/generator_unity_ar_ready.fbx";

    [MenuItem("LOTO/Configure Generator FBX Import")]
    public static void ConfigureGeneratorImport()
    {
        ModelImporter importer = AssetImporter.GetAtPath(GeneratorAssetPath) as ModelImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog(
                "Generator FBX Not Found",
                $"Could not find a ModelImporter at {GeneratorAssetPath}.",
                "OK");
            return;
        }

        importer.importAnimation = true;
        importer.importBlendShapes = true;
        importer.animationType = ModelImporterAnimationType.Generic;

        string takeName = importer.defaultClipAnimations != null && importer.defaultClipAnimations.Length > 0
            ? importer.defaultClipAnimations[0].takeName
            : string.Empty;

        importer.clipAnimations = new[]
        {
            CreateClip("Door_Open", takeName, 1f, 90f),
            CreateClip("Generator_Shutdown", takeName, 100f, 165f),
            CreateClip("Cable_Baked_Shutdown_Wiggle_BlendShapes", takeName, 100f, 165f),
            CreateClip("SwitchBox_Door_Unlock_And_Open", takeName, 170f, 230f),
            CreateClip("MainPower_Handle_Toggle", takeName, 240f, 280f)
        };

        importer.SaveAndReimport();
        AssetDatabase.Refresh();

        Debug.Log("Configured LOTO generator FBX import settings and animation clips.");
    }

    private static ModelImporterClipAnimation CreateClip(string clipName, string takeName, float firstFrame, float lastFrame)
    {
        return new ModelImporterClipAnimation
        {
            name = clipName,
            takeName = takeName,
            firstFrame = firstFrame,
            lastFrame = lastFrame,
            loopTime = false,
            loopPose = false,
            wrapMode = WrapMode.Once
        };
    }
}
