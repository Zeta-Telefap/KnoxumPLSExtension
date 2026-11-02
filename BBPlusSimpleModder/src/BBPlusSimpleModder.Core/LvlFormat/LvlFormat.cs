namespace BBPlusSimpleModder.Core.LvlFormat;

/// <summary>
/// Модель уровня в духе Plus Level Format (PlusStudioLevelFormat, v10 —
/// см. ultimate_dev_mods_decompile.cs, секция строка 155646+):
/// BaldiLevel { levelSize, cells[,], rooms, lights, tileObjects, doors, windows,
/// exits, structures, randomStructures, npcs, posters, premadeRoomPlacements,
/// spawnPoint, spawnDirection, timeLimit, levelTitle, version = 10 }.
/// Чтение/запись бинарного .lvl — Write/Read(BinaryWriter/BinaryReader).
/// </summary>
public class BaldiLevelModel
{
    public const byte Version = 10;

    public int Width { get; set; } = 40;
    public int Height { get; set; } = 40;

    /// <summary>Коды клеток: 0=пусто, 1=коридор, 2=класс, 3=faculty, 4=офис, 5=…</summary>
    public byte[,] Cells { get; set; }

    public List<RoomModel> Rooms { get; } = new();
    public List<DoorModel> Doors { get; } = new();
    public List<WindowModel> Windows { get; } = new();
    public List<NpcSpawnModel> Npcs { get; } = new();
    public List<ItemSpawnModel> Items { get; } = new();
    public List<PosterModel> Posters { get; } = new();
    public List<StructureModel> Structures { get; } = new();

    public float SpawnX { get; set; } = 5f;
    public float SpawnY { get; set; } = 5f;
    public float SpawnZ { get; set; } = 5f;
    public int SpawnDirection { get; set; } = 0;
    public float TimeLimit { get; set; } = 180f;
    public string LevelTitle { get; set; } = "WIP";

    public BaldiLevelModel() => Cells = new byte[Width, Height];
    public BaldiLevelModel(int w, int h) { Width = w; Height = h; Cells = new byte[w, h]; }

    public byte GetCell(int x, int y) => InBounds(x, y) ? Cells[x, y] : (byte)0;
    public void SetCell(int x, int y, byte v) { if (InBounds(x, y)) Cells[x, y] = v; }
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    // ---------- бинарный .lvl (структура по декомпиляту PlusStudioLevelFormat v10) ----------

    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(LevelTitle ?? "");
        w.Write(TimeLimit);
        w.Write(Width); w.Write(Height);
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                w.Write(Cells[x, y]);
        w.Write(SpawnX); w.Write(SpawnY); w.Write(SpawnZ);
        w.Write(SpawnDirection);

        WriteList(w, Rooms, (ww, r) => { ww.Write(r.Function); ww.Write(r.X); ww.Write(r.Y); ww.Write(r.W); ww.Write(r.H); });
        WriteList(w, Doors, (ww, d) => { ww.Write(d.X); ww.Write(d.Y); ww.Write(d.Direction); ww.Write(d.Room); });
        WriteList(w, Windows, (ww, d) => { ww.Write(d.X); ww.Write(d.Y); ww.Write(d.Direction); });
        WriteList(w, Npcs, (ww, n) => { ww.Write(n.NpcGuid); ww.Write(n.X); ww.Write(n.Y); });
        WriteList(w, Items, (ww, i) => { ww.Write(i.ItemGuid); ww.Write(i.X); ww.Write(i.Y); });
        WriteList(w, Posters, (ww, p) => { ww.Write(p.PosterGuid); ww.Write(p.X); ww.Write(p.Y); ww.Write(p.Direction); });
        WriteList(w, Structures, (ww, s) => { ww.Write(s.StructureGuid); ww.Write(s.X); ww.Write(s.Y); });
    }

    public static BaldiLevelModel Read(BinaryReader r)
    {
        var ver = r.ReadByte();
        if (ver != Version) throw new InvalidDataException($"Версия .lvl {ver}, ожидается {Version}");
        var m = new BaldiLevelModel();
        m.LevelTitle = r.ReadString();
        m.TimeLimit = r.ReadSingle();
        m.Width = r.ReadInt32(); m.Height = r.ReadInt32();
        m.Cells = new byte[m.Width, m.Height];
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++)
                m.Cells[x, y] = r.ReadByte();
        m.SpawnX = r.ReadSingle(); m.SpawnY = r.ReadSingle(); m.SpawnZ = r.ReadSingle();
        m.SpawnDirection = r.ReadInt32();

        ReadList(r, m.Rooms, rr => new RoomModel { Function = rr.ReadString(), X = rr.ReadInt32(), Y = rr.ReadInt32(), W = rr.ReadInt32(), H = rr.ReadInt32() });
        ReadList(r, m.Doors, rr => new DoorModel { X = rr.ReadInt32(), Y = rr.ReadInt32(), Direction = rr.ReadInt32(), Room = rr.ReadString() });
        ReadList(r, m.Windows, rr => new WindowModel { X = rr.ReadInt32(), Y = rr.ReadInt32(), Direction = rr.ReadInt32() });
        ReadList(r, m.Npcs, rr => new NpcSpawnModel { NpcGuid = rr.ReadString(), X = rr.ReadInt32(), Y = rr.ReadInt32() });
        ReadList(r, m.Items, rr => new ItemSpawnModel { ItemGuid = rr.ReadString(), X = rr.ReadInt32(), Y = rr.ReadInt32() });
        ReadList(r, m.Posters, rr => new PosterModel { PosterGuid = rr.ReadString(), X = rr.ReadInt32(), Y = rr.ReadInt32(), Direction = rr.ReadInt32() });
        ReadList(r, m.Structures, rr => new StructureModel { StructureGuid = rr.ReadString(), X = rr.ReadInt32(), Y = rr.ReadInt32() });
        return m;
    }

    public void Save(string path)
    {
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        Write(w);
    }

    public static BaldiLevelModel Load(string path)
    {
        using var fs = File.OpenRead(path);
        using var r = new BinaryReader(fs);
        return Read(r);
    }

    private static void WriteList<T>(BinaryWriter w, IReadOnlyList<T> list, Action<BinaryWriter, T> writeItem)
    {
        w.Write(list.Count);
        foreach (var item in list) writeItem(w, item);
    }

    private static void ReadList<T>(BinaryReader r, List<T> list, Func<BinaryReader, T> readItem)
    {
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) list.Add(readItem(r));
    }
}

public class RoomModel { public string Function { get; set; } = "Classroom"; public int X, Y, W, H; }
public class DoorModel { public int X, Y, Direction; public string Room { get; set; } = ""; }
public class WindowModel { public int X, Y, Direction; }
public class NpcSpawnModel { public string NpcGuid { get; set; } = ""; public int X, Y; }
public class ItemSpawnModel { public string ItemGuid { get; set; } = ""; public int X, Y; }
public class PosterModel { public string PosterGuid { get; set; } = ""; public int X, Y, Direction; }
public class StructureModel { public string StructureGuid { get; set; } = ""; public int X, Y; }
