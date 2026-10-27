using System;
using System.IO;
using System.Linq;
using AssemblyZero.Domain.EP04;
using AssemblyZero.Unity.EP04;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AssemblyZero.Unity.Editor
{
    public static class HardwareSceneBuilder
    {
        public const string Root="Assets/_AssemblyZero/Hardware";
        public const string ScenePath="Assets/_AssemblyZero/Scenes/Hardware_Business.unity";
        [MenuItem("Tools/Assembly Zero/Create Hardware Business Slice")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before creating assets.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save current scene changes first.");
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var config=AssetDatabase.LoadAssetAtPath<EP04Config>(Root+"/Economy.asset");
            if(config==null){config=ScriptableObject.CreateInstance<EP04Config>();config.Economy=HardwareCatalog.Create();config.Font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_AssemblyZero/UI/AssemblyZeroChinese.ttf");AssetDatabase.CreateAsset(config,Root+"/Economy.asset");}
            if(config.BaseMaterial==null){config.BaseMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Hardware neutral",enableInstancing=true};AssetDatabase.CreateAsset(config.BaseMaterial,Root+"/Neutral.mat");EditorUtility.SetDirty(config);}
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Chinese SDF.asset");
            if(font==null)
            {
                font=TMP_FontAsset.CreateFontAsset(config.Font,48,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);font.name="Hardware Chinese SDF";
                AssetDatabase.CreateAsset(font,Root+"/Chinese SDF.asset");foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);AssetDatabase.AddObjectToAsset(font.material,font);
                var texts=string.Concat(Directory.GetFiles("Assets/_AssemblyZero/Runtime/EP04","Hardware*.cs").Select(File.ReadAllText))+string.Concat(Directory.GetFiles("Assets/_AssemblyZero/Domain/EP04","Hardware*.cs").Select(File.ReadAllText));font.TryAddCharacters(new string(texts.Distinct().ToArray()),out _);EditorUtility.SetDirty(font);
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Hardware Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=25;camera.nearClipPlane=.1f;camera.farClipPlane=400;camera.backgroundColor=new Color(.34f,.38f,.34f);camera.transform.rotation=Quaternion.Euler(55,0,0);
            var sun=new GameObject("Afternoon sun",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.4f;sun.color=new Color(1,.94f,.82f);sun.transform.rotation=Quaternion.Euler(52,-35,0);sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.69f,.65f);
            var game=new GameObject("Hardware business").AddComponent<HardwareGame>();game.Config=config;game.Font=font;game.ViewCamera=camera;
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("Hardware slice scene ready. Existing EP scenes were not regenerated.");
        }
        [MenuItem("Tools/Assembly Zero/Build Hardware Windows Player")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/Hardware");Directory.CreateDirectory("Reports/Hardware");
            var protectedFiles=new[]{"ProjectSettings/GraphicsSettings.asset","ProjectSettings/QualitySettings.asset","ProjectSettings/ProjectSettings.asset"};var bytes=protectedFiles.Select(File.ReadAllBytes).ToArray();
            var global=GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();var pipeline=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;
            var apis=PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);var auto=PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            var splash=PlayerSettings.SplashScreen.show;
            try
            {
                var compatible=AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>("Assets/_AssemblyZero/EP04/EP04RenderSettings.asset");if(compatible==null)compatible=RenderPipelineGlobalSettingsUtils.Create(global.GetType(),Root+"/GlobalRenderSettings.asset");
                var current=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_AssemblyZero/EP04/EP04Pipeline.asset")??(UniversalRenderPipelineAsset)(quality??pipeline);
                UnityEditor.Rendering.EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(compatible);GraphicsSettings.defaultRenderPipeline=current;QualitySettings.renderPipeline=current;
                // The cached splash background crashes in DrawSplashScreenBackground on this machine.
                // Skip it for this player only; preserve the legacy project's splash setting.
                PlayerSettings.SplashScreen.show=false;
                // Conservative graphics baseline for this build only; restore project settings afterwards.
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});AssetDatabase.SaveAssets();
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Hardware/AssemblyZeroHardware.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                File.WriteAllText("Reports/Hardware/Build.txt","Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nTime: "+report.summary.totalTime+"\n"+string.Join("\n",report.steps.SelectMany(s=>s.messages).Where(m=>m.type!=LogType.Log).Select(m=>m.type+": "+m.content)));
                if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Hardware build failed");
            }
            finally
            {
                PlayerSettings.SplashScreen.show=splash;
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,apis);PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,auto);GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=quality;UnityEditor.Rendering.EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(global);AssetDatabase.SaveAssets();
                // Replace the file rather than truncating it while Unity's importer memory-maps it.
                // Attempt every restore even if one fails; keep its temporary copy for recovery.
                var failures=new System.Collections.Generic.List<Exception>();
                for(int i=0;i<protectedFiles.Length;i++)try
                {
                    var temporary=protectedFiles[i]+".hardware-restore.tmp";File.WriteAllBytes(temporary,bytes[i]);File.Replace(temporary,protectedFiles[i],null);
                }
                catch(Exception error){failures.Add(error);}
                if(failures.Count>0)throw new AggregateException("Build finished but project settings restoration needs attention",failures);
            }
        }
        public static void Walkthrough(){Directory.CreateDirectory("Reports/Hardware/PlayabilityP0P1");File.WriteAllText("Reports/Hardware/PlayabilityP0P1/Walkthrough.txt",string.Join("\n",HardwareWalkthrough.RunOpening(),HardwareWalkthrough.RunExpansion(),HardwareWalkthrough.RunMarket(),HardwareWalkthrough.RunMake(),HardwareWalkthrough.RunRecovery()));}
        // Rewrites only the economy data in Economy.asset; the scene, fonts, materials and layouts are untouched.
        [MenuItem("Tools/Assembly Zero/Refresh Hardware Economy")]
        public static void RefreshEconomy()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before refreshing the economy.");
            var config=AssetDatabase.LoadAssetAtPath<EP04Config>(Root+"/Economy.asset");if(config==null)throw new InvalidOperationException("Missing "+Root+"/Economy.asset");
            Undo.RecordObject(config,"Refresh Hardware Economy");config.Economy=HardwareCatalog.Create();EditorUtility.SetDirty(config);
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Chinese SDF.asset");
            if(font!=null)
            {
                var texts=string.Concat(Directory.GetFiles("Assets/_AssemblyZero/Runtime/EP04","Hardware*.cs").Select(File.ReadAllText))+string.Concat(Directory.GetFiles("Assets/_AssemblyZero/Domain/EP04","Hardware*.cs").Select(File.ReadAllText));
                font.TryAddCharacters(new string(texts.Distinct().ToArray()),out _);EditorUtility.SetDirty(font);
            }
            AssetDatabase.SaveAssets();Debug.Log("Hardware economy refreshed to content version "+HardwareCatalog.ContentVersion+". Scene and layouts were not regenerated.");
        }
    }
}
