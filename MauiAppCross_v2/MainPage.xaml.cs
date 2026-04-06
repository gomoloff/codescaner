using CodeAnalyzer.Core;
using MauiAppCross.Models;
using System.Collections.ObjectModel;

#if WINDOWS
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif

namespace MauiAppCross_v2;

public partial class MainPage : ContentPage
{
    private readonly AnalyzerEngine _analyzer = new();
    private readonly ConfigViewModel _vm = new();

    public Command<string> RemoveSourceFolderCommand
    {
        get;
    }
    public Command<string> RemoveMaskCommand
    {
        get;
    }
    public Command<string> RemoveExtensionCommand
    {
        get;
    }
    public Command<string> RemoveExcludeCommand
    {
        get;
    }

    private bool _isNarrowLayoutApplied;

    public MainPage()
    {
        InitializeComponent();

        BindingContext = _vm;

        RemoveSourceFolderCommand = new Command<string>(path =>
        {
            if (!string.IsNullOrWhiteSpace(path))
                _vm.SourceFoldersList.Remove(path);
        });
        RemoveMaskCommand = new Command<string>(mask =>
        {
            if (!string.IsNullOrWhiteSpace(mask))
                _vm.FileNameMasksList.Remove(mask);
        });
        RemoveExtensionCommand = new Command<string>(ext =>
        {
            if (!string.IsNullOrWhiteSpace(ext))
                _vm.CustomExtensionsList.Remove(ext);
        });
        RemoveExcludeCommand = new Command<string>(folder =>
        {
            if (!string.IsNullOrWhiteSpace(folder))
                _vm.ExcludeFoldersList.Remove(folder);
        });

        OutputFolderEntry.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "CodeAnalysis");
        ExtensionModePicker.SelectedIndex = 0; // Preset
        PresetPicker.SelectedIndex = 0;        // AllSupported
        StatusLabel.Text = "Готово к запуску";

        // Адаптивность
        SizeChanged += (_, __) => ApplyResponsiveLayout();
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        // Ширина страницы
        var w = Width;
        if (double.IsNaN(w) || w <= 0) return;

        // Порог: 900px
        var narrow = w < 900;

        if (narrow == _isNarrowLayoutApplied) return;
        _isNarrowLayoutApplied = narrow;

        if (narrow)
        {
            // Одна колонка, две строки: правая панель уходит под левую
            RootGrid.ColumnDefinitions.Clear();
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            if (RootGrid.RowDefinitions.Count < 2)
                RootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetColumn(LeftPane, 0);
            Grid.SetRow(LeftPane, 0);

            Grid.SetColumn(RightPane, 0);
            Grid.SetRow(RightPane, 1);
        }
        else
        {
            // Две колонки, одна строка
            RootGrid.RowDefinitions.Clear();
            RootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            RootGrid.ColumnDefinitions.Clear();
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            Grid.SetRow(LeftPane, 0);
            Grid.SetColumn(LeftPane, 0);

            Grid.SetRow(RightPane, 0);
            Grid.SetColumn(RightPane, 1);
        }
    }

    private async void OnAddSourceFolderClicked(object sender, EventArgs e)
    {
#if WINDOWS
        var picker = new FolderPicker();

        var window = Application.Current?.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        var hwnd = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            var path = folder.Path;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                _vm.SourceFoldersList.Add(path);
        }
#else
        await DisplayAlert("Инфо",
            "Выбор каталога полностью поддержан на Windows. На других платформах добавьте путь вручную или выберите любой файл в каталоге.",
            "OK");
#endif
    }

    private void OnClearSourceFoldersClicked(object sender, EventArgs e)
    {
        _vm.SourceFoldersList.Clear();
    }

    private async void OnPickOutputFolderClicked(object sender, EventArgs e)
    {
#if WINDOWS
        var picker = new FolderPicker();

        var window = Application.Current?.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        var hwnd = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            var path = folder.Path;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                OutputFolderEntry.Text = path;
        }
#else
        await DisplayAlert("Инфо",
            "Выбор каталога выхода полностью поддержан на Windows. На других платформах укажите путь вручную.",
            "OK");
#endif
    }

    private void OnExtensionModeChanged(object sender, EventArgs e)
    {
        // Пресет актуален только для режима Preset
        PresetPicker.IsEnabled = ExtensionModePicker.SelectedIndex == 0;
    }

    private void OnAddMaskClicked(object sender, EventArgs e)
    {
        var mask = MaskEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(mask))
        {
            _vm.FileNameMasksList.Add(mask);
            MaskEntry.Text = "";
        }
    }

    private void OnAddExtensionClicked(object sender, EventArgs e)
    {
        var ext = ExtensionEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(ext))
        {
            _vm.CustomExtensionsList.Add(ext);
            ExtensionEntry.Text = "";
        }
    }

    private void OnAddExcludeFolderClicked(object sender, EventArgs e)
    {
        var folder = ExcludeEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            _vm.ExcludeFoldersList.Add(folder);
            ExcludeEntry.Text = "";
        }
    }

    private async void OnStartAnalysisClicked(object sender, EventArgs e)
    {
        if (_vm.SourceFoldersList.Count == 0)
        {
            await DisplayAlert("Ошибка", "Добавьте хотя бы одну исходную папку.", "OK");
            return;
        }

        var outputFolder = OutputFolderEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            await DisplayAlert("Ошибка", "Укажите выходную папку.", "OK");
            return;
        }

        if (!Directory.Exists(outputFolder))
        {
            try { Directory.CreateDirectory(outputFolder); }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", $"Не удалось создать выходную папку: {ex.Message}", "OK");
                return;
            }
        }

        _vm.SyncToBase();
        _vm.OutputFolder = outputFolder;

        _vm.ExtensionMode = ExtensionModePicker.SelectedIndex switch
        {
            0 => ExtensionMode.Preset,
            1 => ExtensionMode.Custom,
            2 => ExtensionMode.AllFiles,
            _ => ExtensionMode.Preset
        };

        _vm.SelectedPreset = PresetPicker.SelectedIndex switch
        {
            0 => ExtensionPreset.AllSupported,
            1 => ExtensionPreset.WebProject,
            2 => ExtensionPreset.CSharpProject,
            3 => ExtensionPreset.PythonProject,
            4 => ExtensionPreset.CppProject,
            _ => ExtensionPreset.AllSupported
        };

        _vm.ExcludeBinaryFiles = ExcludeBinarySwitch.IsToggled;
        _vm.IncludeDirectoryStructure = IncludeTreeSwitch.IsToggled;
        _vm.UseAbsolutePathsInOutput = AbsolutePathsSwitch.IsToggled;
        _vm.SeparatorStyle = "ai-plain";

        if (int.TryParse(MaxSizeEntry.Text, out var mb) && mb > 0)
            _vm.MaxFileSizeMB = mb;

        StatusLabel.Text = "Запуск анализа...";
        StatusLabel.TextColor = Colors.Orange;
        LogEditor.Text = "";

        try
        {
            var progress = new Progress<string>(msg =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusLabel.Text = msg;
                    AppendLog(msg);
                });
            });

            await _analyzer.AnalyzeAsync(_vm, progress);

            await DisplayAlert("Готово", $"Файлы отчёта сохранены в:\n{_vm.OutputFolder}", "OK");
            StatusLabel.Text = "Анализ завершён";
            StatusLabel.TextColor = Colors.Green;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
            StatusLabel.Text = "Ошибка анализа";
            StatusLabel.TextColor = Colors.Red;
            AppendLog($"Ошибка: {ex}");
        }
    }

    private void AppendLog(string msg)
    {
        if (string.IsNullOrWhiteSpace(LogEditor.Text))
            LogEditor.Text = msg;
        else
            LogEditor.Text += Environment.NewLine + msg;
    }
}