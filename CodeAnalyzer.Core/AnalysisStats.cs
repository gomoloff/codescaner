namespace CodeAnalyzer.Core;

/// <summary>
/// Содержит статистику по завершённому запуску анализа кода.
/// </summary>
public class AnalysisStats
{
    /// <summary>
    /// Количество обработанных текстовых файлов.
    /// </summary>
    public int FileCount
    {
        get; set;
    }

    /// <summary>
    /// Общий размер всех созданных частей отчёта в байтах.
    /// </summary>
    public long TotalSizeBytes
    {
        get; set;
    }

    /// <summary>
    /// Количество созданных частей отчёта (файлов).
    /// </summary>
    public int PartCount
    {
        get; set;
    }

    /// <summary>
    /// Количество пропущенных бинарных файлов.
    /// </summary>
    public int SkippedBinaryFiles
    {
        get; set;
    }

    /// <summary>
    /// Количество файлов, пропущенных из-за исключённых папок.
    /// </summary>
    public int SkippedExcludedFolders
    {
        get; set;
    }
}