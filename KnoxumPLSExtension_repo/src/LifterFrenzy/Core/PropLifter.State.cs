using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// What the runtime remembers: the held prop, this frame's aim, the per-floor caches and the throttles. What
    /// belongs to one prop lives in CarryState.
    /// </summary>
    public sealed partial class PropLifter
    {

        private static float _nextSoundScanTime;
        private static float _nextSoundErrorLogTime;
        private static float _gameMouseBlockedUntil;

        private readonly List<CarryState> _airborne = new List<CarryState>();
        private CarryState _held;
        private PlayerManager _player;
        private Transform _hoveredRoot;
        private Candidate _hoveredCandidate;
        private Transform _suppressHoverRoot;
        private GlowState _hoverGlow;
        private int _lastPickupFrame = -1;
        private float _nextDoorCheckTime;
        private float _nextPlayerSearchTime;
        private float _lastAimTime;
        private int _aimFrame = -1;
        private PlayerManager _aimPlayer;
        private Candidate _aimResult;
        private Collider _aimBlocker;
        private float _aimBlockerDistance;
        private string _aimNote;
        private Candidate _aimCache;
        private float _aimCacheTime;
        private int _lastNoTargetLogFrame = -1;
        private float _nextHighlightErrorLogTime;
        private float _nextAimErrorLogTime;
        private float _nextSweepErrorLogTime;
        private readonly Dictionary<long, List<FlightDoor>> _flightDoors = new Dictionary<long, List<FlightDoor>>();
        private EnvironmentController _flightDoorsEnvironment;
        private int _flightDoorsVersion = -1;
        private Door[] _levelDoors = new Door[0];
        private Window[] _levelWindows = new Window[0];
        private Bounds[] _levelWindowBounds = new Bounds[0];
        private LockdownDoor[] _levelSuperDoors = new LockdownDoor[0];
        private EnvironmentController _levelDoorsEnvironment;
        private float _levelDoorsBuiltAt = -1000f;
        private int _levelDoorsVersion;
        private readonly List<ProxyProp> _proxies = new List<ProxyProp>();
        private readonly Dictionary<Transform, ProxyProp> _proxyByRoot = new Dictionary<Transform, ProxyProp>();
        private readonly HashSet<Transform> _proxyVisited = new HashSet<Transform>();
        private readonly List<ProxyContainer> _proxyContainers = new List<ProxyContainer>();
        private readonly Stack<ProxyNode> _proxyStack = new Stack<ProxyNode>();
        private List<ProxyProp> _proxyBuilding;
        private int _proxyContainerIndex;
        private int _proxyCursor;
        private EnvironmentController _proxyEnvironment;
        private float _proxyBuiltAt = -1000f;
        private bool _proxyErrorLogged;
    }
}
