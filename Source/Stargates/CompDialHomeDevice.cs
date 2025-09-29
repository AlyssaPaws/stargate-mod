using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Verse.AI;

namespace StargatesMod
{
    public class CompDialHomeDevice : ThingComp
    {
        CompFacility compFacility;
        public PlanetTile lastDialledAddress;

        public CompProperties_DialHomeDevice Props => (CompProperties_DialHomeDevice)this.props;

        public CompStargate GetLinkedStargate()
        {
            if (Props.selfDialler) return parent.TryGetComp<CompStargate>(); 
            if (compFacility.LinkedBuildings.Count == 0)  return null; 
            return compFacility.LinkedBuildings[0].TryGetComp<CompStargate>();
        }

        public static Thing GetDHDOnMap(Map map)
        {
            Thing dhdOnMap = map.listerThings.AllThings.Where(t => t.TryGetComp<CompDialHomeDevice>() != null  && t.def.thingClass != typeof(Building_Stargate)).FirstOrFallback();
            
            return dhdOnMap;
        }

        public bool IsConnectedToStargate
        {
            get
            {
                if (Props.selfDialler) return true;
                if (compFacility.LinkedBuildings.Count == 0)
                    return false;

                return true;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            compFacility = parent.GetComp<CompFacility>();
        }
        
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) 
                yield return gizmo;

            CompStargate stargate = GetLinkedStargate();
            if (stargate != null)
            {
                Command_Action command = new Command_Action
                {
                    defaultLabel = "SGM.CloseStargate".Translate(),
                    defaultDesc = "SGM.CloseStargateDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                    action = delegate
                    {
                        stargate.CloseStargate(true);
                    }
                };
                if (!stargate.StargateIsActive) { command.Disable("SGM.GateIsNotActive".Translate()); }
                else if (stargate.IsReceivingGate) { command.Disable("SGM.CannotCloseIncoming".Translate()); }
                yield return command;
            }
        }
    }
    public class CompProperties_DialHomeDevice : CompProperties
    {
        public CompProperties_DialHomeDevice()
        {
            this.compClass = typeof(CompDialHomeDevice);
        }
        public bool selfDialler = false;
        public bool requiresPower = false;
    }
}
