using System;
using System.IO;
using AssemblyZero.Unity.EP04;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AssemblyZero.Unity.Editor
{
    public static class EP04SceneBuilder
    {
        public const string ScenePath="Assets/_AssemblyZero/Scenes/EP04_Business.unity";
        public const string ConfigPath="Assets/_AssemblyZero/EP04/Economy.asset";
        [MenuItem("Tools/Assembly Zero/Create EP04 Business Prototype")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            for(var i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save current scene edits before generating EP04.");
            Directory.CreateDirectory("Assets/_AssemblyZero/EP04");AssetDatabase.Refresh();
            var config=AssetDatabase.LoadAssetAtPath<EP04Config>(ConfigPath);
            if(config==null){config=ScriptableObject.CreateInstance<EP04Config>();config.Font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_AssemblyZero/UI/AssemblyZeroChinese.ttf");AssetDatabase.CreateAsset(config,ConfigPath);}
            PrepareMaterials(config);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cameraObject=new GameObject("EP04 Camera",typeof(Camera),typeof(AudioListener));cameraObject.tag="MainCamera";
            var camera=cameraObject.GetComponent<Camera>();camera.backgroundColor=new Color(.025f,.055f,.075f);camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.1f;camera.farClipPlane=400;
            var light=new GameObject("Sun",typeof(Light));light.transform.rotation=Quaternion.Euler(55,-25,0);light.GetComponent<Light>().type=LightType.Directional;light.GetComponent<Light>().intensity=1.3f;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.67f,.74f);
            var game=new GameObject("EP04 World").AddComponent<EP04Game>();game.Config=config;game.ViewCamera=camera;
            camera.transform.position=new Vector3(28,70,-15);camera.transform.rotation=Quaternion.Euler(62,0,0);camera.orthographic=true;camera.orthographicSize=27;
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("EP04 scene saved. Press Play to begin a new business. EP03 assets were not rebuilt.");
        }
        [MenuItem("Tools/Assembly Zero/Build EP04 Windows Player")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/EP04");Directory.CreateDirectory("Reports/EP04");
            var graphicsBytes=File.ReadAllBytes("ProjectSettings/GraphicsSettings.asset");var qualityBytes=File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            var originalGlobal=GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
            var originalDefault=GraphicsSettings.defaultRenderPipeline;var originalQuality=QualitySettings.renderPipeline;
            try
            {
                var config=AssetDatabase.LoadAssetAtPath<EP04Config>(ConfigPath);PrepareMaterials(config);
                const string settingsPath="Assets/_AssemblyZero/EP04/EP04RenderSettings.asset";
                var compatible=AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(settingsPath);
                if(compatible==null)compatible=RenderPipelineGlobalSettingsUtils.Create(originalGlobal.GetType(),settingsPath);
                const string pipelinePath="Assets/_AssemblyZero/EP04/EP04Pipeline.asset";
                var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
                if(pipeline==null){pipeline=UnityEngine.Object.Instantiate((UniversalRenderPipelineAsset)(originalQuality??originalDefault));AssetDatabase.CreateAsset(pipeline,pipelinePath);}
                UnityEditor.Rendering.EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(compatible);
                GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;AssetDatabase.SaveAssets();
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/EP04/AssemblyZeroEP04.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                File.WriteAllText("Reports/EP04/Build.txt","Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nBytes: "+report.summary.totalSize+"\nTime: "+report.summary.totalTime+"\nUses new settings initialized by the installed URP package. Original EP03 render settings restored after build.");
            }
            catch(Exception e){File.WriteAllText("Reports/EP04/Build.txt","Failed: "+e);throw;}
            finally
            {
                GraphicsSettings.defaultRenderPipeline=originalDefault;QualitySettings.renderPipeline=originalQuality;
                UnityEditor.Rendering.EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(originalGlobal);
                AssetDatabase.SaveAssets();File.WriteAllBytes("ProjectSettings/GraphicsSettings.asset",graphicsBytes);File.WriteAllBytes("ProjectSettings/QualitySettings.asset",qualityBytes);
            }
        }
        public static void PrepareMaterials(EP04Config config)
        {
            if(config.BaseMaterial==null)
            {
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="EP04 Base",enableInstancing=true};
                AssetDatabase.CreateAsset(material,"Assets/_AssemblyZero/EP04/Base.mat");config.BaseMaterial=material;EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
            }
        }
    }
}
