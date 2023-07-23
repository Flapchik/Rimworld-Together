﻿using RimworldTogether.GameClient.Values;
using Verse;

namespace RimworldTogether.GameClient.Core
{
    public class ModConfigs : ModSettings
    {
        public bool transferBool;
        public bool siteRewardsBool;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref transferBool, "transferBool");
            Scribe_Values.Look(ref siteRewardsBool, "siteRewardsBool");
            base.ExposeData();

            ClientValues.autoDenyTransfers = transferBool;
            ClientValues.autoRejectSiteRewards = siteRewardsBool;
        }
    }
}
