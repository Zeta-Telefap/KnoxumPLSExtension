// Decompiled reference: CoreGameManager
// Key APIs used by KnoxumLuaExtensions:
//   Singleton<CoreGameManager>.Instance
//   .GetPlayer(int num) → PlayerManager
//   .GetCamera(int num) → GameCamera
//   .GetHud(int num) → HudManager
//   .audMan → AudioManager (public field)
//   .setPlayers → int (public field, player count)
//   .EndGame(Transform player, Baldi baldi)
//   .AddPoints(int points, int player, bool playAnimation)
//   .GetPoints(int player) → int
//   .Lives → int (property)
//   .Paused → bool (property)
//   .MapOpen → bool (property)
//   .disablePause → bool (public field)

// Decompiled reference: Door
// Key APIs:
//   .open → bool (PUBLIC FIELD)
//   .locked → bool (PUBLIC FIELD)
//   .IsOpen → bool (property, reads .open)
//   .Open(bool cancelTimer, bool makeNoise) — virtual
//   .Shut() — virtual
//   .Lock(bool cancelTimer) — virtual
//   .LockTimed(float time) — virtual
//   .Unlock() — virtual
//   .Toggle(bool cancelTimer, bool makeNoise) — virtual
//   .aTile → Cell (property, ec.cells[position])
//   .bTile → Cell (property, ec.cells[position + bOffset])
//   .CellOnOtherSide(RoomController thisSideRoom) → Cell
//   .direction → Direction (inherited from TileBasedObject)
//   .position → IntVector2 (inherited from TileBasedObject)
//   .noiseValue → int (public field)
//   .closeBlocks → bool (public field)
//   .lockBlocks → bool (public field, default true)

// Decompiled reference: Navigator
// Key APIs:
//   .ec → EnvironmentController (public field)
//   .npc → NPC (public field)
//   .entity → Entity (protected field)
//   .FindPath(Vector3 startPos, Vector3 targetPos, bool targeting)
//   .FindPath(Vector3 startPos, Vector3 targetPos)
//   .FindPath(Vector3 targetPosition) — convenience overload
//   .ClearDestination()
//   .WanderRandom()
//   .WanderRounds()
//   .WanderFlee(DijkstraMap dijkstraMap)
//   .CheckPath()
//   .SetSpeed(float val)
//   .SetRoomAvoidance(bool val)
//   .HasDestination → bool (property, destinationPoints.Count > 0)
//   .NextPoint → Vector3 (property)
//   .CurrentDestination → Vector3 (property)
//   .Wandering → bool (property)
//   .Speed → float (property)
//   .maxSpeed → float (public field, default 15)
//   .accel → float (public field, default 15)
//   .radius → float (protected field, default 2)
//   .Am → ActivityModifier (property)
//   .Entity → Entity (property)

// Decompiled reference: Map
// Key APIs:
//   .Ec → EnvironmentController (property)
//   .size → IntVector2 (public field)
//   .foundTiles → bool[,] (public field)
//   .tiles → MapTile[,] (public field)
//   .Initialize(EnvironmentController ec, IntVector2 size)
//   .Find(int posX, int posZ, int bin, RoomController room)
//   .CompleteMap() — reveals entire map
//   .UpdateTile(int posX, int posZ, int bin, RoomController room)
//   .OpenMap(bool toMap)
//   .CloseMap()
//   .TurnOn()
//   .TurnOff()
//   .AddArrow(Entity target, Color color)
//   .AddIcon(MapIcon iconPre, Transform target, Color color) → MapIcon
//   .AddMarker(Vector3 position, int id)
//   .DestroyMarker(MapMarker marker)
//   .SaveMarkers(List<Vector2> positions, List<int> ids)
//   .LoadMarkers(List<Vector2> positions, List<int> ids)
//   .UpdateIcons()
//   .FlipToMap()
//   .FlipToStickers()
//   .TotalFoundCells → int (property)
//   .MapDiscoveryRange → int (private property)
