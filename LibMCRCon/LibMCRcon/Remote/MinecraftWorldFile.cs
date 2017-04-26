using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using LibMCRcon.WorldData;

namespace LibMCRcon.Remote
{
    public class MinecraftWorldFile : MinecraftFile
    {

        public List<MinecraftFile> Regions { get; private set; }
        public List<MinecraftWorldFile> WorldMaps { get; private set; }

        public void InitRegionList()
        {
            Regions = new List<MinecraftFile>();
        }
        public void InitWorldMapList()
        {
            WorldMaps = new List<MinecraftWorldFile>();
        }

        public void InitRegionList(MinecraftFile AddMinecraftFile)
        {
            Regions = new List<MinecraftFile>();
            Regions.Add(AddMinecraftFile);
        }

        public MinecraftWorldFile()
        {
            RegionsPerWorld = 8;
            SetVoxel(0, 0, 0, int.MaxValue, BlocksPerWorld);
            MCKind = MineCraftRegionFileKind.TILE;

        }

        public MinecraftWorldFile(int RegionsPerWorld)
        {

            this.RegionsPerWorld = RegionsPerWorld;
            SetVoxel(0, 0, 0, int.MaxValue, BlocksPerWorld);
            MCKind = MineCraftRegionFileKind.TILE;
        }

        public MinecraftWorldFile(string FileName)
        {
            var x = new MinecraftFile(FileName);
            if (x.IsOfMCKind(MineCraftRegionFileKind.IMG))
            {
                SetVoxel(0, x.X, x.Z, int.MaxValue, BlocksPerWorld);
            }
        }

        public void ForEachRegionInWorldMaps(Action<MinecraftFile> action)
        {
            if (WorldMaps != null)
                foreach(var w in WorldMaps)
                    if (w.Regions != null)
                        w.Regions.ForEach(action);
        }


        public void CollectWorldFiles(bool FromRemote = false,  List<MinecraftFile> Source = null, MineCraftRegionFileKind MCKind = MineCraftRegionFileKind.MCA)
        {

            InitWorldMapList();

            var w = new MinecraftWorldFile(RegionsPerWorld);

            Action<MinecraftFile> actCollect = (MinecraftFile x) =>
                {

                    if (x.IsValid)
                    {

                        w.SetVoxel(0, x.X, x.Z);
                        var fw = WorldMaps.Find((f) => f.Xs == w.Xs && f.Zs == w.Zs);

                        if (fw == null)
                        {

                            WorldMaps.Add(w);
                            w.InitRegionList(x);
                            w = new MinecraftWorldFile(RegionsPerWorld);

                        }
                        else
                        {
                            //found a worldfile, so check to see if worldfile has minecraft file
                            
                            var mf = x.IsOfMCKind(MineCraftRegionFileKind.POI) ? fw.Regions.Find(f => f.X == x.X && f.Z == x.Z) :fw.Regions.Find(f => f.Xs == x.Xs && f.Zs == x.Zs);
                            if (mf == null)
                                fw.Regions.Add(x);

                        }

                    }
                };

            if (Source == null)
                Source = (FromRemote == false) ? localMClist : remoteMClist;

            var SourceList = Source.FindAll(x => x.IsOfMCKind(MCKind));
            
            if (SourceList != null)
                SourceList.ForEach(actCollect);

        }

        public void CreateMapFiles(string localpath)
        {

            if (WorldMaps == null) return;
            
            if (WorldMaps.Count > 0)
            {

                int minX = int.MaxValue;
                int minZ = int.MaxValue;
                int maxX = int.MinValue;
                int maxZ = int.MinValue;
                int X = 0;
                int Z = 0;

                foreach(MinecraftWorldFile WF in WorldMaps)
                {
                    if (WF.Xs < minX)
                        minX = WF.Xs;
                    
                    if (WF.Xs > maxX)
                        maxX = WF.Xs;

                    if (WF.Zs < minZ)
                        minZ = WF.Zs;
                    
                    if (WF.Zs > maxZ)
                        maxZ = WF.Zs;

                }

                X = Math.Abs(minX - maxX);
                Z = Math.Abs(minZ - maxZ);

                WorldMaps.ForEach(x => x.CreateMapFile(localpath));

            }

        }

        public void CreateMapFile(string localpath)
        {

            if (Regions == null) return;

            Brush SolidBrush = new SolidBrush(Color.Black);

            Bitmap topo;
            Bitmap tile;

            if (Regions.Count > 0)
            {

                var v = new Voxel(0, 0, 0, int.MaxValue, BlocksPerWorld);
                var p = new Voxel(0, 0, 0, int.MaxValue, PixelsPerRegion);

                topo = new Bitmap(PixelsPerRegion, PixelsPerRegion);
                tile = new Bitmap(PixelsPerRegion, PixelsPerRegion);

                Image img = null;

                Graphics gTopo = Graphics.FromImage(topo);
                Graphics gTile = Graphics.FromImage(tile);

                gTopo.FillRectangle(SolidBrush, 0, 0, PixelsPerRegion, PixelsPerRegion);
                gTile.FillRectangle(SolidBrush, 0, 0, PixelsPerRegion, PixelsPerRegion);


                foreach (MinecraftFile mf in Regions)
                {

                    v.SetVoxel(0, mf.X, mf.Z);
                    p.SetVoxel(0, v.Xo, v.Zo);


                    img = Image.FromFile(Path.Combine(localpath, FileName(MineCraftRegionFileKind.TOPOX, mf.Xs, mf.Zs)));
                    gTopo.DrawImage(img, (p.Xs * PixelsPerWorldRegion), (p.Zs * PixelsPerWorldRegion), PixelsPerWorldRegion, PixelsPerWorldRegion);
                    img.Dispose();

                    img = Image.FromFile(Path.Combine(localpath, FileName(MineCraftRegionFileKind.TILEX, mf.Xs, mf.Zs)));
                    gTile.DrawImage(img, (p.Xs * PixelsPerWorldRegion), (p.Zs * PixelsPerWorldRegion), PixelsPerWorldRegion, PixelsPerWorldRegion);
                    img.Dispose();
                }

                string TopoFile = Path.Combine(localpath, FileName(MineCraftRegionFileKind.TOPO));
                string TileFile = Path.Combine(localpath, FileName(MineCraftRegionFileKind.TILE));

                topo.Save(TopoFile, System.Drawing.Imaging.ImageFormat.Png);
                tile.Save(TileFile, System.Drawing.Imaging.ImageFormat.Png);

                gTopo.Dispose();
                gTile.Dispose();
                topo.Dispose();
                tile.Dispose();

            }




        }
        

    }
  

    


    
}