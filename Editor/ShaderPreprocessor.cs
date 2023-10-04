using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using WaterSystem;
using WaterSystem.Rendering;
using WaterSystem.Settings;

#if UNITY_2022_1_OR_NEWER
// Simple example of stripping of a debug build configuration
class ShaderPreprocessor : IPreprocessShaders
{
    private bool _validRefCube;
    private bool _validRefProbe;
    private bool _validRefPlanar;
    private bool _validRefSSR;
    
    private bool _validSSRLow;
    private bool _validSSRMed;
    private bool _validSSRHigh;
    
    private bool _validShadowLow;
    private bool _validShadowMed;
    private bool _validShadowHigh;
    
    ShaderKeyword m_KeywordStructBuffer;

    private List<ValidKeywordPair> keywordPairs;

    public ShaderPreprocessor()
    {
        m_KeywordStructBuffer = new ShaderKeyword("USE_STRUCTURED_BUFFER");
    }

    // Multiple callback may be implemented.
    // The first one executed is the one where callbackOrder is returning the smallest number.
    public int callbackOrder { get { return 0; } }

    public void OnProcessShader(
        Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> shaderCompilerData)
    {
        // In development, don't strip debug variants
        if (!shader.name.Contains("Boat Attack/Water"))
            return;

        var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        var qualityLevels = QualitySettings.GetActiveQualityLevelsForPlatform(targetGroup.ToString());
        Debug.Log($"{shader.name} Stripping for {qualityLevels.Length} quality levels");
        
        //var refType = 0;
        foreach (var level in qualityLevels)
        {
            Debug.Log($"Stripping {shader.name} based on quality level {level}.");
            var settings = ProjectSettings.GetQualitySettings(level);
            
            // reflections
            _validRefCube |= settings.reflectionSettings.reflectionType == Data.ReflectionSettings.Type.Cubemap;
            _validRefProbe |= settings.reflectionSettings.reflectionType == Data.ReflectionSettings.Type.ReflectionProbe;
            _validRefPlanar |= settings.reflectionSettings.reflectionType == Data.ReflectionSettings.Type.PlanarReflection;
            _validRefSSR |= settings.reflectionSettings.reflectionType == Data.ReflectionSettings.Type.ScreenSpaceReflection;
            
            // ssr
            if(settings.reflectionSettings.reflectionType == Data.ReflectionSettings.Type.ScreenSpaceReflection)
            {
                _validSSRLow |= settings.reflectionSettings.ssrSettings.steps == Data.SsrSettings.Steps.Low;
                _validSSRMed |= settings.reflectionSettings.ssrSettings.steps == Data.SsrSettings.Steps.Medium;
                _validSSRHigh |= settings.reflectionSettings.ssrSettings.steps == Data.SsrSettings.Steps.High;
            }
            
            // shadows
            if (settings.lightingSettings.Mode == Data.LightingSettings.LightingMode.Volume)
            {
                _validShadowLow |= settings.lightingSettings.VolumeSamples == Data.LightingSettings.VolumeSample.Low;
                _validShadowMed |= settings.lightingSettings.VolumeSamples == Data.LightingSettings.VolumeSample.Medium;
                _validShadowHigh |= settings.lightingSettings.VolumeSamples == Data.LightingSettings.VolumeSample.High;
            }
        }
        
        for (int i = 0; i < shaderCompilerData.Count; ++i)
        {
            var strip = false;
            // reflection types
            strip |= StripData(ref shaderCompilerData, ref i, Utilities.ShaderKeywords.GetReflectionKeyword(Data.ReflectionSettings.Type.Cubemap), _validRefCube);
            strip |= StripData(ref shaderCompilerData, ref i, Utilities.ShaderKeywords.GetReflectionKeyword(Data.ReflectionSettings.Type.ReflectionProbe), _validRefProbe);
            strip |= StripData(ref shaderCompilerData, ref i, Utilities.ShaderKeywords.GetReflectionKeyword(Data.ReflectionSettings.Type.PlanarReflection), _validRefPlanar);
            strip |= StripData(ref shaderCompilerData, ref i, Utilities.ShaderKeywords.GetReflectionKeyword(Data.ReflectionSettings.Type.ScreenSpaceReflection), _validRefSSR);
            
            // ssr
            strip |= StripData(ref shaderCompilerData, ref i, Utilities.ShaderKeywords.GetSsrKeyword(Data.SsrSettings.Steps.Low), _validRefSSR && _validSSRLow);
            strip |= StripData(ref shaderCompilerData, ref i, Utilities.ShaderKeywords.GetSsrKeyword(Data.SsrSettings.Steps.Medium), _validRefSSR && _validSSRMed);
            strip |= StripData(ref shaderCompilerData, ref i, Utilities.ShaderKeywords.GetSsrKeyword(Data.SsrSettings.Steps.High), _validRefSSR && _validSSRHigh);

            // shadows
            strip |= StripData(ref shaderCompilerData, ref i, Utilities.ShaderKeywords.KeyShadowsLow, _validShadowLow);
            strip |= StripData(ref shaderCompilerData, ref i, Utilities.ShaderKeywords.KeyShadowsMedium, _validShadowMed);
            strip |= StripData(ref shaderCompilerData, ref i, Utilities.ShaderKeywords.KeyShadowsHigh, _validShadowHigh);
            
            if (!strip) continue;
            
            shaderCompilerData.RemoveAt(i);
            --i;
        }
    }

    private bool StripData(ref IList<ShaderCompilerData> data, ref int index, GlobalKeyword keyword, bool valid)
    {
        return data[index].shaderKeywordSet.IsEnabled(keyword) && !valid;
    }

    private class ValidKeywordPair
    {
        public ShaderKeyword keyword;
        public bool valid;
        public Action test;
    }
}
#endif