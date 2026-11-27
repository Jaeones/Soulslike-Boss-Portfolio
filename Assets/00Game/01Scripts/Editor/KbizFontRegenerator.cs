using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class KbizFontRegenerator
{
    private const string TargetFontAssetPath = "Assets/00Game/03Art/Font/KBIZ한마음명조 B SDF.asset";
    private const string SourceFontPath = "Assets/00Game/03Art/Font/KBIZ한마음명조 B.ttf";
    private const string ScanRootPath = "Assets/00Game";
    private const int AtlasSize = 2048;
    private const int AtlasPadding = 9;
    private static readonly string[] IncludedExtensions = { ".unity", ".prefab", ".asset", ".cs", ".json", ".txt", ".md" };
    private static readonly Regex UnicodeEscapePattern = new Regex(@"\\u([0-9a-fA-F]{4})", RegexOptions.Compiled);

    [MenuItem("Tools/Fonts/Regenerate KBIZ SDF")]
    public static void RegenerateKbizSdfMenu()
    {
        RegenerateKbizSdfInternal(exitWhenDone: false);
    }

    public static void RegenerateKbizSdfBatchMode()
    {
        RegenerateKbizSdfInternal(exitWhenDone: Application.isBatchMode);
    }

    private static void RegenerateKbizSdfInternal(bool exitWhenDone)
    {
        try
        {
            TMP_FontAsset targetFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetFontAssetPath);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);

            if (targetFontAsset == null)
            {
                throw new InvalidOperationException($"Target font asset not found at '{TargetFontAssetPath}'.");
            }

            if (sourceFont == null)
            {
                throw new InvalidOperationException($"Source font not found at '{SourceFontPath}'.");
            }

            string characterSet = BuildCharacterSet(targetFontAsset);
            string fontGuid = AssetDatabase.AssetPathToGUID(SourceFontPath);

            ConfigureSourceFontReference(targetFontAsset, sourceFont, fontGuid);

            targetFontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            targetFontAsset.isMultiAtlasTexturesEnabled = true;
            ConfigureAtlasSettings(targetFontAsset);
            ConfigureCreationSettings(targetFontAsset, fontGuid, characterSet);

            targetFontAsset.ClearFontAssetData(true);

            targetFontAsset.TryAddCharacters(characterSet, out string missingCharacters, true);
            RefreshMaterial(targetFontAsset);

            EditorUtility.SetDirty(targetFontAsset);
            if (targetFontAsset.material != null)
            {
                EditorUtility.SetDirty(targetFontAsset.material);
            }

            foreach (Texture2D atlasTexture in targetFontAsset.atlasTextures)
            {
                if (atlasTexture != null)
                {
                    EditorUtility.SetDirty(atlasTexture);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TargetFontAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            int characterCount = targetFontAsset.characterTable != null ? targetFontAsset.characterTable.Count : 0;
            string summary = $"Regenerated '{targetFontAsset.name}' with {characterCount} glyph entries from {characterSet.Length} collected characters.";

            if (string.IsNullOrEmpty(missingCharacters))
            {
                Debug.Log(summary);
                ExitIfNeeded(exitWhenDone, 0);
                return;
            }

            string missingCodepoints = string.Join(", ", missingCharacters.Select(ch => $"U+{(int)ch:X4}"));
            Debug.LogWarning($"{summary} Missing characters: {missingCodepoints}");
            ExitIfNeeded(exitWhenDone, 1);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to regenerate KBIZ font asset. {exception}");
            ExitIfNeeded(exitWhenDone, 1);
        }
    }

    private static void ConfigureSourceFontReference(TMP_FontAsset targetFontAsset, Font sourceFont, string fontGuid)
    {
        SerializedObject serializedFontAsset = new SerializedObject(targetFontAsset);
        serializedFontAsset.FindProperty("m_SourceFontFileGUID").stringValue = fontGuid;

        SerializedProperty sourceFontEditorRefProperty = serializedFontAsset.FindProperty("m_SourceFontFile_EditorRef");
        if (sourceFontEditorRefProperty != null)
        {
            sourceFontEditorRefProperty.objectReferenceValue = sourceFont;
        }

        SerializedProperty sourceFontProperty = serializedFontAsset.FindProperty("m_SourceFontFile");
        if (sourceFontProperty != null)
        {
            sourceFontProperty.objectReferenceValue = sourceFont;
        }

        serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAtlasSettings(TMP_FontAsset targetFontAsset)
    {
        SerializedObject serializedFontAsset = new SerializedObject(targetFontAsset);
        serializedFontAsset.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        serializedFontAsset.FindProperty("m_AtlasWidth").intValue = AtlasSize;
        serializedFontAsset.FindProperty("m_AtlasHeight").intValue = AtlasSize;
        serializedFontAsset.FindProperty("m_AtlasPadding").intValue = AtlasPadding;
        serializedFontAsset.FindProperty("m_AtlasRenderMode").intValue = (int)GlyphRenderMode.SDFAA;
        serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCreationSettings(TMP_FontAsset targetFontAsset, string fontGuid, string characterSet)
    {
        FontAssetCreationSettings creationSettings = targetFontAsset.creationSettings;
        creationSettings.sourceFontFileGUID = fontGuid;
        creationSettings.pointSize = 90;
        creationSettings.pointSizeSamplingMode = 0;
        creationSettings.padding = AtlasPadding;
        creationSettings.packingMode = 0;
        creationSettings.atlasWidth = AtlasSize;
        creationSettings.atlasHeight = AtlasSize;
        creationSettings.characterSetSelectionMode = 7;
        creationSettings.characterSequence = characterSet;
        creationSettings.renderMode = (int)GlyphRenderMode.SDFAA;
        creationSettings.includeFontFeatures = true;
        targetFontAsset.creationSettings = creationSettings;
    }

    private static void RefreshMaterial(TMP_FontAsset targetFontAsset)
    {
        if (targetFontAsset.material == null || targetFontAsset.atlasTexture == null)
        {
            return;
        }

        Material material = targetFontAsset.material;
        material.SetTexture(ShaderUtilities.ID_MainTex, targetFontAsset.atlasTexture);
        material.SetFloat(ShaderUtilities.ID_TextureWidth, targetFontAsset.atlasWidth);
        material.SetFloat(ShaderUtilities.ID_TextureHeight, targetFontAsset.atlasHeight);
        material.SetFloat(ShaderUtilities.ID_GradientScale, targetFontAsset.atlasPadding + 1);
        material.SetFloat(ShaderUtilities.ID_WeightNormal, targetFontAsset.normalStyle);
        material.SetFloat(ShaderUtilities.ID_WeightBold, targetFontAsset.boldStyle);
    }

    private static string BuildCharacterSet(TMP_FontAsset targetFontAsset)
    {
        SortedSet<int> characters = new SortedSet<int>();

        AddPrintableAscii(characters);
        AddExistingCharacters(characters, targetFontAsset);

        foreach (string assetPath in EnumerateScannedFiles())
        {
            AddCharactersFromFile(characters, assetPath);
        }

        return string.Concat(characters.Select(char.ConvertFromUtf32));
    }

    private static void AddPrintableAscii(ISet<int> characters)
    {
        for (int codePoint = 0x20; codePoint <= 0x7E; codePoint++)
        {
            characters.Add(codePoint);
        }
    }

    private static void AddExistingCharacters(ISet<int> characters, TMP_FontAsset targetFontAsset)
    {
        foreach (TMP_Character character in targetFontAsset.characterTable)
        {
            characters.Add((int)character.unicode);
        }
    }

    private static IEnumerable<string> EnumerateScannedFiles()
    {
        foreach (string extension in IncludedExtensions)
        {
            string filter = $"*{extension}";
            foreach (string fullPath in Directory.EnumerateFiles(ScanRootPath, filter, SearchOption.AllDirectories))
            {
                yield return fullPath.Replace('\\', '/');
            }
        }
    }

    private static void AddCharactersFromFile(ISet<int> characters, string assetPath)
    {
        byte[] bytes = File.ReadAllBytes(assetPath);
        if (bytes.Contains((byte)0))
        {
            return;
        }

        string content = Encoding.UTF8.GetString(bytes);
        foreach (char character in content)
        {
            if (!ShouldIncludeCharacter(character))
            {
                continue;
            }

            characters.Add(character);
        }

        foreach (Match match in UnicodeEscapePattern.Matches(content))
        {
            int codePoint = Convert.ToInt32(match.Groups[1].Value, 16);
            if (ShouldIncludeCharacter(codePoint))
            {
                characters.Add(codePoint);
            }
        }
    }

    private static bool ShouldIncludeCharacter(char character)
    {
        return ShouldIncludeCharacter((int)character);
    }

    private static bool ShouldIncludeCharacter(int codePoint)
    {
        if (codePoint < 0x20 || codePoint == 0xFEFF || codePoint == 0xFFFD)
        {
            return false;
        }

        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0);
        switch (category)
        {
            case UnicodeCategory.Control:
            case UnicodeCategory.Format:
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.SpacingCombiningMark:
            case UnicodeCategory.EnclosingMark:
            case UnicodeCategory.PrivateUse:
            case UnicodeCategory.Surrogate:
            case UnicodeCategory.OtherNotAssigned:
                return false;
        }

        if (codePoint <= 0x024F)
        {
            return true;
        }

        if (IsHangulCodePoint(codePoint))
        {
            return true;
        }

        if ((codePoint >= 0x2000 && codePoint <= 0x206F) || (codePoint >= 0x3000 && codePoint <= 0x303F))
        {
            return category == UnicodeCategory.SpaceSeparator
                || category == UnicodeCategory.ConnectorPunctuation
                || category == UnicodeCategory.DashPunctuation
                || category == UnicodeCategory.OpenPunctuation
                || category == UnicodeCategory.ClosePunctuation
                || category == UnicodeCategory.InitialQuotePunctuation
                || category == UnicodeCategory.FinalQuotePunctuation
                || category == UnicodeCategory.OtherPunctuation
                || category == UnicodeCategory.MathSymbol
                || category == UnicodeCategory.CurrencySymbol
                || category == UnicodeCategory.ModifierSymbol
                || category == UnicodeCategory.OtherSymbol;
        }

        return false;
    }

    private static bool IsHangulCodePoint(int codePoint)
    {
        return (codePoint >= 0x1100 && codePoint <= 0x11FF)
            || (codePoint >= 0x3130 && codePoint <= 0x318F)
            || (codePoint >= 0xA960 && codePoint <= 0xA97F)
            || (codePoint >= 0xAC00 && codePoint <= 0xD7A3)
            || (codePoint >= 0xD7B0 && codePoint <= 0xD7FF);
    }

    private static void ExitIfNeeded(bool exitWhenDone, int exitCode)
    {
        if (!exitWhenDone)
        {
            return;
        }

        EditorApplication.Exit(exitCode);
    }
}
