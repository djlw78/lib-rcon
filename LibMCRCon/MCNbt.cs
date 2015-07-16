﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using System.IO;
using System.Collections;
using System.Drawing;


namespace MinecraftServer
{

    public enum NbtType
    {

        TAG_end = 0,
        TAG_byte = 1,
        TAG_short = 2,
        TAG_int = 3,
        TAG_long = 4,
        TAG_float = 5,
        TAG_double = 6,
        TAG_array_byte = 7,
        TAG_string = 8,
        TAG_list = 9,
        TAG_compound = 10,
        TAG_array_int = 11



        

    }

    public interface INbtValues<T>
    {

        T tagvalues { get; set; }
    }

    public static class NbtWriter
    {
        public static void TagType(NbtType t, Stream s)
        {
            s.WriteByte((byte)t);
        }
        public static void TagByte(byte data, Stream s)
        {
            s.WriteByte(data);
        }
        public static void TagInt24(Int32 data, Stream s)
        {
            byte[] payload = new byte[3];
            byte[] intdata = new byte[4];

            intdata = BitConverter.GetBytes(data);
            Array.Copy(intdata, 0, payload, 0, 3);
            
            if (BitConverter.IsLittleEndian) Array.Reverse(payload);
            
            s.Write(payload, 0, 3);
        }
        public static void TagShort(Int16 data, Stream s)
        {
            byte[] payload = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian) Array.Reverse(payload);
            s.Write(payload, 0, 2);
        }
        public static void TagShort(UInt16 data,Stream s)
        {
            byte[] payload = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian) Array.Reverse(payload);
            s.Write(payload, 0, 2);
        }
        public static void TagInt(Int32 data, Stream s)
        {
            byte[] payload = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian) Array.Reverse(payload);
            s.Write(payload, 0, 4);
        }
        public static void TagInt(UInt32 data, Stream s)
        {
            byte[] payload = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian) Array.Reverse(payload);
            s.Write(payload, 0, 4);
        }
        public static void TagLong(Int64 data,Stream s)
        {
            byte[] payload = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian) Array.Reverse(payload);
            s.Write(payload, 0, 8);
        }
        public static void TagLong(UInt64 data, Stream s)
        {
            byte[] payload = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian) Array.Reverse(payload);
            s.Write(payload, 0, 8);
        }
        public static void TagFloat(Single data,Stream s)
        {
            byte[] payload = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian) Array.Reverse(payload);
            s.Write(payload, 0, 4);
        }
        public static void TagDouble(Double data, Stream s)
        {
            byte[] payload = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian) Array.Reverse(payload);
            s.Write(payload, 0, 8);
        }
        public static void TagString(String data, Stream s)
        {

            byte[] payload = Encoding.UTF8.GetBytes(data);
            TagShort((short)payload.Length, s);
            s.Write(payload, 0, payload.Length);
        }
        public static void TagRawString(String data,Stream s)
        {

            byte[] payload = Encoding.UTF8.GetBytes(data);
            s.Write(payload, 0, payload.Length);
        }
        public static void TagIntArray(Int32[] data, Stream s)
        {
            Int32 size = data.Length;
           
            TagInt(size, s);
            for(int x = 0; x < size; x++)
                TagInt(data[x], s);
        }
        public static void TagByteArray(byte[] data, Stream s)
        {
            Int32 size = data.Length;

            TagInt(size, s);
            for (int x = 0; x < size; x++)
                TagByte(data[x], s);
        }
        public static void WriteTag(NbtBase Nbt, Stream s)
        {
            TagType(Nbt.tagtype, s);
            TagString(Nbt.tagname, s);
            Nbt.WriteStream(s);
        }
       
    }
    public static class NbtReader
    {
        public static NbtType TagType(Stream s)
        {

            int rb = s.ReadByte();
            if (rb != -1)
                return (NbtType)rb;
            else
                return NbtType.TAG_end;

        }
        public static byte TagByte(Stream s)
        {
            return (byte)s.ReadByte();
        }
        public static Int32 TagInt24(Stream s)
        {
            byte[] payload = new byte[3];
            byte[] intdata = new byte[4];

            Array.Clear(intdata, 0, 4);

            if (s.Read(payload, 0, 3) == 3)
            {
                if (BitConverter.IsLittleEndian) Array.Reverse(payload);
                Array.Copy(payload, 0, intdata, 0, 3);
                return BitConverter.ToInt32(intdata, 0);

            }

            return 0;
        }

        public static Int16 TagShort(Stream s)
        {
            byte[] payload = new byte[2];
            if (s.Read(payload, 0, 2) == 2)
            {
                if (BitConverter.IsLittleEndian) Array.Reverse(payload);
                return BitConverter.ToInt16(payload, 0);

            }

            return 0;
        }
        public static UInt16 TagUShort(Stream s)
        {
            byte[] payload = new byte[2];
            if (s.Read(payload, 0, 2) == 2)
            {
                if (BitConverter.IsLittleEndian) Array.Reverse(payload);
                return BitConverter.ToUInt16(payload, 0);

            }

            return 0;
        }
       
        public static Int32 TagInt(Stream s)
        {
            byte[] payload = new byte[4];
            if (s.Read(payload, 0, 4) == 4)
            {
                if (BitConverter.IsLittleEndian) Array.Reverse(payload);
                return BitConverter.ToInt32(payload, 0);

            }

            return 0;


        }
        public static UInt32 TagUInt(Stream s)
        {
            byte[] payload = new byte[4];
            if (s.Read(payload, 0, 4) == 4)
            {
                if (BitConverter.IsLittleEndian) Array.Reverse(payload);
                return BitConverter.ToUInt32(payload, 0);

            }

            return 0;


        }

        public static Int64 TagLong(Stream s)
        {
            byte[] payload = new byte[8];
            if (s.Read(payload, 0, 8) == 8)
            {
                if (BitConverter.IsLittleEndian) Array.Reverse(payload);
                return BitConverter.ToInt64(payload, 0);

            }

            return 0;
        }
        public static UInt64 TagULong(Stream s)
        {
            byte[] payload = new byte[8];
            if (s.Read(payload, 0, 8) == 8)
            {
                if (BitConverter.IsLittleEndian) Array.Reverse(payload);
                return BitConverter.ToUInt64(payload, 0);

            }

            return 0;
        }

        public static Single TagFloat(Stream s)
        {
            byte[] payload = new byte[4];
            if (s.Read(payload, 0, 4) == 4)
            {
                if (BitConverter.IsLittleEndian) Array.Reverse(payload);
                return BitConverter.ToSingle(payload, 0);

            }

            return Single.NaN;
        }
        public static Double TagDouble(Stream s)
        {
            byte[] payload = new byte[8];
            if (s.Read(payload, 0, 8) == 8)
            {
                if (BitConverter.IsLittleEndian) Array.Reverse(payload);
                return BitConverter.ToDouble(payload, 0);

            }

            return Double.NaN;
        }
        public static String TagString(Stream s)
        {

            Int16 slen = TagShort(s);

            byte[] payload = new byte[slen];
            if (s.Read(payload, 0, slen) == slen)
            {
                return Encoding.UTF8.GetString(payload);
            }

            return string.Empty;


        }
        public static Int32[] TagIntArray(Stream s)
        {

            Int32 size = TagInt(s);
            Int32[] intarr = new Int32[size];
            for (int x = 0; x < size; x++)
                intarr[x] = TagInt(s);

            return intarr;

        }
        public static Byte[] TagByteArray(Stream s)
        {

            Int32 size = TagInt(s);
            byte[] bytearr = new byte[size];
            for (int x = 0; x < size; x++)
                bytearr[x] = TagByte(s);

            return bytearr;
        }
        public static NbtBase ReadTag(Stream s)
        {
            NbtBase n = NbtBase.createtag(s);
            n.ReadStream(s);

            return n;
        }
    }

    public abstract class NbtBase
    {
        
        public long endpos { get; set; }

        public NbtType tagtype { get; set; }
        public string tagname { get; set; }
        public bool IsEnd { get { return tagtype == NbtType.TAG_end; } }

        public static NbtBase createtag(NbtType tag)
        {
            NbtBase n = null;
            

            switch (tag)
            {
                case NbtType.TAG_byte:
                    n = new NbtByte();
                    break;

                case NbtType.TAG_short:
                    n = new NbtShort();
                    break;

                case NbtType.TAG_int:
                    n = new NbtInt();
                    break;

                case NbtType.TAG_long:
                    n = new NbtLong();
                    break;

                case NbtType.TAG_string:
                    n = new NbtString();
                    break;

                case NbtType.TAG_float:
                    n = new NbtFloat();
                    break;

                case NbtType.TAG_double:
                    n = new NbtDouble();
                    break;

                case NbtType.TAG_array_byte:
                    n = new NbtByteArray();
                    break;

                case NbtType.TAG_array_int:
                    n = new NbtIntArray();
                    break;

                case NbtType.TAG_compound:
                    n = new NbtCompound();
                    break;

                case NbtType.TAG_list:
                    n = new NbtList();
                    break;

                default:
                    n = new NbtByte();
                    
                    
                    break;
            }

            
            n.endpos = 0;

            return n;

        }
        public static NbtBase createtag(Stream s)
        {
            NbtType T = NbtReader.TagType(s);

            if(T == NbtType.TAG_end)
            {
                return new NbtEnd();
            }

            NbtBase n = createtag(T);
            n.tagname = NbtReader.TagString(s);
            return n;
        }
        public NbtBase this[string name]
        {
            get
            {
                if (name.Length == 0)
                    return null;

                if (name == tagname)
                    return this;

                NbtBase found = null;
                List<NbtBase> n;
                List<NbtBase> nn;

                switch (tagtype)
                {
                    case NbtType.TAG_compound:
                        n = ((NbtCompound)this).tagvalue;
                        
                        break;
                    case NbtType.TAG_list:
                        n = ((NbtList)this).tagvalue;
                        break;
                    default:
                        return null;
                }

                found = n.Find(x => x.tagname == name);
                if (found != null)
                    return found;

                nn = n.FindAll(x => (x.tagtype == NbtType.TAG_compound || x.tagtype == NbtType.TAG_list));
                foreach (NbtBase x in nn)
                {
                    found = x[name];
                    if (found != null)
                        return found;
                }

                return null;
            }
           
        }
        
        public abstract void WriteStream(Stream s);
        public abstract void ReadStream(Stream s);

      

    }

    public abstract class NbtTag<T> : NbtBase 
    {

        public virtual T tagvalue { get; set; }
        public abstract override void WriteStream(Stream s);
        public abstract override void ReadStream(Stream s);
           
    }
    
    public class NbtCompound:NbtTag<List<NbtBase>>
    {

        public NbtCompound()
        {
            tagtype = NbtType.TAG_compound;
            tagvalue = new List<NbtBase>();
            tagname = string.Empty;
        }

      
        public NbtBase this[int idx]
        {
            get
            {
                return tagvalue[idx];
            }
        }

        public override void WriteStream(Stream s)
        {
            foreach(NbtBase n in tagvalue)
            {
                NbtWriter.WriteTag(n, s);
                if (n.tagtype == NbtType.TAG_end)
                    break;
            }
        }
        public override void ReadStream(Stream s)
        {
            NbtBase n = createtag(s);
            while(n.tagtype != NbtType.TAG_end)
            {

                n.ReadStream(s);
                tagvalue.Add(n);
                n = createtag(s);
            }
          
        }
    }
    public class NbtList:NbtTag<List<NbtBase>>
    {

        public NbtType listtagtype { get;set;}

        public NbtList()
        {
            tagtype = NbtType.TAG_list;
            tagvalue = new List<NbtBase>();
            tagname = string.Empty;
            
            
        }

   
        public NbtBase this[int idx]
        {
            get
            {
                return tagvalue[idx];
            }
        }

        public override void WriteStream(Stream s)
        {
            NbtWriter.TagType(listtagtype, s);
            NbtWriter.TagInt(tagvalue.Count,s);
            foreach(NbtBase n in tagvalue)
            {
                n.WriteStream(s);
            }
        }
        public override void ReadStream(Stream s)
        {
            listtagtype = NbtReader.TagType(s);
            Int32 size = NbtReader.TagInt(s);
            for(int idx = 0; idx < size;idx++)
            {
                NbtBase n = createtag(listtagtype);
                n.ReadStream(s);
                tagvalue.Add(n);
            }
        }
    }
       
    public class NbtIntArray:NbtTag<Int32[]>
    {
        public NbtIntArray()
        {
            tagtype = NbtType.TAG_array_int;
            tagvalue = new Int32[1];
            tagname = string.Empty;

        }


        public int size { get { return tagvalue.GetLength(0); } }
        public override void WriteStream(Stream s)
        {
            NbtWriter.TagIntArray(tagvalue, s);
        }
        public override void ReadStream(Stream s)
        {
            tagvalue = NbtReader.TagIntArray(s);
        }
    }
    public class NbtByteArray:NbtTag<byte[]>
    {
        public NbtByteArray()
        {
            tagtype = NbtType.TAG_array_byte;
            tagvalue = new byte[1];
            tagname = string.Empty;

        }
        
        public int size { get { return tagvalue.GetLength(0); } }

        public override void WriteStream(Stream s)
        {
            NbtWriter.TagByteArray(tagvalue, s);
        }
        public override void ReadStream(Stream s)
        {
            tagvalue = NbtReader.TagByteArray(s);
        }
    }
    public class NbtString:NbtTag<String>
    {
        public NbtString()
        {
            tagtype = NbtType.TAG_string;
            tagvalue = string.Empty;
            tagname = string.Empty;
        }

        public override void WriteStream(Stream s)
        {
            NbtWriter.TagString(tagvalue, s);
        }
        public override void ReadStream(Stream s)
        {
            tagvalue = NbtReader.TagString(s);
        }
    }
    public class NbtDouble:NbtTag<Double>
    {        
        public NbtDouble()
        {
            tagtype = NbtType.TAG_double;
            tagvalue = Double.NaN;
            tagname = string.Empty;
        }

        public override void WriteStream( Stream s)
        {
            NbtWriter.TagDouble(tagvalue, s);
        }
        public override void ReadStream(Stream s)
        {
            tagvalue = NbtReader.TagDouble(s);
        }
    }
    public class NbtFloat:NbtTag<Single>
    {
        public NbtFloat()
        {
            tagtype = NbtType.TAG_float;
            tagvalue = Single.NaN;
            tagname = string.Empty;
        }

        public override void WriteStream(Stream s)
        {
            NbtWriter.TagFloat(tagvalue, s);
        }
        public override void ReadStream(Stream s)
        {
            tagvalue = NbtReader.TagFloat(s);
        }
    }
    public class NbtLong:NbtTag<Int64>
    {

        public NbtLong()
        {
            tagtype = NbtType.TAG_long;
            tagvalue = 0;
            tagname = string.Empty;
        }

        public override void WriteStream( Stream s)
        {
            NbtWriter.TagLong(tagvalue, s);
        }
        public override void ReadStream(Stream s)
        {
            tagvalue = NbtReader.TagLong(s);
        }
    }
    public class NbtInt:NbtTag<Int32>
    {
        public NbtInt()
        {
            tagtype = NbtType.TAG_int;
            tagvalue = 0;
            tagname = string.Empty;


        }

        public override void WriteStream(Stream s)
        {
            NbtWriter.TagInt(tagvalue, s);
        }
        public override void ReadStream(Stream s)
        {
            tagvalue = NbtReader.TagInt(s);
        }
    }
    public class NbtShort:NbtTag<Int16>
    {

        public NbtShort()
        {
            tagtype = NbtType.TAG_short;
            tagvalue = 0;
            tagname = string.Empty;
        }

        public override void WriteStream( Stream s)
        {
            NbtWriter.TagShort(tagvalue, s);
        }
        public override void ReadStream(Stream s)
        {
            tagvalue = NbtReader.TagShort(s);
        }
    }
    public class NbtByte:NbtTag<byte>
    {

      
        public NbtByte()
        {
            tagtype = NbtType.TAG_byte;
            tagvalue = 0;
            tagname = string.Empty;
        }


        public override void WriteStream(Stream s)
        {
            NbtWriter.TagByte(tagvalue, s);
        }
        public override void ReadStream(Stream s)
        {
            tagvalue = NbtReader.TagByte(s);
        }
    }
    public class NbtEnd:NbtTag<NbtEnd>
    {
        public NbtEnd()
        {
            tagtype = NbtType.TAG_end;
            tagvalue = null;
            tagname = "END";

        }

        public override NbtEnd tagvalue
        {
            set
            {
                base.tagvalue = null;
            }
        }



        public override void WriteStream(Stream s)
        {
            s.WriteByte(0);
        }

        public override void ReadStream(Stream s)
        {
            return;
        }
    }


    public class Region : RegionVoxel 
    {

     
        NbtChunk nbtChunk;
        NbtChunkSection nbtChunkSection;

        int lastYsect = -1;
        int lastRX = int.MaxValue;
        int lastRZ = int.MaxValue;

        public bool ShouldLoadChunk 
        { 
            get
            {
                if (lastRX != Region.ZoneX || lastRZ != Region.ZoneZ)
                {
                    ShouldLoadChunk = false;
                    return true;
                }
                else
                    return false;
            }
            set
            {
                if (value == true)
                {
                    lastRX = int.MaxValue;
                    lastRZ = int.MaxValue;
                }
                else
                {
                    lastRX = Region.ZoneX;
                    lastRZ = Region.ZoneZ;
                }
            }
        }

        public Region() : base(0, 0, 0) {}
        public Region(int y, int x, int z) : base(y, x, z) {}


        public void WorldAlignment(int y, int x, int z)
        {
           Region.SetVoxel(y, x, z);
           UpdateChunk();

        }
        public void RegionAlignment(int y, int x, int z)
        {
            Chunk.SetVoxel(y, x, z);
            UpdateRegion();
        }

        private void CheckChunkLoad(RegionMCA mca)
        {
            if (ShouldLoadChunk == true)
            {
                nbtChunk = new NbtChunk( mca[this].chunkNBT);
                lastYsect = -1;
                ShouldLoadChunk = false;
            }
        }
        private void CheckYSectLoad(RegionMCA mca)
        {
            CheckChunkLoad(mca);

            int Ysect = Chunk.ZoneY;

            if (lastYsect != Ysect)
            {
                lastYsect = Ysect;

                NbtCompound nbtSectionData = nbtChunk.Section(lastYsect);
                nbtChunkSection = new NbtChunkSection(nbtSectionData);
            }

        }

        public NbtChunk NbtChunk(RegionMCA mca)  {  CheckChunkLoad(mca); return nbtChunk;  }
        public NbtChunkSection NbtChunkSection(RegionMCA mca) {  CheckYSectLoad(mca); return nbtChunkSection; }



    }

    public class ChunkMCA
    {
        public int chunksectorsize { get; protected set; }
        public DateTime timestamp { get; protected set; }

        public byte[] chunkdata { get; private set; }
        public bool chunkloaded { get; private set; }
        public bool chunkexists { get; private set; }

        public ChunkMCA()
        {

            chunksectorsize = 0;
            chunkdata = new byte[4096];
            chunkexists = false;


        }
        public ChunkMCA(int ChunkOffset, int ChunkSectorsSize, Stream readstream)
        {


            chunksectorsize = ChunkSectorsSize;
            chunkdata = new byte[chunksectorsize];

            if (ChunkOffset > 0)
            {
                chunkexists = true;


                if (readstream.Read(chunkdata, 0, chunksectorsize) == chunksectorsize)

                    chunkloaded = true;

                else
                    chunkloaded = false;
            }
            else
            {
                chunkexists = false;
                chunkloaded = false;
            }




        }


        public NbtCompound chunkNBT
        {
            get
            {
                if (chunkexists == false)
                    return null;

                    MemoryStream ms;


                    byte[] int32data = new byte[4];
                    byte[] int16data = new byte[2];

                   // byte[] chunkraw = new byte[0xfa000];

                    Array.Copy(chunkdata, 0, int32data, 0, 4);
                    if(BitConverter.IsLittleEndian) Array.Reverse(int32data);

                    Array.Copy(chunkdata, 5, int16data, 0, 2);
                    if (BitConverter.IsLittleEndian) Array.Reverse(int16data);
                    
                    int zcomp = chunkdata[4];
                    int chunksize = BitConverter.ToInt32(int32data, 0);
                    int zcomphdr = BitConverter.ToInt16(int16data, 0);

                    //ms = new MemoryStream(chunkdata, 7, chunksize - 3);
                    ms = new MemoryStream(chunkdata);
                    ms.Seek(7, SeekOrigin.Begin);

                    DeflateStream zlib = new DeflateStream(ms, CompressionMode.Decompress);

                    NbtBase nbt = NbtReader.ReadTag(zlib);
                    zlib.Close();

                    return (NbtCompound)nbt;
               
     
                 
            }
        }

    }
    public class RegionMCA
    {


        Int32[] chunkhdr;
        Int32[] chunksect;
        DateTime[] timehdr;

        List<ChunkMCA> chunks;
        int lastX = -1;
        int lastZ = -1;


        string mcaFilePath = string.Empty;

        public RegionMCA()
        {

            chunks = new List<ChunkMCA>(1024);

            chunkhdr = new Int32[1024];
            chunksect = new Int32[1024];
            timehdr = new DateTime[1024];
            lastX = int.MaxValue;
            lastZ = int.MaxValue;
            
        }

        public RegionMCA(string regionPath):this()
        {
            mcaFilePath = regionPath;
        }

        public void SaveRegion()
        {
            if (lastX == -1 && lastZ == -1)
                return;


        }
        public void LoadRegion(RegionVoxel R)
        {
            
            if (R != null)
            {
                Voxel Zone = R.Region.Zone;

                if (lastX != Zone.X || lastZ != Zone.Z)
                {
                    lastX = Zone.X;
                    lastZ = Zone.Z;


                    DirectoryInfo regionDir = new DirectoryInfo(mcaFilePath);
                    FileInfo f = new FileInfo(Path.Combine(regionDir.FullName, string.Format("r.{0}.{1}.mca", lastX, lastZ)));

                    if (f.Exists)
                    {

                        chunks.Clear();


                        FileStream fs = f.OpenRead();

                        for (int c = 0; c < 1024; c++)
                        {
                            chunkhdr[c] = NbtReader.TagInt24(fs) * 4096;
                            chunksect[c] = NbtReader.TagByte(fs) * 4096;
                        }

                        for (int c = 0; c < 1024; c++)
                            timehdr[c] = DateTime.FromBinary(NbtReader.TagInt(fs));


                        for (int c = 0; c < 1024; c++)
                        {

                            try
                            {
                                fs.Seek(chunkhdr[c], SeekOrigin.Begin);
                                chunks.Add(new ChunkMCA(chunkhdr[c], chunksect[c], fs));
                            }
                            catch (Exception)
                            {
                                break;
                            }

                        }

                        fs.Close();

                    }
                }
            }
        }
        public ChunkMCA this[int index]
        {
            get
            {
                if (chunks.Count == 0 || chunks.Count < index)
                    return null;
                return chunks[index];
            }
        }

        public ChunkMCA this[RegionVoxel R]
        {
            get
            {
                return this[R.ChunkIdx()];
            }
        }
 
        public bool IsLoaded { get { return chunks.Count > 0; } }
    
        
    }

    public class NbtChunk
    {
        public ZoneVoxel Chunk { get; set; }

        public NbtInt xPos { get; private set; }
        public NbtInt zPos { get; private set; }
        public NbtLong lastUpdate { get; private set; }
        public NbtByte lightPopulated { get; private set; }
        public NbtByte terrainPopulated { get; private set; }
        public NbtByte V { get; private set; }
        public NbtLong inhabitedTime { get; private set; }
        public NbtByteArray biomes { get; private set; }
        public NbtIntArray heightMap { get; private set; }
        public NbtList sections { get; private set; }
        public NbtList entities { get; private set; }
        public NbtList tileEntities { get; private set; }
        public NbtList tileTicks { get; private set; }

        public bool IsLoaded { get; private set; }

        private void LoadChunkData(NbtCompound chunkdata)
        {
                xPos = (NbtInt)chunkdata["xPos"];
                zPos = (NbtInt)chunkdata["zPos"];
                lastUpdate = (NbtLong)chunkdata["LastUpdate"];
                lightPopulated = (NbtByte)chunkdata["LighPopulated"];
                terrainPopulated = (NbtByte)chunkdata["TerrainPopulated"];
                V = (NbtByte)chunkdata["V"];
                inhabitedTime = (NbtLong)chunkdata["InhabitedTime"];
               
                LoadForSurvey(chunkdata);
                LoadForEntities(chunkdata);

                tileTicks = (NbtList)chunkdata["TileTicks"];
        }
        
        private void LoadForSurvey(NbtCompound chunkdata)
        {
                sections = (NbtList)chunkdata["Sections"];
                biomes = (NbtByteArray)chunkdata["Biomes"];
                heightMap = (NbtIntArray)chunkdata["HeightMap"];

        }
        private void LoadForEntities(NbtCompound chunkdata)
        {
            entities = (NbtList)chunkdata["Entities"];
            tileEntities = (NbtList)chunkdata["TileEntities"];
        }

        public NbtChunk() {}
        
        public NbtChunk(NbtCompound chunkdata)
            : this()
        {
            IsLoaded = false;

            if (chunkdata != null)
            {
                LoadChunkData(chunkdata);
                IsLoaded = true;
            }

        }
 
        public int ChunkX { get { return xPos.tagvalue; } }
        public int ChunkZ { get { return zPos.tagvalue; } }
       
        public bool HasTerrain { get { return terrainPopulated != null ? terrainPopulated.tagvalue == 1 ? true : false : false; } }

        public int Height(int x, int z)
        {
            return heightMap.tagvalue[((z & 0x000F) * 16) + (x & 0x000F)];
        }

        public int Height(ZoneVoxel Chunk)
        {
            if (heightMap == null)
                return 255;
            return heightMap.tagvalue[MinecraftOrdinates.ChunkZXidx(Chunk)];
        }
        public int[] HeightData
        {
            get
            {
                return heightMap.tagvalue;
            }
        }
              
        public int Biome(int x, int z)
        {
            return biomes.tagvalue[((z & 0x000F) * 16) + (x & 0x000F)];

        }
        public int Biome(ZoneVoxel Chunk)
        {
            if (biomes == null)
                return -1;
            return biomes.tagvalue[MinecraftOrdinates.ChunkZXidx(Chunk)];

        }
        public byte[] BiomeData
        {
            get
            {
                return biomes.tagvalue;
            }
        }

        public NbtCompound Section(Int32 y)
        {
            if (sections == null)
                return null;
            
            return (NbtCompound)sections.tagvalue.Find(x => (((NbtByte) (((NbtCompound)x)["Y"])).tagvalue == y));

        }

        public List<NbtChunkSection> BlockSections()
        {

            List<NbtChunkSection> blocks = new List<NbtChunkSection>(sections.tagvalue.Count);

            foreach (NbtCompound section in sections.tagvalue)
                blocks.Add(new NbtChunkSection(section));



            return blocks;
        }

     

    }
    public class NbtChunkSection
    {
        
        public NbtByte Y { get; private set; }
        public NbtByteArray Blocks { get; private set; }
        public NbtByteArray Add { get; private set; }
        public NbtByteArray Data { get; private set; }
        public NbtByteArray SkyLight { get; private set; }
        public NbtByteArray BlockLight { get; private set; }
               
        public void LoadBlockData(NbtCompound section)
        {
            if (section != null)
            {
                Y = (NbtByte)section["Y"];
                Blocks = (NbtByteArray)section["Blocks"];
                Add = (NbtByteArray)section["Add"];
                Data = (NbtByteArray)section["Data"];
                SkyLight = (NbtByteArray)section["SkyLight"];
                BlockLight = (NbtByteArray)section["BlockLight"];
                IsLoaded = true;
            }
            else
                IsLoaded = false;
        }

        public bool IsLoaded { get; set; }

        public NbtChunkSection() { }
        public NbtChunkSection(NbtCompound section)
        {
            LoadBlockData(section);
        }

        public void UpdateBlockId(int BlockPos, int BlockID)
        {
            byte block_a = (byte)(BlockID & 0x00FF);
            byte block_b =  (byte)((BlockID & 0x0F00) >> 8);

            if (Add == null && block_b > 0)
            {
                Add = new NbtByteArray();
                Add.tagvalue = new byte[2048];
            }
            if(block_b > 0)
               UpdateBlockNibble(Add, BlockPos, block_b);
            
            Blocks.tagvalue[BlockPos] = block_a;

        }
        public void UpdateBlockData(int BlockPos, byte BlockData)
        {
            UpdateBlockNibble(Data, BlockPos, BlockData);
        }
        public void UpdateSkyLightData(int BlockPos, byte SkyLightkData)
        {
            UpdateBlockNibble(SkyLight, BlockPos, SkyLightkData);
        }
        public void UpdateBlockLightData(int BlockPos, byte BlockLightData)
        {
            UpdateBlockNibble(BlockLight, BlockPos, BlockLightData );
        }
       
        public int BlockID(int BlockPos)
        {

            byte block_a = Blocks.tagvalue[BlockPos];
            byte block_b = (Add != null) ? BlockNibble(Add, BlockPos) : (byte)0;

            return (int)(block_a + (block_b << 8));

        }
        public int BlockData(int BlockPos)
        {  
            if(Data != null)
               return BlockNibble(Data, BlockPos);

            return -1;
        }
        public int SkyLightData(int BlockPos)
        {
            if (SkyLight != null)
                return BlockNibble(SkyLight, BlockPos);

            return -1;
        }
        public int BlockLightData(int BlockPos)
        {
            if (BlockLight != null)
                return BlockNibble(BlockLight, BlockPos);

            return -1;

        }

        public static NbtChunkSection FindYSect(List<NbtChunkSection> YSections, int Y)
        {

            foreach (NbtChunkSection bd in YSections)
                if (bd.Y.tagvalue == Y) return bd;

            return null;

        }
        public static byte BlockNibble(NbtByteArray arr, int BlockPos)
        {

            int k = BlockPos % 2 == 0 ? arr.tagvalue[BlockPos / 2] & 0x0F : (arr.tagvalue[BlockPos / 2] >> 4) & 0x0F;

            return (byte)k;

        }
        public static void UpdateBlockNibble(NbtByteArray Bnibble, int BlockPos, byte Data)
        {

            byte blockadd = Bnibble.tagvalue[BlockPos / 2];

            if (BlockPos % 2 == 0)
                blockadd = (byte)((blockadd & 0xF0) | (Data & 0x0F));
            else
                blockadd = (byte)(((Data << 4) & 0x00F0) | (blockadd & 0x000F));

            Bnibble.tagvalue[BlockPos / 2] = blockadd;

        }
 

    }

  
}

public class Voxel
{

    public static int ZoneAxis(int Size, int Axis)
    {
        return Axis < 0 ? -((((Axis * -1) - 1) / Size) + 1) : Axis / Size;
    }
    public static int OffsetAxis(int Size, int Axis)
    {
        return Axis < 0 ? Size - ((Axis * -1) - ((((Axis * -1) - 1) / Size) * Size)) : Axis - ((Axis / Size) * Size);
    }
    public static int Axis(int Size, int Zone, int Offset)
    {
        return (Zone * Size) + Offset;
    }

    public int[] V { get; set; }

    public Voxel() { V = new int[3] { 0, 0, 0 }; IsValid = true; }
    public Voxel(int y, int x, int z) { V = new int[3] { y, x, z }; IsValid = true; }
    public Voxel(Voxel Voxel) { this.V = new int[3] { Voxel.Y, Voxel.X, Voxel.Z }; IsValid = true; }

    public void SetVoxel(int y, int x, int z)
    {
        Y = y;
        X = x;
        Z = z;
    }
    public void SetVoxel(Voxel Voxel)
    {
        Y = Voxel.Y;
        X = Voxel.X;
        Z = Voxel.Z;
    }
    public void MergeVoxel(Voxel Voxel)
    {
        V = Voxel.V;
    }

    public int X { get { return V[1]; } set { V[1] = value; } }
    public int Z { get { return V[2]; } set { V[2] = value; } }
    public int Y { get { return V[0]; } set { V[0] = value; } }

    public int ZoneX(int xZoneSize)
    {
        return X < 0 ? -((((X * -1) - 1) / xZoneSize) + 1) : X / xZoneSize;
    }
    public int ZoneY(int yZoneSize)
    {
        return Y < 0 ? -((((Y * -1) - 1) / yZoneSize) + 1) : Y / yZoneSize;
    }
    public int ZoneZ(int zZoneSize)
    {
        return Z < 0 ? -((((Z * -1) - 1) / zZoneSize) + 1) : Z / zZoneSize;
    }

    public int OffsetX(int xZoneSize)
    {
        return X < 0 ? xZoneSize - ((X * -1) - ((((X * -1) - 1) / xZoneSize) * xZoneSize)) : X - ((X / xZoneSize) * xZoneSize);
    }
    public int OffsetY(int yZoneSize)
    {
        return Y < 0 ? yZoneSize - ((Y * -1) - ((((Y * -1) - 1) / yZoneSize) * yZoneSize)) : Y - ((Y / yZoneSize) * yZoneSize);
    }
    public int OffsetZ(int zZoneSize)
    {
        return Z < 0 ? zZoneSize - ((Z * -1) - ((((Z * -1) - 1) / zZoneSize) * zZoneSize)) : Z - ((Z / zZoneSize) * zZoneSize);
    }

    public bool IsValid { get; set; }

}

public class ZoneVoxel : Voxel
{

    public int[] Dimensions { get; internal set; }
    public ZoneVoxel() : base(0, 0, 0) { Dimensions = new int[3] { int.MaxValue, int.MaxValue, int.MaxValue }; }
    public ZoneVoxel(int y, int x, int z) : base(y, x, z) { Dimensions = new int[3] { int.MaxValue, int.MaxValue, int.MaxValue }; }
    public ZoneVoxel(int y, int x, int z, int ySize, int xSize, int zSize) : base(y, x, z) { Dimensions = new int[3] { ySize, xSize, zSize }; }
    public ZoneVoxel(Voxel Voxel, int ySize, int xSize, int zSize) : base(Voxel.Y, Voxel.X, Voxel.Z) { Dimensions = new int[3] { ySize, xSize, zSize }; }

    public void SetDimensions(int ySize, int xSize, int zSize)
    {

        Dimensions[0] = ySize;
        Dimensions[1] = xSize;
        Dimensions[2] = zSize;
    }

    public ZoneVoxel ZoneVoxelByZoneOrdinates(int y, int x, int z)
    {
        return new ZoneVoxel(Axis(Dimensions[0], y, 0), Axis(Dimensions[1], x, 0), Axis(Dimensions[2], z, 0), Dimensions[0], Dimensions[1], Dimensions[2]);
    }
    public ZoneVoxel ZoneVoxelByOffsetOrdinates(int y, int x, int z)
    {
        return new ZoneVoxel(Axis(Dimensions[0], ZoneY(Dimensions[0]), y), Axis(Dimensions[1], ZoneX(Dimensions[1]), x), Axis(Dimensions[2], ZoneZ(Dimensions[2]), z));
    }

    public Voxel Zone
    {
        get
        {
            return new Voxel(ZoneY, ZoneX, ZoneZ);
        }
        set
        {
            V[0] = Axis(Dimensions[0], value.Y, 0);
            V[1] = Axis(Dimensions[1], value.X, 0);
            V[2] = Axis(Dimensions[2], value.Z, 0);
        }
    }
    public Voxel Offset
    {
        get
        {
            return new Voxel(OffsetY, OffsetX, OffsetZ);
        }
        set
        {
            V[0] = Axis(Dimensions[0], ZoneY, value.Y);
            V[1] = Axis(Dimensions[1], ZoneX, value.X);
            V[2] = Axis(Dimensions[2], ZoneZ, value.Z);
        }
    }

    public void ZoneOrdinates(int y, int x, int z)
    {
        ZoneY = y;
        ZoneX = x;
        ZoneZ = z;
    }
    public void OffsetOrdinates(int y, int x, int z)
    {
        OffsetY = y;
        OffsetX = x;
        OffsetZ = z;
    }

    public ZoneVoxel ZoneFromOffset(int ySize, int xSize, int zSize)
    {
        ZoneVoxel v = new ZoneVoxel(Offset, ySize, xSize, zSize);
        return v;
    }
    public Voxel Voxel { get { return this; } set { V[0] = value.V[0]; V[1] = value.V[1]; V[2] = value.V[2]; } }

    public new int ZoneX { get { return ZoneX(Dimensions[1]); } set { V[1] = Axis(Dimensions[1], value, 0); } }
    public new int ZoneY { get { return ZoneY(Dimensions[0]); } set { V[0] = Axis(Dimensions[0], value, 0); } }
    public new int ZoneZ { get { return ZoneZ(Dimensions[2]); } set { V[2] = Axis(Dimensions[2], value, 0); } }

    public new int OffsetX { get { return OffsetX(Dimensions[1]); } set { V[1] = Axis(Dimensions[1], ZoneX, value); } }
    public new int OffsetY { get { return OffsetY(Dimensions[0]); } set { V[0] = Axis(Dimensions[0], ZoneY, value); } }
    public new int OffsetZ { get { return OffsetZ(Dimensions[2]); } set { V[2] = Axis(Dimensions[2], ZoneZ, value); } }



}

public class RegionVoxel
{
    private ZoneVoxel _Region;
    private ZoneVoxel _Chunk;


    public ZoneVoxel Region { get { return _Region; } set { _Region = value; _Chunk = Region.ZoneFromOffset(16, 16, 16); } }
    public ZoneVoxel Chunk { get { return _Chunk; } set { _Region.Offset = value; _Chunk = Region.ZoneFromOffset(16, 16, 16); } }

    public RegionVoxel() { Region = new ZoneVoxel(0, 0, 0, int.MaxValue, 512, 512); }
    public RegionVoxel(int y, int x, int z) { Region = new ZoneVoxel(y, x, z, int.MaxValue, 512, 512); }

    public void UpdateRegion()
    {
        _Region.Offset = Chunk;
    }
    public void UpdateChunk()
    {
        _Chunk = _Region.ZoneFromOffset(16, 16, 16);
    }

    public int ChunkIdx() { return (_Chunk.ZoneZ * 32) + _Chunk.ZoneX; }
    public int ChunkZXIdx() { return (_Chunk.OffsetZ * 16) + _Chunk.OffsetX; }
    public int ChunkBlockPos() { return (_Chunk.OffsetY * 16 * 16) + (_Chunk.OffsetZ * 16) + _Chunk.OffsetX; }

}

public static class MinecraftOrdinates
{
    public static ZoneVoxel Region() { return new ZoneVoxel(0, 0, 0, int.MaxValue, 512, 512); }
    public static ZoneVoxel Region(int y, int x, int z) { return new ZoneVoxel(y, x, z, int.MaxValue, 512, 512); }
    public static ZoneVoxel Region(Voxel Voxel) { return new ZoneVoxel(Voxel.Y, Voxel.X, Voxel.Z, int.MaxValue, 512, 512); }

    public static ZoneVoxel Chunk() { return new ZoneVoxel(0, 0, 0, 16, 16, 16); }
    public static ZoneVoxel Chunk(int y, int x, int z) { return new ZoneVoxel(y, x, z, 16, 16, 16); }
    public static ZoneVoxel Chunk(ZoneVoxel Region) { return new ZoneVoxel(Region.Offset, 16, 16, 16); }
    public static ZoneVoxel Chunk(Voxel Voxel) { return new ZoneVoxel(Voxel, 16, 16, 16); }

    public static int ChunkIdx(ZoneVoxel Chunk) { return (Chunk.ZoneZ * 32) + Chunk.ZoneX; }
    public static int ChunkZXidx(ZoneVoxel Chunk) { return (Chunk.OffsetZ * 16) + Chunk.OffsetX; }
    public static int ChunkBlockPos(ZoneVoxel Chunk) { return (Chunk.OffsetY * 16 * 16) + (Chunk.OffsetZ * 16) + Chunk.OffsetX; }

    public static void SetRegion(ZoneVoxel Region, int y, int x, int z)
    {
        Region.ZoneX = x;
        Region.ZoneZ = z;
        Region.OffsetY = y;
    }

    public static Voxel ToRegionZone(Voxel Voxel) { ZoneVoxel R = new ZoneVoxel(Voxel, int.MaxValue, 512, 512); return R.Zone; }
    public static Voxel ToRegionOffset(Voxel Voxel) { ZoneVoxel R = new ZoneVoxel(Voxel, int.MaxValue, 512, 512); return R.Offset; }
    public static Voxel ToChunkZone(Voxel Voxel) { ZoneVoxel C = new ZoneVoxel(Voxel, 16, 16, 16); return C.Zone; }
    public static Voxel ToChunkOffset(Voxel Voxel) { ZoneVoxel C = new ZoneVoxel(Voxel, 16, 16, 16); return C.Offset; }

}

   