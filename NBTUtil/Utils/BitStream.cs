using System;

namespace NBTUtil.Utils
{
    class BitStream
    {
        private byte[] data;
        public int dataBitLength { get { return data.Length * 8; } }
        public int arrayIndex { get; private set; }
        public int bitIndex { get; private set; }

        public int totalOffset { get; private set; }

        public BitStream(byte[] data)
        {
            this.arrayIndex = 0;
            this.bitIndex = 0;
            this.totalOffset = 0;
            this.data = data;
        }

        public BitStream(Int64[] data) : this(ToByteArray(data))
        {
        }

        public void Seek(int offset)
        {
            arrayIndex = offset >> 8;
            bitIndex = offset & 0xFF;
        }

        public void WriteValue(long value, int bits) {
            int writeAmount = Math.Min(bits, 64);

            for (int i = 0; i < writeAmount; i++)
            {
                WriteBit((value & 1) == 1);
                value >>= 1;
            }
        }

        public void WriteBit(bool bit)
        {
            CheckModifiable();

            if (bit)
                data[arrayIndex] |= (byte) (1 << bitIndex);
            else
                data[arrayIndex] &= (byte) ~(1 << bitIndex);

            IncrementBitOffset();
        }

        public long ReadValue(int bits)
        {
            long value = 0;

            for (int i = 0; i < bits; i++)
            {
                if (ReadBit())
                    value += 1 << i;

            }

            return value;
        }

        public bool ReadBit()
        {
            CheckModifiable();

            bool bit = (data[arrayIndex] & (1 << bitIndex)) >> bitIndex == 1;
            IncrementBitOffset();

            return bit;
        }

        public void SkipThisLong()
        {
            SkipBits(64 - (totalOffset % 64));
        }

        public void SkipBits(int bits)
        {
            for (int i = 0; i < bits; i++)
                IncrementBitOffset();
        }

        public void IncrementBitOffset()
        {
            totalOffset++;
            bitIndex++;

            if (bitIndex >= 8)
            {
                bitIndex = 0;
                arrayIndex++;
            }
        }

        public byte[] ToByteArray()
        {
            return (byte[]) data.Clone();
        }

        public long[] ToLongArray()
        {
            return FromByteArray(data);
        }

        private void CheckModifiable()
        {
            if (!CanModify())
                throw new InvalidOperationException("End of stream reached!");
        }

        public bool CanRead()
        {
            return CanModify();
        }

        public bool CanWrite()
        {
            return CanModify();
        }

        public bool CanModify()
        {
            return arrayIndex < data.Length;
        }

        private static long[] FromByteArray(byte[] data)
        {
            if (data == null)
                throw new ArgumentException("Data byte array must not be null!");

            long[] longData = new long[data.Length / 8];

            for (int i = 0; i < data.Length; i++)
            {
                longData[i / 8] |= ((long)data[i]) << ((i % 8) * 8);
            }

            return longData;
        }

        private static byte[] ToByteArray(long[] data)
        {
            if (data == null)
                throw new ArgumentException("Data long array must not be null!");

            byte[] byteData = new byte[data.Length << 3];

            for (int i = 0; i < byteData.Length; i++)
            {
                byteData[i] = (byte)((data[i / 8] & (((long)0xFF) << (i * 8))) >> (i * 8));
            }

            return byteData;
        }
    }
}
