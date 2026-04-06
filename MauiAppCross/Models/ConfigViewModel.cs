// MauiAppCross/Models/ConfigViewModel.cs
using CodeAnalyzer.Core;
using System.Collections.ObjectModel;

namespace MauiAppCross.Models;

/// <summary>
/// Обёртка над AnalyzerConfig для удобной привязки данных в MAUI.
/// </summary>
public class ConfigViewModel : AnalyzerConfig
{
    /// <summary>
    /// Список исходных папок (для CollectionView).
    /// </summary>
    public ObservableCollection<string> SourceFoldersList { get; set; } = new();

    /// <summary>
    /// Список исключаемых папок.
    /// </summary>
    public ObservableCollection<string> ExcludeFoldersList { get; set; } = new();

    /// <summary>
    /// Список пользовательских расширений.
    /// </summary>
    public ObservableCollection<string> CustomExtensionsList { get; set; } = new();

    /// <summary>
    /// Конструктор по умолчанию.
    /// </summary>
    public ConfigViewModel()
    {
        SourceFoldersList = new(SourceFolders);
        ExcludeFoldersList = new(ExcludeFolders);
        CustomExtensionsList = new(CustomExtensions);
    }

    /// <summary>
    /// Синхронизирует списки обратно в базовый класс.
    /// </summary>
    public void SyncToBase()
    {
        SourceFolders = SourceFoldersList.ToList();
        ExcludeFolders = ExcludeFoldersList.ToList();
        CustomExtensions = CustomExtensionsList.ToList();
    }
}