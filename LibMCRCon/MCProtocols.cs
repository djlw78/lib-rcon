using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using System.IO;
using System.IO.Compression;
namespace MinecraftServer
{


   class mcProtoPacket
   {
       public int Length { get; private set; }
       public int DataLength {get;private set;}
       
       public int PacketId { get;set; }
       public byte[] Data { get; set; }
       
       public bool DataRead { get; private set; }
       public bool Compression { get; set; }
       
       public mcProtoPacket()
       {
           Length = 0;
           PacketId = 0;
           DataLength = 0;
           Compression = false;
           DataRead = false;
       }
       public mcProtoPacket(bool UseCompression):this()
       {
           Compression = UseCompression;
       }

       public void ReadPacket(Stream s)
       {
           int slen = 0;
           byte[] rdata;

           Length = Serializer.Deserialize<Int32>(s);

           if (Compression == false)
           {
               PacketId = Serializer.Deserialize<Int32>(s);
               DataLength = Length - ((Int32)s.Position - 1);
               Data = new byte[DataLength];

               if (s.Read(Data, 0, DataLength) == DataLength)
                   DataRead = true;
               else
                   DataRead = false;
           }
           else
           {
               DataLength = Serializer.Deserialize<Int32>(s);
               Data = new byte[DataLength];

               slen = Length - ((Int32)s.Position - 1);
               rdata = new byte[slen];

               if (s.Read(rdata, 0, slen) == slen)
                   DataRead = true;
               else
                   DataRead = false;

               if (DataRead == true)
               {

                   byte[] zlib16hdr = new byte[2];
                   byte[] zlib32hdr = new byte[4];

                   MemoryStream ms;

                   ms = new MemoryStream(rdata);
                   ms.Read(zlib16hdr, 0, 2);
                   if(BitConverter.IsLittleEndian) Array.Reverse(zlib16hdr);

                   if(zlib16hdr[1] > 0)
                   {
                       ms.Read(zlib32hdr,0,4);
                       if(BitConverter.IsLittleEndian) Array.Reverse(zlib32hdr);
                   }

                   DeflateStream zlib = new DeflateStream(ms, CompressionMode.Decompress);
                   
                   if (zlib.Read(Data, 0, DataLength) == DataLength)
                       DataRead = true;
                   else
                       DataRead = false;

                   ms.Close();

                   ms = new MemoryStream(Data);


               }

           }


       }
   }

    class mcProtoPacketZlib:mcProtoPacket
    {
        public mcProtoPacketZlib()
        {
            
        }
        public mcProtoPacketZlib(Stream s)  
        {


        }
    }


}
