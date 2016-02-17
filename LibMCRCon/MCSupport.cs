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
using System.IO;
using System.IO.Compression;


using LibMCRcon.Nbt;
using LibMCRcon.WorldData;



//!Classes directly related to the minecraft server.
namespace LibMCRcon.RCon
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
            catch (Exception e)
            {
                isBadPacket = true;
                response = e.Message;

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

        public TCPState StateTCP { get; set; }
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
        public TCPRcon()
            : base()
        {


        }
        /// <summary>
        /// Create a TCPRcon connection.  Does not open on creation.
        /// </summary>
        /// <param name="MineCraftServer">DNS address of the rcon server.</param>
        /// <param name="port">Port RCon is listening to on the server.</param>
        /// <param name="password">Configured password for the RCon server.</param>
        public TCPRcon(string host, int port, string password)
            : base()
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

            TCPRcon r = new TCPRcon(RConHost, RConPort, RConPass);
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
                    StopComms();
                }

            bgCommThread = null;
            Connecting = true;

            TimeCheck tc = new TimeCheck();

            for (int x = 0; x < 6; x++)
            {
                ResetConnectAttemps = x;

                cli = new TcpClient();
                bgCommThread = new Thread(ConnectAndProcess);
                bgCommThread.IsBackground = true;

                StateTCP = TCPState.IDLE;
                StateRCon = RConState.IDLE;

                bgCommThread.Start();

                tc.Reset(10000);
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


            StopComms();

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

                            if (Count > 1500)
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

            RconPacket p;
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
            Voxel pV = MinecraftOrdinates.Region();
            string result;
            if (r.IsReadyForCommands)
            {
                result = r.ExecuteCmd(string.Format("execute {0} ~ ~ ~ testforblock ~ ~-1 ~ minecraft:lava", player));
                if (result.Length > 0)
                {
                    int ix = result.IndexOf("is");
                    if (ix != -1)
                    {
                        string data = result.Substring(12, result.IndexOf("is") - 12);
                        string[] tpdata = data.Split(new char[] { ',' });

                        float tf = 0;

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
                                    return pV;
                                }

                            }
                        }
                    }
                }
            }

            pV.IsValid = false;
            return pV;

        }
        public Voxel PlayerLocationTP(TCPRcon r, StringBuilder sb, String player)
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
                    sb.AppendFormat(@"{0} => Connection:{1}, Network:{2}", ee.Message, r.LastTCPError, r.StateTCP, r.StateRCon);

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
namespace LibMCRcon.Maps
{


    /// <summary>
    /// Enumeration to set the facing of the 'player' before rendering fill commands.
    /// </summary>
    public enum MCRenderFacing { North = 0, South = 1, East = 2, West = 3 };
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
        public string BlockType { get; set; }

        int[] p1 = new int[3];
        int[] p2 = new int[3];

        /// <summary>
        /// Permanently offsets the fill cube.
        /// </summary>
        /// <param name="offsetVoxel">An array representing 3 axis of offset. X,Y,Z [0..2]</param>
        public void OffsetFill(int[] offsetVoxel)
        {
            for (int x = 0; x < 3; x++)
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
        public MCFill(int xs, int ys, int zs, int x1, int y1, int z1, string BlockType)
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
            foreach (MCFill mcf in this)
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

        public Poi() { }

        public Poi(int MineCraftX, int MineCraftZ)
        {
            X = MineCraftX;
            Y = MineCraftZ;

            Calculate();
        }

        public void SetPoi(int MineCraftX, int MineCraftZ)
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


        public void RenderPoiQuery(StringBuilder sb, string imgName)
        {

            sb.AppendFormat(@"<img src=""{2}"" style=""position:absolute;top:{0}px;left:{1}px;z-index:1;pointer-events:none;"" />", oy - 7, ox - 7, imgName);
        }

        public void RenderPoiText(StringBuilder sb)
        {

            sb.AppendFormat(@"<input type=""text"" style=""position:absolute;top:{0}px;left:{1}px;z-index:1"" />", oy + 7, ox);
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

namespace LibMCRcon.Rendering
{

    public class ColorStep
    {
        public int Steps { get; set; }
        public Color Color { get; set; }

        public ColorStep() { Color = Color.Black; Steps = 1; }
        public ColorStep(Color Color) { this.Color = Color; Steps = 1; }
        public ColorStep(Color Color, int Steps) { this.Color = Color; this.Steps = Steps; }

        public static Color[] CreatePallet(List<ColorStep> cList)
        {
            Color[] p = new Color[256];

            int z = 0;

            ColorStep A;
            ColorStep B;

            for (int y = 0; y < cList.Count; y++)
            {

                A = cList[y];

                if ((y + 1) < cList.Count)
                    B = cList[y + 1];
                else
                    B = Color.White.ColorStep(0);



                for (int x = 0; x < A.Steps; x++)
                {


                    Single aL = (1f / A.Steps) * x;


                    byte R1 = 0;
                    byte G1 = 0;
                    byte B1 = 0;

                    R1 = (byte)(((A.Color.R * (1 - aL)) + (B.Color.R * aL)) / 2);
                    G1 = (byte)(((A.Color.G * (1 - aL)) + (B.Color.G * aL)) / 2);
                    B1 = (byte)(((A.Color.B * (1 - aL)) + (B.Color.B * aL)) / 2);



                    p[z] = Color.FromArgb(R1, G1, B1);
                    z++;

                    if (z > 255)
                        return p;

                }
            }

            return p;
        }
        public static Color MixColors(Single Percentage, Color A, Color B, int WhiteBalance = 10)
        {
            int R1 = 0;
            int G1 = 0;
            int B1 = 0;
            Single aL = Percentage / 100;

            R1 = (int)((((A.R * aL) + (B.R * (1 - aL))) / 2) + WhiteBalance);
            G1 = (int)((((A.G * aL) + (B.G * (1 - aL))) / 2) + WhiteBalance);
            B1 = (int)((((A.B * aL) + (B.B * (1 - aL))) / 2) + WhiteBalance);

            if (R1 > 255) R1 = 255;
            if (G1 > 255) G1 = 255;
            if (B1 > 255) B1 = 255;


            return Color.FromArgb(R1, G1, B1);
        }
    }
    public static class ColorStepExtension
    {

        public static ColorStep ColorStep(this Color Color, int Steps)
        {
            return new ColorStep(Color, Steps);
        }

    }

    public static class MCRegionMaps
    {
        public static Color[][] BlockPalette()
        {
            Color[][] Blocks = new Color[256][];

            for (int x = 0; x < 256; x++)
            {
                switch (x)
                {
                    case 1://stone
                        Blocks[x] = new Color[] { Color.Gray };
                        break;
                    case 2://grass
                        Blocks[x] = new Color[] { Color.Green };
                        break;
                    case 3://dirt
                        Blocks[x] = new Color[] { Color.Brown };
                        break;
                    case 4://cobble
                        Blocks[x] = new Color[] { Color.LightGray };
                        break;
                    case 5://wood plank
                        Blocks[x] = new Color[] { ColorStep.MixColors(80, Color.Brown, Color.Black) };
                        break;
                    case 6://sapling
                        Blocks[x] = new Color[] { Color.Green };
                        break;
                    case 7://bed rock
                        Blocks[x] = new Color[] { Color.DarkGray };
                        break;
                    case 8://flowing water
                    case 9://water
                        Blocks[x] = new Color[] { Color.Blue };
                        break;

                    case 10://flo lava
                    case 11://lava
                        Blocks[x] = new Color[] { Color.Orange };
                        break;
                    case 12://sand
                        Blocks[x] = new Color[] { ColorStep.MixColors(90, Color.Beige, Color.Black) };
                        break;
                    case 13://gravel
                        Blocks[x] = new Color[] { ColorStep.MixColors(50, Color.Gray, Color.Black) };
                        break;
                    case 14://gold ore
                        Blocks[x] = new Color[] { Color.Yellow };
                        break;
                    case 15://iron ore
                    case 16://coal ore
                        Blocks[x] = new Color[] { ColorStep.MixColors(30, Color.Gold, Color.Black) };
                        break;
                    case 17://wood
                        Blocks[x] = new Color[] { Color.Brown };
                        break;
                    case 18://leaves
                    case 161://Acacia Leaves (0),(1) dark oak leaves
                    case 162://acacia wood (0), (1) dark oak wood
                        Blocks[x] = new Color[] { Color.DarkGreen };
                        break;
                    case 19://sponge
                        Blocks[x] = new Color[] { Color.Beige };
                        break;
                    case 20://glass
                        Blocks[x] = new Color[] { Color.LightBlue };
                        break;
                    case 21://lapis ore
                    case 22://lapis block
                        Blocks[x] = new Color[] { Color.DarkBlue };
                        break;
                    case 24://sandstone
                        Blocks[x] = new Color[] { ColorStep.MixColors(90, Color.Beige, Color.Black) };
                        break;
                    case 35://wool
                    case 41://gold block
                    case 42://iron block
                    case 43://x2 stone slab
                    case 44://stone slab
                        Blocks[x] = new Color[] { Color.Gray };
                        break;
                    //  case 45://bricks
                    case 49://obsidian
                        Blocks[x] = new Color[] { Color.DarkViolet };
                        break;
                    case 51://fire
                        Blocks[x] = new Color[] { Color.Orange };
                        break;
                    //case 52://monster spawner
                    case 56://diamond ore
                    case 57://diamond block
                        Blocks[x] = new Color[] { Color.LightBlue };
                        break;
                    case 59://wheat crops
                        Blocks[x] = new Color[] { Color.Wheat };
                        break;
                    case 60://farmland
                        Blocks[x] = new Color[] { Color.BurlyWood };
                        break;
                    case 73://redstone ore
                    case 74://glowing redstone ore
                        Blocks[x] = new Color[] { Color.Red };
                        break;
                    case 78://snow
                        Blocks[x] = new Color[] { Color.White };
                        break;
                    case 79://ice
                        Blocks[x] = new Color[] { Color.SkyBlue };
                        break;
                    case 80://snow block
                        Blocks[x] = new Color[] { Color.White };
                        break;
                    case 81://cactus
                        Blocks[x] = new Color[] { Color.MediumSpringGreen };
                        break;
                    case 82://clay
                        Blocks[x] = new Color[] { Color.Gray };
                        break;
                    case 83://sugar canes
                        Blocks[x] = new Color[] { Color.LimeGreen };
                        break;
                    case 86://pumpkins
                        Blocks[x] = new Color[] { Color.DarkOrange };
                        break;
                    case 87://netherrack
                        Blocks[x] = new Color[] { Color.DarkRed };
                        break;
                    case 88://soul sand
                        Blocks[x] = new Color[] { Color.DarkGray };
                        break;
                    case 89://glow stone
                        Blocks[x] = new Color[] { Color.Goldenrod };
                        break;
                    case 90://nether portal
                        Blocks[x] = new Color[] { Color.PaleVioletRed };
                        break;
                    case 91://jack o'Lantern
                        Blocks[x] = new Color[] { Color.DarkOrange };
                        break;
                    //case 95://stained glass
                    case 98://stone bricks
                        Blocks[x] = new Color[] { Color.Gray };
                        break;
                    case 99://mushroom block (brown)
                        Blocks[x] = new Color[] { ColorStep.MixColors(75, Color.Beige, Color.Brown) };
                        break;
                    case 100://mushroom block (red)
                        Blocks[x] = new Color[] { Color.DeepPink };
                        break;
                    case 103://melon block
                        Blocks[x] = new Color[] { Color.Lime };
                        break;
                    case 110://mycelium
                        Blocks[x] = new Color[] { Color.MediumAquamarine };
                        break;
                    case 112://nether brick
                        Blocks[x] = new Color[] { Color.Maroon };
                        break;
                    case 125://x2 wood slab
                    case 126://wood slab
                        Blocks[x] = new Color[] { Color.BurlyWood };
                        break;
                    case 129://emerald ore
                    case 133://emerald block
                        Blocks[x] = new Color[] { Color.LightGreen };
                        break;
                    case 137://command block
                    case 141://carrots
                        Blocks[x] = new Color[] { ColorStep.MixColors(80, Color.Orange, Color.White) };
                        break;
                    case 142://potatoes
                        Blocks[x] = new Color[] { Color.DarkGoldenrod };
                        break;
                    case 152://redstone block
                        Blocks[x] = new Color[] { Color.Red };
                        break;
                    case 153://nether quartz block
                    case 155://quartz block
                        Blocks[x] = new Color[] { Color.MintCream };
                        break;

                    case 159://white stained clay


                        Blocks[x] = new Color[16] {Color.Beige,Color.Beige,Color.Beige,Color.Beige,Color.Beige
                                            ,Color.Beige,Color.Beige,Color.Beige,Color.Beige,Color.Beige,Color.Beige,Color.Beige
                                            ,Color.Beige,Color.Beige,Color.Beige,Color.Beige
                                        };

                        break;

                    //case 160://stained glass

                    //case 165://slime block
                    //case 166://barrier
                    case 168://prismarine
                    case 169://sea lantern
                        Blocks[x] = new Color[] { Color.SeaGreen };
                        break;

                    case 170://hay bale
                        Blocks[x] = new Color[] { Color.LightYellow };
                        break;
                    case 171://carpet (0-white, 1-15)
                        Blocks[x] = new Color[16] {Color.Beige,Color.Beige,Color.Beige,Color.Beige,Color.Beige
                                            ,Color.Beige,Color.Beige,Color.Beige,Color.Beige,Color.Beige,Color.Beige,Color.Beige
                                            ,Color.Beige,Color.Beige,Color.Beige,Color.Beige
                                        };

                        break;
                    case 172://hardened clay
                        Blocks[x] = new Color[] { Color.Firebrick };
                        break;
                    case 173://block of coal
                        Blocks[x] = new Color[] { Color.Black };
                        break;
                    case 174://packed ice
                        Blocks[x] = new Color[] { Color.LightSkyBlue };
                        break;
                    case 179://red sandstone
                    case 181://x2 red sandstone slab
                    case 182://red sandstone slab

                        Blocks[x] = new Color[] { Color.Firebrick };
                        break;

                    default:
                        Blocks[x] = new Color[] { Color.Gray };
                        break;


                }
            }

            return Blocks;
        }
        public static Color[][] Palettes()
        {

            Color[] Water;
            Color[] Topo;

            List<ColorStep> cList = new List<ColorStep>();

            cList.Add(Color.Black.ColorStep(20));
            cList.Add(Color.Pink.ColorStep(20));
            cList.Add(Color.Blue.ColorStep(20));
            cList.Add(Color.FromArgb(0xDF, 0xC7, 0x00).ColorStep(20));
            cList.Add(Color.DarkGreen.ColorStep(20));
            cList.Add(Color.Orange.ColorStep(20));
            cList.Add(Color.Brown.ColorStep(20));
            cList.Add(Color.Plum.ColorStep(20));
            cList.Add(Color.Magenta.ColorStep(20));
            cList.Add(Color.Coral.ColorStep(20));
            cList.Add(Color.Aqua.ColorStep(20));
            cList.Add(Color.LightCyan.ColorStep(20));
            cList.Add(Color.Yellow.ColorStep(15));


            Topo = ColorStep.CreatePallet(cList);


            cList.Clear();
            cList.Add(Color.Blue.ColorStep(50));
            cList.Add(Color.Aqua.ColorStep(50));
            cList.Add(Color.Teal.ColorStep(50));
            cList.Add(Color.Cyan.ColorStep(50));
            cList.Add(Color.SkyBlue.ColorStep(25));
            cList.Add(Color.Turquoise.ColorStep(25));

            Water = ColorStep.CreatePallet(cList);






            return new Color[][] { Topo, Water };
        }

        public static void Stitched(string ImagesPath, Voxel V,string ImgType = "topo")
        {

            DirectoryInfo imgDir = new DirectoryInfo(ImagesPath);

            string SaveBitMap = string.Format(Path.Combine(imgDir.FullName, string.Format("{2}Cent.{0}.{1}.png", V.Xs, V.Zs, ImgType)));
           
            Voxel R = MinecraftOrdinates.Region(V);
            
            R.X -= 256;
            R.Z -= 256;

            int sx = R.Xo;
            int sy = R.Zo;

            Rectangle fR = new Rectangle();
            FileInfo fQ;

            Bitmap b1;
            Graphics g;
            Image bQ = null;

            Bitmap b2 = new Bitmap(512, 512);

            if (sx == 0 && sy == 0)
            {
                b1 = new Bitmap(512, 512);
                g = Graphics.FromImage(b1);

                fQ = new FileInfo(Path.Combine(ImagesPath, string.Format("{2}.{0}.{1}.png", R.Xs, R.Zs, ImgType)));
                if (fQ.Exists)
                {
                    bQ = Image.FromFile(fQ.FullName);
                    g.DrawImage(bQ, 0, 0);
                    bQ.Dispose();
                }
            }
            else if (sx > 0 && sy == 0)
            {
                b1 = new Bitmap(1024, 512);
                g = Graphics.FromImage(b1);

                fQ = new FileInfo(Path.Combine(ImagesPath, string.Format("{2}.{0}.{1}.png", R.Xs, R.Zs, ImgType)));
                if (fQ.Exists)
                {
                    bQ = Image.FromFile(fQ.FullName);
                    g.DrawImage(bQ, 0, 0);
                    bQ.Dispose();
                }

                fQ = new FileInfo(Path.Combine(ImagesPath, string.Format("{2}.{0}.{1}.png", R.Xs + 1, R.Zs, ImgType)));
                if (fQ.Exists)
                {
                    bQ = Image.FromFile(fQ.FullName);
                    g.DrawImage(bQ, 512, 0);
                    bQ.Dispose();
                }
            }
            else if (sx == 0 && sy > 0)
            {
                b1 = new Bitmap(512, 1024);
                g = Graphics.FromImage(b1);


                fQ = new FileInfo(Path.Combine(ImagesPath, string.Format("{2}.{0}.{1}.png", R.Xs, R.Zs, ImgType)));
                if (fQ.Exists)
                {
                    bQ = Image.FromFile(fQ.FullName);
                    g.DrawImage(bQ, 0, 0);
                    bQ.Dispose();
                }

                fQ = new FileInfo(Path.Combine(ImagesPath, string.Format("{2}.{0}.{1}.png", R.Xs, R.Zs + 1, ImgType)));
                if (fQ.Exists)
                {
                    bQ = Image.FromFile(fQ.FullName);
                    g.DrawImage(bQ, 0, 512);
                    bQ.Dispose();
                }
            }
            else
            {
                b1 = new Bitmap(1024, 1024);
                g = Graphics.FromImage(b1);

                fQ = new FileInfo(Path.Combine(ImagesPath, string.Format("{2}.{0}.{1}.png", R.Xs, R.Zs, ImgType)));
                if (fQ.Exists)
                {
                    bQ = Image.FromFile(fQ.FullName);
                    g.DrawImage(bQ, 0, 0);
                    bQ.Dispose();
                }

                fQ = new FileInfo(Path.Combine(ImagesPath, string.Format("{2}.{0}.{1}.png", R.Xs + 1, R.Zs, ImgType)));
                if (fQ.Exists)
                {
                    bQ = Image.FromFile(fQ.FullName);
                    g.DrawImage(bQ, 512, 0);
                    bQ.Dispose();
                }

                fQ = new FileInfo(Path.Combine(ImagesPath, string.Format("{2}.{0}.{1}.png", R.Xs, R.Zs + 1, ImgType)));
                if (fQ.Exists)
                {
                    bQ = Image.FromFile(fQ.FullName);
                    g.DrawImage(bQ, 0, 512);
                    bQ.Dispose();
                }

                fQ = new FileInfo(Path.Combine(ImagesPath, string.Format("{2}.{0}.{1}.png", R.Xs + 1, R.Zs + 1, ImgType)));
                if (fQ.Exists)
                {
                    bQ = Image.FromFile(fQ.FullName);
                    g.DrawImage(bQ, 512, 512);
                    bQ.Dispose();
                }
            }




            g.Dispose();
            g = Graphics.FromImage(b2);

            fR.X = 0;
            fR.Y = 0;
            fR.Width = 512;
            fR.Height = 512;

            g.DrawImage(b1, fR, sx, sy, 512 ,512, GraphicsUnit.Pixel);
            
            b1.Dispose();
            g.Dispose();


            b2.Save(SaveBitMap, System.Drawing.Imaging.ImageFormat.Png);
            b2.Dispose();
        }
        public static void RenderBlockPngFromRegion(byte[][] TopoData, Color[] BlockData, string ImgPath, WorldData.Region RV)
        {
            byte[] hMap = TopoData[0];
            byte[] wMap = TopoData[1];

            Bitmap bit = new Bitmap(512, 512);

            Color[][] pal = Palettes();
            Color[] tRGB = pal[0];
            Color[] wRGB = pal[1];

            for (int zz = 0; zz < 512; zz++)
            {

                for (int xx = 0; xx < 512; xx++)
                {

                    int gI = (zz * 512) + xx;

                    if (wMap[gI] < 255)
                        bit.SetPixel(xx, zz, ColorStep.MixColors(35, BlockData[gI], wRGB[wMap[gI]]));
                    else
                        bit.SetPixel(xx, zz, BlockData[gI]);
                }

            }



            DirectoryInfo imgDir = new DirectoryInfo(ImgPath);
            string SaveBitMap = string.Format(Path.Combine(imgDir.FullName, string.Format("tile.{0}.{1}.png", RV.Xs, RV.Zs)));
            bit.Save(SaveBitMap, System.Drawing.Imaging.ImageFormat.Png);
            bit.Dispose();

        }
        public static void RenderTopoPngFromRegion(byte[][] HeightData, string ImgPath, WorldData.Region RV)
        {
            byte[] hMap = HeightData[0];
            byte[] hWMap = HeightData[1];

            Bitmap bit = new Bitmap(512, 512);

            Color[][] pal = Palettes();
            Color[] tRGB = pal[0];
            Color[] wRGB = pal[1];

            for (int zz = 0; zz < 512; zz++)
            {

                for (int xx = 0; xx < 512; xx++)
                {

                    int gI = (zz * 512) + xx;

                    if (hWMap[gI] < 255)

                        bit.SetPixel(xx, zz, wRGB[hWMap[gI]]);
                    else
                    {

                        bit.SetPixel(xx, zz, tRGB[hMap[gI]]);
                    }




                }

            }



            DirectoryInfo imgDir = new DirectoryInfo(ImgPath);
            string SaveBitMap = string.Format(Path.Combine(imgDir.FullName, string.Format("topo.{0}.{1}.png", RV.Xs, RV.Zs)));

            bit.Save(SaveBitMap, System.Drawing.Imaging.ImageFormat.Png);
            bit.Dispose();

        }
        public static void RenderLegend(string ImgPath)
        {

            FileInfo legend = new FileInfo(Path.Combine(ImgPath, "legend.png"));
            if (legend.Exists == false)
            {

                Bitmap bit = new Bitmap(20, 512);

                Color[][] pal = Palettes();

                Color[] tRGB = pal[0];
                Color[] wRGB = pal[1];


                Graphics gBit = Graphics.FromImage(bit);

                for (int z = 0; z < 256; z++)
                {
                    gBit.DrawLine(new Pen(tRGB[z]), 0, 255 - z, 15, 255 - z);
                    gBit.DrawLine(new Pen(wRGB[z]), 0, 511 - z, 15, 511 - z);

                    if (z % 10 == 0)
                    {
                        gBit.DrawLine(new Pen(Color.Black), 16, 255 - z, 19, 255 - z);
                        gBit.DrawLine(new Pen(Color.Black), 16, 511 - z, 19, 511 - z);
                    }
                }

                gBit.Dispose();

                DirectoryInfo imgDir = new DirectoryInfo(ImgPath);
                string SaveBitMap = string.Format(Path.Combine(imgDir.FullName, "legend.png"));
                bit.Save(SaveBitMap, System.Drawing.Imaging.ImageFormat.Png);
                bit.Dispose();
            }
        }
        
        public static void RenderDataFromRegion(RegionMCA mca, WorldData.Region rVox, byte[][] TopoData, Color[] Blocks = null)
        {

            byte[] hMap = TopoData[0];
            byte[] hWMap = TopoData[1];
            Color[][] BlockColors = BlockPalette();
            Voxel Chunk;


            if (mca.IsLoaded)
            {

                for (int zz = 0; zz < 32; zz++)
                    for (int xx = 0; xx < 32; xx++)
                    {


                        rVox.SetOffset(65, xx * 16, zz * 16);
                        rVox.RefreshChunk();
                        Chunk = rVox.Chunk;


                        NbtChunk c = rVox.NbtChunk(mca);
                        NbtChunkSection s;


                        for (int x = 0; x < 16; x++)
                            for (int z = 0; z < 16; z++)
                            {
                                Chunk.Xo = x;
                                Chunk.Zo = z;

                                // c = rVox.NbtChunk(mca);
                                // Debug.Print("{0} {1} {2}", Chunk.X, Chunk.Z, Chunk.Y);
                                int cl = c.Height(Chunk.Xo, Chunk.Zo);
                                if (cl > 0) cl--;

                                hMap[(Chunk.Z * 512) + Chunk.X] = (byte)cl;
                                hWMap[(Chunk.Z * 512) + Chunk.X] = 255;

                                Chunk.Y = cl;

                                s = rVox.NbtChunkSection(mca);

                                int block = s.BlockID(Chunk.ChunkBlockPos());
                                int blockdata = s.BlockData(Chunk.ChunkBlockPos());

                                switch (block)
                                {
                                    case 8:
                                    case 9:


                                        for (int ycl = cl; ycl > 0; ycl--)
                                        {

                                            Chunk.Y = ycl;
                                            s = rVox.NbtChunkSection(mca);
                                            block = s.BlockID(Chunk.ChunkBlockPos());


                                            if (block != 9 && block != 8)
                                            {

                                                hWMap[(Chunk.Z * 512) + Chunk.X] = (byte)ycl;

                                                if (Blocks != null)
                                                {
                                                    switch (block)
                                                    {
                                                        case 159:
                                                            blockdata = s.BlockData(Chunk.ChunkBlockPos());
                                                            if (blockdata > 15 || blockdata < 0)
                                                                Blocks[(Chunk.Z * 512) + Chunk.X] = BlockColors[block][0];
                                                            else
                                                                Blocks[(Chunk.Z * 512) + Chunk.X] = BlockColors[block][blockdata];

                                                            break;

                                                        default:
                                                            Blocks[(Chunk.Z * 512) + Chunk.X] = BlockColors[block][0];
                                                            break;
                                                    }
                                                }


                                                break;
                                            }
                                        }

                                        break;


                                    default:
                                        if (Blocks != null)
                                        {
                                            switch (block)
                                            {
                                                case 159:

                                                    if (blockdata > 15 || blockdata < 0)
                                                        Blocks[(Chunk.Z * 512) + Chunk.X] = BlockColors[block][0];
                                                    else
                                                        Blocks[(Chunk.Z * 512) + Chunk.X] = BlockColors[block][blockdata];

                                                    break;

                                                default:
                                                    Blocks[(Chunk.Z * 512) + Chunk.X] = BlockColors[block][0];
                                                    break;
                                            }
                                        }
                                        break;
                                }


                                /*
                                switch (block)
                                {
                                    case 1://stone
                                    case 2://grass
                                    case 3://dirt
                                    case 4://cobble
                                    case 5://wood plank
                                    case 6://sapling
                                    case 7://bed rock
                                    case 8://flowing water
                                    case 9://water
                                    case 10://flo lava
                                    case 11://lava
                                    case 12://sand
                                    case 13://gravel
                                    case 14://gold ore
                                    case 15://iron ore
                                    case 16://coal ore
                                    case 17://wood
                                    case 18://leaves
                                    case 19://sponge
                                    case 20://glass
                                    case 21://lapis ore
                                    case 22://lapis block
                                    case 24://sandstone
                                    case 35://wool
                                    case 41://gold block
                                    case 42://iron block
                                    case 43://x2 stone slab
                                    case 44://stone slab
                                    case 45://bricks
                                    case 49://obsidian
                                    case 51://fire
                                    case 52://monster spawner
                                    case 56://diamond ore
                                    case 57://diamond block
                                    case 59://wheat crops
                                    case 60://farmland
                                    case 73://redstone ore
                                    case 74://glowing redstone ore
                                    case 78://snow
                                    case 79://ice
                                    case 80://snow block
                                    case 81://cactus
                                    case 82://clay
                                    case 83://sugar canes
                                    case 86://pumpkins
                                    case 87://netherrack
                                    case 88://soul sand
                                    case 89://glow stone
                                    case 90://nether portal
                                    case 91://jack o'Lantern
                                    case 95://stained glass
                                    case 98://stone bricks
                                    case 99://mushroom block (brown)
                                    case 100://mushroom block (red)
                                    case 103://melon block
                                    case 110://mycelium
                                    case 112://nether brick
                                    case 125://x2 wood slab
                                    case 126://wood slab
                                    case 129://emerald ore
                                    case 133://emerald block
                                    case 137://command block
                                    case 141://carrots
                                    case 142://potatoes
                                    case 152://redstone block
                                    case 153://nether quartz block
                                    case 155://quartz block
                                        break;

                                    case 159://white stained clay
                                        switch (blockdata)
                                        {

                                            case 0://white
                                            case 1://orange
                                            case 2://magenta
                                            case 3://light blue
                                            case 4://yellow
                                            case 5://lime
                                            case 6://pink
                                            case 7://gray
                                            case 8://light gray
                                            case 9://cyan
                                            case 10://purple
                                            case 11://blue
                                            case 12://brown
                                            case 13://green
                                            case 14://red
                                            case 15://black
                                                break;

                                        }
                                        break;

                                    case 160://stained glass
                                    case 161://Acacia Leaves (0),(1) dark oak leaves
                                    case 162://acacia wood (0), (1) dark oak wood
                                    case 165://slime block
                                    case 166://barrier
                                    case 168://prismarine
                                    case 169://sea lantern
                                    case 170://hay bale
                                    case 171://carpet (0-white, 1-15)
                                    case 172://hardened clay
                                    case 173://block of coal
                                    case 174://packed ice
                                    case 179://red sandstone
                                    case 181://x2 red sandstone slab
                                    case 182://red sandstone slab

                                    default:
                                        break;


                                }
                                 */


                            }

                    }


            }

        }
       
        public static byte[][] RenderTopoDataFromRegion(RegionMCA mca, WorldData.Region mcr)
        {

            byte[][] topo = new byte[][] { new byte[512 * 512], new byte[512 * 512] };

            RenderDataFromRegion(mca, mcr, topo);

            return topo;

        }
        public static byte[][] RetrieveHDT(Voxel RV, string RegionPath)
        {
            byte[][] MapData = new byte[][] { new byte[512 * 512], new byte[512 * 512] };

            FileInfo mcaF = new FileInfo(Path.Combine(RegionPath, string.Format("r.{0}.{1}.hdt", RV.Xs, RV.Zs)));
            if (mcaF.Exists == true)
            {
                FileStream tempFS = mcaF.Open(FileMode.Open, FileAccess.Read);
                tempFS.Read(MapData[0], 0, 512 * 512);
                tempFS.Read(MapData[1], 0, 512 * 512);
                tempFS.Close();
            }

            return MapData;
        }

    }

   

}
 
namespace LibMCRcon.Remote
{
    
    public class MinecraftRegionFile:WorldData.Region
    {
        public string MCAFileName { get { return string.Format("r.{0}.{1}.mca", Xs, Zs); } }
        public string HDTFileName { get { return string.Format("r.{0}.{1}.hdt", Xs, Zs); } }
        public string TopoFileName { get { return string.Format("topo.{0}.{1}.png", Xs, Zs); } }
        public string TileFileName { get { return string.Format("tile.{0}.{1}.png", Xs, Zs); } }

        public FileInfo MCAFileInfo(string RegionDirectory)
        {
            return new FileInfo(Path.Combine(RegionDirectory, MCAFileName));
        }
        public FileInfo HDTFileInfo(string RegionDirectory)
        {
            return new FileInfo(Path.Combine(RegionDirectory, HDTFileName));
        }

        public bool FTPTransfered { get; set;}

        public bool Exists { get; set; }
        public DateTime LastWriteTime { get; set; }

        public MinecraftRegionFile() : base() { FTPTransfered = false; }
        
        public MinecraftRegionFile(int RegionX, int RegionZ)
            : this()
        {

            Xs = RegionX;
            Zs = RegionZ;
            Y = 255;
            Xo = 256;
            Zo = 256;

        }
        
        public MinecraftRegionFile(string FileName)
            : this()
        {

            string[] v = FileName.Split('.');

            if (v.Length > 2)
            {
                int x = 0;
                if (int.TryParse(v[1], out x) == true)
                {
                    int z = 0;
                    if (int.TryParse(v[2], out z) == true)
                    {
                        SetSegmentOffset(x, z, 256, 256);
                        Y = 255;
                    }
                }
            }
        }

        public MinecraftRegionFile(int x, int y, int z):base(x,y,z){ }
        public MinecraftRegionFile(Voxel v) : base(v) { }

        public bool Done { get; private set; }

        public void Start(bool FullRender, string RegionDirectory, string ImgsDirectory, System.Diagnostics.Process TogosJavaProc)
        {
            
            DirectoryInfo RegionsDir = new DirectoryInfo(RegionDirectory);
            DirectoryInfo ImgsDir = new DirectoryInfo(ImgsDirectory);


            Done = false;

            if (FullRender == true)
            {

                byte[][] MapData = new byte[][] { new byte[512 * 512], new byte[512 * 512] };
                Color[] BlockData = new Color[512 * 512];

                RegionMCA mca = new RegionMCA(RegionsDir.FullName);
                
                mca.LoadRegion(Xs, Zs);

                LibMCRcon.Rendering.MCRegionMaps.RenderDataFromRegion(mca, this, MapData, BlockData);
                LibMCRcon.Rendering.MCRegionMaps.RenderTopoPngFromRegion(MapData, ImgsDir.FullName, this);
                //LibMCRcon.Rendering.MCRegionMaps.RenderBlockPngFromRegion(MapData, BlockData, ImgsDir.FullName, RV);


                FileInfo mcaH = new FileInfo(Path.Combine(RegionsDir.FullName, HDTFileName));

                FileStream tempFS = mcaH.Create();

                tempFS.Write(MapData[0], 0, 512 * 512);
                tempFS.Write(MapData[1], 0, 512 * 512);
                tempFS.Flush();
                tempFS.Close();


                mcaH.LastWriteTime = LastWriteTime;


                TogosJavaProc.StartInfo.Arguments = string.Format("-jar tmcmr.jar -f -o {0} {1}", ImgsDir.FullName, MCAFileName);
                if (TogosJavaProc.Start() == true)
                    TogosJavaProc.WaitForExit();




            }
            else
            {
                byte[][] MapData = new byte[][] { new byte[512 * 512], new byte[512 * 512] };

                FileInfo Hdt = new FileInfo(Path.Combine(RegionsDir.FullName, HDTFileName));
                FileStream tempFS = Hdt.Open(FileMode.Open, FileAccess.Read);

                tempFS.Read(MapData[0], 0, 512 * 512);
                tempFS.Read(MapData[1], 0, 512 * 512);
                tempFS.Close();


                LibMCRcon.Rendering.MCRegionMaps.RenderTopoPngFromRegion(MapData, ImgsDir.FullName, this);

            }

            Done = true;
        }

        public DateTime MCALastWrite(string RegionDirectory)
        {
            FileInfo f = new FileInfo(Path.Combine(RegionDirectory, MCAFileName));
            if (f.Exists)
                return f.LastWriteTime;
            else
                return DateTime.MinValue;
        }
        public DateTime HDTLastWrite(string RegionDirectory)
        {
            FileInfo f = new FileInfo(Path.Combine(RegionDirectory, HDTFileName));
            if (f.Exists)
                return f.LastWriteTime;
            else
                return DateTime.MinValue;
        }
       

    }
    public abstract class FTP
    {

        public abstract void Open();
        public abstract void Close();
        public abstract bool FindFile(string RemoteFullName);
        public abstract bool Download(string RemoteFullName, string DownloadFullPath);
        public abstract bool IsOpen { get; set; }

        public DateTime LastWriteDT { get; set; }
        public bool LastDownloadSuccess { get; set; }


        public string RemoteRegionPath { get; set; }
        public string FtpHost { get; set; }
        public int FtpPort { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public abstract List<MinecraftRegionFile> FindRemoteFiles(string remotepath, string mask);

    }
    
    public class RegionProcessing<AbstractedFTP> : Queue<MinecraftRegionFile> where AbstractedFTP : FTP, new()
    {
        private object thisLock = new object();
        private AbstractedFTP FTP;

        private List<MinecraftRegionFile> _RFHDT = new List<MinecraftRegionFile>();
        private List<MinecraftRegionFile> _RFMCA = new List<MinecraftRegionFile>();


        public string RegionPath { get; set; }
        public string ImgsPath { get; set; }

        public int ProcessThreashold { get; set; }
        public int FileAge { get; set; }

        public bool FTPActive { get; private set; }
        public bool MappingActive { get; private set; }
        
        public bool LocalOnly { get; set; }

        public RegionProcessing()
            : base()
        {
            FileAge = 300;
            FTP = new AbstractedFTP();
            LocalOnly = false;
        }

        public RegionProcessing(string RegionPath, string ImgsPath)
            : this()
        {

            this.RegionPath = RegionPath;
            this.ImgsPath = ImgsPath;

        }

        public RegionProcessing(string RegionPath, string ImgsPath, string RemoteRegionPath, string FtpAddress, int FtpPort, string UserName, string Password)
            : this(RegionPath, ImgsPath)
        {

            FTP.RemoteRegionPath = RemoteRegionPath;
            FTP.FtpHost = FtpAddress;
            FTP.FtpPort = FtpPort;
            FTP.UserName = UserName;
            FTP.Password = Password;


        }


        public void Enqueue(List<MinecraftRegionFile> Q)
        {

            lock (thisLock)
            {
                Q.ForEach(x => base.Enqueue(x));
            }

        }

        private void ProcessFTP()
        {

            MinecraftRegionFile RV = null;
            List<FileInfo> filesTransfered = new List<FileInfo>();
            bool DoFTP = true;

            try
            {


                while (Count > 0)
                {

                    RV = Dequeue();

                    DateTime lastCheckTS = DateTime.MinValue;
                    DateTime latestFileTS = lastCheckTS;


                    string ftpFileName = string.Format(@"{0}/{1}", FTP.RemoteRegionPath, RV.MCAFileName);
                    string localFileName = Path.Combine(RegionPath, RV.MCAFileName);
                    string localHdtFileName = Path.Combine(RegionPath, RV.HDTFileName);
                    string localImgFileName = Path.Combine(ImgsPath, RV.TopoFileName);


                    FileInfo lcHdtFile = new FileInfo(localHdtFileName);
                    FileInfo lcImgFile = new FileInfo(localImgFileName);
                    FileInfo lcMcaFile = new FileInfo(localFileName);

                    DateTime ft = DateTime.MinValue;


                    if (LocalOnly == true)
                    {
                        DoFTP = false;

                        if (lcImgFile.Exists == false)
                        {

                            if (lcMcaFile.Exists)
                            {
                                RV.LastWriteTime = lcMcaFile.LastWriteTime;
                                _RFMCA.Add(RV);
                                
                            }
                            else if (lcHdtFile.Exists)
                            {
                                RV.LastWriteTime = lcHdtFile.LastWriteTime;
                                _RFHDT.Add(RV);
                                
                            }

                        }

                    }
                    else
                        DoFTP = true;

                    if (DoFTP == true)
                    {
                        if (FTP.IsOpen == false)
                        {
                            FTP.Open();
                            FTPActive = true;
                        }

                        if (FTP.FindFile(ftpFileName))
                        {

                            ft = FTP.LastWriteDT;
                            RV.LastWriteTime = ft;

                            if (lcHdtFile.Exists)
                            {

                                TimeSpan diff = ft - lcHdtFile.LastWriteTime;
                                if (diff.TotalSeconds > FileAge)
                                {
                                    if (FTP.Download(ftpFileName, localFileName))
                                        _RFMCA.Add(RV);

                                }
                                else
                                {
                                    if (lcImgFile.Exists == false)
                                        _RFHDT.Add(RV);
                                
                                    else
                                    {

                                        diff = DateTime.Now - lcImgFile.LastWriteTime;

                                        if (diff.TotalSeconds > FileAge)
                                        {
                                            _RFHDT.Add(RV);
                                
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (FTP.Download(ftpFileName, localFileName))
                                    _RFMCA.Add(RV);
                                
                            }

                        }
                    }

                }
            }

            catch (Exception)
            {
                Enqueue(RV);
            }

            if (FTP.IsOpen)
            {
                FTP.Close();
                FTPActive = false;
            }
        }

        private void ProcessMaps()
        {

            MappingActive = true;



            //Togos processing

            string JarName = Path.Combine(RegionPath, "tcmcr.jar");

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.EnableRaisingEvents = false;
            proc.StartInfo.FileName = "java";
            proc.StartInfo.WorkingDirectory = RegionPath;

            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            proc.StartInfo.CreateNoWindow = true;

            _RFMCA.ForEach(x => x.Start(true, RegionPath, ImgsPath, proc));
            _RFHDT.ForEach(x => x.Start(false, RegionPath, ImgsPath, proc));
            

            //_procRegionsList.ForEach(x => x.Start());

            MappingActive = false;
        }

        public void Start()
        {

            _RFMCA.Clear();
            _RFHDT.Clear();

            ProcessFTP();
            ProcessMaps();

        }

        public List<MinecraftRegionFile> ProcessEntireServer()
        {

            

            if(FTP.IsOpen == false)
                FTP.Open();
            
            List<MinecraftRegionFile> RF = FTP.FindRemoteFiles(FTP.RemoteRegionPath, ".mca");

            Enqueue(RF);
            Start();
            
            return RF;
        }
        public List<MinecraftRegionFile> StartVoxelCentered(Voxel V)
        {

            Voxel R = MinecraftOrdinates.Region(V);

            List<MinecraftRegionFile> Q = new List<MinecraftRegionFile>();

            R.X -= 256;
            R.Z -= 256;

            int sx = R.Xo;
            int sy = R.Zo;

            R.Xo = 256;
            R.Zo = 256;

            Voxel S = MinecraftOrdinates.Region(R);

            if (sx == 0 && sy == 0)
            {
                Q.Add(new MinecraftRegionFile(R));
            }
            else if (sx > 0 && sy == 0)
            {
                Q.Add(new MinecraftRegionFile(R));
                S.SetSegment(R.Xs + 1, R.Zs);
                Q.Add(new MinecraftRegionFile(S));
            }
            else if (sx == 0 && sy > 0)
            {
                Q.Add(new MinecraftRegionFile(R));
                S.SetSegment(R.Xs, R.Zs + 1);
                Q.Add(new MinecraftRegionFile(S));
            }
            else
            {
                
                Q.Add(new MinecraftRegionFile(R));

                S.SetSegment(R.Xs+1, R.Zs);
                Q.Add(new MinecraftRegionFile(S));

                S.SetSegment(R.Xs, R.Zs + 1);
                Q.Add(new MinecraftRegionFile(S));

                S.SetSegment(R.Xs+1, R.Zs + 1);
                Q.Add(new MinecraftRegionFile(S));

            }
           
            Enqueue(Q);
            Start();

            return Q;

        }

     

    }

   
}