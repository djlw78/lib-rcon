using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using System.Threading;
using System.Net.Sockets;
using System.Data.SqlClient;

using NBT;
using WorldData;
using WorldData.Ordinates;



//!Classes directly related to the minecraft server.
namespace Server
{
    /// <summary>
    /// RCon packet reader/writter.
    /// </summary>
    public class RconPacket
    {
        Int32 size;
        Int32 id;
        Int32 packettype;
        String cmd;
        String response;
        bool isBadPacket;

        /// <summary>
        /// Constructor with default settings, empty packet.
        /// </summary>
        public RconPacket()
        {
            size = 10;
            id = 0;
            packettype = 1;

        }

        /// <summary>
        /// Used internally to create a packet for transmission/reception.
        /// </summary>
        /// <param name="Command">Payload to send to server such as minecraft commands.</param>
        /// <param name="ServerPacket">As per RCon specification, what type of packet.</param>
        /// <param name="SessionID">Once generated, used throughout lifespan of connection.</param>
        private RconPacket(String Command, Int32 ServerPacket, Int32 SessionID)
        {
            if (Command.Length > 1446)
                cmd = Command.Substring(0, 1446);
            else
                cmd = Command;

            size = cmd.Length + 10;
            id = SessionID;
            packettype = ServerPacket;

        }
        /// <summary>
        /// Helper function to create an Authorization packet, used in establishing connection to RCon handler on the minecraft server.
        /// </summary>
        /// <param name="Password">Password, plain text.</param>
        /// <param name="SessionID">Randomly generated integer, maintained throughout authorized connection lifetime.</param>
        /// <returns>A RCon packet ready to participate in Authentication handshake.</returns>
        public static RconPacket AuthPacket(String Password, Int32 SessionID)
        {
            return new RconPacket(Password, 3, SessionID);
        }
        /// <summary>
        /// Helper function to create a communication packet from client->server.
        /// </summary>
        /// <param name="Command">The payload of the packet, the minecraft server command in text.</param>
        /// <param name="SessionID">Maintains authorization and keeps all communication related.</param>
        /// <returns>A RCon packet ready to be transmitted and receive response.</returns>
        public static RconPacket CmdPacket(String Command, Int32 SessionID)
        {
            return new RconPacket(Command, 2, SessionID);
        }
        /// <summary>
        /// Helper function to fill a section of a byte array from using the entire source array.
        /// </summary>
        /// <param name="dest">Array to receive bytes.</param>
        /// <param name="source">Array bytes are from.</param>
        /// <param name="offset">Where to start the overlay of data in the destination array.</param>
        /// <param name="size">How many bytes to copy from the start of the source array into the destination. Must not be larger than the size of either array.</param>
        /// <returns>The dest array passed into the function.</returns>
        private byte[] fillByteArray(byte[] dest, byte[] source, int offset, int size)
        {

          
            if (dest.Length > offset + size)
                if (source.Length <= size)
                    for (int x = 0; x < size; x++)
                        dest[offset + x] = source[x];

            return dest;


        }
        /// <summary>
        /// Transmits the contents of the RCon packet - converts data into the packet format require by RCon and writes to the network stream.
        /// </summary>
        /// <param name="NS">Open network stream ready to receive data.  Will block until done.</param>
        public void SendToNetworkStream(NetworkStream NS)
        {
            isBadPacket = false;
            byte[] dataout = new byte[size + 4];

            dataout = fillByteArray(dataout, BitConverter.GetBytes(size), 0, 4);
            dataout = fillByteArray(dataout, BitConverter.GetBytes(id), 4, 4);
            dataout = fillByteArray(dataout, BitConverter.GetBytes(packettype), 8, 4);
            dataout = fillByteArray(dataout, Encoding.ASCII.GetBytes(cmd), 12, cmd.Length);
            dataout[size + 2] = 0;
            dataout[size + 3] = 0;

            try
            {
                NS.Write(dataout, 0, dataout.Length);
            }
            catch (Exception)
            {
                isBadPacket = true;
            }
        }
        /// <summary>
        /// Read from the network stream the next valid RCon packet. Will block until completed or an error detected.
        /// </summary>
        /// <param name="NS">Network stream ready for reception of data.</param>
        public void ReadFromNetworkSteam(NetworkStream NS)
        {
            byte[] s = new byte[4];
            Int16 endzeros;

            isBadPacket = false;


            try
            {
                NS.Read(s, 0, 4);
                size = BitConverter.ToInt32(s, 0);

                if (size >= 10 && size < 4096)
                {
                    byte[] data = new byte[size];
                    NS.Read(data, 0, size);

                    id = BitConverter.ToInt32(data, 0);
                    packettype = BitConverter.ToInt32(data, 4);

                    if ((size - 10) > 0)
                    {
                        response = Encoding.ASCII.GetString(data, 8, size - 10);

                    }

                    endzeros = BitConverter.ToInt16(data, size - 2);


                    if (endzeros != 0)
                    {
                        //frame is bad - always ends in 2 zeros..

                        isBadPacket = true;
                    }
                }
                else
                    isBadPacket = true;


            }
            catch (Exception)
            {
                isBadPacket = true;

            }


        }

        /// <summary>
        /// Returns the current response stored.
        /// </summary>
        public String Response { get { return response; } }
        /// <summary>
        /// Generally bad packets/communication won't automatically close the connection.  The packet is marked bad if something wrong happens.
        /// </summary>
        public bool IsBadPacket { get { return isBadPacket; } }
        /// <summary>
        /// The RCon packet type.
        /// </summary>
        public Int32 ServerType { get { return packettype; } }
        /// <summary>
        /// The session id for the packet.
        /// </summary>
        public Int32 SessionID { get { return id; } }
        /// <summary>
        /// The payload of an RCon packet, in this case expected to be a minecraft command.
        /// </summary>
        public String Cmd { get { return cmd; } }

    }
    /// <summary>
    /// Extending a Queue of RConPacket(s), a TCP stream connection with background threads for asyncronous send/receive.
    /// </summary>
    public class TCPRcon : Queue<RconPacket>
    {


        public enum TCPState { IDLE, CONNECTING, CONNECTED, CLOSING, ABORTED };
        public enum RConState { IDLE, AUTHENTICATE, READY, NETWORK_FAIL, AUTHENTICATE_FAIL };
        public string LastTCPError { get; set; }

        public string RConHost { get; set; }
        public string RConPass { get; set; }
        public int RConPort { get; set; }

        public TCPState  StateTCP { get; set; }
        public RConState StateRCon { get; set; }

        protected bool AbortTCP { get; set; }
        public bool ConnectTimedOut { get; set; }
        public bool Connecting { get; set; }
        public int ResetConnectAttemps { get; set; }

        TcpClient cli;
    

        Thread bgCommThread;
        Queue<RconPacket> cmdQue = new Queue<RconPacket>();

        int sessionID = -1;
        /// <summary>
        /// Default constructor, will still need RCon server url, password and port.
        /// </summary>
        public TCPRcon(): base()
        {


        }
        /// <summary>
        /// Create a TCPRcon connection.  Does not open on creation.
        /// </summary>
        /// <param name="MineCraftServer">DNS address of the rcon server.</param>
        /// <param name="port">Port RCon is listening to on the server.</param>
        /// <param name="password">Configured password for the RCon server.</param>
        public TCPRcon(string host, int port, string password):base()
        {
            RConHost = host;
            RConPort = port;
            RConPass = password;

        }

        /// <summary>
        /// Asynchronous que of command.  Will be sent and response collected as soon as possible.
        /// </summary>
        /// <param name="Command">The string command to send, no larger than rcon message specification for minecraft's implementation.</param>
        public void QueCommand(String Command)
        {
            cmdQue.Enqueue(RconPacket.CmdPacket(Command, sessionID));
        }

        /// <summary>
        /// Clones the current connection allowing another session to the rcon server.
        /// </summary>
        /// <returns>Return TCPRcon with the same host,port, and password.</returns>
        public TCPRcon CopyConnection()
        {
            
            TCPRcon r = new TCPRcon(RConHost,RConPort,RConPass);
            r.StartComms();

            return r;
        }

        /// <summary>
        /// Start the asynchronous communication process.
        /// </summary>
        /// <returns>True of successfully started, otherwise false.</returns>
        public bool StartComms()
        {
            if (bgCommThread != null)
                if (bgCommThread.IsAlive)
                {
                    AbortTCP = true;
                    bgCommThread.Join();

                }

            Connecting = true;
            TimeCheck tc = new TimeCheck(3000);

            for (int x = 0; x < 10; x++)
            {
                ResetConnectAttemps = x;

                cli = new TcpClient();
                bgCommThread = new Thread(ConnectAndProcess);
                bgCommThread.IsBackground = true;

                StateTCP = TCPState.IDLE;
                StateRCon = RConState.IDLE;

                bgCommThread.Start();

                while (tc.Expired == false)
                {
                    if (StateTCP == TCPState.CONNECTED)
                        if (StateRCon == RConState.READY)
                            return true;


                    Thread.Sleep(100);

                }
                if (Connecting == true)
                    bgCommThread.Abort();
                else
                    break;
            }

            tc.Reset(10000);
            
            do
            {
                if (StateTCP == TCPState.CONNECTED)
                    if (StateRCon == RConState.READY)
                        return true;

                Thread.Sleep(100);
            } while (tc.Expired == false);

            ConnectTimedOut = true;
            AbortTCP = true;


            if (bgCommThread.IsAlive)
                bgCommThread.Join();

            cli.Close();
            return false;


        }
        /// <summary>
        /// Stop communication and close all connections.  Will block until complete or timed out.
        /// </summary>
        
        public void StopComms()
        {

            StateTCP = TCPState.CLOSING;
            AbortTCP = true;
            if (bgCommThread != null)
                if (bgCommThread.IsAlive)
                    bgCommThread.Join();

            bgCommThread = null;
            StateRCon = RConState.IDLE;


        }
        /// <summary>
        /// True if connected and active.
        /// </summary>
        public bool IsConnected { get { return cli.Connected; } }
        /// <summary>
        /// True if the asynchronous thread is running.
        /// </summary>
        public bool IsStarted { get { return bgCommThread.IsAlive; } }
        /// <summary>
        /// True if the connection is open and the queue is ready for commands.
        /// </summary>
        public bool IsReadyForCommands { get { return StateTCP == TCPState.CONNECTED && StateRCon == RConState.READY; } }
     
        private void ConnectAndProcess()
        {
            DateTime transmitLatch = DateTime.Now.AddMilliseconds(-1);
            Random r = new Random();

            sessionID = r.Next(1, int.MaxValue) + 1;

            StateTCP = TCPState.CONNECTING;
            StateRCon = RConState.IDLE;

            AbortTCP = false;
            Connecting = true;

            try
            {
                
                cli.Connect(RConHost, RConPort);
                Connecting = false;

                StateTCP = TCPState.CONNECTED;
                StateRCon = RConState.AUTHENTICATE;
               
                RconPacket auth = RconPacket.AuthPacket(RConPass, sessionID);
                auth.SendToNetworkStream(cli.GetStream());

                if (auth.IsBadPacket == false)
                {
                    RconPacket resp = new RconPacket();
                    resp.ReadFromNetworkSteam(cli.GetStream());

                    if (resp.IsBadPacket == false)
                    {
                        if (resp.SessionID == -1 && resp.ServerType == 2)
                            StateRCon = RConState.AUTHENTICATE_FAIL;
                        else
                            StateRCon = RConState.READY;
                    }
                    else
                        StateRCon = RConState.NETWORK_FAIL;
                }


                if (StateTCP == TCPState.CONNECTED)
                {
                    if (cli.Connected == false)
                    {
                        StateTCP = TCPState.ABORTED;
                        AbortTCP = true;

                    }

                    if (StateRCon != RConState.READY)
                    {
                        AbortTCP = true;
                        StateTCP = TCPState.ABORTED;
                        StateRCon = RConState.AUTHENTICATE_FAIL;
                        return;
                    }
                }

            }
            catch (Exception e)
            {
                LastTCPError = e.Message;
                AbortTCP = true;
                StateRCon = RConState.NETWORK_FAIL;
                Connecting = false;
            }

            if (AbortTCP == true)
            {
                if (cli.Connected == true)
                {
                    cli.Close();
                    StateTCP = TCPState.ABORTED;
                }
                return;
            }
                    
           
            Comms();

            if (cli.Connected == true)
                cli.Close();

            StateTCP = TCPState.ABORTED;

        }

        private void Comms()
        {
            
            TimeCheck tc = new TimeCheck();
            Int32 dT = 200;

            cli.SendTimeout = 5000;
            cli.ReceiveTimeout = 20000;
            
            try
            {

                if (cli.Connected == false) //Not connected, shut it down...
                {
                    StateRCon = RConState.NETWORK_FAIL;
                    StateTCP = TCPState.CLOSING;
                    AbortTCP = true;
                }


                tc.Reset(dT);

                while (AbortTCP == false)
                {
                    
                    do
                    {
                        if (cli.Available > 0)
                        {


                            RconPacket resp = new RconPacket();
                            resp.ReadFromNetworkSteam(cli.GetStream());

                            if (resp.IsBadPacket == true)
                            {
                                StateTCP = TCPState.ABORTED;
                                StateRCon = RConState.NETWORK_FAIL;
                                AbortTCP = true;
                                break;

                            }

                            if (Count > 500)
                            {
                                StateRCon = RConState.IDLE;
                                StateTCP = TCPState.ABORTED;
                                AbortTCP = true;
                                break;
                            }
                            else
                            {

                                Enqueue(resp);
                                StateRCon = RConState.READY;
                            }

                            if (tc.Expired == false)
                                tc.Reset(dT);
                        }

                        Thread.Sleep(1);


                    } while (tc.Expired == false || cli.Available > 0);

                    if (AbortTCP == true)
                        break;


                    if (cmdQue.Count > 0)
                    {
                        RconPacket Cmd = cmdQue.Dequeue();

                        Cmd.SendToNetworkStream(cli.GetStream());
                        tc.Reset(dT);
                    }

                    Thread.Sleep(1);
                }
            }

            catch (Exception ee)
            {
                AbortTCP = true;
                LastTCPError = ee.Message;
                StateTCP = TCPState.ABORTED;
                StateRCon = RConState.NETWORK_FAIL;
            }


            if (cli.Connected == true)
                cli.Close();


        }

        private void ShutDownComms()
        {

            AbortTCP = true;
            if (bgCommThread.IsAlive)
                bgCommThread.Join();
            else
            {
                StateTCP = TCPState.IDLE;
                StateRCon = RConState.IDLE;
            }

            if (cli.Connected == true)
                cli.Close();
        }
        /// <summary>
        /// Execute a command and wait for a response, blocking main calling thread.  Once response given return.
        /// </summary>
        /// <param name="formatedCmd">Allows for C# style formated string, final result in a minecraft style command.</param>
        /// <param name="args">Same arguments supplied to the string.format function.</param>
        /// <returns>If command is sent and a response given, the repsonse is removed from the response que an returned.</returns>
        public string ExecuteCmd(string formatedCmd, params object[] args)
        {
            return ExecuteCmd(string.Format(formatedCmd, args));
        }
        /// <summary>
        /// Execute a command and wait for a response, blocking main calling thread.  Once response given return.
        /// </summary>
        /// <param name="Cmd">Command to be sent to the rcon server for the minecraft server to execute.</param>
        /// <returns>If command is sent and a response given, the repsonse is removed from the response que an returned.</returns>
        public string ExecuteCmd(string Cmd)
        {

            if (AbortTCP == true)
                return "RCON_ABORTED";

            Server.RconPacket p;
            StringBuilder sb = new StringBuilder();

            TimeCheck tc = new TimeCheck();

            QueCommand(Cmd);

            while (Count == 0)
            {
                Thread.Sleep(100);
                if (AbortTCP == true) break;
                if (tc.Expired == true) break;
            }

            while (Count > 0)
            {
                p = Dequeue();
                sb.Append(p.Response);

                if (AbortTCP == true) break;
            }

            return sb.ToString();

        }


    }
 
    //!Track time passing using computer time.
    public class TimeCheck
    {
        DateTime dT;

        /// <summary>
        /// Create a TimeCheck object setting to a default of 5 seconds into the future from the time created.
        /// </summary>
        public TimeCheck()
        {
            dT = DateTime.Now.AddMilliseconds(5000);
        }
        /// <summary>
        /// Create a TimeCheck object set to X milliseconds into the future from the time created.
        /// </summary>
        /// <param name="Milliseconds">Number of milliseconds to add.</param>
        public TimeCheck(Int32 Milliseconds)
        {
            dT = DateTime.Now.AddMilliseconds((double)Milliseconds);

        }

        /// <summary>
        /// Checks to see if the current time is passed the stored checkpoint and returns true if passed.
        /// </summary>
        public bool Expired 
        { 
            get 
            { 
                return (DateTime.Now > dT) ? true : false;
            }
        }

        /// <summary>
        /// Reset the time point by adding X milliseconds to the current time from calling this function.
        /// </summary>
        /// <param name="Milliseconds">Number of milliseconds to add.</param>
        public void Reset(Int32 Milliseconds)
        {
            dT = DateTime.Now.AddMilliseconds((double)Milliseconds);
        }

    }

    //!Helper functions for various minecraft console commands
    public class MCHelper
    {
        bool isbusy = false;
        /// <summary>
        /// Without knowing the safe heighth to be teleported to, a search is done.
        /// Several command exchanges will occur with the server as fast as they can
        /// which may require throttle controlls.  If a safe space cannot be found, the player
        /// is returned from the location found prior to the transfer.  Any operators on the server will
        /// receive RCON spam.
        /// </summary>
        /// <param name="r">The active RCon connection.</param>
        /// <param name="sb">Output cache from RCon interaction, html encoded.</param>
        /// <param name="player">Player name on the server, must be logged in.</param>
        /// <param name="x">X axis</param>
        /// <param name="y">Y axis</param>
        /// <param name="z">Z axis</param>
        public void TeleportSafeSearch(TCPRcon r, StringBuilder sb, String player, Int32 x, Int32 y, Int32 z)
        {
            if (isbusy == true)
            {
                sb.AppendFormat(@"<br/>{0} Teleporter busy, try again in a bit...", DateTime.Now);
                return;
            }
            else
                isbusy = true;

            if (r.IsReadyForCommands)
            {
                try
                {

                    string resp = "";
                    bool safeTp = false;

                    r.ExecuteCmd("tell {0} To lands unknown you go!!!", player);
                    r.ExecuteCmd("gamemode sp {0}", player);
                    r.ExecuteCmd("tp {3} {0} {1} {2}", x, y, z, player);

                    sb.AppendFormat(@"<br/>{0} Initiated TP sequence for {1}. Waiting for chunk loads", DateTime.Now, player);
                    TimeCheck tchk = new TimeCheck(8000);

                    while (tchk.Expired == false)
                    {
                        resp = r.ExecuteCmd(@"testforblock {0} {1} {2} minecraft:air", x, y, z);
                        if (!resp.Contains("Cannot test"))
                            break;

                        Thread.Sleep(1000);
                    }

                    if (!resp.Contains("Successfully")
                        && r.ExecuteCmd("testforblock {0} {1} {2} minecraft:air", x, y + 1, z).Contains("Successfully")
                        && !r.ExecuteCmd("testforblock {0} {1} {2} minecraft:air", x, y - 1, z).Contains("Successfully")
                        && !r.ExecuteCmd("testforblock {0} {1} {2} minecraft:lava", x, y - 1, z).Contains("Successfully"))
                    {

                        sb.AppendFormat(@"<br/>{0} Found safe landing at {1} {3} {2}", DateTime.Now, x, z, y);
                        safeTp = true;
                    }

                    else
                    {


                        sb.AppendFormat(@"<br/>{0} Scanning for safe landing", DateTime.Now);

                        r.ExecuteCmd("tell {0} Looking for safe tp landing site...", player);


                        for (Int32 yy = 255; yy > 1; yy = yy - 15)
                        {
                            if (!r.ExecuteCmd("testforblock {0} {1} {2} minecraft:air", x, yy, z).Contains("Successfully") || yy < 30)
                                for (Int32 ys = yy + 15; ys > 1; ys--)
                                {


                                    if (!r.ExecuteCmd("testforblock {0} {1} {2} minecraft:air", x, ys, z).Contains("Successfully")
                                        && !r.ExecuteCmd("testforblock {0} {1} {2} minecraft:lava", x, ys, z).Contains("Successfully"))
                                    {

                                        if (r.ExecuteCmd("testforblock {0} {1} {2} minecraft:air", x, ys + 1, z).Contains("Successfully")
                                               && r.ExecuteCmd("testforblock {0} {1} {2} minecraft:air", x, ys + 2, z).Contains("Successfully"))
                                        {


                                            r.ExecuteCmd("tp {3} {0} {1} {2}", x, ys + 1, z, player);
                                            sb.AppendFormat(@"<br/>{0} Found safe landing at {1} {3} {2}", DateTime.Now, x, z, ys + 1);
                                            safeTp = true;
                                            break;
                                        }
                                    }


                                }

                            if (safeTp) break;
                        }

                    }

                    if (safeTp == false)
                    {

                        sb.AppendFormat(@"<br/>{0} No safe landing found, return to nexus", DateTime.Now);
                        r.ExecuteCmd("tell {0} No safe tp found, returned to nexus", player);
                        r.ExecuteCmd("tp {0} 0 65 0", player);
                        r.ExecuteCmd("gamemode s {0}", player);
                    }
                    else
                    {


                        r.ExecuteCmd("tell {0} Welcome to your new adventure...", player);
                        r.ExecuteCmd("gamemode s {0}", player);
                    }


                }
                catch (Exception)
                {
                    sb.AppendFormat(@"<br/>{0} Unexpected error occured in processing safe teleport", DateTime.Now);
                }
            }

            isbusy = false;
        }
        /// <summary>
        /// Locate the player and return a Voxel describing the location of the player in the world.
        /// This is achieved by attempting to teleport the player in place, which causes the server to 
        /// report back where the player is located in the success message.
        /// </summary>
        /// <param name="r">Active RCon object.</param>
        /// <param name="sb">Output cache from RCon execution, raw.</param>
        /// <param name="player">Player name on the server, must be logged in.</param>
        /// <returns>A Voxel, an object with X,Y,Z coordinates buddled together.</returns>
        public Voxel PlayerLocation(TCPRcon r, StringBuilder sb, String player)
        {

            Voxel pV = new Voxel();
            string result;
            if (r.IsReadyForCommands)
            {
                try
                {

                    result = r.ExecuteCmd(string.Format("tp {0} ~ ~ ~", player));

                    sb.AppendFormat("{0}: {1}", player, result);

                    //!Filter output, strip out response language and break out X,Y,Z coordinates
                    string[] data = result.Split(new string[] { "to" }, StringSplitOptions.RemoveEmptyEntries);
                    string[] tpdata = data[1].Split(',');

                    try
                    {
                        pV.X = (int)float.Parse(tpdata[0]);
                        pV.Y = (int)float.Parse(tpdata[1]);
                        pV.Z = (int)float.Parse(tpdata[2]);
                    }
                    catch (Exception)
                    {
                        sb.AppendLine("Error in parsing player x,y,z");
                        pV.IsValid = false;
                    }

                }
                catch (Exception e)
                {
                    sb.AppendFormat("Error in locating player:{0}", e.Message);
                    pV.IsValid = false;
                }
                

            }
            else
                pV.IsValid = false;

            return pV;

        }
        public static TCPRcon ActivateRcon(string Host, int port, string password)
        {
            var r = new TCPRcon(Host, port, password);

            r.StartComms();
            return r;

        }
        public static List<string> LoadPlayers(TCPRcon r, StringBuilder sb)
        {
           
            string[] list_players;


            if (r.IsReadyForCommands == true)
            {

                try
                {
                    string resp = r.ExecuteCmd("list");

                    string[] list_cmd = resp.Split(':');
                    list_players = list_cmd[1].Replace(" ", string.Empty).Split(',');

                }
                catch (Exception ee)
                {
                    list_players = new string[] { "NO_ONE_ONLINE" };
                    sb.AppendFormat(@"{0} => Connection:{1}, Network:{2}",ee.Message,r.LastTCPError,r.StateTCP,r.StateRCon);
                    
                }


            }
            else
            {
                list_players = new string[] { "RCON_ERROR" };
            }
        

            return new List<string>(list_players);

        }

    }

}




/// <summary>
/// A way to programmatically control and track changes made by the /fill command in minecraft.
/// 
/// Either the player is the point of origin, or all coordinates are absolute.  A set of tri-ordinates (Voxel - volume pixel) x,y,z are 
/// used as points of reference.  All rendered fills are cubes of various size.  A collection of fills build a 'room' and
/// a collection of 'rooms' make a map.  The template system that supplies default fills use cube space that is 6x6x6 blocks.
/// All fill templates are based off a model where the fill cubes are rendered in front of the player.  So starting with
/// xyz of 0,0,1 is a block that is on the same level the player is standing, 1 unit in front.  The default render model renders
/// the X axis with + right, and - left.  Y axis with + up and - down.  Z axis + front of player, - behind player.
/// 
/// Most render functions request a facing and pitch.  Pitch is assumed to be level with ground, but can be set straight up or straight down.
/// Facing rotates the model as if the player was facing the direction in game.  In minecraft, North follows -Z, South follows
/// +Z, East follows +X, and West follows -X.  The fill voxels will be translated to the minecraft facing and coodinate system at rendering.
/// All fill primatives are expected to fit into a 6x6x6 cube, so the Mapping portion of the system will align rooms up correctly.
/// </summary>
namespace FillRendering
{
   

    /// <summary>
    /// Enumeration to set the facing of the 'player' before rendering fill commands.
    /// </summary>
    public enum MCRenderFacing { North=0, South=1, East=2, West=3 };
    /// <summary>
    /// Defines a fill command, including what tile block entity should be used to fill and 2 sets of X,Y,Z voxels (3-d ordinates)
    /// that create a cube.
    /// 
    /// Fills are cubes.  These cubes us a common coordinate system from the perspective of a player
    /// in Minecraft standing on origin.  For these fill primatives, the +Z axis goes out forward from
    /// the player, +X strafes right.  +Y goes above the player.  For creating these cubes, player
    /// facing is not considered. 
    /// -Z,-X, and -Y are the opposite. 
    /// 
    /// The voxels can either be offsets from player origin or absolute coordinates.
    /// </summary>
    public class MCFill
    {
        public string BlockType {get;set;} 

        int[] p1 = new int[3];
        int[] p2 = new int[3];

        /// <summary>
        /// Permanently offsets the fill cube.
        /// </summary>
        /// <param name="offsetVoxel">An array representing 3 axis of offset. X,Y,Z [0..2]</param>
        public void OffsetFill(int[] offsetVoxel)
        {
            for(int x = 0; x<3;x++)
            {

                p1[x] += offsetVoxel[x];
                p2[x] += offsetVoxel[x];

            }

        }
        
        /// <summary>
        /// Render the cube, using relative coodinates of the player executing the command.
        /// </summary>
        /// <param name="Facing">Which compass facing to render the cube, from the player location, level to ground.</param>
        /// <returns></returns>
        public string Render(MCRenderFacing Facing)
        {
            return Render(Facing, 0, false, 0, 0, 0);
        }
        /// <summary>
        /// Render the cube, using relative coodinates of the player executing the command.
        /// </summary>
        /// <param name="Facing">Compass direction to render centered on player location.</param>
        /// <param name="Pitch">Render the cube level (0), looking up (1), looking down (-1)</param>
        /// <param name="IsAbsolute">The coordinates stored for the cube are either relative to the player or
        /// absolute world coordinates.</param>
        /// <param name="offset">If there should be an offset computed first, does not change the orginal cube.</param>
        /// <returns></returns>
        public string Render(MCRenderFacing Facing, int Pitch, bool IsAbsolute, params int[] offset)
        {
            int rx = 0, ry = 1, rz = 0;
            int sx = 0, sy = 0, sz = 0;

            int[] r1 = new int[3];
            int[] r2 = new int[3];

            switch (Facing)
            {
                case MCRenderFacing.North:

                    switch (Pitch)
                    {

                        case -1:

                            rx = 0; ry = 2; rz = 1;
                            sx = 1; sy = -1; sz = -1;

                            break;
                        case 1:

                            rx = 0; ry = 2; rz = 1;
                            sx = -1; sy = 1; sz = 1;
                            break;

                        default:
                            rx = 0; ry = 1; rz = 2;
                            sx = 1; sy = 1; sz = -1;
                            break;
                    }
                    break;
                case MCRenderFacing.South:
                    switch (Pitch)
                    {
                        case -1:
                            rx = 0; ry = 2; rz = 1;
                            sx = -1; sy = 1; sz = -1;
                            break;
                        case 1:
                            rx = 0; ry = 2; rz = 1;
                            sx = 1; sy = -1; sz = 1;
                            break;
                        default:
                            rx = 0; ry = 1; rz = 2;
                            sx = -1; sy = 1; sz = 1;
                            break;
                    }
                    break;

                case MCRenderFacing.East:
                    switch (Pitch)
                    {
                        case -1:
                            rx = 2; ry = 0; rz = 1;
                            sx = 1; sy = 1; sz = -1;
                            break;
                        case 1:
                            rx = 2; ry = 0; rz = 1;
                            sx = -1; sy = -1; sz = 1;
                            break;
                        default:
                            rx = 2; ry = 1; rz = 0;
                            sx = 1; sy = 1; sz = 1;
                            break;
                    }
                    break;
                case MCRenderFacing.West:
                    switch (Pitch)
                    {
                        case -1:
                            rx = 2; ry = 0; rz = 1;
                            sx = -1; sy = -1; sz = -1;
                            break;
                        case 1:
                            rx = 2; ry = 0; rz = 1;
                            sx = 1; sy = 1; sz = 1;
                            break;
                        default:
                            rx = 2; ry = 1; rz = 0;
                            sx = -1; sy = 1; sz = -1;
                            break;
                    }
                    break;



            }

            r1[0] = sx * (offset[rx] + p1[rx]);
            r1[1] = sy * (offset[ry] + p1[ry]);
            r1[2] = sz * (offset[rz] + p1[rz]);
            r2[0] = sx * (offset[rx] + p2[rx]);
            r2[1] = sy * (offset[ry] + p2[ry]);
            r2[2] = sz * (offset[rz] + p2[rz]);

            if (IsAbsolute == true)
                return string.Format(@"fill {1} {2} {3} {4} {5} {6} {0}", BlockType, r1[0], r1[1], r1[2], r2[0], r2[1], r2[2]);
            else
                return string.Format(@"fill ~{1} ~{2} ~{3} ~{4} ~{5} ~{6} {0}", BlockType, r1[0], r1[1], r1[2], r2[0], r2[1], r2[2]);


        }


        /// <summary>
        /// Sets the first point in the fill cube.
        /// </summary>
        /// <param name="x">X axis relative or absolute.</param>
        /// <param name="y">Y axis relative or absolute.</param>
        /// <param name="z">Z axis relative or absolute.</param>
        public void SetPoint1(int x, int y, int z)
        {
            p1[0] = x;
            p1[1] = y;
            p1[2] = z;
        }
        /// <summary>
        /// Sets the second point in the fill cube.
        /// </summary>
        /// <param name="x">X axis relative or absolute.</param>
        /// <param name="y">Y axis relative or absolute.</param>
        /// <param name="z">Z axis relative or absolute.</param>
        public void SetPoint2(int x, int y, int z)
        {
            p2[0] = x;
            p2[1] = y;
            p2[2] = z;
        }

        /// <summary>
        /// Default fill type is stone.
        /// </summary>
        public MCFill()
        {
            BlockType = "minecraft:stone";
        }

        /// <summary>
        /// Set the entire fill cube up in one setting.
        /// </summary>
        /// <param name="BlockType">Minecraft verbose item in minecraft:[block entity id].</param>
        /// <param name="x1">1st X axis</param>
        /// <param name="y1">1st Y axis</param>
        /// <param name="z1">1st Z axis</param>
        /// <param name="xs">X axis length</param>
        /// <param name="ys">Y axis length</param>
        /// <param name="zs">Z axis length</param>
        public MCFill(int xs, int ys, int zs, int x1, int y1, int z1,string BlockType)
        {
            this.BlockType = BlockType;
            Reset(x1, y1, z1, xs, ys, zs);

        }

        public MCFill(int xs, int ys, int zs, string BlockType)
        {
            this.BlockType = BlockType;
            Reset(0, 0, 0, xs, ys, zs);
        }

        public int[] size
        {
            get
            {
                int[] sxyz = new int[3];

                for (int x = 0; x < 3; x++)
                    sxyz[x] = ((int)Math.Abs(p2[x] - p1[x])) + 1;
                
                return sxyz;
            }
        }

        public MCFill OffsetClone(int x, int y, int z, string BlockType)
        {
            int[] s = size;
            return new MCFill(s[0], s[1], s[2], x, y, z, BlockType);
          
        }

        public MCFill Clone(string BlockType)
        {
            int[] s = size;
            return new MCFill(s[0], s[1], s[2], p1[0], p1[1], p1[2], BlockType);
        }

        public void Reset(int x, int y, int z)
        {
            int[] s = size;
            Reset(x, y, z, s[0], s[1], s[2]);
        }

        public void Reset(int x1, int y1, int z1, int xs, int ys, int zs)
        {   

            if (x1 < 0)
            {
                p1[0] = x1 - (xs - 1);
                p2[0] = x1;
            }
            else
            {
                p1[0] = x1;
                p2[0] = x1 + (xs - 1);
            }

            if (y1 < 0)
            {
                p1[1] = y1 - (ys - 1);
                p2[1] = y1;
            }
            else
            {
                p1[1] = y1;
                p2[1] = y1 + (ys - 1);
            }

            if (z1 < 0)
            {
                p1[2] = z1 - (zs - 1);
                p2[2] = z1;
            }
            else
            {
                p1[2] = z1;
                p2[2] = z1 + (zs - 1);
            }
            
          
        }

        

    }

   
    /// <summary>
    /// A collection of MCFill objects that could resemble a room structure.
    /// </summary>
    public class MCRoomFill : List<MCFill>
    {

  
        public MCRoomFill() : base() { }
        public MCRoomFill(MCRoomFill Room) : base(Room) { }
        public MCRoomFill(params MCFill[] Fill)
            : base()
        {
            AddRange(Fill);
        }
        public MCRoomFill(MCRoomFill Room, params MCFill[] Fill)
            : base(Room)
        {
            AddRange(Fill);
        }
        public MCRoomFill(params MCRoomFill[] Rooms)
            : base()
        {
            foreach (MCRoomFill rm in Rooms)
                AddRange(rm);
        }


        public string[] Render()
        {
            string[] render = new string[this.Count];
            for (int x = 0; x < this.Count; x++)
                render[x] = this[x].Render(MCRenderFacing.North);

            return render;
        }
        public string[] Render(MCRenderFacing Facing, int Pitch, bool IsAbsolute, params int[] Offset)
        {
            string[] render = new string[this.Count];
            for (int x = 0; x < this.Count; x++)
                render[x] = this[x].Render(Facing, Pitch, IsAbsolute, Offset);

            return render;
        }

        public void OffsetMCFill(int[] voxelOffset)
        {
            foreach(MCFill mcf in this)
            {
                mcf.OffsetFill(voxelOffset);

            }


        }

    }

    /// <summary>
    /// Template class to help with creating 'rooms', various fill patterns.
    /// 
    ///
    /// </summary>
    public class MCRoomFillTemplate
    {


        private string emptyBlock;
        private string fillBlock;
        private int[] offsetVoxel = new int[3];

        private bool autoOffset = false;

        /// <summary>
        /// Any fill objects will be permamently offset changed using the values passed.  Set an activate
        /// offset render mode.
        /// </summary>
        /// <param name="x">X Axis offset.</param>
        /// <param name="y">Y Axis offset.</param>
        /// <param name="z">Z Axis offset.</param>
        public void ActivateOffset(int x, int y, int z)
        {
            offsetVoxel[0] = x; offsetVoxel[1] = y; offsetVoxel[2] = z;
            autoOffset = true;
        }
        /// <summary>
        /// Toggle offset adjustments on or off.
        /// </summary>
        public void ActivateOffset()
        {
            autoOffset = true;
        }

        /// <summary>
        /// Turn off offset option.
        /// </summary>
        public void DeactivateOffset() { autoOffset = false; }

        private MCRoomFill applyOffset(MCRoomFill Room)
        {

            if (autoOffset == true)
                Room.OffsetMCFill(offsetVoxel);

            return Room;


        }

        /// <summary>
        /// Minecraft block entity string used for an Emptied State
        /// </summary>
        public string EmptyFillBlock { get { return emptyBlock; } set { emptyBlock = value; } }
        /// <summary>
        /// Minecraft block entity string used for Filled State
        /// </summary>
        public string FillBlock { get { return fillBlock; } set { fillBlock = value; } }


        public MCFill Fill(string BlockType) { return new MCFill(6, 6, 6, 0, 0, 1, BlockType); }

        public MCFill FillCenter(string BlockType) { return new MCFill(4, 6, 4, 1, 0, 1, BlockType); }
        public MCFill FillCenterPillar(string BlockType) { return new MCFill(2, 6, 2, 2, 0, 3, BlockType); }

        public MCFill FillLeftWall(string BlockType) { return new MCFill(1, 6, 6, 0, 0, 1, BlockType); }
        public MCFill FillRightWall(string BlockType) { return new MCFill(1, 6, 6, 5, 0, 1, BlockType); }
        public MCFill FillFrontWall(string BlockType) { return new MCFill(6, 6, 1, 0, 0, 1, BlockType); }
        public MCFill FillBackWall(string BlockType) { return new MCFill(6, 6, 1, 0, 0, 6, BlockType); }

        public MCFill FillCenterLeftWall(string BlockType) { return new MCFill(1, 6, 4, 1, 0, 1, BlockType); }
        public MCFill FillCenterRightWall(string BlockType) { return new MCFill(1, 6, 4, 4, 0, 1, BlockType); }
        public MCFill FillCenterFrontWall(string BlockType) { return new MCFill(4, 6, 1, 1, 0, 1, BlockType); }
        public MCFill FillCenterBackWall(string BlockType) { return new MCFill(4, 6, 1, 1, 0, 5, BlockType); }

        public MCFill FillCenterPillarLeft(string BlockType) { return new MCFill(1, 6, 2, 2, 0, 3, BlockType); }
        public MCFill FillCenterPillarRight(string BlockType) { return new MCFill(1, 6, 2, 3, 0, 3, BlockType); }
        public MCFill FillCenterPillarFront(string BlockType) { return new MCFill(2, 6, 1, 2, 0, 3, BlockType); }
        public MCFill FillCenterPillarBack(string BlockType) { return new MCFill(2, 6, 1, 2, 0, 4, BlockType); }

        public MCFill FillFloor(string BlockType) { return new MCFill(6, 1, 6, 0, 0, 1, BlockType); }
        public MCFill FillCeiling(string BlockType) { return new MCFill(6, 1, 6, 0, 5, 1, BlockType); }

        public MCFill FillBackLeftCorner(string BlockType) { return new MCFill(2, 6, 2, 0, 0, 5, BlockType); }
        public MCFill FillBackRightCorner(string BlockType) { return new MCFill(2, 6, 2, 4, 0, 5, BlockType); }
        public MCFill FillFrontLeftCorner(string BlockType) { return new MCFill(2, 6, 2, 0, 0, 1, BlockType); }
        public MCFill FillFrontRightCorner(string BlockType) { return new MCFill(2, 6, 2, 4, 0, 1, BlockType); }

        public MCFill FillLRHall(string BlockType) { return new MCFill(6, 6, 2, 0, 0, 3, BlockType); }
        public MCFill FillFBHall(string BlockType) { return new MCFill(2, 6, 6, 2, 0, 1, BlockType); }

        public MCFill FillLeftWallSolid(string BlockType) { return new MCFill(3, 6, 6, 0, 0, 1, BlockType); }
        public MCFill FillRightWallSolid(string BlockType) { return new MCFill(3, 6, 6, 3, 0, 1, BlockType); }
        public MCFill FillBackWallSolid(string BlockType) { return new MCFill(6, 6, 3, 0, 0, 4, BlockType); }
        public MCFill FillFrontWallSolid(string BlockType) { return new MCFill(6, 6, 3, 0, 0, 1, BlockType); }



        public MCRoomFillTemplate()
        {

            fillBlock = "minecraft:stone";
            emptyBlock = "minecraft:air";

        }
        public MCRoomFillTemplate(string FillBlock, string EmptyBlock)
        {
            fillBlock = FillBlock;
            emptyBlock = EmptyBlock;



        }



        public MCRoomFill NSEWRoom { get { return applyOffset(new MCRoomFill(Fill(fillBlock), FillCenter(emptyBlock), FillLRHall(emptyBlock), FillFBHall(emptyBlock))); } }
        public MCRoomFill NSEWRoomPillar { get { return applyOffset(new MCRoomFill(NSEWRoom, FillCenterPillar(fillBlock))); } }

        public MCRoomFill NSEWRoomC { get { return applyOffset(new MCRoomFill(FillCenterFrontWall(fillBlock), FillCenterBackWall(fillBlock), FillCenterLeftWall(fillBlock), FillCenterRightWall(fillBlock), FillLRHall(emptyBlock), FillFBHall(emptyBlock))); } }


        public MCRoomFill EmptyRoom { get { return applyOffset(new MCRoomFill(Fill(emptyBlock))); } }
        public MCRoomFill SolidRoom { get { return applyOffset(new MCRoomFill(Fill(fillBlock))); } }

        public MCRoomFill EmptyCenter { get { return applyOffset(new MCRoomFill(FillCenter(emptyBlock))); } }
        public MCRoomFill SolidCenter { get { return applyOffset(new MCRoomFill(FillCenter(fillBlock))); } }

        public MCRoomFill EmptyCenterPillar { get { return applyOffset(new MCRoomFill(FillCenterPillar(emptyBlock))); } }
        public MCRoomFill SolidCenterPillar { get { return applyOffset(new MCRoomFill(FillCenterPillar(fillBlock))); } }

        public MCRoomFill BLRWall { get { return applyOffset(new MCRoomFill(FillLeftWall(fillBlock), FillBackWall(fillBlock), FillRightWall(fillBlock))); } }
        public MCRoomFill BRFWall { get { return applyOffset(new MCRoomFill(FillRightWall(fillBlock), FillFrontWall(fillBlock), FillBackWall(fillBlock))); } }
        public MCRoomFill FLRWall { get { return applyOffset(new MCRoomFill(FillLeftWall(fillBlock), FillFrontWall(fillBlock), FillRightWall(fillBlock))); } }
        public MCRoomFill FLBWall { get { return applyOffset(new MCRoomFill(FillLeftWall(fillBlock), FillFrontWall(fillBlock), FillBackWall(fillBlock))); } }

        public MCRoomFill BLCorner { get { return applyOffset(new MCRoomFill(FillBackLeftCorner(fillBlock))); } }
        public MCRoomFill BRCorner { get { return applyOffset(new MCRoomFill(FillBackRightCorner(fillBlock))); } }
        public MCRoomFill FLCorner { get { return applyOffset(new MCRoomFill(FillFrontLeftCorner(fillBlock))); } }
        public MCRoomFill FRCorner { get { return applyOffset(new MCRoomFill(FillFrontRightCorner(fillBlock))); } }

        public MCRoomFill BLWall { get { return applyOffset(new MCRoomFill(FillLeftWall(fillBlock), FillBackWall(fillBlock))); } }
        public MCRoomFill BRWall { get { return applyOffset(new MCRoomFill(FillRightWall(fillBlock), FillBackWall(fillBlock))); } }
        public MCRoomFill FLWall { get { return applyOffset(new MCRoomFill(FillLeftWall(fillBlock), FillFrontWall(fillBlock))); } }
        public MCRoomFill FRWall { get { return applyOffset(new MCRoomFill(FillRightWall(fillBlock), FillFrontWall(fillBlock))); } }

        public MCRoomFill LRWall { get { return applyOffset(new MCRoomFill(FillLeftWall(fillBlock), FillRightWall(fillBlock))); } }
        public MCRoomFill FBWall { get { return applyOffset(new MCRoomFill(FillFrontWall(fillBlock), FillBackWall(fillBlock))); } }

        public MCRoomFill LRWallC { get { return applyOffset(new MCRoomFill(FillCenterLeftWall(fillBlock), FillCenterRightWall(fillBlock))); } }
        public MCRoomFill FBWallC { get { return applyOffset(new MCRoomFill(FillCenterFrontWall(fillBlock), FillCenterBackWall(fillBlock))); } }

        public MCRoomFill LeftWall { get { return applyOffset(new MCRoomFill(FillLeftWall(fillBlock))); } }
        public MCRoomFill RightWall { get { return applyOffset(new MCRoomFill(FillRightWall(fillBlock))); } }
        public MCRoomFill BackWall { get { return applyOffset(new MCRoomFill(FillBackWall(fillBlock))); } }
        public MCRoomFill FrontWall { get { return applyOffset(new MCRoomFill(FillFrontWall(fillBlock))); } }

        public MCRoomFill LeftWallC { get { return applyOffset(new MCRoomFill(FillCenterLeftWall(fillBlock))); } }
        public MCRoomFill RightWallC { get { return applyOffset(new MCRoomFill(FillCenterRightWall(fillBlock))); } }
        public MCRoomFill BackWallC { get { return applyOffset(new MCRoomFill(FillCenterBackWall(fillBlock))); } }
        public MCRoomFill FrontWallC { get { return applyOffset(new MCRoomFill(FillCenterFrontWall(fillBlock))); } }

        public MCRoomFill LeftWallSolid { get { return applyOffset(new MCRoomFill(FillLeftWallSolid(fillBlock))); } }
        public MCRoomFill RightWallSolid { get { return applyOffset(new MCRoomFill(FillRightWallSolid(fillBlock))); } }
        public MCRoomFill BackWallSolid { get { return applyOffset(new MCRoomFill(FillBackWallSolid(fillBlock))); } }
        public MCRoomFill FrontWallSolid { get { return applyOffset(new MCRoomFill(FillFrontWallSolid(fillBlock))); } }



    }

    public class MCMapRoom
    {

        MCRenderFacing defaultFacing = MCRenderFacing.North;
        int pitch = 0;

        public MCRoomFill room { get; set; }

        int mapX;
        int mapY;
        int mapZ;

        public MCMapRoom()
        {
            room = new MCRoomFill(new MCFill(6, 6, 6, 0, 0, 1, "minecraft:air"));
            mapX = 0;
            mapY = 0;
            mapZ = 0;
            defaultFacing = MCRenderFacing.North;
            pitch = 0;

        }
        public MCMapRoom(MCRoomFill room, int x, int y, int z)
        {
            this.room = room;
            mapX = x; mapY = y; mapZ = z;
            defaultFacing = MCRenderFacing.North;
            pitch = 0;

        }
        public MCMapRoom(MCRoomFill room, int x, int y, int z, MCRenderFacing Facing, int Pitch)
            : this(room, x, y, z)
        {
            defaultFacing = Facing;
            pitch = Pitch;
        }
       
        public string[] Render()
        {
            return room.Render(defaultFacing, pitch, false, mapX * 6, mapY * 6, mapZ * 6);
        }
        public string[] Render(MCRenderFacing Facing, int Pitch)
        {
            return room.Render(Facing, Pitch, false, mapX * 6, mapY * 6, mapZ * 6);
        }
        public string[] RenderAbsolute(int x, int y, int z)
        {
            return room.Render(defaultFacing, pitch, true, x + (mapX * 6), y + (mapY * 6), z + (mapZ * 6));

        }
        public string[] RenderAbsolute(MCRenderFacing Facing, int Pitch, int x, int y, int z)
        {
            return room.Render(Facing, Pitch, true, x + (mapX * 6), y + (mapY * 6), z + (mapZ * 6));
        }


    }

    public class MCMap : List<MCMapRoom>
    {
        MCRenderFacing facing = MCRenderFacing.North;
        int pitch = 0;

        public MCMap() : base() { facing = MCRenderFacing.North; pitch = 0; }
        public MCMap(MCRenderFacing Facing, int Pitch) : base() { facing = Facing; pitch = Pitch; }



        public void InsertMapFill(string BlockType, int FloorWidth, int FloorHeight, int FloorDepth)
        {
            Add(new MCMapRoom(new MCRoomFill(new MCFill(FloorWidth * 6, FloorHeight * 6, FloorDepth * 6, 0, 0, 0, BlockType)), 0, 0, 0));
        }

        public string[] RenderMap()
        {
            List<string> cmds = new List<string>();
            foreach (MCMapRoom room in this)
            {
                cmds.AddRange(room.Render(facing, pitch));

            }

            return cmds.ToArray();
        }
        public string[] RenderMap(MCRenderFacing Facing, int Pitch)
        {

            List<string> cmds = new List<string>();
            foreach (MCMapRoom room in this)
            {
                cmds.AddRange(room.Render(Facing, Pitch));

            }

            return cmds.ToArray();

        }

        public string[] RenderMapAbsolute(int x, int y, int z)
        {

            List<string> cmds = new List<string>();
            foreach (MCMapRoom room in this)
            {
                cmds.AddRange(room.RenderAbsolute(x, y, z));

            }

            return cmds.ToArray();

        }
        public string[] RenderMapAbsolute(MCRenderFacing Facing, int Pitch, int x, int y, int z)
        {

            List<string> cmds = new List<string>();
            foreach (MCMapRoom room in this)
            {
                cmds.AddRange(room.RenderAbsolute(Facing, Pitch, x, y, z));

            }

            return cmds.ToArray();

        }

    }
}

namespace WebData
{
    [Serializable]
    public class Poi
    {
        private int mcX;
        private int mcY;
        private int rx;
        private int ry;
        private int ox;
        private int oy;
                

        private int dbID;

        public int X
        {
            get { return mcX; }
            set
            {
                mcX = value;
                
            }
        }
        public int Y
        {
            get { return mcY; }
            set
            {
                mcY = value;
            }
        }

        public Poi(){}

        public Poi(int MineCraftX, int MineCraftZ)
        {
            X = MineCraftX;
            Y = MineCraftZ;

            Calculate();
        }

        public void SetPoi(int MineCraftX,int MineCraftZ)
        {
            X = MineCraftX;
            Y = MineCraftZ;

            Calculate();
        }
        public void Calculate()
        {
            rx = (mcX < 0) ? (mcX / 512) - 1 : (mcX / 512);
            ox = (mcX < 0) ? 512 + (mcX - ((mcX / 512) * 512)) : mcX - ((mcX / 512) * 512);

            ry = (mcY < 0) ? (mcY / 512) - 1 : (mcY / 512);
            oy = (mcY < 0) ? 512 + (mcY - ((mcY / 512) * 512)) : mcY - ((mcY / 512) * 512);

        }
       
        public string RenderLargeBox()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(@"<img src=""point6.ico"" style=""position:absolute;top:{0}px;left:{1}px;z-index:1;"" />", oy - 31, ox - 31);
            return sb.ToString();
        }


        public void RenderPoiQuery(StringBuilder sb,string imgName)
        {

            sb.AppendFormat(@"<img src=""{2}"" style=""position:absolute;top:{0}px;left:{1}px;z-index:1;pointer-events:none;"" />",oy-7,ox-7,imgName);
        }

        public void RenderPoiText(StringBuilder sb)
        {

            sb.AppendFormat(@"<input type=""text"" style=""position:absolute;top:{0}px;left:{1}px;z-index:1"" />", oy + 7,ox);
        }

        public void RenderPoi(StringBuilder sb)
        {


            sb.AppendFormat(@"<a href=""pointinfo.aspx?x={2}&y={3}"" target=""_blank""><img src=""point4.ico"" style=""position:absolute;top:{0}px;left:{1}px;z-index:1;"" /></a>", oy - 7, ox - 7, mcX, mcY);
            
        }
        public string RenderPoi()
        {
           

            StringBuilder sb = new StringBuilder();

            sb.AppendFormat(@"<a href=""pointinfo.aspx?x={2}&y={3}"" target=""_blank""><img src=""point4.ico"" style=""position:absolute;top:{0}px;left:{1}px;z-index:1;"" /></a>", oy - 7, ox - 7, mcX, mcY);
            return sb.ToString();
        }




    }
    
}