namespace CodeAnalyzer.Core;

/// <summary>
/// Предопределённые наборы расширений для различных типов проектов.
/// Используется при <see cref="ExtensionMode.Preset"/>.
/// </summary>
public enum ExtensionPreset
{
    /// <summary>
    /// Все поддерживаемые расширения (универсальный набор).
    /// </summary>
    AllSupported,

    /// <summary>
    /// Расширения, характерные для веб-проектов (HTML, CSS, JS, JSON и др.).
    /// </summary>
    WebProject,

    /// <summary>
    /// Расширения, характерные для C#/.NET проектов.
    /// </summary>
    CSharpProject,

    /// <summary>
    /// Расширения, характерные для Python-проектов.
    /// </summary>
    PythonProject,

    /// <summary>
    /// Расширения, характерные для C/C++ проектов.
    /// </summary>
    CppProject
}