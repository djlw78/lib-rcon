using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using System.IO;
using System.IO.Compression;
using zlib;

  
namespace MinecraftServer
{
    public enum mcProtoStates { HandShake, Play }
    public enum mcProtoTargets { Server , Client }
   

   class mcProtoPacket
   {


       public int PacketLength { get; internal set; }
       public int DataLength {get;internal set;}
       
       public int PacketId { get;set; }
       public byte[] Data { get; set; }

       public bool DataRead { get; internal set; }
       public bool Compression { get; set; }
       
       public mcProtoPacket()
       {
           PacketLength = 0;
           PacketId = 0;
           DataLength = 0;
           Compression = false;
           DataRead = false;
       }
       public mcProtoPacket(bool UseCompression):this()
       {
           Compression = UseCompression;
       }

       public virtual void ReadPacket(Stream s)
       {
           int slen = 0;
           byte[] rdata;

           PacketLength = Serializer.Deserialize<Int32>(s);

           if (Compression == false)
           {
               PacketId = Serializer.Deserialize<Int32>(s);
               DataLength = PacketLength - ((Int32)s.Length);
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

               slen = PacketLength - ((Int32)s.Length);
               rdata = new byte[slen];

               if (s.Read(rdata, 0, slen) == slen)
                   DataRead = true;
               else
                   DataRead = false;

               if (DataRead == true)
               {

                   MemoryStream ms;
                   
                   ms = new MemoryStream(rdata);
                   ZInputStream zlib = new ZInputStream(ms);
                 
                   if (zlib.Read(Data, 0, DataLength) == DataLength)
                       DataRead = true;
                   else
                       DataRead = false;

                   zlib.Close();

                   ms = new MemoryStream(Data);
                   PacketId = Serializer.Deserialize<Int32>(ms);

                   ms.Read(Data, 0, DataLength - ((Int32)ms.Length));
                   ms.Close();

               }

           }


       }
       public virtual void WritePacket(Stream s)
       {
          
           MemoryStream msPacketLength = new MemoryStream(5);
          
           MemoryStream msPacketId = new MemoryStream(5);

           if (Compression == false)
           {
               Serializer.Serialize<int>(msPacketId, PacketId);
               Serializer.Serialize<int>(msPacketLength, DataLength + ((int)msPacketId.Length));

               msPacketLength.Position = 0;
               msPacketId.Position = 0;

               msPacketLength.CopyTo(s, 5);
               msPacketId.CopyTo(s, 5);
               s.Write(Data, 0, DataLength);


           }
           else
           {
               MemoryStream msDataLength = new MemoryStream(5);
               MemoryStream ms = new MemoryStream(DataLength+5);
               ZOutputStream zo = new ZOutputStream(ms);

               Serializer.Serialize<int>(msPacketId, PacketId);
               msPacketId.Position = 0;

               msPacketId.CopyTo(zo);
               zo.Write(Data, 0, DataLength);

               int slen = (int)zo.Position;
               ms.Position = 0;

              
               Serializer.Serialize<int>(msDataLength, DataLength);
               Serializer.Serialize<int>(msPacketLength, slen + ((int)msDataLength.Length));

               msDataLength.Position = 0;
               msPacketLength.Position = 0;

               msPacketLength.CopyTo(s);
               msDataLength.CopyTo(s);
               ms.CopyTo(s);

               zo.Close();
               msDataLength.Close();
               msPacketLength.Close();
               
           }
           
           msPacketId.Close();
           msPacketLength.Close();

       }

   }

   class mcProtoServerHandshake : mcProtoPacket
   {
       
       public int ProtocolVersion { get; set; }
       public string Address { get; set; }
       public ushort Port { get; set; }
       public int NextState { get; set; }

       public mcProtoServerHandshake()
       {
           this.PacketId = 0x00;
           this.ProtocolVersion = 47;
           this.Port = 25565;

       }

       public void SendPacket(Stream s)
       {
           Data = new byte[17 + Address.Length];
           
           MemoryStream ms = new MemoryStream(Data);
           Serializer.Serialize<int>(ms, ProtocolVersion);
           Serializer.Serialize<int>(ms, Address.Length);
           NbtWriter.TagRawString(Address, ms);
           NbtWriter.TagShort(Port, ms);
           Serializer.Serialize<int>(ms, NextState);

           DataLength = (int)ms.Position;




       
       }


   }

   


}
