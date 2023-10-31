﻿using RimworldTogether.GameServer.Files;
using RimworldTogether.GameServer.Misc;
using RimworldTogether.GameServer.Network;
using RimworldTogether.Shared.JSON.Actions;
using RimworldTogether.Shared.Misc;
using RimworldTogether.Shared.Network;
using RimworldTogether.Shared.Serializers;

namespace RimworldTogether.GameServer.Managers.Actions
{
    public static class SpyManager
    {
        private enum SpyStepMode { Request, Deny }

        public static void ParseSpyPacket(ServerClient client, Packet packet)
        {
            SpyDetailsJSON spyDetailsJSON = (SpyDetailsJSON)ObjectConverter.ConvertBytesToObject(packet.contents);

            switch (int.Parse(spyDetailsJSON.spyStepMode))
            {
                case (int)SpyStepMode.Request:
                    SendRequestedMap(client, spyDetailsJSON);
                    break;

                case (int)SpyStepMode.Deny:
                    //Nothing goes here
                    break;
            }
        }

        private static void SendRequestedMap(ServerClient client, SpyDetailsJSON spyDetailsJSON)
        {
            if (!SaveManager.CheckIfMapExists(spyDetailsJSON.spyData))
            {
                spyDetailsJSON.spyStepMode = ((int)SpyStepMode.Deny).ToString();
                Packet packet = Packet.CreatePacketFromJSON("SpyPacket", spyDetailsJSON);
                client.clientListener.SendData(packet);
            }

            else
            {
                SettlementFile settlementFile = SettlementManager.GetSettlementFileFromTile(spyDetailsJSON.spyData);

                if (UserManager.CheckIfUserIsConnected(settlementFile.owner))
                {
                    spyDetailsJSON.spyStepMode = ((int)SpyStepMode.Deny).ToString();
                    Packet packet = Packet.CreatePacketFromJSON("SpyPacket", spyDetailsJSON);
                    client.clientListener.SendData(packet);
                }

                else
                {
                    MapFile mapFile = SaveManager.GetUserMapFromTile(spyDetailsJSON.spyData);
                    spyDetailsJSON.spyData = Serializer.SerializeToString(mapFile);

                    Packet packet = Packet.CreatePacketFromJSON("SpyPacket", spyDetailsJSON);
                    client.clientListener.SendData(packet);
                }
            }
        }
    }
}
