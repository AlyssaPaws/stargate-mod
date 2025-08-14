using System.Collections.Generic;
using StargatesMod.Mod_Settings;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace StargatesMod
{
    public class JobDriver_DialStargate : JobDriver
    {
        private const TargetIndex targetDHD = TargetIndex.A;
        
        private readonly StargatesMod_Settings _settings = LoadedModManager.GetMod<StargatesMod_Mod>().GetSettings<StargatesMod_Settings>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(targetDHD), job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompDialHomeDevice dhdComp = job.GetTarget(targetDHD).Thing.TryGetComp<CompDialHomeDevice>();
            this.FailOnDestroyedOrNull(targetDHD);
            this.FailOn(() => dhdComp.GetLinkedStargate().StargateIsActive);

            yield return Toils_Goto.GotoCell(job.GetTarget(targetDHD).Thing.InteractionCell, PathEndMode.OnCell);
            yield return new Toil
            {
                initAction = () =>
                {
                    CompStargate linkedStargate = dhdComp.GetLinkedStargate();
                    int lockDelay = 900;
                    if (_settings.ShortenGateDialSeq) lockDelay = 200;
                    linkedStargate.OpenStargateDelayed(dhdComp.lastDialledAddress, lockDelay);
                    if (!dhdComp.Props.selfDialler) SGSoundDefOf.StargateMod_DhdUsual_1.PlayOneShot(SoundInfo.InMap(dhdComp.parent));
                }
            };
        }
    }
}