using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects
{
    [Serializable]
    public class PopupProgressData
    {
        [SerializeField] private string mode = "indeterminate";
        [SerializeField] private float value;
        [SerializeField] private float max = 1f;
        [SerializeField] private string text;

        public string Mode => mode;

        public float Value => value;

        public float Max => max;

        public string Text => text;
    }

    [Serializable]
    public class GameProblemEntryData
    {
        [SerializeField] private string name;
        [SerializeField] [TextArea(2, 4)] private string message;
        [SerializeField] private string type;

        public string Name => name;

        public string Message => message;

        public string Type => type;
    }

    [Serializable]
    public abstract class GameOptionalValueData
    {
        [SerializeField] private bool hasValue;

        public bool HasValue => hasValue;
    }

    [Serializable]
    public class GameOptionalBoolData : GameOptionalValueData
    {
        [SerializeField] private bool value;

        public bool Value => value;
    }

    [Serializable]
    public class GameOptionalIntData : GameOptionalValueData
    {
        [SerializeField] private int value;

        public int Value => value;
    }

    [Serializable]
    public class GameOptionalFloatData : GameOptionalValueData
    {
        [SerializeField] private float value;

        public float Value => value;
    }

    [Serializable]
    public class GameModelImportDefaultsData
    {
        [SerializeField] private string animationCompression;
        [SerializeField] private string animationType;
        [SerializeField] private string avatarSetup;
        [SerializeField] private GameOptionalBoolData bakeAxisConversion = new GameOptionalBoolData();
        [SerializeField] private GameOptionalBoolData importAnimation = new GameOptionalBoolData();
        [SerializeField] private GameOptionalBoolData importBlendShapes = new GameOptionalBoolData();
        [SerializeField] private GameOptionalBoolData importCameras = new GameOptionalBoolData();
        [SerializeField] private GameOptionalBoolData importLights = new GameOptionalBoolData();
        [SerializeField] private GameOptionalBoolData importVisibility = new GameOptionalBoolData();
        [SerializeField] private GameOptionalBoolData isReadable = new GameOptionalBoolData();
        [SerializeField] private GameOptionalBoolData legacyBlendshapeNormals = new GameOptionalBoolData();
        [SerializeField] private string materialImportMode;
        [SerializeField] private string materialLocation;
        [SerializeField] private string materialName;
        [SerializeField] private string materialSearch;
        [SerializeField] private GameOptionalBoolData materialSearchAndRemap = new GameOptionalBoolData();
        [SerializeField] private string normals;
        [SerializeField] private GameOptionalBoolData optimizeMeshPolygons = new GameOptionalBoolData();
        [SerializeField] private GameOptionalBoolData optimizeMeshVertices = new GameOptionalBoolData();
        [SerializeField] private GameOptionalBoolData resampleCurves = new GameOptionalBoolData();
        [SerializeField] private GameOptionalFloatData scaleFactor = new GameOptionalFloatData();
        [SerializeField] private string tangents;
        [SerializeField] private GameOptionalBoolData useFileScale = new GameOptionalBoolData();

        public string AnimationCompression => animationCompression;

        public string AnimationType => animationType;

        public string AvatarSetup => avatarSetup;

        public GameOptionalBoolData BakeAxisConversion => bakeAxisConversion;

        public GameOptionalBoolData ImportAnimation => importAnimation;

        public GameOptionalBoolData ImportBlendShapes => importBlendShapes;

        public GameOptionalBoolData ImportCameras => importCameras;

        public GameOptionalBoolData ImportLights => importLights;

        public GameOptionalBoolData ImportVisibility => importVisibility;

        public GameOptionalBoolData IsReadable => isReadable;

        public GameOptionalBoolData LegacyBlendshapeNormals => legacyBlendshapeNormals;

        public string MaterialImportMode => materialImportMode;

        public string MaterialLocation => materialLocation;

        public string MaterialName => materialName;

        public string MaterialSearch => materialSearch;

        public GameOptionalBoolData MaterialSearchAndRemap => materialSearchAndRemap;

        public string Normals => normals;

        public GameOptionalBoolData OptimizeMeshPolygons => optimizeMeshPolygons;

        public GameOptionalBoolData OptimizeMeshVertices => optimizeMeshVertices;

        public GameOptionalBoolData ResampleCurves => resampleCurves;

        public GameOptionalFloatData ScaleFactor => scaleFactor;

        public string Tangents => tangents;

        public GameOptionalBoolData UseFileScale => useFileScale;
    }

    [Serializable]
    public class GameTextureImportRuleData
    {
        [SerializeField] private string compression;
        [SerializeField] private string filterMode;
        [SerializeField] private GameOptionalIntData maxTextureSize = new GameOptionalIntData();
        [SerializeField] private GameOptionalBoolData mipmapEnabled = new GameOptionalBoolData();
        [SerializeField] private string npotScale;
        [SerializeField] private GameOptionalBoolData srgbTexture = new GameOptionalBoolData();
        [SerializeField] private GameOptionalBoolData streamingMipmaps = new GameOptionalBoolData();
        [SerializeField] private string textureCompression;
        [SerializeField] private string textureType;
        [SerializeField] private string wrapMode;

        public string Compression => compression;

        public string FilterMode => filterMode;

        public GameOptionalIntData MaxTextureSize => maxTextureSize;

        public GameOptionalBoolData MipmapEnabled => mipmapEnabled;

        public string NpotScale => npotScale;

        public GameOptionalBoolData SrgbTexture => srgbTexture;

        public GameOptionalBoolData StreamingMipmaps => streamingMipmaps;

        public string TextureCompression => textureCompression;

        public string TextureType => textureType;

        public string WrapMode => wrapMode;
    }

    [Serializable]
    public class GameTextureImportRuleMapData
    {
        [Serializable]
        public class Entry
        {
            [SerializeField] private string key;
            [SerializeField] private GameTextureImportRuleData value = new GameTextureImportRuleData();

            public string Key => key;

            public GameTextureImportRuleData Value => value;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;
    }

    [Serializable]
    public class SerializedJsonValue
    {
        [SerializeField] [TextArea(3, 12)] private string json = "{}";

        public string Json => json;
    }
}
