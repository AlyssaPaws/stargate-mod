using System.Collections.Generic;
using Verse;

namespace StargatesMod.Mod_Settings
{
    public class StargatesMod_Settings : ModSettings
    {
        /*Mod Settings*/
        public bool ShortenGateDialSeq = false;

        /*Write Settings to file*/
        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShortenGateDialSeq, "ShortenGateDialSeq");
            base.ExposeData();
        }
    }
}