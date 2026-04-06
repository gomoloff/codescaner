// CodeAnalyzer.Core/AnalyzerConfig.cs
namespace CodeAnalyzer.Core;

/// <summary>
/// Конфигурация параметров анализа исходного кода.
/// </summary>
public class AnalyzerConfig
{
    public System.Guid Id { get; set; } = System.Guid.NewGuid();
    public string Name { get; set; } = "Новая конфигурация";
    public List<string> SourceFolders { get; set; } = new();
    public List<string> ExcludeFolders
    {
        get; set;
    } = new()
    {
        "node_modules", ".git", "bin", "obj", "__pycache__", "vendor", ".idea", ".osp", ".vs"
    };

    public ExtensionMode ExtensionMode { get; set; } = ExtensionMode.Preset;
    public ExtensionPreset SelectedPreset { get; set; } = ExtensionPreset.AllSupported;

    // Новое: маски имён файлов (поддерживаются * и ?), например: "*.cs", "*Test*.cs", "*.service.*"
    public List<string> FileNameMasks { get; set; } = new();

    public List<string> CustomExtensions { get; set; } = new();

    public string OutputFolder { get; set; } = "";
    public int MaxFileSizeMB { get; set; } = 25;
    public bool ExcludeBinaryFiles { get; set; } = true;
    public bool IncludeDirectoryStructure { get; set; } = true;
    public string OutputEncoding { get; set; } = "UTF-8";

    // Расширяем допустимые значения: "markdown" (как было), "plain", "ai-plain"
    public string SeparatorStyle { get; set; } = "markdown";

    public bool IncludeLineNumbers { get; set; } = true;
    public bool IncludeFileMetadata { get; set; } = true;

    // Новое: использовать полные пути в выводе (для формата ai-plain по умолчанию = true)
    public bool UseAbsolutePathsInOutput { get; set; } = true;
}