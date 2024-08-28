using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace HatTrick.InMemDb
{
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

    [JsonDerivedType(typeof(ImageAsset), typeDiscriminator: (int)DigitalAssetType.Image)]
    [JsonDerivedType(typeof(VideoAsset), typeDiscriminator: (int)DigitalAssetType.Video)]
    [JsonDerivedType(typeof(DocAsset), typeDiscriminator: (int)DigitalAssetType.Doc)]
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
            return type == DigitalAssetType.Image
                ? new ImageAsset()
                : type == DigitalAssetType.Video
                    ? new VideoAsset()
                    : new DocAsset();
        }

        public override string ToString()
        {
            return this.Name;
        }
    }

    public class ImageAsset : DigitalAsset
    {
        public ImageAsset() : base(DigitalAssetType.Image)
        { }
    }

    public class VideoAsset : DigitalAsset
    {
        public VideoAsset() : base(DigitalAssetType.Video)
        { }
    }

    public class DocAsset : DigitalAsset
    {
        public DocAsset() : base(DigitalAssetType.Doc)
        { }
    }

    public enum DigitalAssetType
    {
        Doc,
        Image,
        Video
    }

    //Asset, DocAsset, ImageAsset, VideoAsset, 
}
