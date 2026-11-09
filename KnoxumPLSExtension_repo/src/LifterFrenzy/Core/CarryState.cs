using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// One prop taken out of the world: its body, its settings, and the flight / event state that belongs to it.
    /// </summary>
    public sealed partial class PropLifter
    {


        private sealed class CarryState
        {
            public Transform Root;
            public EnvironmentObject EnvironmentObject;
            public EnvironmentController Environment;
            public PlayerManager Player;
            public Cell OriginCell;
            public Transform OriginParent;
            public Vector3 OriginPosition;
            public float InitialY;
            public float OriginFloorY;
            public Vector3 CellOffset;
            public Vector3 LocalGrabPoint;
            public Vector3 PositionSmoothVelocity;
            public Quaternion CameraRotation;
            public Quaternion CarryRotation;
            public Quaternion TargetRotation;
            public int ManualRotationSteps;
            // The turn that puts the side the player aimed at toward the camera (see AimedSideRotation).
            public float SideYaw;
            // Gravity Chaos: the turn-over of the carried prop (0 upright, 180 feet up) about CarryRollAxis, the
            // horizontal line toward the player. CarryRotation itself stays the upright, yaw-only pose.
            public float CarryRoll;
            public Vector3 CarryRollAxis;
            public Quaternion OriginalRotation;
            public CellCoverage Coverage;
            public CellCoverage RemovedHardCoverage;
            public CellCoverage RemovedSoftCoverage;
            public bool UsesCoverage;
            public bool IsLocker;
            public ParentKind ParentKind;
            public Renderer[] Renderers;
            public List<Renderer> RegisteredRenderers;
            public Dictionary<Cell, bool> PlacementWalkabilityCache;
            public List<IgnoredCollisionPair> IgnoredCollisionPairs;
            public RoomController PreferredRoom;
            public GlowState Glow;
            public float NextPlacementPreviewTime;
            public Rigidbody Body;
            public Collider[] PlacementColliders;
            public BoxCollider AddedCollider;
            public List<MeshCollider> NonConvexColliders;
            public bool AddedBody;
            public float OriginalMass;
            public float OriginalDrag;
            public float OriginalAngularDrag;
            public bool OriginalIsKinematic;
            public bool OriginalUseGravity;
            public bool OriginalDetectCollisions;
            public RigidbodyConstraints OriginalConstraints;
            public CollisionDetectionMode OriginalCollisionMode;
            public RigidbodyInterpolation OriginalInterpolation;
            public Vector3 OriginalVelocity;
            public Vector3 OriginalAngularVelocity;
            public float Age;
            public bool Thrown;
            public int WallHits;
            // Characters this thrown prop has already pushed (each is pushed once per flight).
            public List<NPC> PushedNpcs;
            // The looping whoosh of a thrown prop: it follows the prop and is destroyed when the prop lands.
            public GameObject FlightSoundObject;
            public float NextImpactSoundTime;
            // Flight of a released prop (see StepFlight): the body is kinematic and moved from here.
            public Vector3 FlightPosition;
            public Vector3 FlightVelocity;
            public Vector3 FlightCenterOffset;
            public Vector3 FlightFullHalf;
            public Vector3 FlightHalf;
            public float FlightDrag;
            public float FlightFloorY;
            public bool Landed;
            // True while a landed prop is being settled: the placement then also tests the full-size box
            // against the walls and doors (see FlightBoxClear).
            public bool CheckFlightBox;
            // What the vanilla events did to this prop (the Events files).
            public EventFx Fx;
        }
    }
}
