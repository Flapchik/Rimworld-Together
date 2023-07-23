﻿using System;
using System.Collections.Generic;
using System.Linq;
using Shared.JSON;
using Shared.Misc;

namespace RimworldTogether
{
    public static class FactionManager
    {
        private enum FactionManifestMode 
        { 
            Create, 
            Delete, 
            NameInUse, 
            NoPower, 
            AddMember, 
            RemoveMember, 
            AcceptInvite, 
            Promote, 
            Demote, 
            AdminProtection,
            MemberList
        }

        private enum FactionRanks { Member, Moderator, Admin }

        public static void ParseFactionPacket(Packet packet)
        {
            FactionManifestJSON factionManifest = Serializer.SerializeFromString<FactionManifestJSON>(packet.contents[0]);

            switch (int.Parse(factionManifest.manifestMode))
            {
                case (int)FactionManifestMode.Create:
                    OnCreateFaction();
                    break;

                case (int)FactionManifestMode.Delete:
                    OnDeleteFaction();
                    break;

                case (int)FactionManifestMode.NameInUse:
                    OnFactionNameInUse();
                    break;

                case (int)FactionManifestMode.NoPower:
                    OnFactionNoPower();
                    break;

                case (int)FactionManifestMode.AddMember:
                    OnFactionGetInvited(factionManifest);
                    break;

                case (int)FactionManifestMode.RemoveMember:
                    OnFactionGetKicked();
                    break;

                case (int)FactionManifestMode.AdminProtection:
                    OnFactionAdminProtection();
                    break;

                case (int)FactionManifestMode.MemberList:
                    OnFactionMemberList(factionManifest);
                    break;
            }
        }

        public static void OnFactionOpen()
        {
            Action r3 = delegate
            {
                DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for member list"));

                FactionManifestJSON factionManifestJSON = new FactionManifestJSON();
                factionManifestJSON.manifestMode = ((int)FactionManifestMode.MemberList).ToString();

                string[] contents = new string[] { Serializer.SerializeToString(factionManifestJSON) };
                Packet packet = new Packet("FactionPacket", contents);
                Network.SendData(packet);
            };

            Action r2 = delegate
            {
                FactionManifestJSON factionManifestJSON = new FactionManifestJSON();
                factionManifestJSON.manifestMode = ((int)FactionManifestMode.RemoveMember).ToString();
                factionManifestJSON.manifestDetails = ClientValues.chosenSettlement.Tile.ToString();

                string[] contents = new string[] { Serializer.SerializeToString(factionManifestJSON) };
                Packet packet = new Packet("FactionPacket", contents);
                Network.SendData(packet);
            };

            Action r1 = delegate
            {
                DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for faction deletion"));

                FactionManifestJSON factionManifestJSON = new FactionManifestJSON();
                factionManifestJSON.manifestMode = ((int)FactionManifestMode.Delete).ToString();

                string[] contents = new string[] { Serializer.SerializeToString(factionManifestJSON) };
                Packet packet = new Packet("FactionPacket", contents);
                Network.SendData(packet);
            };

            RT_Dialog_YesNo d3 = new RT_Dialog_YesNo("Are you sure you want to LEAVE your faction?", r2, null);

            RT_Dialog_YesNo d2 = new RT_Dialog_YesNo("Are you sure you want to DELETE your faction?", r1, null);

            RT_Dialog_3Button d1 = new RT_Dialog_3Button("Faction Management", "Manage your faction from here",
                "Members", "Delete", "Leave",
                delegate { r3(); },
                delegate { DialogManager.PushNewDialog(d2); },
                delegate { DialogManager.PushNewDialog(d3); },
                null);

            DialogManager.PushNewDialog(d1);
        }

        public static void OnNoFactionOpen()
        {
            Action r2 = delegate
            {
                if (string.IsNullOrWhiteSpace(DialogManager.dialog1ResultOne) || DialogManager.dialog1ResultOne.Length > 32)
                {
                    DialogManager.PushNewDialog(new RT_Dialog_Error("Faction name is invalid! Please try again!"));
                }

                else
                {
                    DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for faction creation"));

                    FactionManifestJSON factionManifestJSON = new FactionManifestJSON();
                    factionManifestJSON.manifestMode = ((int)FactionManifestMode.Create).ToString();
                    factionManifestJSON.manifestDetails = DialogManager.dialog1ResultOne;

                    string[] contents = new string[] { Serializer.SerializeToString(factionManifestJSON) };
                    Packet packet = new Packet("FactionPacket", contents);
                    Network.SendData(packet);
                }
            };
            RT_Dialog_1Input d2 = new RT_Dialog_1Input("New Faction Name", "Input the name of your new faction", r2, null);

            Action r1 = delegate { DialogManager.PushNewDialog(d2); };
            RT_Dialog_YesNo d1 = new RT_Dialog_YesNo("You are not a member of any faction! Create one?", r1, null);

            DialogManager.PushNewDialog(d1);
        }

        public static void OnFactionOpenOnMember()
        {
            Action r1 = delegate
            {
                FactionManifestJSON factionManifestJSON = new FactionManifestJSON();
                factionManifestJSON.manifestMode = ((int)FactionManifestMode.Promote).ToString();
                factionManifestJSON.manifestDetails = ClientValues.chosenSettlement.Tile.ToString();

                string[] contents = new string[] { Serializer.SerializeToString(factionManifestJSON) };
                Packet packet = new Packet("FactionPacket", contents);
                Network.SendData(packet);
            };

            Action r2 = delegate
            {
                FactionManifestJSON factionManifestJSON = new FactionManifestJSON();
                factionManifestJSON.manifestMode = ((int)FactionManifestMode.Demote).ToString();
                factionManifestJSON.manifestDetails = ClientValues.chosenSettlement.Tile.ToString();

                string[] contents = new string[] { Serializer.SerializeToString(factionManifestJSON) };
                Packet packet = new Packet("FactionPacket", contents);
                Network.SendData(packet);
            };

            Action r3 = delegate
            {
                FactionManifestJSON factionManifestJSON = new FactionManifestJSON();
                factionManifestJSON.manifestMode = ((int)FactionManifestMode.RemoveMember).ToString();
                factionManifestJSON.manifestDetails = ClientValues.chosenSettlement.Tile.ToString();

                string[] contents = new string[] { Serializer.SerializeToString(factionManifestJSON) };
                Packet packet = new Packet("FactionPacket", contents);
                Network.SendData(packet);
            };

            RT_Dialog_YesNo d5 = new RT_Dialog_YesNo("Are you sure you want to demote this player?", 
                r2,
                delegate { DialogManager.PushNewDialog(DialogManager.previousDialog); });

            RT_Dialog_YesNo d4 = new RT_Dialog_YesNo("Are you sure you want to promote this player?", 
                r1,
                delegate { DialogManager.PushNewDialog(DialogManager.previousDialog); });

            RT_Dialog_YesNo d3 = new RT_Dialog_YesNo("Are you sure you want to kick this player?", 
                r3,
                delegate { DialogManager.PushNewDialog(DialogManager.previousDialog); });

            RT_Dialog_2Button d2 = new RT_Dialog_2Button("Power Management Menu", "Choose what you want to manage",
                "Promote", "Demote",
                delegate { DialogManager.PushNewDialog(d4); },
                delegate { DialogManager.PushNewDialog(d5); },
                null);

            RT_Dialog_2Button d1 = new RT_Dialog_2Button("Management Menu", "Choose what you want to manage", 
                "Powers", "Kick", 
                delegate { DialogManager.PushNewDialog(d2); }, 
                delegate { DialogManager.PushNewDialog(d3); }, 
                null);

            DialogManager.PushNewDialog(d1);
        }

        public static void OnFactionOpenOnNonMember()
        {
            Action r1 = delegate
            {
                FactionManifestJSON factionManifestJSON = new FactionManifestJSON();
                factionManifestJSON.manifestMode = ((int)FactionManifestMode.AddMember).ToString();
                factionManifestJSON.manifestDetails = ClientValues.chosenSettlement.Tile.ToString();

                string[] contents = new string[] { Serializer.SerializeToString(factionManifestJSON) };
                Packet packet = new Packet("FactionPacket", contents);
                Network.SendData(packet);
            };

            RT_Dialog_YesNo d1 = new RT_Dialog_YesNo("Do you want to invite this player to your faction?", r1, null);
            DialogManager.PushNewDialog(d1);
        }

        private static void OnCreateFaction()
        {
            ServerValues.hasFaction = true;

            string[] messages = new string[]
            {
                "Your faction has been created!",
                "You can now access its menu through the same button"
            };

            DialogManager.PopWaitDialog();
            RT_Dialog_OK_Loop d1 = new RT_Dialog_OK_Loop(messages);
            DialogManager.PushNewDialog(d1);
        }

        private static void OnDeleteFaction()
        {
            ServerValues.hasFaction = false;

            if (!ClientValues.isInTransfer) DialogManager.PopWaitDialog();
            DialogManager.PushNewDialog(new RT_Dialog_Error("Your faction has been deleted!"));
        }

        private static void OnFactionNameInUse()
        {
            DialogManager.PopWaitDialog();
            DialogManager.PushNewDialog(new RT_Dialog_Error("That faction name is already in use!"));
        }

        private static void OnFactionNoPower()
        {
            DialogManager.PopWaitDialog();
            DialogManager.PushNewDialog(new RT_Dialog_Error("You don't have enough power for this action!"));
        }

        private static void OnFactionGetInvited(FactionManifestJSON factionManifest)
        {
            Action r1 = delegate
            {
                ServerValues.hasFaction = true;

                factionManifest.manifestMode = ((int)FactionManifestMode.AcceptInvite).ToString();

                string[] contents = new string[] { Serializer.SerializeToString(factionManifest) };
                Packet packet = new Packet("FactionPacket", contents);
                Network.SendData(packet);
            };

            RT_Dialog_YesNo d1 = new RT_Dialog_YesNo($"Invited to {factionManifest.manifestDetails}, accept?", r1, null);
            DialogManager.PushNewDialog(d1);
        }

        private static void OnFactionGetKicked()
        {
            ServerValues.hasFaction = false;

            DialogManager.PushNewDialog(new RT_Dialog_OK("You have been kicked from your faction!"));
        }

        private static void OnFactionAdminProtection()
        {
            DialogManager.PushNewDialog(new RT_Dialog_Error("You can't do this action as a faction admin!"));
        }

        private static void OnFactionMemberList(FactionManifestJSON factionManifest)
        {
            DialogManager.PopWaitDialog();

            List<string> unraveledDetails = new List<string>();
            for (int i = 0; i < factionManifest.manifestComplexDetails.Count(); i++)
            {
                unraveledDetails.Add($"{factionManifest.manifestComplexDetails[i]} " +
                    $"- {(FactionRanks)int.Parse(factionManifest.manifestSecondaryComplexDetails[i])}");
            }

            RT_Dialog_Listing d1 = new RT_Dialog_Listing("Faction Members", 
                "All faction members are depicted here", unraveledDetails.ToArray());

            DialogManager.PushNewDialog(d1);
        }
    }
}
