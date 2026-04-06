// MauiAppCross/Models/ConfigViewModel.cs
using CodeAnalyzer.Core;
using System.Collections.ObjectModel;

namespace MauiAppCross.Models;

public class ConfigViewModel : AnalyzerConfig
{
    public ObservableCollection<string> SourceFoldersList
    {
        get; set;
    }
    public ObservableCollection<string> ExcludeFoldersList
    {
        get; set;
    }
    public ObservableCollection<string> CustomExtensionsList
    {
        get; set;
    }

    public ConfigViewModel()
    {
        SourceFoldersList = new(SourceFolders);
        ExcludeFoldersList = new(ExcludeFolders);
        CustomExtensionsList = new(CustomExtensions);
    }

    public void SyncToBase()
    {
        SourceFolders = SourceFoldersList.ToList();
        ExcludeFolders = ExcludeFoldersList.ToList();
        CustomExtensions = CustomExtensionsList.ToList();
    }
}