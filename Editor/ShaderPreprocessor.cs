using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using WaterSystem;

// Simple example of stripping of a debug build configuration
class ShaderPreprocessor : IPreprocessShaders
{
    ShaderKeyword m_KeywordRefCube;
    ShaderKeyword m_KeywordRefProbes;
    ShaderKeyword m_KeywordRefPlanar;
    ShaderKeyword m_KeywordRefSSR;
    
    ShaderKeyword m_KeywordRefSSR_LOW;
    ShaderKeyword m_KeywordRefSSR_MID;
    ShaderKeyword m_KeywordRefSSR_HIGH;
    
    ShaderKeyword m_KeywordStructBuffer;

    public ShaderPreprocessor()
    {
        m_KeywordRefCube = new ShaderKeyword("_REFLECTION_CUBEMAP");
        m_KeywordRefProbes = new ShaderKeyword("_REFLECTION_PROBES");
        m_KeywordRefPlanar = new ShaderKeyword("_REFLECTION_PLANARREFLECTION");
        m_KeywordRefSSR = new ShaderKeyword("_REFLECTION_SSR");
        
        m_KeywordRefSSR_LOW = new ShaderKeyword("_SSR_SAMPLES_LOW");
        m_KeywordRefSSR_MID = new ShaderKeyword("_SSR_SAMPLES_MEDIUM");
        m_KeywordRefSSR_HIGH = new ShaderKeyword("_SSR_SAMPLES_HIGH");
        
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


        var oceans = AssetDatabase.FindAssets($"t:{nameof(Ocean)}");

        var refType = 0;
        foreach (var oceanGuid in oceans)
        {
            //var ocean = AssetDatabase.LoadAssetAtPath<Ocean>(AssetDatabase.GUIDToAssetPath(oceanGuid));
            //refType |= (int)ocean.settingsData.refType;
        }

        /*
        for (int i = 0; i < shaderCompilerData.Count; ++i)
        {
            // remove cube
            if (shaderCompilerData[i].shaderKeywordSet.IsEnabled(m_KeywordRefCube))
            {
                shaderCompilerData.RemoveAt(i);
                --i;
            }
        }
        */
    }
}