using System;
using HoyoToon.Editor.API.Games;
using HoyoToon.Editor.API.Resources;
using Utf8Json;
using Utf8Json.Resolvers;

namespace HoyoToon.Editor.API
{
    internal static class HoyoToonApi
    {
        internal const string PackageRootAssetPath = "Packages/com.hoyotoon.hoyotoon";
        internal const string ScriptablesAssetPath = PackageRootAssetPath + "/Scriptables";
        internal const string GeneratedGamesFolderName = "Config";

        internal const string Host = "https://hapi.hoyotoon.com";
        internal const string GamesV2HttpUrl = Host + "/games/v2";
        internal const string ResourcesHttpUrl = Host + "/resources";

        internal static readonly IJsonFormatterResolver JsonResolver =
            CompositeResolver.Create(
                new IJsonFormatter[]
                {
                    new RawJsonFormatter(),
                    new ResourceRecordDtoFormatter(),
                },
                new[]
                {
                    StandardResolver.Default,
                });

        private sealed class RawJsonFormatter : IJsonFormatter<RawJson>
        {
            public void Serialize(ref JsonWriter writer, RawJson value, IJsonFormatterResolver formatterResolver)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.Json))
                {
                    writer.WriteNull();
                    return;
                }

                writer.WriteRaw(System.Text.Encoding.UTF8.GetBytes(value.Json));
            }

            public RawJson Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
            {
                if (reader.ReadIsNull())
                {
                    return null;
                }

                ArraySegment<byte> segment = reader.ReadNextBlockSegment();
                return new RawJson
                {
                    Json = System.Text.Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count),
                };
            }
        }

        private sealed class ResourceRecordDtoFormatter : IJsonFormatter<ResourceRecordDto>
        {
            public void Serialize(ref JsonWriter writer, ResourceRecordDto value, IJsonFormatterResolver formatterResolver)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                writer.WriteBeginObject();

                writer.WritePropertyName("displayName");
                writer.WriteString(value.DisplayName);
                writer.WriteValueSeparator();

                writer.WritePropertyName("key");
                writer.WriteString(value.Key);
                writer.WriteValueSeparator();

                writer.WritePropertyName("localPath");
                writer.WriteString(value.LocalPath);
                writer.WriteValueSeparator();

                writer.WritePropertyName("webdavUrl");
                writer.WriteString(value.WebdavUrl);

                writer.WriteEndObject();
            }

            public ResourceRecordDto Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
            {
                if (reader.ReadIsNull())
                {
                    return null;
                }

                reader.ReadIsBeginObjectWithVerify();
                int count = 0;

                var result = new ResourceRecordDto();
                while (!reader.ReadIsEndObjectWithSkipValueSeparator(ref count))
                {
                    ArraySegment<byte> propertyNameSegment = reader.ReadPropertyNameSegmentRaw();
                    string propertyName = System.Text.Encoding.UTF8.GetString(
                        propertyNameSegment.Array,
                        propertyNameSegment.Offset,
                        propertyNameSegment.Count);

                    switch (propertyName)
                    {
                        case "displayName":
                        case "DisplayName":
                            result.DisplayName = reader.ReadString();
                            break;
                        case "key":
                        case "Key":
                            result.Key = reader.ReadString();
                            break;
                        case "localPath":
                        case "LocalPath":
                            result.LocalPath = reader.ReadString();
                            break;
                        case "webdavUrl":
                        case "WebdavUrl":
                            result.WebdavUrl = reader.ReadString();
                            break;
                        default:
                            reader.ReadNextBlock();
                            break;
                    }
                }

                return result;
            }
        }
    }
}
