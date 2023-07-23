﻿using System;
using System.Linq;
using System.Threading;
using RimWorld;
using RimWorld.Planet;
using RimworldTogether.GameClient.Dialogs;
using RimworldTogether.GameClient.Misc;
using RimworldTogether.GameClient.Patches;
using RimworldTogether.GameClient.Values;
using RimworldTogether.Shared.JSON.Actions;
using RimworldTogether.Shared.JSON.Things;
using RimworldTogether.Shared.Network;
using Verse;
using Verse.Sound;

namespace RimworldTogether.GameClient.Managers.Actions
{
    public static class TransferManager
    {
        public enum TransferMode { Gift, Trade, Rebound }

        public enum TransferLocation { Caravan, Settlement }

        public enum TransferStepMode { TradeRequest, TradeAccept, TradeReject, TradeReRequest, TradeReAccept, TradeReReject, Recover }

        public static void ParseTransferPacket(Packet packet)
        {
            TransferManifestJSON transferManifestJSON = Serializer.SerializeFromString<TransferManifestJSON>(packet.contents[0]);

            switch (int.Parse(transferManifestJSON.transferStepMode))
            {
                case (int)TransferStepMode.TradeRequest:
                    ReceiveTransferRequest(transferManifestJSON);
                    break;

                case (int)TransferStepMode.TradeAccept:
                    DialogManager.PopWaitDialog();
                    DialogManager.PushNewDialog(new RT_Dialog_OK("Transfer was a success!"));
                    FinishTransfer(true);
                    break;

                case (int)TransferStepMode.TradeReject:
                    DialogManager.PopWaitDialog();
                    DialogManager.PushNewDialog(new RT_Dialog_Error("Player rejected the trade!"));
                    RecoverTradeItems(TransferLocation.Caravan);
                    break;

                case (int)TransferStepMode.TradeReRequest:
                    DialogManager.PopWaitDialog();
                    ReceiveReboundRequest(transferManifestJSON);
                    break;

                case (int)TransferStepMode.TradeReAccept:
                    DialogManager.PopWaitDialog();
                    SendTransferToSettlement(DeepScribeManager.GetAllTransferedItems(ClientValues.incomingManifest));
                    break;

                case (int)TransferStepMode.TradeReReject:
                    DialogManager.PopWaitDialog();
                    DialogManager.PushNewDialog(new RT_Dialog_Error("Player rejected the trade!"));
                    RecoverTradeItems(TransferLocation.Settlement);
                    break;

                case (int)TransferStepMode.Recover:
                    DialogManager.PopWaitDialog();
                    RecoverTradeItems(TransferLocation.Caravan);
                    break;
            }
        }

        public static void TakeTransferItems(TransferLocation transferLocation)
        {
            ClientValues.outgoingManifest.fromTile = Find.AnyPlayerHomeMap.Tile.ToString();

            if (transferLocation == TransferLocation.Caravan) ClientValues.outgoingManifest.toTile = ClientValues.chosenSettlement.Tile.ToString();
            else if (transferLocation == TransferLocation.Settlement) ClientValues.outgoingManifest.toTile = ClientValues.incomingManifest.fromTile.ToString();

            Action toDo = delegate
            {
                if (TradeSession.deal.TryExecute(out bool actuallyTraded))
                {
                    SoundDefOf.ExecuteTrade.PlayOneShotOnCamera();

                    if (transferLocation == TransferLocation.Caravan)
                    {
                        TradeSession.playerNegotiator.GetCaravan().RecacheImmobilizedNow();
                    }
                }
            };
            toDo.Invoke();
        }

        public static void SendTransferToServer(TransferLocation transferLocation)
        {
            DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for transfer response"));

            if (transferLocation == TransferLocation.Caravan)
            {
                ClientValues.outgoingManifest.transferStepMode = ((int)TransferStepMode.TradeRequest).ToString();

                string[] contents = new string[] { Serializer.SerializeToString(ClientValues.outgoingManifest) };
                Packet packet = new Packet("TransferPacket", contents);
                Network.Network.SendData(packet);
            }

            else if (transferLocation == TransferLocation.Settlement)
            {
                ClientValues.outgoingManifest.transferStepMode = ((int)TransferStepMode.TradeReRequest).ToString();

                string[] contents = new string[] { Serializer.SerializeToString(ClientValues.outgoingManifest) };
                Packet packet = new Packet("TransferPacket", contents);
                Network.Network.SendData(packet);
            }
        }

        public static void RecoverTradeItems(TransferLocation transferLocation)
        {
            try
            {
                Action r1 = delegate
                {
                    Thing[] toRecover = DeepScribeManager.GetAllTransferedItems(ClientValues.outgoingManifest);

                    if (transferLocation == TransferLocation.Caravan)
                    {
                        SendTransferToCaravan(toRecover, false);
                    }

                    else if (transferLocation == TransferLocation.Settlement)
                    {
                        SendTransferToSettlement(toRecover, false);
                    }
                };
                r1.Invoke();
            }

            catch
            {
                Log.Warning("Rethrowing transfer items, might be Rimworld's fault");

                Thread.Sleep(100);

                RecoverTradeItems(transferLocation);
            }
        }

        public static void SendTransferToSettlement(Thing[] things, bool success = true, bool customMap = true, bool invokeMessage = true)
        {
            Action r1 = delegate
            {
                Map map = null;
                if (customMap) map = Find.Maps.Find(x => x.Tile == int.Parse(ClientValues.incomingManifest.toTile));
                else map = Find.AnyPlayerHomeMap;

                IntVec3 location = GetTransferLocationInMap(map);

                foreach (Thing thing in things)
                {
                    if (thing is Pawn) GenSpawn.Spawn(thing, location, map, Rot4.Random);
                    else GenPlace.TryPlaceThing(thing, location, map, ThingPlaceMode.Near);
                }

                FinishTransfer(success);
            };

            if (invokeMessage)
            {
                if (success) DialogManager.PushNewDialog(new RT_Dialog_OK("Transfer was a success!", r1));
                else DialogManager.PushNewDialog(new RT_Dialog_Error("Transfer was cancelled!", r1));
            }
            else r1.Invoke();
        }

        public static void SendTransferToCaravan(Thing[] things, bool success = true, bool invokeMessage = true)
        {
            Action r1 = delegate
            {
                foreach (Thing thing in things)
                {
                    if (TransferManagerHelper.CheckIfThingIsHuman(thing) || TransferManagerHelper.CheckIfThingIsAnimal(thing))
                    {
                        Find.WorldPawns.PassToWorld(thing as Pawn);
                        thing.SetFaction(Faction.OfPlayer);
                    }

                    ClientValues.chosenCaravan.AddPawnOrItem(thing, true);
                }

                FinishTransfer(success);
            };

            if (invokeMessage)
            {
                if (success) DialogManager.PushNewDialog(new RT_Dialog_OK("Transfer was a success!", r1));
                else DialogManager.PushNewDialog(new RT_Dialog_Error("Transfer was cancelled!", r1));
            }
            else r1.Invoke();
        }

        public static void FinishTransfer(bool success)
        {
            //DEBUG
            if (success) SavePatch.ForceSave();

            ClientValues.incomingManifest = new TransferManifestJSON();
            ClientValues.outgoingManifest = new TransferManifestJSON();
            ClientValues.ToggleTransfer(false);
        }

        public static void ReceiveTransferRequest(TransferManifestJSON transferManifestJSON)
        {
            try
            {
                ClientValues.incomingManifest = transferManifestJSON;

                if (!ClientValues.isReadyToPlay || ClientValues.isInTransfer || ClientValues.autoDenyTransfers)
                {
                    RejectRequest((TransferMode)int.Parse(transferManifestJSON.transferMode));
                }

                else
                {
                    Action r1 = delegate
                    {
                        if (int.Parse(transferManifestJSON.transferMode) == (int)TransferMode.Gift)
                        {
                            RT_Dialog_ItemListing d1 = new RT_Dialog_ItemListing(DeepScribeManager.GetAllTransferedItems(transferManifestJSON), TransferMode.Gift);
                            DialogManager.PushNewDialog(d1);
                        }

                        else if (int.Parse(transferManifestJSON.transferMode) == (int)TransferMode.Trade)
                        {
                            RT_Dialog_ItemListing d1 = new RT_Dialog_ItemListing(DeepScribeManager.GetAllTransferedItems(transferManifestJSON), TransferMode.Trade);
                            DialogManager.PushNewDialog(d1);
                        }
                    };

                    if (int.Parse(transferManifestJSON.transferMode) == (int)TransferMode.Gift)
                    {
                        DialogManager.PushNewDialog(new RT_Dialog_OK("You are receiving a gift request", r1));
                    }

                    else if (int.Parse(transferManifestJSON.transferMode) == (int)TransferMode.Trade)
                    {
                        DialogManager.PushNewDialog(new RT_Dialog_OK("You are receiving a trade request", r1));
                    }
                }
            }

            catch
            {
                Log.Warning("Rethrowing transfer items, might be Rimworld's fault");

                Thread.Sleep(100);

                ReceiveTransferRequest(transferManifestJSON);
            }        
        }

        public static void ReceiveReboundRequest(TransferManifestJSON transferManifestJSON)
        {
            try
            {
                ClientValues.incomingManifest = transferManifestJSON;

                RT_Dialog_ItemListing d1 = new RT_Dialog_ItemListing(DeepScribeManager.GetAllTransferedItems(transferManifestJSON), TransferMode.Rebound);
                DialogManager.PushNewDialog(d1);
            }

            catch
            {
                Log.Warning("Rethrowing transfer items, might be Rimworld's fault");

                Thread.Sleep(100);

                ReceiveReboundRequest(transferManifestJSON);
            }
        }

        public static void RejectRequest(TransferMode transferMode)
        {
            if (transferMode == TransferMode.Gift)
            {
                //Nothing should happen here
            }

            else if (transferMode == TransferMode.Trade)
            {
                ClientValues.incomingManifest.transferStepMode = ((int)TransferStepMode.TradeReject).ToString();

                string[] contents = new string[] { Serializer.SerializeToString(ClientValues.incomingManifest) };
                Packet packet = new Packet("TransferPacket", contents);
                Network.Network.SendData(packet);
            }

            else if (transferMode == TransferMode.Rebound)
            {
                ClientValues.incomingManifest.transferStepMode = ((int)TransferStepMode.TradeReReject).ToString();

                string[] contents = new string[] { Serializer.SerializeToString(ClientValues.incomingManifest) };
                Packet packet = new Packet("TransferPacket", contents);
                Network.Network.SendData(packet);

                RecoverTradeItems(TransferLocation.Caravan);
            }

            FinishTransfer(false);
        }

        public static IntVec3 GetTransferLocationInMap(Map map)
        {
            Thing tradingSpot = map.listerThings.AllThings.Find(x => x.def.defName == "RTTransferSpot");
            if (tradingSpot != null) return tradingSpot.Position;
            else
            {
                RT_Dialog_OK_Loop d1 = new RT_Dialog_OK_Loop(new string[] { "You are missing a transfer spot!",
                    "Received items will appear in the center of the map",
                    "Build a trading spot to change the drop location!"});

                DialogManager.PushNewDialog(d1);

                return new IntVec3(map.Center.x, map.Center.y, map.Center.z);
            }
        }

        public static void SendSilverToCaravan(int quantity)
        {
            ItemDetailsJSON itemDetailsJSON = new ItemDetailsJSON();
            itemDetailsJSON.defName = ThingDefOf.Silver.defName;
            itemDetailsJSON.materialDefName = "null";
            itemDetailsJSON.quantity = quantity.ToString();
            itemDetailsJSON.quality = "1";
            itemDetailsJSON.hitpoints = "100";
            itemDetailsJSON.isMinified = false;

            Thing silverToRecover = DeepScribeManager.GetItemSimple(itemDetailsJSON);
            ClientValues.chosenCaravan.AddPawnOrItem(silverToRecover, false);
        }
    }

    public static class TransferManagerHelper
    {
        public static bool CheckIfThingIsHuman(Thing thing)
        {
            if (thing.def.defName == "Human") return true;
            else return false;
        }

        public static bool CheckIfThingIsAnimal(Thing thing)
        {
            PawnKindDef animal = DefDatabase<PawnKindDef>.AllDefs.ToList().Find(fetch => fetch.defName == thing.def.defName);
            if (animal != null) return true;
            else return false;
        }

        public static bool CheckIfThingHasMaterial(Thing thing)
        {
            if (thing.Stuff != null) return true;
            else return false;
        }

        public static string GetThingQuality(Thing thing)
        {
            QualityCategory qc = QualityCategory.Normal;
            thing.TryGetQuality(out qc);

            return ((int)qc).ToString();
        }

        public static bool CheckIfThingIsMinified(Thing thing)
        {
            if (thing.def == ThingDefOf.MinifiedThing || thing.def == ThingDefOf.MinifiedTree) return true;
            else return false;
        }
    }
}
