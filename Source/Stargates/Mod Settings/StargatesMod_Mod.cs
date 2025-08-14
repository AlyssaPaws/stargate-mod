using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace StargatesMod.Mod_Settings
{
    public class StargatesMod_Mod : Mod
    {
        /*Reference to settings*/
        StargatesMod_Settings _settings;
        
        /*Resolve settings reference*/
        public StargatesMod_Mod(ModContentPack content) : base(content)
        {
            _settings = GetSettings<StargatesMod_Settings>();
        }


        /*Settings GUI*/
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            /*Section 1*/
            Listing_Standard sec1 = listingStandard.BeginSection(150);
            sec1.Label("SGM_SettingsCat_Main".Translate());
            sec1.GapLine();
            sec1.CheckboxLabeled("SGM_ShortenDialSeqLabel".Translate(), ref _settings.ShortenGateDialSeq, "SGM_ShortenDialSeqTT".Translate());
            /*sec1.Label("Setting2");
            sec1.Label("Setting3");
            sec1.Label("Setting4");*/
            listingStandard.EndSection(sec1);

            listingStandard.Gap();
            
            /*/*Section 2#1#
            Listing_Standard sec2 = listingStandard.BeginSection(150);
            sec2.Label("Test Section 2");
            sec2.GapLine();
            sec2.Label("Setting1");
            sec2.Label("Setting2");
            sec2.Label("Setting3");
            sec2.Label("Setting4");
            listingStandard.EndSection(sec2);
            
            listingStandard.Gap();*/
            
            /*Toggleable patches notice*/
            Listing_Standard SecTP = listingStandard.BeginSection(75);
            SecTP.Label("SGM_ToggleablePatchesHeader".Translate());
            SecTP.GapLine();
            SecTP.Label("SGM_ToggleablePatchesText".Translate());
            listingStandard.EndSection(SecTP);
            
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        /*Settings mod label*/
        public override string SettingsCategory()
        {
            return "SGM_StargatesMod".Translate();
        }
    }
}