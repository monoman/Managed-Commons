//
// Copyright �2010 Rafael 'Monoman' Teixeira
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;

using Commons.Ebml.IO;

namespace Commons.Ebml
{

    public class Element
    {
        protected byte[] data;
        protected bool dataRead = false;

        public Element(ElementId type, int minSizeLength)
        {
            Type = type;
            MinSizeLength = minSizeLength;
        }

        public void ReadData(Stream source)
        {
            if (Size.Value > 0)
            {
                data = new byte[Size.Value];
                source.read(data, 0, data.Length);
            }
            dataRead = true;
        }

        public void SkipData(Stream source)
        {
            if (Size.Value > 0 && !dataRead)
            {
                source.skip(Size.Value);
            }
            dataRead = true;
        }

        public long Write(IDataWriter writer)
        {
            return WriteHeader(writer) + WriteData(writer);
        }

        public long WriteHeader(IDataWriter writer)
        {
            byte[] bytes = Type.ToCode(Size, MinSizeLength);
            return writer.write(bytes);
        }

        public long WriteData(IDataWriter writer)
        {
            return writer.write(data);
        }

        public virtual byte[] Data
        {
            get
            {
                return data;
            }

            set
            {
                data = value;
                Size = new ElementSize(data.Length);
            }
        }

        public void Clear()
        {
            data = null;
            Size = ElementSize.Zero;
        }

        public ElementId Type { get; private set; }
        public ElementPrototype ElementPrototype { get; set; }
        public Element Parent { get; set; }
        public ElementSize Size { get; set; }
        public int MinSizeLength { get; set; }
        
        public long TotalSize
        {
            get
            {
                long totalSize = Type.Length;
                totalSize += Size.CodedLength;
                totalSize += Size.Value;
                return totalSize;
            }
        }
        
        public byte[] ToByteArray()
        {
            byte[] head = Type.ToCode(Size);
            byte[] ret = new byte[head.Length + data.Length];
            Array.Copy(head, ret, head.Length);
            Array.Copy(data, 0, ret, head.Length, data.Length);
            return ret;
        }

        public override bool Equals(object obj)
        {
            Element other = obj as Element;
            return other != null && Equals(other.Type);
        }

        public bool Equals(ElementId typeId)
        {
            return ElementId.CompareIDs(Type, typeId);
        }

        public bool Equals(ElementPrototype elemType)
        {
            return Equals(elemType.id);
        }

        public static byte[] makeEbmlCode(byte[] typeID, long size)
        {
            int codedLen = codedSizeLength(size);
            byte[] ret = new byte[typeID.Length + codedLen];
            Array.Copy(typeID, ret, typeID.Length);
            byte[] codedSize = makeEbmlCodedSize(size);
            Array.Copy(codedSize, 0, ret, typeID.Length, codedSize.Length);
            return ret;
        }

        public static byte[] makeEbmlCodedSize(long size)
        {
            int len = codedSizeLength(size);
            byte[] ret = new byte[len];
            long mask = 0x00000000000000FFL;
            for (int i = 0; i < len; i++)
            {
                ret[len - 1 - i] = (byte)((size & mask) >> (i * 8));
                mask <<= 8;
            }
            //The first size bits should be clear, otherwise we have an error in the size determination.
            ret[0] |= (byte)(0x80 >> (len - 1));
            return ret;
        }

        public static int getMinByteSize(long value)
        {
            if (value <= 0x7F && value >= 0x80)
            {
                return 1;
            }
            else if (value <= 0x7FFF && value >= 0x8000)
            {
                return 2;
            }
            else if (value <= 0x7FFFFF && value >= 0x800000)
            {
                return 3;
            }
            else if (value <= 0x7FFFFFFF && value >= 0x80000000)
            {
                return 4;
            }
            else if (value <= 0x7FFFFFFFFFL && value >= 0x8000000000L)
            {
                return 5;
            }
            else if (value <= 0x7FFFFFFFFFFFL && value >= 0x800000000000L)
            {
                return 6;
            }
            else if (value <= 0x7FFFFFFFFFFFFFL && value >= 0x80000000000000L)
            {
                return 7;
            }
            else
            {
                return 8;
            }
        }

        public static int getMinByteSizeUnsigned(ulong value)
        {
            int size = 8;
            ulong mask = 0xFF00000000000000L;
            for (int i = 0; i < 8; i++)
            {
                if ((value & mask) == 0)
                {
                    mask = mask >> 8;
                    size--;
                }
                else
                {
                    return size;
                }
            }
            return 8;
        }

        public static int codedSizeLength(long value)
        {
            int codedSize = 0;
            if (value < 127)
            {
                codedSize = 1;
            }
            else if (value < 16383)
            {
                codedSize = 2;
            }
            else if (value < 2097151)
            {
                codedSize = 3;
            }
            else if (value < 268435455)
            {
                codedSize = 4;
            }
            if ((MinSizeLength > 0) && (codedSize <= MinSizeLength))
            {
                codedSize = MinSizeLength;
            }
            return codedSize;
        }

        public static byte[] packIntUnsigned(ulong value)
        {
            int size = getMinByteSizeUnsigned(value);
            return packInt(value, size);
        }

        public static byte[] packInt(long value)
        {
            int size = getMinByteSize(value);
            return packInt(value, size);
        }

        public static byte[] packInt(long value, int size)
        {
            byte[] ret = new byte[size];
            long mask = 0x00000000000000FFL;
            int b = size - 1;
            for (int i = 0; i < size; i++)
            {
                ret[b] = (byte)(((value >> (8 * i)) & mask));
                b--;
            }
            return ret;
        }
    }
}