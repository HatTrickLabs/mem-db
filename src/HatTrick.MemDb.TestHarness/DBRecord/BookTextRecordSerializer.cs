using System;
using System.Text;
using System.IO;
using HatTrick.MemDb;

namespace TestHarness
{
    public class BookTextRecordSerializer : IMemDbSerializer<BookTextRecord>
    {
        private static BookTextRecordSerializer _instance;

        private BookTextRecordSerializer()
        {
        }

        public static BookTextRecordSerializer GetInstance()
        {
            return _instance ?? (_instance = new BookTextRecordSerializer());
        }

        public void Serialize(BookTextRecord record, BinaryWriter to)
        {
            to.Write(record.Id);
            to.Write(record.Text);
            to.Write(record.BookName);
            to.Write(record.WordCount);
        }

        public BookTextRecord Deserialize(BinaryReader from, int length)
        {
            var record = new BookTextRecord();
            record.Id = from.ReadUInt32();
            record.Text = from.ReadString();
            record.BookName = from.ReadString();
            record.WordCount = from.ReadInt32();
            return record;
        }
    }
}
