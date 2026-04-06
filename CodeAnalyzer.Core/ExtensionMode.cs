namespace CodeAnalyzer.Core;

/// <summary>
/// Определяет режим выбора расширений файлов для анализа.
/// </summary>
public enum ExtensionMode
{
    /// <summary>
    /// Использовать одну из предопределённых групп расширений (веб, C#, Python и т.д.).
    /// </summary>
    Preset,

    /// <summary>
    /// Использовать пользовательский список расширений.
    /// </summary>
    Custom,

    /// <summary>
    /// Анализировать все файлы (кроме бинарных, если включено исключение).
    /// </summary>
    AllFiles
}