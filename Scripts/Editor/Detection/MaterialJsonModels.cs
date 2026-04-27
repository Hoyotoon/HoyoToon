using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Utf8Json;
using HoyoToon.Editor.API;

namespace HoyoToon.Editor.Detection
{
    [Serializable]
    public sealed class MaterialJson
    {
        public ShaderData m_Shader = new ShaderData();
        public SavedProperties m_SavedProperties = new SavedProperties();
        public object m_ShaderKeywords;
        public object m_ValidKeywords;
        public object m_InvalidKeywords;
        public int? m_LightmapFlags;
        public object m_EnableInstancingVariants;
        public int? m_CustomRenderQueue;
        public List<string> m_DisabledShaderPasses = new List<string>();
        public Dictionary<string, string> m_StringTagMap = new Dictionary<string, string>();
        public Dictionary<string, string> stringTagMap = new Dictionary<string, string>();
        public string m_Name;
        public string Name;

        private MaterialDetectionContext detectionContext = MaterialDetectionContext.Empty;

        public MaterialDetectionContext DetectionContext => detectionContext;

        internal void RefreshDetectionContext()
        {
            detectionContext = MaterialDetectionContext.Create(this);
        }
    }

    [Serializable]
    public sealed class ShaderData
    {
        public string Name;
        public bool IsNull;
    }

    [Serializable]
    public sealed class SavedProperties
    {
        public Dictionary<string, TexEnv> m_TexEnvs = new Dictionary<string, TexEnv>();
        public Dictionary<string, float> m_Floats = new Dictionary<string, float>();
        public Dictionary<string, ColorData> m_Colors = new Dictionary<string, ColorData>();
        public Dictionary<string, int> m_Ints = new Dictionary<string, int>();
    }

    [Serializable]
    public sealed class TexEnv
    {
        public TextureRef m_Texture = new TextureRef();
        public Vector2Data m_Scale = new Vector2Data();
        public Vector2Data m_Offset = new Vector2Data();
    }

    [Serializable]
    public sealed class TextureRef
    {
        public long m_FileID;
        public long m_PathID;
        public string Name;
        public bool IsNull;
    }

    [Serializable]
    public sealed class Vector2Data
    {
        public float X;
        public float Y;
    }

    [Serializable]
    public sealed class ColorData
    {
        public float r;
        public float g;
        public float b;
        public float a;
        public string Hex;
    }

    public static class MaterialJsonParser
    {
        public static bool TryParse(string rawJson, out MaterialJson materialJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                materialJson = null;
                return false;
            }

            try
            {
                materialJson = JsonSerializer.Deserialize<MaterialJson>(rawJson, HoyoToonApi.JsonResolver);
                materialJson?.RefreshDetectionContext();
                return materialJson != null;
            }
            catch
            {
                materialJson = null;
                return false;
            }
        }
    }

    public sealed class MaterialDetectionContext
    {
        public static MaterialDetectionContext Empty { get; } = new MaterialDetectionContext(
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, List<MaterialDetectedValue>>(StringComparer.Ordinal));

        private readonly HashSet<string> indicators;
        private readonly Dictionary<string, List<MaterialDetectedValue>> valuesByProperty;

        private MaterialDetectionContext(
            HashSet<string> indicators,
            Dictionary<string, List<MaterialDetectedValue>> valuesByProperty)
        {
            this.indicators = indicators;
            this.valuesByProperty = valuesByProperty;
        }

        public bool ContainsIndicator(string token)
        {
            return !string.IsNullOrWhiteSpace(token) && indicators.Contains(token);
        }

        public bool MatchesCondition(string propertyName, string op, string expectedValue)
        {
            if (string.IsNullOrWhiteSpace(propertyName)
                || string.IsNullOrWhiteSpace(op)
                || string.IsNullOrWhiteSpace(expectedValue)
                || !valuesByProperty.TryGetValue(propertyName, out List<MaterialDetectedValue> values))
            {
                return false;
            }

            string normalizedExpectedValue = MaterialDetectedValue.NormalizeExpectedValue(expectedValue);
            foreach (MaterialDetectedValue value in values)
            {
                if (value.Matches(op, normalizedExpectedValue))
                {
                    return true;
                }
            }

            return false;
        }

        public static MaterialDetectionContext Create(MaterialJson materialJson)
        {
            if (materialJson == null)
            {
                return Empty;
            }

            var builder = new Builder();
            builder.RegisterMaterial(materialJson);
            return builder.Build();
        }

        private sealed class Builder
        {
            private readonly HashSet<string> indicators = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<MaterialDetectedValue>> valuesByProperty =
                new Dictionary<string, List<MaterialDetectedValue>>(StringComparer.Ordinal);

            internal MaterialDetectionContext Build()
            {
                return new MaterialDetectionContext(indicators, valuesByProperty);
            }

            internal void RegisterMaterial(MaterialJson material)
            {
                RegisterMaterialName(material);
                RegisterShader(material.m_Shader);
                RegisterSavedProperties(material.m_SavedProperties);
                RegisterStringMap(material.m_StringTagMap);
                RegisterStringMap(material.stringTagMap);
                RegisterKeywordSource(nameof(MaterialJson.m_ShaderKeywords), material.m_ShaderKeywords);
                RegisterKeywordSource(nameof(MaterialJson.m_ValidKeywords), material.m_ValidKeywords);
                RegisterKeywordSource(nameof(MaterialJson.m_InvalidKeywords), material.m_InvalidKeywords);
                RegisterStringList(nameof(MaterialJson.m_DisabledShaderPasses), material.m_DisabledShaderPasses);
                RegisterNullableNumber(nameof(MaterialJson.m_LightmapFlags), material.m_LightmapFlags);
                RegisterNullableNumber(nameof(MaterialJson.m_CustomRenderQueue), material.m_CustomRenderQueue);
                RegisterBooleanLike(nameof(MaterialJson.m_EnableInstancingVariants), material.m_EnableInstancingVariants);
            }

            private void RegisterMaterialName(MaterialJson material)
            {
                string materialName = !string.IsNullOrWhiteSpace(material.m_Name) ? material.m_Name : material.Name;
                if (string.IsNullOrWhiteSpace(materialName))
                {
                    return;
                }

                AddIndicator(materialName);
                AddValue(nameof(MaterialJson.m_Name), MaterialDetectedValue.FromString(materialName));
                AddValue(nameof(MaterialJson.Name), MaterialDetectedValue.FromString(materialName));
            }

            private void RegisterShader(ShaderData shader)
            {
                if (shader == null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(shader.Name))
                {
                    AddIndicator(shader.Name);
                    AddValue("m_Shader.Name", MaterialDetectedValue.FromString(shader.Name));
                }

                AddValue("m_Shader.IsNull", MaterialDetectedValue.FromBoolean(shader.IsNull));
            }

            private void RegisterSavedProperties(SavedProperties savedProperties)
            {
                if (savedProperties == null)
                {
                    return;
                }

                RegisterNumericMap(savedProperties.m_Floats, static value => value);
                RegisterNumericMap(savedProperties.m_Ints, static value => value);
                RegisterColorMap(savedProperties.m_Colors);
                RegisterTexEnvMap(savedProperties.m_TexEnvs);
            }

            private void RegisterNumericMap<TValue>(IDictionary<string, TValue> values, Func<TValue, double> selector)
            {
                if (values == null)
                {
                    return;
                }

                foreach ((string key, TValue value) in values)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    AddIndicator(key);
                    AddValue(key, MaterialDetectedValue.FromNumber(selector(value)));
                }
            }

            private void RegisterColorMap(IDictionary<string, ColorData> colors)
            {
                if (colors == null)
                {
                    return;
                }

                foreach ((string key, ColorData value) in colors)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    AddIndicator(key);
                    if (value == null)
                    {
                        continue;
                    }

                    AddValue($"{key}.r", MaterialDetectedValue.FromNumber(value.r));
                    AddValue($"{key}.g", MaterialDetectedValue.FromNumber(value.g));
                    AddValue($"{key}.b", MaterialDetectedValue.FromNumber(value.b));
                    AddValue($"{key}.a", MaterialDetectedValue.FromNumber(value.a));

                    if (!string.IsNullOrWhiteSpace(value.Hex))
                    {
                        AddValue($"{key}.Hex", MaterialDetectedValue.FromString(value.Hex));
                    }
                }
            }

            private void RegisterTexEnvMap(IDictionary<string, TexEnv> texEnvs)
            {
                if (texEnvs == null)
                {
                    return;
                }

                foreach ((string key, TexEnv value) in texEnvs)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    AddIndicator(key);
                    if (value == null)
                    {
                        continue;
                    }

                    if (value.m_Texture != null)
                    {
                        if (!string.IsNullOrWhiteSpace(value.m_Texture.Name))
                        {
                            AddIndicator(value.m_Texture.Name);
                            AddValue($"{key}.Name", MaterialDetectedValue.FromString(value.m_Texture.Name));
                        }

                        AddValue($"{key}.IsNull", MaterialDetectedValue.FromBoolean(value.m_Texture.IsNull));
                    }

                    if (value.m_Scale != null)
                    {
                        AddValue($"{key}.Scale.X", MaterialDetectedValue.FromNumber(value.m_Scale.X));
                        AddValue($"{key}.Scale.Y", MaterialDetectedValue.FromNumber(value.m_Scale.Y));
                    }

                    if (value.m_Offset != null)
                    {
                        AddValue($"{key}.Offset.X", MaterialDetectedValue.FromNumber(value.m_Offset.X));
                        AddValue($"{key}.Offset.Y", MaterialDetectedValue.FromNumber(value.m_Offset.Y));
                    }
                }
            }

            private void RegisterStringMap(IDictionary<string, string> values)
            {
                if (values == null)
                {
                    return;
                }

                foreach ((string key, string value) in values)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    AddIndicator(key);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        AddValue(key, MaterialDetectedValue.FromString(value.Trim()));
                    }
                }
            }

            private void RegisterKeywordSource(string propertyName, object raw)
            {
                RegisterStringValues(propertyName, MaterialKeywordSetBuilder.Extract(raw));
            }

            private void RegisterStringList(string propertyName, IEnumerable<string> values)
            {
                RegisterStringValues(propertyName, values);
            }

            private void RegisterStringValues(string propertyName, IEnumerable<string> values)
            {
                if (string.IsNullOrWhiteSpace(propertyName) || values == null)
                {
                    return;
                }

                foreach (string value in values)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    string trimmed = value.Trim();
                    AddIndicator(trimmed);
                    AddValue(propertyName, MaterialDetectedValue.FromString(trimmed));
                }
            }

            private void RegisterNullableNumber(string propertyName, int? value)
            {
                if (value.HasValue)
                {
                    AddValue(propertyName, MaterialDetectedValue.FromNumber(value.Value));
                }
            }

            private void RegisterBooleanLike(string propertyName, object raw)
            {
                if (MaterialValueCoercion.TryCoerceBoolean(raw, out bool value))
                {
                    AddValue(propertyName, MaterialDetectedValue.FromBoolean(value));
                }
            }

            private void AddIndicator(string token)
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    indicators.Add(token);
                }
            }

            private void AddValue(string propertyName, MaterialDetectedValue value)
            {
                if (string.IsNullOrWhiteSpace(propertyName) || value == null)
                {
                    return;
                }

                if (!valuesByProperty.TryGetValue(propertyName, out List<MaterialDetectedValue> values))
                {
                    values = new List<MaterialDetectedValue>();
                    valuesByProperty[propertyName] = values;
                }

                values.Add(value);
            }
        }
    }

    public enum MaterialDetectedValueKind
    {
        String,
        Number,
        Boolean,
        Null,
    }

    public sealed class MaterialDetectedValue
    {
        private MaterialDetectedValue(MaterialDetectedValueKind kind, string stringValue, double? numberValue, bool? booleanValue)
        {
            Kind = kind;
            StringValue = stringValue;
            NumberValue = numberValue;
            BooleanValue = booleanValue;
        }

        public MaterialDetectedValueKind Kind { get; }

        public string StringValue { get; }

        public double? NumberValue { get; }

        public bool? BooleanValue { get; }

        public static MaterialDetectedValue FromString(string value)
        {
            return new MaterialDetectedValue(MaterialDetectedValueKind.String, value, null, null);
        }

        public static MaterialDetectedValue FromNumber(double value)
        {
            return new MaterialDetectedValue(MaterialDetectedValueKind.Number, null, value, null);
        }

        public static MaterialDetectedValue FromBoolean(bool value)
        {
            return new MaterialDetectedValue(MaterialDetectedValueKind.Boolean, null, null, value);
        }

        public bool Matches(string op, string expectedValue)
        {
            switch (op)
            {
                case "==":
                case "=":
                    return EqualsValue(expectedValue);

                case "!=":
                    return !EqualsValue(expectedValue);

                case ">":
                    return CompareNumber(expectedValue, out int greaterThanComparison) && greaterThanComparison > 0;

                case ">=":
                    return CompareNumber(expectedValue, out int greaterThanOrEqualComparison) && greaterThanOrEqualComparison >= 0;

                case "<":
                    return CompareNumber(expectedValue, out int lessThanComparison) && lessThanComparison < 0;

                case "<=":
                    return CompareNumber(expectedValue, out int lessThanOrEqualComparison) && lessThanOrEqualComparison <= 0;

                default:
                    return false;
            }
        }

        public static string NormalizeExpectedValue(string value)
        {
            string trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return string.Empty;
            }

            bool hasDoubleQuotes = trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"';
            bool hasSingleQuotes = trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[trimmed.Length - 1] == '\'';
            return hasDoubleQuotes || hasSingleQuotes
                ? trimmed.Substring(1, trimmed.Length - 2)
                : trimmed;
        }

        private bool EqualsValue(string expectedValue)
        {
            switch (Kind)
            {
                case MaterialDetectedValueKind.String:
                    return string.Equals(StringValue, expectedValue, StringComparison.Ordinal);

                case MaterialDetectedValueKind.Number:
                    return NumberValue.HasValue
                        && double.TryParse(expectedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double expectedNumber)
                        && NumberValue.Value.Equals(expectedNumber);

                case MaterialDetectedValueKind.Boolean:
                    return BooleanValue.HasValue
                        && bool.TryParse(expectedValue, out bool expectedBoolean)
                        && BooleanValue.Value == expectedBoolean;

                case MaterialDetectedValueKind.Null:
                    return string.Equals(expectedValue, "null", StringComparison.OrdinalIgnoreCase);

                default:
                    return false;
            }
        }

        private bool CompareNumber(string expectedValue, out int comparison)
        {
            comparison = 0;
            if (Kind != MaterialDetectedValueKind.Number
                || !NumberValue.HasValue
                || !double.TryParse(expectedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double expectedNumber))
            {
                return false;
            }

            comparison = NumberValue.Value.CompareTo(expectedNumber);
            return true;
        }
    }

    internal static class MaterialKeywordSetBuilder
    {
        private static readonly char[] TokenSeparators = { ' ', ',', ';', '\t', '\r', '\n' };

        public static IEnumerable<string> Extract(object raw)
        {
            var keywords = new HashSet<string>(StringComparer.Ordinal);
            Collect(raw, keywords);
            return keywords;
        }

        private static void Collect(object raw, HashSet<string> keywords)
        {
            if (raw == null || keywords == null)
            {
                return;
            }

            if (raw is string text)
            {
                AddTokens(text.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries), keywords);

                return;
            }

            if (raw is IDictionary<string, object> objectMap)
            {
                CollectTruthyKeys(objectMap, keywords, static value => MaterialValueCoercion.IsTruthyKeywordEntry(value));

                return;
            }

            if (raw is IDictionary<string, bool> boolMap)
            {
                CollectTruthyKeys(boolMap, keywords, static value => value);

                return;
            }

            if (raw is IDictionary<string, string> stringMap)
            {
                CollectTruthyKeys(stringMap, keywords, static value => MaterialValueCoercion.IsTruthyKeywordEntry(value));

                return;
            }

            if (raw is IDictionary map)
            {
                CollectTruthyEntries(map, keywords);

                return;
            }

            if (raw is IEnumerable sequence)
            {
                foreach (object item in sequence)
                {
                    Collect(item, keywords);
                }

                return;
            }

            AddToken(raw.ToString(), keywords);
        }

        private static void AddTokens(IEnumerable<string> tokens, HashSet<string> keywords)
        {
            if (tokens == null)
            {
                return;
            }

            foreach (string token in tokens)
            {
                AddToken(token, keywords);
            }
        }

        private static void CollectTruthyKeys<TValue>(
            IDictionary<string, TValue> values,
            HashSet<string> keywords,
            Func<TValue, bool> isTruthy)
        {
            if (values == null || keywords == null || isTruthy == null)
            {
                return;
            }

            foreach ((string key, TValue value) in values)
            {
                if (!string.IsNullOrWhiteSpace(key) && isTruthy(value))
                {
                    AddToken(key, keywords);
                }
            }
        }

        private static void CollectTruthyEntries(IDictionary values, HashSet<string> keywords)
        {
            if (values == null || keywords == null)
            {
                return;
            }

            foreach (DictionaryEntry entry in values)
            {
                if (entry.Key is string key && !string.IsNullOrWhiteSpace(key) && MaterialValueCoercion.IsTruthyKeywordEntry(entry.Value))
                {
                    AddToken(key, keywords);
                }
            }
        }

        private static void AddToken(string token, HashSet<string> keywords)
        {
            string trimmed = token?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && !IsArtifactToken(trimmed))
            {
                keywords.Add(trimmed);
            }
        }

        private static bool IsArtifactToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token == ",")
            {
                return true;
            }

            for (int index = 0; index < token.Length; index++)
            {
                if (char.IsLetterOrDigit(token[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal static class MaterialValueCoercion
    {
        public static bool TryCoerceBoolean(object raw, out bool value)
        {
            value = false;
            if (raw == null)
            {
                return false;
            }

            switch (raw)
            {
                case bool booleanValue:
                    value = booleanValue;
                    return true;

                case int integerValue:
                    value = integerValue != 0;
                    return true;

                case long longValue:
                    value = longValue != 0L;
                    return true;

                case float floatValue:
                    value = Math.Abs(floatValue) > float.Epsilon;
                    return true;

                case double doubleValue:
                    value = Math.Abs(doubleValue) > double.Epsilon;
                    return true;

                case decimal decimalValue:
                    value = decimalValue != decimal.Zero;
                    return true;

                case string text:
                    string trimmed = text.Trim();
                    if (trimmed.Length == 0)
                    {
                        return false;
                    }

                    if (bool.TryParse(trimmed, out bool parsedBoolean))
                    {
                        value = parsedBoolean;
                        return true;
                    }

                    if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedNumber))
                    {
                        value = parsedNumber != 0d;
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }

        public static bool IsTruthyKeywordEntry(object raw)
        {
            if (raw == null)
            {
                return true;
            }

            if (raw is string text && text.Trim().Length == 0)
            {
                return true;
            }

            return TryCoerceBoolean(raw, out bool value) ? value : true;
        }
    }
}