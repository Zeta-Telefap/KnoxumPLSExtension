using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace BBPlusSimpleModder.Compiler;

/// <summary>Результат компиляции.</summary>
public class CompileResult
{
    public byte[] Dll { get; set; } = Array.Empty<byte>();
    public byte[] Pdb { get; set; } = Array.Empty<byte>();
    public string XmlDoc { get; set; } = "";
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Success => Errors.Count == 0;
}

/// <summary>
/// Компиляция сгенерированного C# в BepInEx-плагин in-memory через Roslyn:
/// на выходе DLL + портативный PDB + XML-документация.
/// </summary>
public static class RoslynCompiler
{
    public static CompileResult Compile(
        IReadOnlyDictionary<string, string> sources,
        IEnumerable<string> referencePaths,
        string assemblyName,
        bool debug = true)
    {
        var result = new CompileResult();

        var trees = sources.Select(kv =>
            CSharpSyntaxTree.ParseText(SourceText.From(kv.Value, Encoding.UTF8), path: kv.Key));

        var references = referencePaths
            .Where(File.Exists)
            .Distinct()
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();

        // netstandard / mscorlib для таргета мода
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => Path.GetFileName(p) is "netstandard.dll" or "mscorlib.dll" or "System.Runtime.dll")
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p));
        references.AddRange(trustedAssemblies);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            trees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: debug ? OptimizationLevel.Debug : OptimizationLevel.Release,
                allowUnsafe: true));

        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        using var xmlStream = new MemoryStream();

        var emitOptions = new EmitOptions(
            debugInformationFormat: DebugInformationFormat.PortablePdb,
            documentationFilePath: assemblyName + ".xml");

        var emit = compilation.Emit(
            peStream: peStream,
            pdbStream: debug ? pdbStream : null,
            xmlDocumentationStream: xmlStream,
            options: emitOptions);

        foreach (var d in emit.Diagnostics)
        {
            var msg = $"{d.Id}: {d.GetMessage()} ({d.Location.GetLineSpan().StartLinePosition.Line + 1})";
            if (d.Severity == DiagnosticSeverity.Error) result.Errors.Add(msg);
            else if (d.Severity == DiagnosticSeverity.Warning) result.Warnings.Add(msg);
        }

        if (emit.Success)
        {
            result.Dll = peStream.ToArray();
            result.Pdb = pdbStream.ToArray();
            result.XmlDoc = Encoding.UTF8.GetString(xmlStream.ToArray());
        }
        return result;
    }
}
