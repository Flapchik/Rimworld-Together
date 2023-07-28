﻿using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimworldTogether.GameClient.Managers;
using RimworldTogether.GameClient.Managers.Actions;
using RimworldTogether.GameClient.Misc;
using RimworldTogether.GameClient.Planet;
using RimworldTogether.GameClient.Values;
using RimworldTogether.Shared.Misc;
using Verse;

namespace RimworldTogether.GameClient.Patches
{
    [HarmonyPatch(typeof(TradeDeal), "AddAllTradeables")]
    public static class AddTradeablePatch
    {
        [HarmonyPrefix]
        public static bool DoPre(ref List<Tradeable> ___tradeables)
        {
            if (!Network.Network.isConnectedToServer) return true;
            else if (!PlanetFactions.playerFactions.Contains(TradeSession.trader.Faction)) return true;
            else
            {
                ___tradeables = new List<Tradeable>();
                ___tradeables.AddRange(ClientValues.listToShowInTradesMenu);
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(Tradeable), "ResolveTrade")]
    public static class GetTradeablePatch
    {
        [HarmonyPrefix]
        public static bool DoPre(List<Thing> ___thingsColony, int ___countToTransfer)
        {
            if (Network.Network.isConnectedToServer)
            {
                if (PlanetFactions.playerFactions.Contains(TradeSession.trader.Faction))
                {
                    if (TransferManagerHelper.CheckIfThingIsHuman(___thingsColony[0]))
                    {
                        Pawn pawn = ___thingsColony[0] as Pawn;

                        ClientValues.outgoingManifest.humanDetailsJSONS.Add(Serializer.SerializeToString
                            (DeepScribeManager.TransformHumanToString(pawn, false)));
                    }

                    else if (TransferManagerHelper.CheckIfThingIsAnimal(___thingsColony[0]))
                    {
                        Pawn pawn = ___thingsColony[0] as Pawn;

                        ClientValues.outgoingManifest.animalDetailsJSON.Add(Serializer.SerializeToString
                            (DeepScribeManager.TransformAnimalToString(pawn)));
                    }

                    else
                    {
                        ClientValues.outgoingManifest.itemDetailsJSONS.Add(Serializer.SerializeToString
                            (DeepScribeManager.TransformItemToString(___thingsColony[0], ___countToTransfer)));
                    }
                }
            }

            return true;
        }
    }
}
