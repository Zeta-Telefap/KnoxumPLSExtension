using System;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The small types of the swept flight: a door in its way, a hit, the edges of a cell, and the scale table for a
    /// prop too big for a doorway.
    /// </summary>
    public sealed partial class PropLifter
    {

        private static readonly float[] FlightFitScales = { 1f, 0.85f, 0.7f, 0.55f, 0.4f, 0.3f, 0.2f, 0.12f, 0.06f };

        // ------------------------------------------------------------------------------------------
        // Flight of released props (RMB throw and LMB drop)
        // ------------------------------------------------------------------------------------------

        private enum FlightHitKind
        {
            Wall,
            Door,
            Floor,
            Ceiling,
            Collider,
            Support
        }

        private enum FlightEdgeKind
        {
            None,
            Wall,
            Door
        }

        private struct FlightHit
        {
            public float T;
            public Vector3 Normal;
            public FlightHitKind Kind;
            public bool Inside;
            public FlightDoor Door;
            public Collider Collider;
            // A locker whose box stopped the prop (it has no collider to report).
            public Transform Piece;
            public int EdgeX;
            public int EdgeZ;
            public int EdgeAxis;
        }

        private sealed class FlightDoor
        {
            public Door Door;
            public Collider[] Colliders;
        }
    }
}
