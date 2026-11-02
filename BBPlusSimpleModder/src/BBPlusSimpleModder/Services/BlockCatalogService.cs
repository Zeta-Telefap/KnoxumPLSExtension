using BBPlusSimpleModder.Compiler;
using BBPlusSimpleModder.Core.BlockEngine;
using BBPlusSimpleModder.Core.Models;

namespace BBPlusSimpleModder.Services;

/// <summary>
/// Каталог блоков: ручные «кривые» (Core.BlockCatalog) + авто-блоки из сборок игры
/// (CecilBlockCatalog по папке игры). Поиск по имени/категории.
/// </summary>
public class BlockCatalogService
{
    private readonly Logger _logger;
    private List<BlockDefinition>? _auto;

    public BlockCatalogService(Logger logger) => _logger = logger;

    /// <summary>Подгрузить авто-блоки из папки игры (вызывается при открытии проекта).</summary>
    public void LoadAutoBlocks(string gamePath)
    {
        try
        {
            var refs = ReferenceResolver.Resolve(gamePath);
            var interesting = refs.All.Where(p =>
            {
                var n = Path.GetFileName(p);
                return n is "Assembly-CSharp.dll" or "MTM101BaldAPI.dll" or "PixelInternalAPI.dll";
            });
            _auto = CecilBlockCatalog.BuildAutoBlocks(interesting);
            _logger.Info($"Авто-блоки из сборок игры: {_auto.Count}");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Авто-блоки не загружены: {ex.Message}");
            _auto = new List<BlockDefinition>();
        }
    }

    public List<BlockDefinition> All()
        => BlockCatalog.Handcrafted.Concat(_auto ?? new List<BlockDefinition>()).ToList();

    public BlockDefinition? Get(string id)
        => All().FirstOrDefault(b => b.Id == id) ?? BlockCatalog.Find(id);

    public record BlockCategory(string Category, List<BlockDefinition> Blocks);

    public List<BlockCategory> Search(string filter)
    {
        var all = All();
        if (!string.IsNullOrWhiteSpace(filter))
            all = all.Where(b =>
                b.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                b.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                b.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        return all.GroupBy(b => b.Category)
                  .OrderBy(g => g.Key)
                  .Select(g => new BlockCategory(g.Key, g.ToList()))
                  .ToList();
    }
}
