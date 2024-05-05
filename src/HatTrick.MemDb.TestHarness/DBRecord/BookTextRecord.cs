using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HatTrick.MemDb;

namespace TestHarness
{
    public class BookTextRecord
    {
        public int Id { get; set; }

        public string Text { get; set; }

        public string BookName { get; set; }

        public int WordCount { get; set; }

        public void DeserializeFrom(BinaryReader buffer)
        {
            this.Id = buffer.ReadInt32();
            this.Text = buffer.ReadString();
            this.BookName = buffer.ReadString();
            this.WordCount = buffer.ReadInt32();
        }

        public void SerializeTo(BinaryWriter buffer)
        {
            buffer.Write(this.Id);
            buffer.Write(this.Text);
            buffer.Write(this.BookName);
            buffer.Write(this.WordCount);
        }
    }
}
