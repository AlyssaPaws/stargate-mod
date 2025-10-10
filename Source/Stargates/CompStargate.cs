using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace StargatesMod
{
    public class CompStargate : ThingComp
    {
        const int glowRadius = 10;
        const string alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private List<Thing> _sendBuffer = new List<Thing>();
        private List<Thing> _recvBuffer = new List<Thing>();
        public int TicksSinceBufferUnloaded;
        public int TicksSinceOpened;
        public PlanetTile GateAddress;
        public bool StargateIsActive;
        public bool IsReceivingGate;
        public bool HasIris = false;
        public int TicksUntilOpen = -1;
        public bool IrisIsActivated = false;
        PlanetTile _queuedAddress;
        PlanetTile _connectedAddress = -1;
        Thing _connectedStargate;

        private Sustainer _puddleSustainer;
        private Graphic _stargatePuddle;
        private Graphic _stargateIris;

        public CompProperties_Stargate Props => (CompProperties_Stargate)props;

        Graphic StargatePuddle =>
            _stargatePuddle ?? (_stargatePuddle = GraphicDatabase.Get<Graphic_Single>(Props.puddleTexture,
                ShaderDatabase.Mote, Props.puddleDrawSize, Color.white));

        Graphic StargateIris =>
            _stargateIris ?? (_stargateIris = GraphicDatabase.Get<Graphic_Single>(Props.irisTexture,
                ShaderDatabase.Mote, Props.puddleDrawSize, Color.white));

        bool GateIsLoadingTransporter
        {
            get
            {
                CompTransporter transComp = parent.GetComp<CompTransporter>();
                return transComp != null && (transComp.LoadingInProgressOrReadyToLaunch && transComp.AnyInGroupHasAnythingLeftToLoad);
            }
        }

        public IEnumerable<IntVec3> VortexCells
        {
            get
            {
                foreach (IntVec3 offset in Props.vortexPattern) yield return offset + parent.Position;
            }
        }

        #region DHD Controls

        public void OpenStargateDelayed(PlanetTile address, int delay)
        {
            _queuedAddress = address;
            TicksUntilOpen = delay;
        }

        public void OpenStargate(PlanetTile address)
         {
            MapParent connectedMapParent = Find.WorldObjects.MapParentAt(address);
            if (!connectedMapParent.HasMap)
            {
                if (Prefs.LogVerbose) Log.Message($"StargatesMod: generating map for {connectedMapParent}");
                
                LongEventHandler.QueueLongEvent(delegate
                {
                    GetOrGenerateMapUtility.GetOrGenerateMap(connectedMapParent.Tile, connectedMapParent is WorldObject_PermSGSite ? new IntVec3(75, 1, 75) : Find.World.info.initialMapSize, connectedMapParent.def);
                }, "SGM.GeneratingStargateSite", doAsynchronously: false, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap, callback: delegate
                {
                    if (Prefs.LogVerbose) Log.Message($"StargatesMod: finished generating map");

                    FinishDiallingStargate(address, connectedMapParent);
                }); 
            }
            else
            {
                FinishDiallingStargate(address, connectedMapParent);
            }
         }


        private void FinishDiallingStargate(PlanetTile address, MapParent connectedMapParent)
        {
            Thing connectedGate = GetStargateOnMap(connectedMapParent.Map);

            if (address > -1 && (connectedGate == null || connectedGate.TryGetComp<CompStargate>().StargateIsActive))
            {
                Messages.Message("SGM.GateDialFailed".Translate(), MessageTypeDefOf.NegativeEvent);
                SGSoundDefOf.StargateMod_SGFailDial.PlayOneShot(SoundInfo.InMap(parent));
                return;
            }
            StargateIsActive = true;
            _connectedAddress = address;

            if (_connectedAddress > -1)
            {
                _connectedStargate = connectedGate;
                CompStargate sgComp = _connectedStargate.TryGetComp<CompStargate>();
                sgComp.StargateIsActive = true;
                sgComp.IsReceivingGate = true;
                sgComp._connectedAddress = GateAddress;
                sgComp._connectedStargate = parent;

                sgComp._puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(sgComp.parent));
                SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(sgComp.parent));

                CompGlower otherGlowComp = sgComp.parent.GetComp<CompGlower>();
                otherGlowComp.Props.glowRadius = glowRadius;
                otherGlowComp.PostSpawnSetup(false);
            }
            
            _puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(parent));
            SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(parent));

            CompGlower glowComp = parent.GetComp<CompGlower>();
            glowComp.Props.glowRadius = glowRadius;
            glowComp.PostSpawnSetup(false);
            if (Prefs.LogVerbose) Log.Message($"StargatesMod: finished opening gate {parent}");
        }


        public void CloseStargate(bool closeOtherGate)
        {
            CompTransporter transComp = parent.GetComp<CompTransporter>();
            transComp?.CancelLoad();

            //clear buffers just in case
            foreach (Thing thing in _sendBuffer)
                GenSpawn.Spawn(thing, parent.InteractionCell, parent.Map);

            foreach (Thing thing in _recvBuffer)
                GenSpawn.Spawn(thing, parent.InteractionCell, parent.Map);

            CompStargate connectedGateComp = null;
            if (closeOtherGate)
            {
                connectedGateComp = _connectedStargate.TryGetComp<CompStargate>();
                
                if (_connectedStargate == null || connectedGateComp == null)
                    Log.Warning($"Receiving stargate connected to stargate {parent.ThingID} didn't have CompStargate, but this stargate wanted it closed.");
                else connectedGateComp.CloseStargate(false);
            }

            SoundDef puddleCloseDef = SGSoundDefOf.StargateMod_SGClose;
            puddleCloseDef.PlayOneShot(SoundInfo.InMap(parent));
            if (connectedGateComp != null) puddleCloseDef.PlayOneShot(SoundInfo.InMap(connectedGateComp.parent));

            _puddleSustainer?.End();

            CompGlower glowComp = parent.GetComp<CompGlower>();
            glowComp.Props.glowRadius = 0;
            glowComp.PostSpawnSetup(false);

            if (Props.explodeOnUse)
            {
                CompExplosive explosive = parent.TryGetComp<CompExplosive>();
                
                if (explosive == null) Log.Warning($"Stargate {parent.ThingID} has the explodeOnUse tag set to true but doesn't have CompExplosive.");
                else explosive.StartWick();
            }

            StargateIsActive = false;
            TicksSinceBufferUnloaded = 0;
            TicksSinceOpened = 0;
            _connectedAddress = -1;
            _connectedStargate = null;
            IsReceivingGate = false;
        }

        #endregion

        public static Thing GetStargateOnMap(Map map, Thing thingToIgnore = null)
        {
            Thing gateOnMap = null;
            
            Thing thing = map.listerBuildings.allBuildingsColonist.Where(t => t.def.thingClass == typeof(Building_Stargate)).FirstOrFallback() ??
                          map.listerBuildings.allBuildingsNonColonist.Where(t => t.def.thingClass == typeof(Building_Stargate)).FirstOrFallback();

            if (thing != thingToIgnore) 
                gateOnMap = thing;
            
            return gateOnMap;
        }

        public static string GetStargateDesignation(PlanetTile address)
        {
            if (address.tileId < 0) return "UnknownLower".Translate();
            
            Rand.PushState(address.tileId);
            //pattern: (pLDesignation)(num)(char)-(num)(num)(num)
            string pLDesignation = address.Layer.Def.isSpace ? "O" : "P"; //Planet layer designation: O for orbit / space, P for planetary / other
            string designation = $"{pLDesignation}{Rand.RangeInclusive(0, 9)}{alpha[Rand.RangeInclusive(0, 25)]}-{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}"; 
            Rand.PopState();
            
            return designation;
        }

        private void PlayTeleportSound()
        {
            DefDatabase<SoundDef>.GetNamed($"StargateMod_teleport_{Rand.RangeInclusive(1, 4)}").PlayOneShot(SoundInfo.InMap(parent));
        }

        private void DoUnstableVortex()
        {
            List<Thing> excludedThings = new List<Thing> { parent };
            
            excludedThings.AddRange(from pos in Props.vortexPattern 
                from thing in parent.Map.thingGrid.ThingsAt(parent.Position + pos) 
                where thing.def.passability == Traversability.Standable select thing);

            foreach (IntVec3 pos in Props.vortexPattern)
            {
                DamageDef damType = DefDatabase<DamageDef>.GetNamed("StargateMod_KawooshExplosion");

                Explosion explosion = (Explosion)GenSpawn.Spawn(ThingDefOf.Explosion, parent.Position, parent.Map);
                explosion.damageFalloff = false;
                explosion.damAmount = damType.defaultDamage;
                explosion.Position = parent.Position + pos;
                explosion.radius = 0.5f;
                explosion.damType = damType;
                explosion.StartExplosion(null, excludedThings);
            }
        }

        public void AddToSendBuffer(Thing thing)
        {
            _sendBuffer.Add(thing);
            PlayTeleportSound();
        }

        public void AddToReceiveBuffer(Thing thing)
        {
            _recvBuffer.Add(thing);
        }

        private void WormholeContentDisposal(bool isRecvBuffer)
        {
            Thing thingToDestroy = isRecvBuffer ? _recvBuffer[0] : _sendBuffer[0];
            if (thingToDestroy is Pawn pawn)
            {
                // Remove death refusal hediff (if present) before killing pawn, to avoid error.
                foreach (var hediff in pawn.health.hediffSet.hediffs.ToList().Where(hediff => hediff.def.defName == "DeathRefusal"))
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
                    
            if (!thingToDestroy.DestroyedOrNull()) thingToDestroy.Kill();
            
            if (!isRecvBuffer) _sendBuffer.Remove(thingToDestroy);
            else
            {
                _recvBuffer.Remove(thingToDestroy);
                SGSoundDefOf.StargateMod_IrisHit.PlayOneShot(SoundInfo.InMap(parent));
            }
        }
        
        #region Comp Overrides

        public override void PostDraw()
        {
            base.PostDraw();
            if (IrisIsActivated)
                StargateIris.Draw(parent.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.BuildingOnTop) - (Vector3.one * 0.01f), Rot4.North, parent);

            if (StargateIsActive)
                StargatePuddle.Draw(parent.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.BuildingOnTop) - (Vector3.one * 0.02f), Rot4.North, parent);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (TicksUntilOpen > 0)
            {
                TicksUntilOpen--;
                if (TicksUntilOpen == 0)
                {
                    TicksUntilOpen = -1;
                    OpenStargate(_queuedAddress);
                    _queuedAddress = -1;
                }
            }

            if (!StargateIsActive) return;
            if (!IrisIsActivated && TicksSinceOpened < 150 && TicksSinceOpened % 10 == 0)
                DoUnstableVortex();

            if (parent.Fogged()) FloodFillerFog.FloodUnfog(parent.Position, parent.Map);

            CompStargate sgComp = _connectedStargate.TryGetComp<CompStargate>();
            CompTransporter transComp = parent.GetComp<CompTransporter>();
            
            if (transComp != null)
            {
                Thing transportThing = transComp.innerContainer.FirstOrFallback();
                if (transportThing != null)
                {
                    if (transportThing.Spawned) transportThing.DeSpawn();
                    AddToSendBuffer(transportThing);
                    transComp.innerContainer.Remove(transportThing);
                }
                else if (transComp.LoadingInProgressOrReadyToLaunch && !transComp.AnyInGroupHasAnythingLeftToLoad)
                    transComp.CancelLoad();
            }

            if (_sendBuffer.Any())
            {
                if (!IsReceivingGate)
                {
                    sgComp.AddToReceiveBuffer(_sendBuffer[0]);
                    _sendBuffer.Remove(_sendBuffer[0]);
                }
                else WormholeContentDisposal(false);
            }

            if (_recvBuffer.Any() && TicksSinceBufferUnloaded > Rand.Range(10, 80))
            {
                TicksSinceBufferUnloaded = 0;
                if (!IrisIsActivated)
                {
                    GenSpawn.Spawn(_recvBuffer[0], parent.InteractionCell, parent.Map);
                    _recvBuffer.Remove(_recvBuffer[0]);
                    PlayTeleportSound();
                }
                else WormholeContentDisposal(true);

                if (_connectedAddress == -1 && !_recvBuffer.Any())
                    CloseStargate(false);

                TicksSinceBufferUnloaded++;
                TicksSinceOpened++;
                if (IsReceivingGate && TicksSinceBufferUnloaded > 2500 && !_connectedStargate.TryGetComp<CompStargate>().GateIsLoadingTransporter)
                    CloseStargate(true);
            }

            if (_connectedAddress == -1 && !_recvBuffer.Any())
                CloseStargate(false);

            TicksSinceBufferUnloaded++;
            TicksSinceOpened++;

            if (IsReceivingGate && TicksSinceBufferUnloaded > 2500 && !_connectedStargate.TryGetComp<CompStargate>().GateIsLoadingTransporter)
                CloseStargate(true);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            GateAddress = parent.Map.Tile;
            Find.World.GetComponent<WorldComp_StargateAddresses>().AddAddress(GateAddress);

            if (StargateIsActive)
            {
                if (_connectedStargate == null && _connectedAddress != -1) 
                    _connectedStargate = GetStargateOnMap(Find.WorldObjects.MapParentAt(_connectedAddress).Map);
                _puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(parent));
            }

            //fix nullreferenceexception that happens when the innercontainer disappears for some reason, hopefully this doesn't end up causing a bug that will take hours to track down ;)
            CompTransporter transComp = parent.GetComp<CompTransporter>();
            if (transComp != null && transComp.innerContainer == null)
                transComp.innerContainer = new ThingOwner<Thing>(transComp);
            
            if (Prefs.LogVerbose) Log.Message($"StargateMod: compsg postspawnssetup: sgactive={StargateIsActive} connectgate={_connectedStargate} connectaddress={_connectedAddress}, mapparent={parent.Map.Parent}");
        }

        public string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SGM.GateAddress".Translate(GetStargateDesignation(GateAddress)));
            
            switch (StargateIsActive)
            {
                case false when TicksUntilOpen <= -1:
                    sb.AppendLine("InactiveFacility".Translate().CapitalizeFirst());
                    break;
                case true:
                    sb.AppendLine("SGM.ConnectedToGate".Translate(GetStargateDesignation(_connectedAddress), (IsReceivingGate ? "SGM.Incoming" : "SGM.Outgoing").Translate()));
                    break;
            }

            if (HasIris) sb.AppendLine("SGM.IrisStatus".Translate((IrisIsActivated ? "SGM.IrisClosed" : "SGM.IrisOpen").Translate()));
            if (TicksUntilOpen > 0) sb.AppendLine("SGM.TimeUntilGateLock".Translate(TicksUntilOpen.ToStringTicksToPeriod()));
            
            return sb.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;

            if (Props.canHaveIris && HasIris)
            {
                Command_Action irisControl = new Command_Action
                {
                    defaultLabel = "SGM.OpenCloseIris".Translate(),
                    defaultDesc = "SGM.OpenCloseIrisDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get(Props.irisTexture),
                    action = delegate
                    {
                        IrisIsActivated = !IrisIsActivated;
                        if (IrisIsActivated) SGSoundDefOf.StargateMod_IrisOpen.PlayOneShot(SoundInfo.InMap(parent));
                        else SGSoundDefOf.StargateMod_IrisClose.PlayOneShot(SoundInfo.InMap(parent));
                    }
                };
                yield return irisControl;
            }

            if (!Prefs.DevMode) yield break;
            
            Command_Action devAddRemoveIris = new Command_Action
            {
                defaultLabel = "Add/remove iris",
                action = delegate { HasIris = !HasIris; }
            };
            yield return devAddRemoveIris;
            devAddRemoveIris = new Command_Action
            {
                defaultLabel = "Force close",
                defaultDesc = "Force close this gate to hopefully remove strange behaviours (this will not close gate at the other end).",
                action = delegate
                {
                    CloseStargate(false);
                    Log.Message($"Stargate {parent.ThingID} was force-closed.");
                }
            };
            yield return devAddRemoveIris;
        }

        private void CleanupGate()
        {
            if (_connectedStargate != null) CloseStargate(true);

            Find.World.GetComponent<WorldComp_StargateAddresses>().RemoveAddress(GateAddress);
        }
        
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            CleanupGate();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            CleanupGate();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref StargateIsActive, "StargateIsActive");
            Scribe_Values.Look(ref IsReceivingGate, "IsReceivingGate");
            Scribe_Values.Look(ref HasIris, "HasIris");
            Scribe_Values.Look(ref IrisIsActivated, "IrisIsActivated");
            Scribe_Values.Look(ref TicksSinceOpened, "TicksSinceOpened");
            Scribe_Values.Look(ref _connectedAddress, "_connectedAddress");
            Scribe_References.Look(ref _connectedStargate, "_connectedStargate");
            Scribe_Collections.Look(ref _recvBuffer, "_recvBuffer", LookMode.GlobalTargetInfo);
            Scribe_Collections.Look(ref _sendBuffer, "_sendBuffer", LookMode.GlobalTargetInfo);
        }

        public override string CompInspectStringExtra()
        {
            return base.CompInspectStringExtra() + "SGM.RespawnGateString".Translate();
        }

        #endregion
    }

    public class CompProperties_Stargate : CompProperties
    {
        public CompProperties_Stargate()
        {
            compClass = typeof(CompStargate);
        }

        public bool canHaveIris = true;
        public bool explodeOnUse = false;
        public string puddleTexture;
        public string irisTexture;
        public Vector2 puddleDrawSize;

        public List<IntVec3> vortexPattern = new List<IntVec3>
        {
            new IntVec3(0, 0, 1),
            new IntVec3(1, 0, 1),
            new IntVec3(-1, 0, 1),
            new IntVec3(0, 0, 0),
            new IntVec3(1, 0, 0),
            new IntVec3(-1, 0, 0),
            new IntVec3(0, 0, -1),
            new IntVec3(1, 0, -1),
            new IntVec3(-1, 0, -1),
            new IntVec3(0, 0, -2),
            new IntVec3(1, 0, -2),
            new IntVec3(-1, 0, -2),
            new IntVec3(0, 0, -3)
        };
    }
}

