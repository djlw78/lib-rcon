using System.Text;

namespace LibMCRcon.WorldData
{
    public class Voxel
    {

        public static int Segment(int Size, int Ordinate)
        {
            return Ordinate < 0 ? ((Ordinate + 1) / Size) - 1 : Ordinate / Size;
        }

        public static int Offset(int Size, int Ordinate)
        {
            return Ordinate < 0 ? -((Size * (((Ordinate + 1) / Size) - 1)) - Ordinate) : Ordinate - ((Ordinate / Size) * Size);
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

        
        public Voxel() { V = new int[3] { 0, 0, 0 }; S = new int[3] { 0, 0, 0 }; Sz = new int[3] { int.MaxValue, 512, 512 }; IsValid = true; }
        
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
        public Voxel(Voxel Voxel, int XZSize)
        {
            Sz = new int[3] { int.MaxValue, XZSize, XZSize };
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
        public void SetVoxel(Voxel Voxel,int XZSize)
        {
            SetVoxel(Voxel.Y, Voxel.X, Voxel.Z, int.MaxValue,XZSize);
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
        public virtual int Xo { get { return V[1]; } set { V[1] = value; } }
        public virtual int Zo { get { return V[2]; } set { V[2] = value; } }

        public bool IsValid { get; set; }

        public int ChunkIdx() { return (S[2] * 32) + S[1]; }
        public int ChunkZXIdx() { return (V[2] * 16) + V[1]; }
        public int ChunkBlockPos() { return (V[0] * 16 * 16) + (V[2] * 16) + V[1]; }



        public virtual bool Parse(string CsvXYZ)
        {
            float tf = 0;
            Voxel pV = this;

            StringBuilder sb = new StringBuilder();

            sb.Append(CsvXYZ);
            sb.Replace(' ', ',');


            string[] tpdata = sb.ToString().Split(',');

            if (tpdata.Length > 2)
            {

                if (float.TryParse(tpdata[0], out tf))
                {
                    pV.X = (int)tf;
                    tf = 0;
                    if (float.TryParse(tpdata[1], out tf))
                    {
                        pV.Y = (int)tf;
                        tf = 0;
                        if (float.TryParse(tpdata[2], out tf))
                        {
                            pV.Z = (int)tf;
                            pV.IsValid = true;

                        }

                    }
                }
            }
            else if (tpdata.Length > 1)
            {
                if (float.TryParse(tpdata[0], out tf))
                {
                    pV.X = (int)tf;
                    tf = 0;
                    if (float.TryParse(tpdata[1], out tf))
                    {
                        pV.Z = (int)tf;
                        pV.IsValid = true;
                    }
                }
            }

            return pV.IsValid;
        }
        public virtual bool ParseSegment(string CsvYXZ)
        {
            float tf = 0;

            StringBuilder sb = new StringBuilder();

            sb.Append(CsvYXZ);
            sb.Replace(' ', ',');


            string[] tpdata = sb.ToString().Split(',');

            if (tpdata.Length > 2)
            {

                if (float.TryParse(tpdata[0], out tf))
                {
                    Y = (int)tf;
                    tf = 0;
                    if (float.TryParse(tpdata[1], out tf))
                    {
                        Xs = (int)tf;
                        tf = 0;
                        if (float.TryParse(tpdata[2], out tf))
                        {
                            Zs = (int)tf;
                            IsValid = true;

                        }

                    }
                }
            }
            else if (tpdata.Length > 1)
            {
                if (float.TryParse(tpdata[0], out tf))
                {
                    Xs = (int)tf;
                    tf = 0;
                    if (float.TryParse(tpdata[1], out tf))
                    {
                        Zs = (int)tf;
                        IsValid = true;
                    }
                }
            }

            return IsValid;
        }
        public virtual bool ParseSegment(string CsvYXZ, params int[] Offset)
        {
            if (ParseSegment(CsvYXZ) == true)
            {

             
                if (Offset.Length > 2)
                {
                    SetOffset(Offset[0], Offset[1], Offset[2]);
                }
                else if (Offset.Length > 1)
                {
                    Xo = Offset[0];
                    Zo = Offset[1];
                }
            }

            return IsValid;

        }
    }
}