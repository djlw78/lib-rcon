
using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Compression;
using System.IO;
using System.Collections;


using NBT;
using WorldData;


namespace WorldData
{
    
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
                        chunkhdr[c] = NBT.NbtReader.TagInt24(fs) * 4096;
                        chunksect[c] = NBT.NbtReader.TagByte(fs) * 4096;
                    }

                    for (int c = 0; c < 1024; c++)
                        timehdr[c] = DateTime.FromBinary(NBT.NbtReader.TagInt(fs));


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

  
}

namespace WorldData.Ordinates
    {

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

            public int[] V { get; internal set; }


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
                Voxel.V = V;
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

        public class VoxelZone : Voxel
        {

            public int[] Dimensions { get; internal set; }

            private Voxel _Zone;
            private Voxel _Offset;

            public VoxelZone()
                : base(0, 0, 0)
            {
                Dimensions = new int[3] { int.MaxValue, 512, 512 };
                _Zone = new Voxel(0, 0, 0);
                _Offset = new Voxel(0, 0, 0);
            }
            public VoxelZone(int y, int x, int z)
                : base(y, x, z)
            {
                Dimensions = new int[3] { int.MaxValue, 512, 512 };
                _Zone = new Voxel();
                _Offset = new Voxel();

                ResetZoneOffset();
            }
            public VoxelZone(int y, int x, int z, int ySize, int xSize, int zSize)
                : base(y, x, z)
            {
                Dimensions = new int[3] { ySize, xSize, zSize };
                _Zone = new Voxel();
                _Offset = new Voxel();

                ResetZoneOffset();
            }
            public VoxelZone(Voxel Voxel, int ySize, int xSize, int zSize)
                : base(Voxel)
            {
                Dimensions = new int[3] { ySize, xSize, zSize };
                _Zone = new Voxel();
                _Offset = new Voxel();

                ResetZoneOffset();
            }

            public Voxel Zone { get { return _Zone; } set { UpdateZone(value); } }
            public Voxel Offset { get { return _Offset; } set { UpdateOffset(value); } }

            public VoxelZone LinkedOffset(int ySize, int xSize, int zSize)
            {
                VoxelZone v = new VoxelZone(_Offset, ySize, xSize, zSize);
                _Offset.MergeVoxel(v);

                return v;
            }

            public new int ZoneX { get { return _Zone.X; } set { X = Axis(Dimensions[1], value, OffsetX); _Zone.X = value; } }
            public new int ZoneY { get { return _Zone.Y; } set { Y = Axis(Dimensions[0], value, OffsetY); _Zone.Y = value; } }
            public new int ZoneZ { get { return _Zone.Z; } set { Z = Axis(Dimensions[2], value, OffsetZ); _Zone.Z = value; } }

            public new int OffsetX { get { return _Offset.X; } set { X = Axis(Dimensions[1], ZoneX, value); _Offset.X = value; } }
            public new int OffsetY { get { return _Offset.Y; } set { Y = Axis(Dimensions[0], ZoneY, value); _Offset.Y = value; } }
            public new int OffsetZ { get { return _Offset.Z; } set { Z = Axis(Dimensions[2], ZoneZ, value); _Offset.Z = value; } }

            public void ResetZoneOffset()
            {
                _Zone.X = ZoneX(Dimensions[1]);
                _Zone.Y = ZoneY(Dimensions[0]);
                _Zone.Z = ZoneZ(Dimensions[2]);

                _Offset.X = OffsetX(Dimensions[1]);
                _Offset.Y = OffsetY(Dimensions[0]);
                _Offset.Z = OffsetZ(Dimensions[2]);


            }
            public void ResetVoxel()
            {
                X = Axis(Dimensions[1], ZoneX, OffsetX);
                Y = Axis(Dimensions[0], ZoneY, OffsetY);
                Z = Axis(Dimensions[2], ZoneZ, OffsetZ);
            }

            public void UpdateVoxel(Voxel Voxel)
            {
                SetVoxel(Voxel);
                ResetZoneOffset();
            }
            public void UpdateVoxel(int y, int x, int z)
            {
                SetVoxel(y, x, z);
                ResetZoneOffset();
            }


            public void UpdateZone(Voxel Voxel)
            {
                _Zone.SetVoxel(Voxel);
                ResetVoxel();
            }
            public void UpdateZone(int y, int x, int z)
            {
                _Zone.SetVoxel(y, x, z);
                ResetVoxel();
            }

            public void UpdateOffset(Voxel Voxel)
            {
                _Offset.SetVoxel(Voxel);
                ResetVoxel();

            }
            public void UpdateOffset(int y, int x, int z)
            {
                _Offset.SetVoxel(y, x, z);
                ResetVoxel();

            }


        }

        public class VoxelRegion : VoxelZone
        {


            public VoxelZone Chunk { get; set; }

            public VoxelRegion() : base(0, 0, 0, int.MaxValue, 512, 512) { Chunk = LinkedOffset(16, 16, 16); }
            public VoxelRegion(int y, int x, int z) : base(y, x, z, int.MaxValue, 512, 512) { Chunk = LinkedOffset(16, 16, 16); }

            public int ChunkIdx() { return (Chunk.ZoneZ * 32) + Chunk.ZoneX; }
            public int ChunkZXIdx() { return (Chunk.OffsetZ * 16) + Chunk.OffsetX; }
            public int ChunkBlockPos() { return (Chunk.OffsetY * 16 * 16) + (Chunk.OffsetZ * 16) + Chunk.OffsetX; }


        }

        public static class MinecraftOrdinates
        {
            public static VoxelZone Region() { return new VoxelZone(0, 0, 0, int.MaxValue, 512, 512); }
            public static VoxelZone Region(int y, int x, int z) { return new VoxelZone(y, x, z, int.MaxValue, 512, 512); }
            public static VoxelZone Region(Voxel Voxel) { return new VoxelZone(Voxel, int.MaxValue, 512, 512); }

            public static VoxelZone Chunk() { return new VoxelZone(0, 0, 0, 16, 16, 16); }
            public static VoxelZone Chunk(int y, int x, int z) { return new VoxelZone(y, x, z, 16, 16, 16); }
            public static VoxelZone Chunk(VoxelZone Region) { return new VoxelZone(Region.Offset, 16, 16, 16); }
            public static VoxelZone Chunk(Voxel Voxel) { return new VoxelZone(Voxel, 16, 16, 16); }

            public static int ChunkIdx(VoxelZone Chunk) { return (Chunk.ZoneZ * 32) + Chunk.ZoneX; }
            public static int ChunkZXidx(VoxelZone Chunk) { return (Chunk.OffsetZ * 16) + Chunk.OffsetX; }
            public static int ChunkBlockPos(VoxelZone Chunk) { return (Chunk.OffsetY * 16 * 16) + (Chunk.OffsetZ * 16) + Chunk.OffsetX; }

        }


    }


namespace WorldData.Ordinates.MapData
    {

        public class Region : VoxelRegion
        {


            WorldData.NbtChunk nbtChunk;
            WorldData.NbtChunkSection nbtChunkSection;

            int lastYsect = -1;
            int lastCX = int.MaxValue;
            int lastCZ = int.MaxValue;

            public bool ShouldLoadChunk
            {
                get
                {
                    if (nbtChunk == null)
                        return true;

                    VoxelZone c = Chunk;

                    if (lastCX != c.ZoneX || lastCZ != c.ZoneZ)
                    {
                        return true;
                    }
                    else
                        return false;
                }

                set
                {
                    if (value == true)
                    {
                        lastCX = int.MaxValue;
                        lastCZ = int.MaxValue;
                    }
                    else
                    {
                        VoxelZone c = Chunk;

                        lastCX = c.ZoneX;
                        lastCZ = c.ZoneZ;
                    }
                }
            }

            public Region() : base(0, 0, 0) { }
            public Region(int y, int x, int z) : base(y, x, z) { }


            public void WorldAlignment(int y, int x, int z)
            {
                UpdateVoxel(y, x, z);

            }

            public void RegionAlignment(int y, int x, int z)
            {

                UpdateZone(0, x, z);

                OffsetX = 0;
                OffsetZ = 0;
                OffsetY = y;

            }



            private void CheckChunkLoad(RegionMCA mca)
            {
                if (ShouldLoadChunk == true)
                {
                    nbtChunk = new NbtChunk(mca[ChunkIdx()].chunkNBT);
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

            public NbtChunk NbtChunk(RegionMCA mca)
            {

                CheckChunkLoad(mca);
                return nbtChunk;
            }
            public NbtChunkSection NbtChunkSection(RegionMCA mca)
            {
                CheckYSectLoad(mca);
                return nbtChunkSection;
            }


        }

    }
