using System;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                List<IntVec3> pattern = new List<IntVec3>();

                List<IntVec3> vortexPattern = new List<IntVec3>();
                if (rot == Rot4.North) vortexPattern = sgProps.vortexPattern_north;
                if (rot == Rot4.South) vortexPattern = sgProps.vortexPattern_south;
                if (rot == Rot4.East) vortexPattern = sgProps.vortexPattern_east;
                if (rot == Rot4.West) vortexPattern = sgProps.vortexPattern_west;
                
                foreach (IntVec3 vec in vortexPattern)
                {
                    pattern.Add(center + vec);
                }
                GenDraw.DrawFieldEdges(pattern, Color.red);
                return;
            }
        }
	}
}
