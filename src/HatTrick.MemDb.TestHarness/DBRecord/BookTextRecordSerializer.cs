using System;
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

        public void Serialize(BookTextRecord record, BinaryWriter buffer)
        {
            buffer.Write(record.Id);
            buffer.Write(record.Text);
            buffer.Write(record.BookName);
            buffer.Write(record.WordCount);
        }

        public BookTextRecord Deserialize(BinaryReader buffer)
        {
            var record = new BookTextRecord();
            record.Id = buffer.ReadUInt32();
            record.Text = buffer.ReadString();
            record.BookName = buffer.ReadString();
            record.WordCount = buffer.ReadInt32();
            return record;
        }
    }
}
