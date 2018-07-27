/*
Code shameless stolen from Phoenix and Shaostoal Laserdrill Mod (who got it from midspaces admin helper). 
I asked phoenix and I am allowed to use it.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using ProtoBuf;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Game;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game;
using VRage.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace LSE.TractorNetwork
{
    #region MP messaging
    public enum MessageSide
    {
        ServerSide,
        ClientSide
    }


    #endregion

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    class NetworkSessionTractorBeam : MySessionComponentBase
    {
        bool _isInitialized = false;

        protected override void UnloadData()
        {
            try
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(MessageUtils.MessageId, MessageUtils.HandleMessage);
            }
            catch (Exception ex) { LSELogger.Instance.LogException(ex); }

            base.UnloadData();
        }

        public override void UpdateBeforeSimulation()
        {
            if (!_isInitialized && MyAPIGateway.Session != null)
                Init();
        }

        private void Init()
        {
            _isInitialized = true;
            MyAPIGateway.Multiplayer.RegisterMessageHandler(MessageUtils.MessageId, MessageUtils.HandleMessage);
        }
    }

    /// <summary>
    /// This class is a quick workaround to get an abstract class deserialized. It is to be removed when using a byte serializer.
    /// </summary>
    [ProtoContract]
    public class MessageContainer
    {
        [ProtoMember(1)]
        public MessageBase Content;
    }

    public static class MessageUtils
    {
        public static List<byte> Client_MessageCache = new List<byte>();
        public static Dictionary<ulong, List<byte>> Server_MessageCache = new Dictionary<ulong, List<byte>>();

        public static readonly ushort MessageId = 5402;
        static readonly int MAX_MESSAGE_SIZE = 4096;

        public static void SendMessageToServer(MessageBase message)
        {
            message.Side = MessageSide.ServerSide;
            if (MyAPIGateway.Session.Player != null)
                message.SenderSteamId = MyAPIGateway.Session.Player.SteamUserId;
            var xml = MyAPIGateway.Utilities.SerializeToXML<MessageContainer>(new MessageContainer() { Content = message });
            byte[] byteData = System.Text.Encoding.UTF8.GetBytes(xml);
            LSELogger.Instance.LogDebug(string.Format("SendMessageToServer {0} {1} {2}, {3}b", message.SenderSteamId, message.Side, message.GetType().Name, byteData.Length));
            if (byteData.Length <= MAX_MESSAGE_SIZE)
                MyAPIGateway.Multiplayer.SendMessageToServer(MessageId, byteData);
            else
                SendMessageParts(byteData, MessageSide.ServerSide);
        }

        /// <summary>
        /// Creates and sends an entity with the given information for the server and all players.
        /// </summary>
        /// <param name="content"></param>
        public static void SendMessageToAll(MessageBase message, bool syncAll = true)
        {
            if (MyAPIGateway.Session.Player != null)
                message.SenderSteamId = MyAPIGateway.Session.Player.SteamUserId;

            if (syncAll || !MyAPIGateway.Multiplayer.IsServer)
            {
                SendMessageToServer(message);
            }
            SendMessageToAllPlayers(message);
        }

        public static void SendMessageToAllPlayers(MessageBase messageContainer)
        {
            //MyAPIGateway.Multiplayer.SendMessageToOthers(StandardClientId, System.Text.Encoding.Unicode.GetBytes(ConvertData(content))); <- does not work as expected ... so it doesn't work at all?
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, p => p != null && !p.IsHost());
            foreach (IMyPlayer player in players)
            {
                SendMessageToPlayer(player.SteamUserId, messageContainer);
            }
        }

        public static void SendMessageToPlayer(ulong steamId, MessageBase message)
        {
            message.Side = MessageSide.ClientSide;
            var xml = MyAPIGateway.Utilities.SerializeToXML(new MessageContainer() { Content = message });
            byte[] byteData = System.Text.Encoding.UTF8.GetBytes(xml);

            LSELogger.Instance.LogDebug(string.Format("SendMessageToPlayer {0} {1} {2}, {3}b", steamId, message.Side, message.GetType().Name, byteData.Length));
            
            if (byteData.Length <= MAX_MESSAGE_SIZE)
                MyAPIGateway.Multiplayer.SendMessageTo(MessageId, byteData, steamId);
            else
                SendMessageParts(byteData, MessageSide.ClientSide, steamId);
        }
			
        #region Message Splitting
        /// <summary>
        /// Calculates how many bytes can be stored in the given message.
        /// </summary>
        /// <param name="message">The message in which the bytes will be stored.</param>
        /// <returns>The number of bytes that can be stored until the message is too big to be sent.</returns>
        public static int GetFreeByteElementCount(MessageIncomingMessageParts message)
        {
            message.Content = new byte[1];
            var xmlText = MyAPIGateway.Utilities.SerializeToXML<MessageContainer>(new MessageContainer() { Content = message });
            var oneEntry = System.Text.Encoding.UTF8.GetBytes(xmlText).Length;

            message.Content = new byte[4];
            xmlText = MyAPIGateway.Utilities.SerializeToXML<MessageContainer>(new MessageContainer() { Content = message });
            var twoEntries = System.Text.Encoding.UTF8.GetBytes(xmlText).Length;

            // we calculate the difference between one and two entries in the array to get the count of bytes that describe one entry
            // we divide by 3 because 3 entries are stored in one block of the array
            var difference = (double)(twoEntries - oneEntry) / 3d;

            // get the size of the message without any entries
            var freeBytes = MAX_MESSAGE_SIZE - oneEntry - Math.Ceiling(difference);

            int count = (int)Math.Floor((double)freeBytes / difference);

            // finally we test if the calculation was right
            message.Content = new byte[count];
            xmlText = MyAPIGateway.Utilities.SerializeToXML<MessageContainer>(new MessageContainer() { Content = message });
            var finalLength = System.Text.Encoding.UTF8.GetBytes(xmlText).Length;
            LSELogger.Instance.LogDebug(string.Format("FinalLength: {0}", finalLength));
            if (MAX_MESSAGE_SIZE >= finalLength)
                return count;
            else
                throw new Exception(string.Format("Calculation failed. OneEntry: {0}, TwoEntries: {1}, Difference: {2}, FreeBytes: {3}, Count: {4}, FinalLength: {5}", oneEntry, twoEntries, difference, freeBytes, count, finalLength));
        }

        private static void SendMessageParts(byte[] byteData, MessageSide side, ulong receiver = 0)
        {
            LSELogger.Instance.LogDebug(string.Format("SendMessageParts {0} {1} {2}", byteData.Length, side, receiver));

            var byteList = byteData.ToList();

            while (byteList.Count > 0)
            {
                // we create an empty message part
                var messagePart = new MessageIncomingMessageParts()
                {
                    Side = side,
                    SenderSteamId = side == MessageSide.ServerSide ? MyAPIGateway.Session.Player.SteamUserId : 0,
                    LastPart = false,
                };

                try
                {
                    // let's check how much we could store in the message
                    int freeBytes = GetFreeByteElementCount(messagePart);

                    int count = freeBytes;

                    // we check if that might be the last message
                    if (freeBytes > byteList.Count)
                    {
                        messagePart.LastPart = true;

                        // since we changed LastPart, we should make sure that we are still able to send all the stuff
                        if (GetFreeByteElementCount(messagePart) > byteList.Count)
                        {
                            count = byteList.Count;
                        }
                        else
                            throw new Exception("Failed to send message parts. The leftover could not be sent!");
                    }

                    // fill the message with content
                    messagePart.Content = byteList.GetRange(0, count).ToArray();
                    var xmlPart = MyAPIGateway.Utilities.SerializeToXML<MessageContainer>(new MessageContainer() { Content = messagePart });
                    var bytes = System.Text.Encoding.UTF8.GetBytes(xmlPart);

                    // and finally send the message
                    switch (side)
                    {
                        case MessageSide.ClientSide:
                            if (MyAPIGateway.Multiplayer.SendMessageTo(MessageId, bytes, receiver))
                                byteList.RemoveRange(0, count);
                            else
                                throw new Exception("Failed to send message parts to client.");
                            break;
                        case MessageSide.ServerSide:
                            if (MyAPIGateway.Multiplayer.SendMessageToServer(MessageId, bytes))
                                byteList.RemoveRange(0, count);
                            else
                                throw new Exception("Failed to send message parts to server.");
                            break;
                    }

                }
                catch (Exception ex)
                {
                    LSELogger.Instance.LogException(ex);
                    return;
                }
            }
        }
        #endregion

        public static void HandleMessage(byte[] rawData)
        {
            try
            {
                var data = System.Text.Encoding.UTF8.GetString(rawData);
                var message = MyAPIGateway.Utilities.SerializeFromXML<MessageContainer>(data);

                LSELogger.Instance.LogDebug("HandleMessage()");
                if (message != null && message.Content != null)
                {
                    message.Content.InvokeProcessing();
                }
                return;
            }
            catch (Exception e)
            {
                // Don't warn the user of an exception, this can happen if two mods with the same message id receive an unknown message
                LSELogger.Instance.LogMessage(string.Format("Processing message exception. Exception: {0}", e.ToString()));
                //Logger.Instance.LogException(e);
            }

        }
    }

    [ProtoContract]
    public class MessageClientConnected : MessageBase
    {

        public override void ProcessClient()
        {
		}

        public override void ProcessServer()
		{
            //var messageOut = configPair.Value;
            //MessageUtils.SendMessageToPlayer(SenderSteamId, (MessageBase)messageOut);
        }
    }

    [ProtoContract]
    public class MessageDebug : MessageBase
    {
        [ProtoMember(1)]
        public string Text;

        public override void ProcessClient()
        {
            MyAPIGateway.Utilities.ShowNotification(Text, 6000);
        }

        public override void ProcessServer()
        {
            // None
        }

    }


    [ProtoContract]
    public class MessageConfig : MessageBase
    {
        [ProtoMember(1)]
        public long EntityId;

        [ProtoMember(2)]
        public float Max = 15; // meters

        [ProtoMember(3)]
        public float Min = 10; // meters

        [ProtoMember(4)]
        public float Strength = 50;  // Newton

        public override void ProcessClient()
        {
            Proc();
        }

        public override void ProcessServer()
        {
            Proc();
            // None
        }


        public void Proc()
        {
            var controls = new List<Sandbox.ModAPI.Interfaces.Terminal.IMyTerminalControl>();

            IMyEntity block;
            if (MyAPIGateway.Entities.TryGetEntityById(EntityId, out block))
            {
                var traction = block.GameLogic.GetAs<TractorBeam.TractorBeamTurret>();

                traction.UI.MinSlider.SetterNoCheck((IMyTerminalBlock)block, Min);
                traction.UI.MaxSlider.SetterNoCheck((IMyTerminalBlock)block, Max);
                traction.UI.StrengthSlider.SetterNoCheck((IMyTerminalBlock)block, Strength);

                MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>(out controls);
                foreach (var control in controls)
                {
                    control.UpdateVisual();
                }
            }
        }
    }		
    #region Message Splitting
    [ProtoContract]
    public class MessageIncomingMessageParts : MessageBase
    {
        [ProtoMember(1)]
        public byte[] Content;

        [ProtoMember(2)]
        public bool LastPart;

        public override void ProcessClient()
        {
            MessageUtils.Client_MessageCache.AddRange(Content.ToList());

            if (LastPart)
            {
                MessageUtils.HandleMessage(MessageUtils.Client_MessageCache.ToArray());
                MessageUtils.Client_MessageCache.Clear();
            }
        }

        public override void ProcessServer()
        {
            if (MessageUtils.Server_MessageCache.ContainsKey(SenderSteamId))
                MessageUtils.Server_MessageCache[SenderSteamId].AddRange(Content.ToList());
            else
                MessageUtils.Server_MessageCache.Add(SenderSteamId, Content.ToList());

            if (LastPart)
            {
                MessageUtils.HandleMessage(MessageUtils.Server_MessageCache[SenderSteamId].ToArray());
                MessageUtils.Server_MessageCache[SenderSteamId].Clear();
            }
        }

    }
    #endregion

    /// <summary>
    /// This is a base class for all messages
    /// </summary>
    // ALL CLASSES DERIVED FROM MessageBase MUST BE ADDED HERE
    [XmlInclude(typeof(MessageIncomingMessageParts))]
	[XmlInclude(typeof(MessageClientConnected))]
	[XmlInclude(typeof(MessageConfig))]
	[XmlInclude(typeof(MessageDebug))]

    [ProtoContract]
    public abstract class MessageBase
    {
        /// <summary>
        /// The SteamId of the message's sender. Note that this will be set when the message is sent, so there is no need for setting it otherwise.
        /// </summary>
        [ProtoMember(1)]
        public ulong SenderSteamId;

        /// <summary>
        /// Defines on which side the message should be processed. Note that this will be set when the message is sent, so there is no need for setting it otherwise.
        /// </summary>
        [ProtoMember(2)]
        public MessageSide Side = MessageSide.ClientSide;

        /*
        [ProtoAfterDeserialization]
        void InvokeProcessing() // is not invoked after deserialization from xml
        {
            Logger.Debug("START - Processing");
            switch (Side)
            {
                case MessageSide.ClientSide:
                    ProcessClient();
                    break;
                case MessageSide.ServerSide:
                    ProcessServer();
                    break;
            }
            Logger.Debug("END - Processing");
        }
        */

        public void InvokeProcessing()
        {
            switch (Side)
            {
                case MessageSide.ClientSide:
                    InvokeClientProcessing();
                    break;
                case MessageSide.ServerSide:
                    InvokeServerProcessing();
                    break;
            }
        }

        private void InvokeClientProcessing()
        {
            LSELogger.Instance.LogDebug(string.Format("START - Processing [Client] {0}", this.GetType().Name));
            try
            {
                ProcessClient();
            }
            catch (Exception ex)
            {
                LSELogger.Instance.LogException(ex);
            }
            LSELogger.Instance.LogDebug(string.Format("END - Processing [Client] {0}", this.GetType().Name));
        }

        private void InvokeServerProcessing()
        {
            LSELogger.Instance.LogDebug(string.Format("START - Processing [Server] {0}", this.GetType().Name));

            try
            {
                ProcessServer();
            }
            catch (Exception ex)
            {
                LSELogger.Instance.LogException(ex);
            }

            LSELogger.Instance.LogDebug(string.Format("END - Processing [Server] {0}", this.GetType().Name));
        }

        public abstract void ProcessClient();
        public abstract void ProcessServer();
    }
}
// vim: tabstop=4 expandtab shiftwidth=4 nobackup
