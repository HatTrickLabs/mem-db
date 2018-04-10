using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HatTrick.MemDb;

namespace TestHarness
{
    public class BookTextRecord : MemDbRecord//, IMemDbSerializable
    {
        public string Text { get; set; }

        public string BookName { get; set; }

        public int WordCount { get; set; }

        public override void DeserializeFrom(Stream buffer, int length)
        {
            base.DeserializeFrom(buffer, MemDbRecord.BaseRecordLength);

            int payloadLength = (length - MemDbRecord.BaseRecordLength);

            byte[] payload = new byte[payloadLength];

            buffer.Read(payload, 0, payloadLength);

            string val = Encoding.UTF8.GetString(payload);

            string[] vars = val.Split('|');
            this.Text = vars[0];
            this.BookName = vars[1];
            this.WordCount = int.Parse(vars[2]);
        }

        public override void SerializeTo(Stream buffer)
        {
            base.SerializeTo(buffer);

            //byte[] val = Encoding.UTF8.GetBytes($"{this.Text}|{this.BookName}|{this.WordCount}");
            byte[] val = Encoding.UTF8.GetBytes(this.Text + "|" + this.BookName + "|" + this.WordCount);

            buffer.Write(val, 0, val.Length);
        }
    }
}
