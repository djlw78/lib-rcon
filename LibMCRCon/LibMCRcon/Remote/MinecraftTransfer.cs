using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace LibMCRcon.Remote
{
    public abstract class MinecraftTransfer
    {

        public string RemotePath { get; set; }
        public bool StopTransfer { get; set; }
        public bool LastTranserSuccess { get; set; }

        public string FullRemotePath(string RootPath, string FileName)
        {
            if (RemotePath == string.Empty)
                return string.Format("{0}/{1}", RootPath, FileName);
            else
                return string.Format("{0}/{1}/{2}", RootPath, RemotePath, FileName);

            
        }
        public TransferDirection Direction { get; set; }
        
        
        public abstract bool ValidateTransfer(FileInfo item, TransferDirection Direction);
        public abstract int TransferItemAge(FileInfo item, TransferDirection Direction);
        public abstract bool TransferNext(FileInfo item, TransferDirection Direction);
        public abstract bool TransferNext(string FileName, Stream item, TransferDirection Direction);

        public abstract bool Exists(string FileName);
        public abstract int Age(string FileName);

        public abstract List<MinecraftFile> GetRemoteData();
        public abstract List<MinecraftFile> GetRemoteData(string RemotePath);
        public abstract List<MinecraftFile> GetRemoteData(string RemotePath, string Filter);

        public bool LockOut { get; set; }
        public abstract void Open();
        public abstract void Close();

        public bool IsOpen { get; set; }

        public async Task TransferRun(TransferQueue<FileInfo> Items, TransferDirection Direction, bool Continous = false)
        {
            await TransferRun(Items, null, Direction, Continous);
        }
        public async Task TransferRun(TransferQueue<FileInfo> Items, TransferQueue<FileInfo> Finished, TransferDirection Direction, bool Continous = false)
        {
            if (IsOpen == false)
                Open();

            Action actTransfer = () =>
            {
                Action<FileInfo> Finish;

                if (Finished != null)
                    Finish = (x) => {Finished.Enqueue(x); };
                else
                    Finish = (x) => { };



                if (IsOpen)
                {

                   FileInfo item;

                   while (Items.Count > 0 || Continous)
                    {

                            item = Items.Dequeue();

                        if (item != null)
                            if (ValidateTransfer(item, Direction))
                                TransferNext(item, Direction);

                        Finish(item);
 

                        if (StopTransfer) break;
                    }
                }
            };

            await Task.Run(actTransfer);
        }

        public async Task<bool> Upload(FileInfo Item)
        {
            return await Task.Run(() => TransferNext(Item, TransferDirection.SEND));
        }
        public async Task<bool> Upload(Stream Item, string FileName)
        {
            return await Task.Run(() => TransferNext(FileName,  Item, TransferDirection.SEND));
        }
        public async Task<bool> Download(FileInfo Item)
        {
            return await Task.Run(() => TransferNext(Item, TransferDirection.RECEIVE));
        }
        public async Task<bool> Download(Stream Item, string FileName)
        {
            return await Task.Run(() => TransferNext(FileName, Item, TransferDirection.RECEIVE));
        }


    }
  

    


    
}