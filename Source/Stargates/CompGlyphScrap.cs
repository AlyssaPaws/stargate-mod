using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace StargatesMod
{
    public class CompGlyphScrap : ThingComp
    {
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (!selPawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly))
            {
                yield break;
            }

            if (DefDatabase<ResearchProjectDef>.GetNamed("StargateMod_GlyphDeciphering").IsFinished)
            {
                yield return new FloatMenuOption("SGM_DecodeSGSymbols".Translate(), () =>
                {
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_DecodeGlyphs"), parent);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
            else
            {
                yield return new FloatMenuOption("SGM_CannotDecodeSGSymbols".Translate(), null);
            }
        }
    }

    public class CompProperties_GlyphScrap : CompProperties
    {
        public CompProperties_GlyphScrap()
        {
            compClass = typeof(CompGlyphScrap);
        }
    }
}
