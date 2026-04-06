// MauiAppCross/MainPage.xaml.cs
using CodeAnalyzer.Core;
using MauiAppCross.Models;

namespace MauiAppCross;

/// <summary>
/// Главная страница приложения анализатора кода.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly ConfigViewModel _config = new();
    private readonly AnalyzerEngine _analyzer = new();

    /// <summary>
    /// Инициализирует главную страницу.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();
        LoadConfig();
        MaxSizeSlider.ValueChanged += (s, e) => MaxSizeLabel.Text = $"Текущий: {e.NewValue:F0} МБ";
    }

    private void LoadConfig()
    {
        NameEntry.Text = _config.Name;
        OutputFolderEntry.Text = _config.OutputFolder;
        MaxSizeSlider.Value = _config.MaxFileSizeMB;
        ModePicker.SelectedIndex = (int)_config.ExtensionMode;
        PresetPicker.SelectedIndex = (int)_config.SelectedPreset;
        UpdateModeVisibility();
    }

    private void UpdateModeVisibility()
    {
        var mode = (ExtensionMode)ModePicker.SelectedIndex;
        PresetLabel.IsVisible = (mode == ExtensionMode.Preset);
        PresetPicker.IsVisible = (mode == ExtensionMode.Preset);
    }

    private async void OnAddSourceFolderClicked(object sender, EventArgs e)
    {
#if WINDOWS || MACCATALYST
        var folder = await FilePicker.PickFolderAsync();
        if (folder?.FullPath is string path)
        {
            _config.SourceFoldersList.Add(path);
            SourceFoldersList.ItemsSource = _config.SourceFoldersList;
        }
#endif
    }

    private void OnAddExcludeClicked(object sender, EventArgs e)
    {
        var text = NewExcludeEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            _config.ExcludeFoldersList.Add(text);
            ExcludeFoldersList.ItemsSource = _config.ExcludeFoldersList;
            NewExcludeEntry.Text = "";
        }
    }

    private void OnModeChanged(object sender, EventArgs e) => UpdateModeVisibility();

    private async void OnSelectOutputFolderClicked(object sender, EventArgs e)
    {
#if WINDOWS || MACCATALYST
        var folder = await FilePicker.PickFolderAsync();
        if (folder?.FullPath is string path)
            OutputFolderEntry.Text = path;
#endif
    }

    private async void OnStartAnalysisClicked(object sender, EventArgs e)
    {
        _config.Name = NameEntry.Text;
        _config.OutputFolder = OutputFolderEntry.Text;
        _config.MaxFileSizeMB = (int)MaxSizeSlider.Value;
        _config.ExtensionMode = (ExtensionMode)ModePicker.SelectedIndex;
        _config.SelectedPreset = (ExtensionPreset)PresetPicker.SelectedIndex;
        _config.SyncToBase();

        if (_config.SourceFolders.Count == 0)
        {
            await DisplayAlert("Ошибка", "Добавьте хотя бы одну папку для анализа", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(_config.OutputFolder))
        {
            await DisplayAlert("Ошибка", "Укажите выходную папку", "OK");
            return;
        }

        try
        {
            await _analyzer.AnalyzeAsync(_config, new Progress<string>(msg =>
                MainThread.BeginInvokeOnMainThread(() => DisplayAlert("Прогресс", msg, "OK"))));
            await DisplayAlert("Готово!", "Анализ завершён", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    private async void OnOpenResultsClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_config.OutputFolder) && Directory.Exists(_config.OutputFolder))
        {
#if WINDOWS
            Process.Start("explorer.exe", _config.OutputFolder);
#elif MACCATALYST
            Process.Start("open", _config.OutputFolder);
#endif
        }
        else
        {
            await DisplayAlert("Ошибка", "Папка не существует", "OK");
        }
    }
}