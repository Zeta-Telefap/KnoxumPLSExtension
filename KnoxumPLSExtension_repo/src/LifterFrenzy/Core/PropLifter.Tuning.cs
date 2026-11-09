using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Every number and name of the mod, in one place.
    /// </summary>
    public sealed partial class PropLifter
    {
        private const float PickupRange = 16f;
        private const float CarryOrbitRadius = 6f;
        // RMB throw and wheel-click kick (20 in the first version): a long, hard throw.
        private const float ThrowSpeed = 40f;
        private const float CarryRotationSpeed = 720f;
        // Gravity Chaos: how fast a carried prop turns over with its carrier (degrees per second).
        private const float CarryRollRate = 540f;
        private const float CarryMoveSmoothTime = 0.08f;
        private const float DoorWidthTolerance = 0.05f;
        private static readonly Color CarryHighlightColor = new Color(0.15f, 0.65f, 1f, 1f);
        private static readonly Color ValidPlacementColor = new Color(0.15f, 0.9f, 0.35f, 1f);
        private static readonly Color InvalidPlacementColor = new Color(1f, 0.12f, 0.08f, 1f);
        private const float GroundSupportNormal = 0.65f;
        private const float PlacementSearchFineStep = 0.25f;
        private const float PlacementSearchFineRadius = 2.5f;
        private const float PlacementSearchCoarseStep = 0.5f;
        private const float PlacementSearchRadius = 5f;
        // Aim resolution: ONE fresh query per frame feeds both the glow and the G key, so the glowing
        // prop is by construction the prop G picks. The grace only bridges one-frame gaps; it never
        // overrides a fresh target.
        private const float HighlightGraceTime = 0.12f;
        private const float SuppressReleaseTime = 0.3f;
        private const float AimCandidateReuseTime = 0.2f;
        private const float ProxyDepthEpsilon = 0.02f;
        private const float ProxyPruneDistance = PickupRange + 20f;
        // The aim-assist registry is built from the places where props live (see BeginProxyBuild), a little per frame,
        // once per floor and again every ProxyRefreshInterval seconds; positions are kept up to date a few per frame.
        private const float ProxyRefreshInterval = 60f;
        private const float ProxyScanBudgetMilliseconds = 1f;
        private const int ProxyMaxDepth = 4;
        private const int ProxyRoomDepth = 2;
        private const int ProxyPositionsPerFrame = 64;
        private const string GlowObjectName = "Carry placement glow";
        // Set to true to log every change of the aimed prop (off by default; G always logs its result).
        private static readonly bool VerboseAimLog = false;
        private const float PlacementPreviewRefreshInterval = 0.25f;
        private const float ReleaseTimeout = 8f;
        private const float WorldTileWidth = 10f;
        private const float DoorLookAhead = 7f;
        private const float DoorLateralTolerance = 3f;
        private const float DoorOpenDuration = 2.5f;
        private const float DoorSafeCloseDistance = 2f;
        private const float DoorContactPadding = 0.35f;
        private const float DoorCheckInterval = 0.12f;
        // Without a CoreGameManager the player is looked up in the scene, at most this often (seconds).
        private const float PlayerSearchInterval = 2f;
        // Thrown and dropped props fly on a KINEMATIC body that this class integrates itself (StepFlight):
        // a swept box against the cell grid (walls, void, closed doors and windows), the floor and the
        // ceiling, plus a box cast for loose props. Nothing in the guarantee depends on the Unity layer
        // matrix, on dynamic rigidbody collision or on continuous detection - those are what let the
        // earlier versions fly through BB+ walls and the floor.
        private const float FlightSkin = 0.02f;
        private const float FlightWallHalfThickness = 0.1f;
        private const float FlightCeilingHeight = 10f;
        private const float FlightGravityFallback = -9.81f;
        private const float FlightMaxSpeed = 80f;
        private const int FlightMaxBounces = 4;
        private const int FlightMaxDoorRetries = 6;
        private const float FlightBounceRestitution = 0.35f;
        private const float FlightBounceFriction = 0.85f;
        private const float FlightBounceMinNormalSpeed = 0.6f;
        private const float FlightScanStep = 0.1f;
        private const float FlightMinHalfExtent = 0.05f;
        private const float FlightGrowFactor = 1.3f;
        // The doors of a floor do not change once it is built; the list is renewed only when it is this old and asked for.
        private const float LevelDoorsInterval = 120f;
        private const float FlightParallelEpsilon = 1e-7f;
        // A thrown prop that hits a character pushes it with the call the Bully uses in Bully.Push:
        // Entity.AddForce(new Force(direction, speed, acceleration)). The Bully's own values are speed 5 and
        // acceleration -5. Here the speed grows with the prop's speed (acceleration = -speed, like the Bully)
        // and is multiplied by NpcPushStrength; the Bang sound (1st Prize's audBang) is played. With
        // acceleration = -speed the force lasts 1 s and moves the character speed / 2, so the strength
        // multiplies the speed and the distance alike (1 = the first version; 8 = eight times stronger).
        private const float NpcPushMinSpeed = 5f;
        private const float NpcPushMaxSpeed = 25f;
        private const float NpcPushPerPropSpeed = 0.5f;
        private const float NpcPushStrength = 8f;
        private const float NpcMinHitSpeed = 3f;
        private const float NpcHitBounce = 0.3f;
        // A thrown prop that touches a character breaks the player's "Bullying" rule (PlayerManager.RuleBreak, the
        // way drinking or entering a faculty room do): the Principal scolds the player when he sees it within
        // BullyingLinger seconds, for at least BullyingSensitivity seconds in total.
        private const string BullyingRule = "Bullying";
        private const float BullyingLinger = 1f;
        private const float BullyingSensitivity = 0.25f;
        // While a prop is carried, and for a moment after it was thrown or placed, the game does not see the
        // mouse: LMB (PlayerClick), RMB (ItemManager.UseItem) and the wheel (item selection).
        private const float GameMouseBlockAfterRelease = 0.3f;
        // Sounds (SoundObject asset names). Bang is also 1st Prize's audBang, Ben_Gum_Whoosh is also the audWhoosh of
        // the Gum. The whoosh loops while a thrown prop flies, Bang plays on every wall impact, LockDoorStop (quiet)
        // when the prop touches the floor.
        private const string BangSoundName = "Bang";
        private const string WhooshSoundName = "Ben_Gum_Whoosh";
        private const string FloorSoundName = "LockDoorStop";
        private const float FloorSoundVolume = 0.25f;
        private const float SoundMinDistance = 10f;
        private const float SoundMaxDistance = 70f;
        private const float ImpactSoundMinSpeed = 2.5f;
        private const float ImpactSoundInterval = 0.2f;
        private const float SoundRescanInterval = 30f;

        private static readonly FieldInfo HardCoverageField = typeof(Cell).GetField(
            "hardCoverage", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SoftCoverageField = typeof(Cell).GetField(
            "softCoverage", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly RaycastHit[] RayHits = new RaycastHit[128];
        private static readonly RaycastHit[] SweepHits = new RaycastHit[64];
        private static readonly Collider[] WalkabilityHits = new Collider[128];
        private static readonly List<Collider> NpcColliders = new List<Collider>();
        private static readonly RayHitComparer RayHitOrder = new RayHitComparer();
        private static readonly Dictionary<string, SoundObject> SoundsByName =
            new Dictionary<string, SoundObject>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SoundsReported = new HashSet<string>();
    }
}
