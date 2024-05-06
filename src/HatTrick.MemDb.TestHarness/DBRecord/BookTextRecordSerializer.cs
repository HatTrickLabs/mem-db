using System;
using System.IO;
using HatTrick.MemDb;

namespace TestHarness
{
    public class BookTextRecordSerializer : ISerializationProvider<BookTextRecord>
    {
        private static BookTextRecordSerializer _instance;

        private BookTextRecordSerializer()
        {
        }

        public static BookTextRecordSerializer GetInstance()
        {
            return _instance ?? (_instance = new BookTextRecordSerializer());
        }

        public void SerializeTo(BookTextRecord record, BinaryWriter buffer)
        {
            buffer.Write(record.Id);
            buffer.Write(record.Text);
            buffer.Write(record.BookName);
            buffer.Write(record.WordCount);
        }

        public BookTextRecord DeserializeFrom(BinaryReader buffer)
        {
            var record = new BookTextRecord();
            record.Id = buffer.ReadInt32();
            record.Text = buffer.ReadString();
            record.BookName = buffer.ReadString();
            record.WordCount = buffer.ReadInt32();
            return record;
        }
    }
}
