using System;
using System.IO;
using System.Text.Json.Serialization;

namespace HatTrick.InMemDb
{
    #region i digital asset
    [JsonDerivedType(typeof(TextAsset), typeDiscriminator: (int)DigitalAssetType.Text)]
    [JsonDerivedType(typeof(JsonAsset), typeDiscriminator: (int)DigitalAssetType.Json)]
    [JsonDerivedType(typeof(ExtensionlessAsset), typeDiscriminator: (int)DigitalAssetType.Unknown)]
    public interface IDigitalAsset
    {
        uint Id { get; set; }
        string Name { get; set; }
        string Directory { get; set; }
        DateTime Created { get; set; }
        DateTime LastAccess { get; set; }
        DateTime LastWrite { get; set; }
        long Length { get; set; }

        DateTime Imported { get; set; }
        ulong XXHash { get; set; }

        DigitalAssetType AssetType { get; }

        string FullPath { get; }
        string Extension { get; }
    }
    #endregion

    #region digital asset [abstract]
    [JsonDerivedType(typeof(TextAsset), typeDiscriminator: (int)DigitalAssetType.Text)]
    [JsonDerivedType(typeof(JsonAsset), typeDiscriminator: (int)DigitalAssetType.Json)]
    [JsonDerivedType(typeof(ExtensionlessAsset), typeDiscriminator: (int)DigitalAssetType.Unknown)]
    public abstract class DigitalAsset : IDigitalAsset
    {
        private DigitalAssetType _type;

        public uint Id { get; set; }
        public string Name { get; set; }
        public string Directory { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastAccess { get; set; }
        public DateTime LastWrite { get; set; }
        public long Length { get; set; }

        public DateTime Imported { get; set; }
        public ulong XXHash { get; set; }

        public DigitalAssetType AssetType => _type;

        public string FullPath => Path.Join(this.Directory, this.Name);
        public string Extension => Path.GetExtension(this.Name);

        protected DigitalAsset(DigitalAssetType type)
        {
            _type = type;
        }

        public static DigitalAsset CreateNew(DigitalAssetType type)
        {
            return type == DigitalAssetType.Text
                ? new TextAsset()
                : type == DigitalAssetType.Json
                    ? new JsonAsset()
                    : new ExtensionlessAsset();
        }

        public static DigitalAsset CreateNew(string extension)
        {
            return string.Compare(extension, ".txt", true) == 0
                ? new TextAsset()
                : string.Compare(extension, ".json", true) == 0
                    ? new JsonAsset()
                    : new ExtensionlessAsset();
        }

        public override string ToString()
        {
            return this.Name;
        }
    }
    #endregion

    #region text asset
    public class TextAsset : DigitalAsset
    {
        public TextAsset() : base(DigitalAssetType.Text)
        { }
    }
    #endregion

    #region json asset
    public class JsonAsset : DigitalAsset
    {
        public JsonAsset() : base(DigitalAssetType.Json)
        { }
    }
    #endregion

    #region extensionless asset
    public class ExtensionlessAsset : DigitalAsset
    {
        public ExtensionlessAsset() : base(DigitalAssetType.Unknown)
        { }
    }
    #endregion

    #region digital asset type [enum]
    public enum DigitalAssetType
    {
        Unknown,
        Text,
        Json,
    }
    #endregion
}
