using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace StargatesMod
{
    public class FloatMenuOptionProvider_DHD : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;

        protected override bool MechanoidCanDo => true;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            CompDialHomeDevice dhdComp =  clickedThing.TryGetComp<CompDialHomeDevice>();
            if (dhdComp == null) yield break;
            CompStargate sgComp = dhdComp.GetLinkedStargate();
            
            
            if (!CanReachDHD(context.FirstSelectedPawn, clickedThing) || !dhdComp.IsConnectedToStargate) yield break;
            
            if (dhdComp.Props.requiresPower)
            {
                CompPowerTrader compPowerTrader = dhdComp.parent.TryGetComp<CompPowerTrader>();
                if (compPowerTrader != null && !compPowerTrader.PowerOn)
                {
                    yield return new FloatMenuOption("SGM_CannotDialNoPower".Translate(), null);
                    yield break;
                }
            }
            if (sgComp.IsHibernating)
            {
                yield return new FloatMenuOption("SGM_CannotDialHibernating".Translate(), null);
                yield break;
            }
            if (sgComp.StargateIsActive)
            {
                yield return new FloatMenuOption("SGM_CannotDialGateIsActive".Translate(), null);
                yield break;
            }
            WorldComp_StargateAddresses addressComp = Find.World.GetComponent<WorldComp_StargateAddresses>();
            addressComp.CleanupAddresses();
            if (addressComp.AddressList.Count < 2)
            {
                yield return new FloatMenuOption("SGM_CannotDialNoDestinations".Translate(), null);
                yield break;
            }
            if (sgComp.TicksUntilOpen > -1)
            {
                if (sgComp.IsReceivingGate)
                {
                    yield return new FloatMenuOption("SGM_CannotDialIncoming".Translate(), null);
                    yield break;
                }
                yield return new FloatMenuOption("SGM_CannotDialAlreadyDialling".Translate(), null);
                yield break; 
            }
                
            foreach (PlanetTile pT in addressComp.AddressList)
            {
                if (pT == sgComp.GateAddress) continue;
                
                Site sgDestSite = Find.WorldObjects.SiteAt(pT);
                MapParent sgDestNonSite = null;
                if (sgDestSite == null) sgDestNonSite = Find.WorldObjects.MapParentAt(pT);

                string siteLabel = "placeHolder";

                /*Account for the destination address being a non-site, eg player colony*/
                if (sgDestSite != null)
                {
                    siteLabel = sgDestSite.Label;

                    if (sgDestSite.Label == "ancient stargate site") siteLabel = sgDestSite.customLabel;
                }
                else if (sgDestNonSite != null) siteLabel = sgDestNonSite.Label;
                else
                {
                    Log.Error($"StargatesMod: Site and MapParent both were null in FloatMenuOptionProvider_DHD");
                }
                
                
                yield return new FloatMenuOption("SGM_DialGate".Translate(CompStargate.GetStargateDesignation(pT), siteLabel), () =>
                {
                    dhdComp.lastDialledAddress = pT;
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_DialStargate"), dhdComp.parent);
                    context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
        }

        private static AcceptanceReport CanReachDHD(Pawn pawn, Thing dhd)
        {
            if (!pawn.CanReach(dhd, PathEndMode.ClosestTouch, Danger.Deadly))
            {
                return "NoPath".Translate();
            }
            return true;
        }
    }
}
