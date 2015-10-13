
using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Compression;
using System.IO;
using System.Collections;


using LibMCRcon.Nbt;

namespace LibMCRcon.WorldData
{

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
                if (BitConverter.IsLittleEndian) Array.Reverse(int32data);

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


        Int32[] chunkhdr = new Int32[1024];
        Int32[] chunksect = new Int32[1024];
        DateTime[] timehdr = new DateTime[1024];

        ChunkMCA[] chunks = new ChunkMCA[1024];

        int lastX = -1;
        int lastZ = -1;

        public int Count { get; set; }

        string mcaFilePath = string.Empty;

        public RegionMCA()
        {

            lastX = int.MaxValue;
            lastZ = int.MaxValue;

        }

        public RegionMCA(string regionPath)
            : this()
        {
            mcaFilePath = regionPath;
        }

        public void SaveRegion()
        {
            if (lastX == -1 && lastZ == -1)
                return;


        }
        public void LoadRegion(int RegionX, int RegionZ)
        {

            if (lastX != RegionX || lastZ != RegionZ)
            {
                lastX = RegionX;
                lastZ = RegionZ;


                DirectoryInfo regionDir = new DirectoryInfo(mcaFilePath);
                FileInfo f = new FileInfo(Path.Combine(regionDir.FullName, string.Format("r.{0}.{1}.mca", lastX, lastZ)));

                if (f.Exists)
                {

                    for (int c = 0; c < 1024; c++)
                        chunks[c] = null;

                    Count = 0;


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
                            chunks[c] = new ChunkMCA(chunkhdr[c], chunksect[c], fs);
                            Count += 1;
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

        public ChunkMCA this[int index]
        {
            get
            {
                if (Count == 0 || Count < index)
                    return null;
                return chunks[index];
            }
        }
        public ChunkMCA this[int ChunkX, int ChunkZ]
        {
            get
            {
                return this[(ChunkZ * 32) + ChunkX];
            }
        }

        public bool IsLoaded { get { return Count > 0; } }


    }

    public class NbtChunk
    {


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

        public NbtChunk() { }

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
            if (heightMap == null)
                return 255;

            return heightMap.tagvalue[((z & 0x000F) * 16) + (x & 0x000F)];
        }
        public int Height(int ChunkZXIdx)
        {
            if (heightMap == null)
                return 255;
            return heightMap.tagvalue[ChunkZXIdx];
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
            if (biomes == null)
                return -1;

            return biomes.tagvalue[((z & 0x000F) * 16) + (x & 0x000F)];

        }
        public int Biome(int ChunkZXIdx)
        {
            if (biomes == null)
                return -1;
            return biomes.tagvalue[ChunkZXIdx];

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

            return (NbtCompound)sections.tagvalue.Find(x => (((NbtByte)(((NbtCompound)x)["Y"])).tagvalue == y));

        }

        public NbtChunkSection BlockSection(Int32 y) { return new NbtChunkSection(Section(y)); }
        public List<NbtChunkSection> BlockSections()
        {

            if (sections == null)
            {
                return new List<NbtChunkSection>();
            }

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
            byte block_b = (byte)((BlockID & 0x0F00) >> 8);

            if (Add == null && block_b > 0)
            {
                Add = new NbtByteArray();
                Add.tagvalue = new byte[2048];
            }
            if (block_b > 0)
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
            UpdateBlockNibble(BlockLight, BlockPos, BlockLightData);
        }

        public int BlockID(int BlockPos)
        {
            if (Blocks == null)
                return 0;

            byte block_a = Blocks.tagvalue[BlockPos];
            byte block_b = (Add != null) ? BlockNibble(Add, BlockPos) : (byte)0;

            return (int)(block_a + (block_b << 8));

        }
        public int BlockData(int BlockPos)
        {
            if (Data != null)
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




    public class Voxel
    {

        public static int Segment(int Size, int Ordinate)
        {
            return Ordinate < 0 ? -(((Ordinate * -1) / Size) + 1) : Ordinate / Size;
        }
        public static int Offset(int Size, int Ordinate)
        {
            return Ordinate < 0 ? Size - ((Ordinate * -1) - (((Ordinate * -1) / Size) * Size)) : Ordinate - ((Ordinate / Size) * Size);
        }
        public static int Ordinate(int Size, int Segment, int Offset)
        {


            if (Segment < 0)
                return -(((Segment * -1) * Size) - Offset);
            else
                return (Segment * Size) + Offset;


        }

        //YXZ
        public int[] V { get; internal set; }
        public int[] S { get; internal set; }
        public int[] Sz { get; internal set; }


        public Voxel() { V = new int[3] { 0, 0, 0 }; S = new int[3] { 0, 0, 0 }; Sz = new int[3] { 256, 512, 512 }; IsValid = true; }
        public Voxel(int y, int x, int z)
        {
            Sz = new int[3] { 256, 512, 512 };
            V = new int[3] { Offset(Sz[0], y), Offset(Sz[1], x), Offset(Sz[2], z) };
            S = new int[3] { Segment(Sz[0], y), Segment(Sz[1], x), Segment(Sz[2], z) };
            IsValid = true;
        }
        public Voxel(int y, int x, int z, int YSize, int XZSize)
        {
            Sz = new int[3] { YSize, XZSize, XZSize };
            V = new int[3] { Offset(Sz[0], y), Offset(Sz[1], x), Offset(Sz[2], z) };
            S = new int[3] { Segment(Sz[0], y), Segment(Sz[1], x), Segment(Sz[2], z) };
            IsValid = true;
        }
        public Voxel(Voxel Voxel)
        {
            Sz = new int[3] { Voxel.Sz[0], Voxel.Sz[1], Voxel.Sz[2] };
            V = new int[3] { Voxel.V[0], Voxel.V[1], Voxel.V[2] };
            S = new int[3] { Voxel.S[0], Voxel.S[1], Voxel.S[2] };
            IsValid = true;
        }


        public void SetVoxel(int y, int x, int z)
        {
            S[0] = Segment(Sz[0], y);
            S[1] = Segment(Sz[1], x);
            S[2] = Segment(Sz[2], z);

            V[0] = Offset(Sz[0], y);
            V[1] = Offset(Sz[1], x);
            V[2] = Offset(Sz[2], z);
        }
        public void SetVoxel(int y, int x, int z, int YSize, int XZSize)
        {

            Sz[0] = YSize;
            Sz[1] = XZSize;
            Sz[2] = XZSize;

            SetVoxel(y, x, z);

        }
        public void SetVoxel(Voxel Voxel)
        {
            SetVoxel(Voxel.Y, Voxel.X, Voxel.Z);
        }


        public void SetSegment(int xSeg, int zSeg)
        {
            S[1] = xSeg;
            S[2] = zSeg;

        }
        public void SetSegmentOffset(int xSeg, int zSeg, int xOff, int zOff)
        {
            S[1] = xSeg;
            S[2] = zSeg;
            V[1] = xOff;
            V[2] = zOff;


        }

        public Voxel SegmentAlignedVoxel()
        {
            int y = Ordinate(Sz[0], S[0], 0);
            int x = Ordinate(Sz[1], S[1], 0);
            int z = Ordinate(Sz[2], S[2], 0);

            return new Voxel(y, x, z, Sz[0], Sz[1]);
        }
        public Voxel SegmentAlignedVoxel(int YSize, int XZSize)
        {
            int y = Ordinate(Sz[0], S[0], 0);
            int x = Ordinate(Sz[1], S[1], 0);
            int z = Ordinate(Sz[2], S[2], 0);

            return new Voxel(y, x, z, YSize, XZSize);

        }

        public Voxel OffsetVoxel(int YSize, int XZSize)
        {
            return new Voxel(V[0], V[1], V[2], YSize, XZSize);
        }

        public void SetOffset(int y, int x, int z)
        {
            V[0] = y;
            V[1] = x;
            V[2] = z;
        }
        public void SetOffset(Voxel Voxel)
        {
            V[0] = Voxel.Y;
            V[1] = Voxel.X;
            V[2] = Voxel.Z;

        }

        public int Y
        {
            get { return Ordinate(Sz[0], S[0], V[0]); }
            set { S[0] = Segment(Sz[0], value); V[0] = Offset(Sz[0], value); }
        }
        public int X
        {
            get { return Ordinate(Sz[1], S[1], V[1]); }
            set { S[1] = Segment(Sz[1], value); V[1] = Offset(Sz[1], value); }
        }
        public int Z
        {
            get { return Ordinate(Sz[2], S[2], V[2]); }
            set { S[2] = Segment(Sz[2], value); V[2] = Offset(Sz[2], value); }
        }

        public int Ys { get { return S[0]; } set { S[0] = value; } }
        public int Xs { get { return S[1]; } set { S[1] = value; } }
        public int Zs { get { return S[2]; } set { S[2] = value; } }

        public int Yo { get { return V[0]; } set { V[0] = value; } }
        public int Xo { get { return V[1]; } set { V[1] = value; } }
        public int Zo { get { return V[2]; } set { V[2] = value; } }

        public bool IsValid { get; set; }

        public int ChunkIdx() { return (S[2] * 32) + S[1]; }
        public int ChunkZXIdx() { return (V[2] * 16) + V[1]; }
        public int ChunkBlockPos() { return (V[0] * 16 * 16) + (V[2] * 16) + V[1]; }

    }
    public static class MinecraftOrdinates
    {
        public static Voxel Region() { return new Voxel(0, 0, 0, int.MaxValue, 512); }
        public static Voxel Region(int y, int x, int z) { return new Voxel(y, x, z, int.MaxValue, 512); }
        public static Voxel Region(Voxel Voxel) { return new Voxel(Voxel.Y, Voxel.X, Voxel.Z, int.MaxValue, 512); }

        public static Voxel Chunk() { return new Voxel(0, 0, 0, 16, 16); }
        public static Voxel Chunk(int y, int x, int z) { return new Voxel(y, x, z, 16, 16); }
        public static Voxel Chunk(Voxel Voxel) { return Voxel.OffsetVoxel(16, 16); }

        public static int ChunkIdx(Voxel Chunk) { return (Chunk.Zs * 32) + Chunk.Xs; }
        public static int ChunkZXidx(Voxel Chunk) { return (Chunk.Zo * 16) + Chunk.Xo; }
        public static int ChunkBlockPos(Voxel Chunk) { return (Chunk.Yo * 16 * 16) + (Chunk.Zo * 16) + Chunk.Xo; }

    }

    public class Region : Voxel
    {

        public Region() : base() { Chunk = OffsetVoxel(16, 16); }
        public Region(int x, int y, int z) : base(y, x, z) { Chunk = OffsetVoxel(16, 16); }
        public Region(Voxel Voxel) : base(Voxel.SegmentAlignedVoxel()) { Chunk = OffsetVoxel(16, 16); }

        public Voxel Chunk { get; private set; }

        NbtChunk nbtChunk;
        NbtChunkSection[] nbtChunkSection = new NbtChunkSection[16];

        int lastChunkIdx = int.MaxValue;
        int lastYSect = int.MaxValue;

        private void ChunkLoad(RegionMCA mca, Voxel Chunk)
        {
            int idx = Chunk.ChunkIdx();
            if (idx != lastChunkIdx)
            {
                nbtChunk = new NbtChunk(mca[idx].chunkNBT);
                lastYSect = int.MaxValue;
                lastChunkIdx = idx;
            }


        }

        private void ChunkYSectLoad(RegionMCA mca, Voxel Chunk)
        {
            int idx = Chunk.Ys;

            if (idx != lastYSect)
            {
                NbtCompound nbtComp = nbtChunk.Section(idx);
                nbtChunkSection[idx] = new NbtChunkSection(nbtComp);
                lastYSect = idx;
            }
        }

        public NbtChunk NbtChunk(RegionMCA mca, Voxel Chunk)
        {

            ChunkLoad(mca, Chunk);
            return nbtChunk;

        }
        public NbtChunk NbtChunk(RegionMCA mca)
        {

            ChunkLoad(mca, Chunk);
            return nbtChunk;
        }

        public NbtChunkSection NbtChunkSection(RegionMCA mca, Voxel Chunk)
        {

            ChunkLoad(mca, Chunk);
            ChunkYSectLoad(mca, Chunk);

            return nbtChunkSection[lastYSect];
        }
        public NbtChunkSection NbtChunkSection(RegionMCA mca)
        {

            ChunkLoad(mca, Chunk);
            ChunkYSectLoad(mca, Chunk);

            return nbtChunkSection[lastYSect];
        }
        public NbtChunkSection NbtChunSection()
        {
            return nbtChunkSection[lastYSect];
        }

        public void RefreshChunk()
        {
            Chunk.SetVoxel(Y, Xo, Zo, 16, 16);
        }
        public void MergeChunk()
        {
            SetOffset(Chunk);
        }

    }

}