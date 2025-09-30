using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StargatesMod
{
    public class PlaceWorker_Stargate : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            base.DrawGhost(def, center, rot, ghostCol, thing);
            
            foreach (CompProperties props in def.comps)
            {
                if (!(props is CompProperties_Stargate sgProps)) continue;
                
                List<IntVec3> pattern = sgProps.vortexPattern.Select(vec => center + vec).ToList();

                GenDraw.DrawFieldEdges(pattern, Color.red);
                return;
            }
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
            if (CompStargate.GetStargateOnMap(map, thing) != null)
                return new AcceptanceReport("SGM.OnlyOneSGPerMap".Translate());
            
            /*Pocket maps currently not supported*/
            if(map.IsPocketMap) return new AcceptanceReport("SGM.PocketMapDisallowed".Translate());

			return true;
		}
	}
}
