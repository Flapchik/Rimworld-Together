﻿using RimworldTogether.GameServer.Files;
using RimworldTogether.GameServer.Misc;
using RimworldTogether.GameServer.Network;
using RimworldTogether.Shared.JSON.Actions;
using RimworldTogether.Shared.Network;

namespace RimworldTogether.GameServer.Managers.Actions
{
    public static class EventManager
    {
        public enum EventStepMode { Send, Receive, Recover }

        public static void ParseEventPacket(Client client, Packet packet)
        {
            EventDetailsJSON eventDetailsJSON = Serializer.SerializeFromString<EventDetailsJSON>(packet.contents[0]);

            switch (int.Parse(eventDetailsJSON.eventStepMode))
            {
                case (int)EventStepMode.Send:
                    SendEvent(client, eventDetailsJSON);
                    break;

                case (int)EventStepMode.Receive:
                    //Nothing goes here
                    break;

                case (int)EventStepMode.Recover:
                    //Nothing goes here
                    break;
            }
        }

        public static void SendEvent(Client client, EventDetailsJSON eventDetailsJSON)
        {
            if (!SettlementManager.CheckIfTileIsInUse(eventDetailsJSON.toTile)) ResponseShortcutManager.SendIllegalPacket(client);
            else
            {
                SettlementFile settlement = SettlementManager.GetSettlementFileFromTile(eventDetailsJSON.toTile);
                if (!UserManager.CheckIfUserIsConnected(settlement.owner))
                {
                    eventDetailsJSON.eventStepMode = ((int)EventStepMode.Recover).ToString();
                    string[] contents = new string[] { Serializer.SerializeToString(eventDetailsJSON) };
                    Packet packet = new Packet("EventPacket", contents);
                    Network.Network.SendData(client, packet);
                }

                else
                {
                    Client target = UserManager.GetConnectedClientFromUsername(settlement.owner);
                    if (target.inSafeZone)
                    {
                        eventDetailsJSON.eventStepMode = ((int)EventStepMode.Recover).ToString();
                        string[] contents = new string[] { Serializer.SerializeToString(eventDetailsJSON) };
                        Packet packet = new Packet("EventPacket", contents);
                        Network.Network.SendData(client, packet);
                    }

                    else
                    {
                        target.inSafeZone = true;

                        string[] contents = new string[] { Serializer.SerializeToString(eventDetailsJSON) };
                        Packet packet = new Packet("EventPacket", contents);
                        Network.Network.SendData(client, packet);

                        eventDetailsJSON.eventStepMode = ((int)EventStepMode.Receive).ToString();
                        contents = new string[] { Serializer.SerializeToString(eventDetailsJSON) };
                        Packet rPacket = new Packet("EventPacket", contents);
                        Network.Network.SendData(target, rPacket);
                    }
                }
            }
        }
    }
}
