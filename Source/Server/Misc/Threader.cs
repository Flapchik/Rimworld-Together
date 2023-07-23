﻿using RimworldTogether.GameServer.Managers;
using RimworldTogether.GameServer.Managers.Actions;
using RimworldTogether.GameServer.Network;

namespace RimworldTogether.GameServer.Misc
{
    public static class Threader
    {
        public enum ServerMode { Start, Heartbeat, Sites, Console }

        public enum ClientMode { Start }

        public static void GenerateServerThread(ServerMode mode)
        {
            if (mode == ServerMode.Start)
            {
                Thread thread = new Thread(new ThreadStart(Network.Network.ReadyServer));
                thread.IsBackground = true;
                thread.Name = "Networking";
                thread.Start();
            }

            else if (mode == ServerMode.Heartbeat)
            {
                Thread thread = new Thread(Network.Network.HearbeatClients);
                thread.IsBackground = true;
                thread.Name = "Heartbeat";
                thread.Start();
            }

            else if (mode == ServerMode.Sites)
            {
                Thread thread = new Thread(SiteManager.StartSiteTicker);
                thread.IsBackground = true;
                thread.Name = "Sites";
                thread.Start();
            }

            else if (mode == ServerMode.Console)
            {
                Thread thread = new Thread(ServerCommandManager.ListenForServerCommands);
                thread.IsBackground = true;
                thread.Name = "Console";
                thread.Start();
            }
        }

        public static void GenerateClientThread(ClientMode mode, Client client)
        {
            if (mode == ClientMode.Start)
            {
                Thread thread = new Thread(() => Network.Network.ListenToClient(client));
                thread.IsBackground = true;
                thread.Name = $"Client {client.SavedIP}";
                thread.Start();
            }
        }
    }
}
