using System;
using System.Collections.Generic;
using System.IO;

namespace HatTrick.InMemDb
{
    public class DigitalAsset
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public string Directory { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastAccess { get; set; }
        public DateTime LastWrite { get; set; }
        public long Length { get; set; }

        public DateTime Imported { get; set; }
        public ulong XXHash { get; set; }

        public string FullPath => Path.Join(this.Directory, this.Name);
        public string Extension => Path.GetExtension(this.Name);

        public override string ToString()
        {
            return this.Name;
        }
    }

    //Asset, DocAsset, ImageAsset, VideoAsset, 
}
