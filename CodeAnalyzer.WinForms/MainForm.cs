using CodeAnalyzer.Core;
using MaterialSkin;
using MaterialSkin.Controls;

namespace CodeAnalyzer.WinForms;

public partial class MainForm : MaterialSkin.Controls.MaterialForm
{
    private readonly List<AnalyzerConfig> configs = new();
    private readonly AnalyzerEngine analyzer = new();
    private ListBox configListBox;
    private Panel detailPanel;
    private AnalyzerConfig currentConfig;

    public MainForm()
    {
        var materialSkinManager = MaterialSkinManager.Instance;
        materialSkinManager.AddFormToManage(this);
        materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;
        materialSkinManager.ColorScheme = new ColorScheme(
            Primary.Blue700, Primary.Blue900,
            Primary.Blue500, Accent.LightBlue200,
            TextShade.WHITE);

        try
        {
            var iconPath = Path.Combine(Application.StartupPath, "appicon.ico");
            if (File.Exists(iconPath))
                this.Icon = new Icon(iconPath);
        }
        catch { }

        InitializeComponent();
        LoadConfigs();
    }

    private void InitializeComponent()
    {
        this.Text = "Анализатор кода";
        this.Size = new Size(1000, 650);
        this.MinimumSize = new Size(800, 500);
        this.StartPosition = FormStartPosition.CenterScreen;

        // Верхняя панель
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(20, 0, 20, 0)
        };
        this.Controls.Add(topPanel);

        var lblTitle = new MaterialLabel
        {
            Text = "🔍 Анализатор кода",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Location = new Point(0, 15),
            AutoSize = true
        };
        topPanel.Controls.Add(lblTitle);

        var btnNew = new MaterialButton
        {
            Text = "➕ Новая",
            Location = new Point(700, 12),
            Size = new Size(100, 36),
            UseAccentColor = true
        };
        btnNew.Click += BtnNew_Click;
        topPanel.Controls.Add(btnNew);

        var btnStart = new MaterialButton
        {
            Text = "▶ Запуск",
            Location = new Point(810, 12),
            Size = new Size(100, 36),
            UseAccentColor = true
        };
        btnStart.Click += BtnStart_Click;
        topPanel.Controls.Add(btnStart);

        var btnOpen = new MaterialButton
        {
            Text = "📁 Открыть",
            Location = new Point(920, 12),
            Size = new Size(100, 36),
            UseAccentColor = true
        };
        btnOpen.Click += BtnOpen_Click;
        topPanel.Controls.Add(btnOpen);

        // Основной контейнер
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(20)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        this.Controls.Add(table);

        // Левая панель — список конфигураций
        var leftPanel = new Panel { Dock = DockStyle.Fill };
        table.Controls.Add(leftPanel, 0, 0);

        var lblConfigs = new MaterialLabel
        {
            Text = "КОНФИГУРАЦИИ",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Dock = DockStyle.Top
        };
        leftPanel.Controls.Add(lblConfigs);

        configListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            DisplayMember = "Name",
            SelectionMode = SelectionMode.One,
            Font = new Font("Segoe UI", 10),
            IntegralHeight = false
        };
        configListBox.DrawMode = DrawMode.OwnerDrawFixed;
        configListBox.MeasureItem += (s, e) => e.ItemHeight = 28;
        configListBox.DrawItem += ConfigListBox_DrawItem;
        configListBox.SelectedIndexChanged += (s, e) => ShowSelectedConfig();
        leftPanel.Controls.Add(configListBox);

        var btnDelete = new MaterialButton
        {
            Text = "🗑 Удалить",
            Dock = DockStyle.Bottom,
            Height = 40,
            UseAccentColor = false
        };
        btnDelete.Click += (s, e) =>
        {
            if (configListBox.SelectedItem is AnalyzerConfig cfg &&
                MessageBox.Show("Удалить?", "Подтверждение", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                configs.Remove(cfg);
                RefreshConfigList();
                SaveConfigs();
                ShowSelectedConfig();
            }
        };
        leftPanel.Controls.Add(btnDelete);

        // Правая панель — детали
        detailPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
        table.Controls.Add(detailPanel, 1, 0);
    }

    private void ConfigListBox_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var cfg = (AnalyzerConfig)configListBox.Items[e.Index];
        var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        e.DrawBackground();
        using var brush = new SolidBrush(isSelected ? MaterialSkinManager.Instance.ColorScheme.PrimaryColor : Color.White);
        e.Graphics.FillRectangle(brush, e.Bounds);

        using var textBrush = new SolidBrush(isSelected ? Color.White : Color.FromArgb(50, 50, 50));
        var text = cfg.Name;
        var x = e.Bounds.Left + 12;
        var y = e.Bounds.Top + (e.Bounds.Height - 16) / 2;
        e.Graphics.DrawString(text, new Font("Segoe UI", 10), textBrush, x, y);
    }

    private void ShowSelectedConfig()
    {
        currentConfig = configListBox.SelectedItem as AnalyzerConfig;
        detailPanel.Controls.Clear();

        if (currentConfig == null)
        {
            var lbl = new MaterialLabel
            {
                Text = "Выберите конфигурацию\nили создайте новую",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(150, 150),
                Size = new Size(400, 100),
                TextAlign = ContentAlignment.MiddleCenter
            };
            detailPanel.Controls.Add(lbl);
            return;
        }

        int y = 10;
        const int labelWidth = 200;
        const int controlWidth = 350;
        int panelWidth = detailPanel.ClientSize.Width - 40;

        // Название
        AddLabel("Название:", 20, y);
        var txtName = AddTextBox(currentConfig.Name, 20 + labelWidth + 10, y, controlWidth);
        txtName.TextChanged += (s, e) => { currentConfig.Name = txtName.Text; SaveConfigs(); RefreshConfigList(); };
        y += 40;

        // Папки
        AddLabel("Папки для анализа:", 20, y);
        y += 30;
        var folderPanel = CreateFolderList(currentConfig.SourceFolders, true);
        folderPanel.Location = new Point(20 + labelWidth + 10, y - 30);
        folderPanel.Size = new Size(Math.Min(controlWidth + 100, panelWidth - labelWidth - 40), 100);
        detailPanel.Controls.Add(folderPanel);
        y += 110;

        var btnAddFolder = AddButton("+ Добавить", 20 + labelWidth + 10, y, 120);
        btnAddFolder.Click += (s, e) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Выберите папку" };
            if (dialog.ShowDialog() == DialogResult.OK && !currentConfig.SourceFolders.Contains(dialog.SelectedPath))
            {
                currentConfig.SourceFolders.Add(dialog.SelectedPath);
                UpdateFolderList(folderPanel, currentConfig.SourceFolders, true);
                SaveConfigs();
            }
        };

        // Исключения
        AddLabel("Исключаемые папки:", 20, y += 45);
        y += 30;
        var excludePanel = CreateFolderList(currentConfig.ExcludeFolders, false);
        excludePanel.Location = new Point(20 + labelWidth + 10, y - 30);
        excludePanel.Size = new Size(Math.Min(controlWidth + 100, panelWidth - labelWidth - 40), 100);
        detailPanel.Controls.Add(excludePanel);
        y += 110;

        var btnAddExclude = AddButton("+ Добавить", 20 + labelWidth + 10, y, 120);
        btnAddExclude.Click += (s, e) =>
        {
            var form = new InputBoxForm("Исключение", "Введите имя или маску (например: bin, *test*)");
            if (form.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(form.Input))
            {
                var input = form.Input.Trim();
                if (!currentConfig.ExcludeFolders.Contains(input))
                {
                    currentConfig.ExcludeFolders.Add(input);
                    UpdateFolderList(excludePanel, currentConfig.ExcludeFolders, false);
                    SaveConfigs();
                }
            }
        };

        // Режим расширений
        AddLabel("Режим расширений:", 20, y += 45);
        var cmbMode = new MaterialComboBox
        {
            Location = new Point(20 + labelWidth + 10, y),
            Size = new Size(controlWidth, 40),
            Items = { "Предустановки", "Свой список", "Все файлы" },
            SelectedIndex = (int)currentConfig.ExtensionMode
        };
        cmbMode.SelectedIndexChanged += (s, e) => { currentConfig.ExtensionMode = (ExtensionMode)cmbMode.SelectedIndex; SaveConfigs(); ShowSelectedConfig(); };
        detailPanel.Controls.Add(cmbMode);

        if (currentConfig.ExtensionMode == ExtensionMode.Preset)
        {
            AddLabel("Предустановка:", 20, y += 50);
            var cmbPreset = new MaterialComboBox
            {
                Location = new Point(20 + labelWidth + 10, y),
                Size = new Size(controlWidth, 40),
                Items = { "Все поддерживаемые", "Веб-проект", "C# проект", "Python проект", "C++ проект" },
                SelectedIndex = (int)currentConfig.SelectedPreset
            };
            cmbPreset.SelectedIndexChanged += (s, e) => { currentConfig.SelectedPreset = (ExtensionPreset)cmbPreset.SelectedIndex; SaveConfigs(); };
            detailPanel.Controls.Add(cmbPreset);
        }
        else if (currentConfig.ExtensionMode == ExtensionMode.Custom)
        {
            AddLabel("Свои расширения:", 20, y += 50);
            var txtExt = AddTextBox(string.Join(",", currentConfig.CustomExtensions), 20 + labelWidth + 10, y, controlWidth);
            txtExt.Leave += (s, e) =>
            {
                currentConfig.CustomExtensions = txtExt.Text
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(e => e.StartsWith(".") ? e : "." + e)
                    .ToList();
                SaveConfigs();
            };
        }

        // Выходная папка
        AddLabel("Выходная папка:", 20, y += 50);
        var txtOutput = AddTextBox(currentConfig.OutputFolder, 20 + labelWidth + 10, y, controlWidth - 50);
        txtOutput.TextChanged += (s, e) => { currentConfig.OutputFolder = txtOutput.Text; SaveConfigs(); };
        var btnBrowse = AddButton("📁", 20 + labelWidth + 10 + controlWidth - 50 + 10, y, 40);
        btnBrowse.Click += (s, e) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Выберите папку" };
            if (dialog.ShowDialog() == DialogResult.OK)
                txtOutput.Text = dialog.SelectedPath;
        };

        // Макс. размер
        AddLabel("Макс. размер части (МБ):", 20, y += 50);
        var numSize = new NumericUpDown
        {
            Value = currentConfig.MaxFileSizeMB,
            Location = new Point(20 + labelWidth + 10, y),
            Size = new Size(120, 30),
            Minimum = 1,
            Maximum = 1000
        };
        numSize.ValueChanged += (s, e) => { currentConfig.MaxFileSizeMB = (int)numSize.Value; SaveConfigs(); };
        detailPanel.Controls.Add(numSize);
    }

    private MaterialLabel AddLabel(string text, int x, int y)
    {
        var lbl = new MaterialLabel { Text = text, Location = new Point(x, y), Size = new Size(200, 22), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        detailPanel.Controls.Add(lbl);
        return lbl;
    }

    private MaterialTextBox AddTextBox(string text, int x, int y, int width)
    {
        var txt = new MaterialTextBox { Text = text, Location = new Point(x, y), Size = new Size(width, 40), Font = new Font("Segoe UI", 10) };
        detailPanel.Controls.Add(txt);
        return txt;
    }

    private MaterialButton AddButton(string text, int x, int y, int width)
    {
        var btn = new MaterialButton { Text = text, Location = new Point(x, y), Size = new Size(width, 36), UseAccentColor = true };
        detailPanel.Controls.Add(btn);
        return btn;
    }

    private Panel CreateFolderList(List<string> folders, bool isSource)
    {
        var panel = new Panel { AutoScroll = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
        foreach (var folder in folders)
        {
            var item = CreateFolderItem(folder, panel, isSource);
            panel.Controls.Add(item);
        }
        return panel;
    }

    private Panel CreateFolderItem(string folder, Panel container, bool isSource)
    {
        int y = container.Controls.Count * 35;
        var panel = new Panel { Location = new Point(5, y), Size = new Size(container.ClientSize.Width - 25, 30), BackColor = Color.FromArgb(248, 248, 248) };
        var lbl = new Label { Text = folder, Location = new Point(10, 5), Size = new Size(panel.ClientSize.Width - 60, 20), Font = new Font("Segoe UI", 9) };
        var btnDel = new Button
        {
            Text = "×",
            Location = new Point(panel.ClientSize.Width - 40, 3),
            Size = new Size(24, 24),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Arial", 12, FontStyle.Bold),
            ForeColor = Color.Red
        };
        btnDel.Click += (s, e) =>
        {
            if (isSource) currentConfig.SourceFolders.Remove(folder);
            else currentConfig.ExcludeFolders.Remove(folder);
            SaveConfigs();
            ShowSelectedConfig();
        };
        panel.Controls.Add(lbl);
        panel.Controls.Add(btnDel);
        return panel;
    }

    private void UpdateFolderList(Panel panel, List<string> folders, bool isSource)
    {
        panel.Controls.Clear();
        foreach (var folder in folders)
        {
            var item = CreateFolderItem(folder, panel, isSource);
            panel.Controls.Add(item);
        }
    }

    private void BtnNew_Click(object sender, EventArgs e)
    {
        var config = new AnalyzerConfig
        {
            Name = $"Конфигурация {configs.Count + 1}",
            OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CodeAnalysis"),
            MaxFileSizeMB = 50
        };
        configs.Add(config);
        RefreshConfigList();
        configListBox.SelectedItem = config;
        SaveConfigs();
    }

    private async void BtnStart_Click(object sender, EventArgs e)
    {
        if (currentConfig?.SourceFolders.Count == 0)
        {
            MessageBox.Show("Добавьте хотя бы одну папку для анализа", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(currentConfig?.OutputFolder))
        {
            MessageBox.Show("Укажите выходную папку", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var progress = new ProgressForm();
        progress.Show();

        try
        {
            await analyzer.AnalyzeAsync(currentConfig, new Progress<string>(msg => progress.AddLog(msg)));
            progress.Close();
            MessageBox.Show("Анализ завершён!", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            progress.Close();
            MessageBox.Show($"Ошибка:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnOpen_Click(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(currentConfig?.OutputFolder))
        {
            try
            {
                if (!Directory.Exists(currentConfig.OutputFolder))
                    Directory.CreateDirectory(currentConfig.OutputFolder);
                System.Diagnostics.Process.Start("explorer.exe", currentConfig.OutputFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть папку:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            MessageBox.Show("Сначала укажите выходную папку", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void RefreshConfigList()
    {
        configListBox.DataSource = null;
        configListBox.DataSource = configs;
        configListBox.DisplayMember = "Name";
    }

    private void LoadConfigs()
    {
        try
        {
            var file = Path.Combine(Application.StartupPath, "configs.json");
            if (File.Exists(file))
            {
                var json = File.ReadAllText(file);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                configs.AddRange(System.Text.Json.JsonSerializer.Deserialize<List<AnalyzerConfig>>(json, options) ?? new List<AnalyzerConfig>());
            }
        }
        catch { }

        if (configs.Count == 0)
            configs.Add(new AnalyzerConfig
            {
                Name = "Основная конфигурация",
                OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CodeAnalysis")
            });

        RefreshConfigList();
        if (configListBox.Items.Count > 0)
            configListBox.SelectedIndex = 0;
    }

    private void SaveConfigs()
    {
        try
        {
            var file = Path.Combine(Application.StartupPath, "configs.json");
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(configs, options);
            File.WriteAllText(file, json);
        }
        catch { }
    }
}

// Вложенный класс вместо отдельного файла
public class InputBoxForm : Form
{
    public string Input { get; private set; } = "";
    private TextBox textBox;

    public InputBoxForm(string title, string prompt)
    {
        this.Text = title;
        this.Size = new Size(400, 180);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;

        var label = new Label
        {
            Text = prompt,
            Location = new Point(20, 20),
            Size = new Size(350, 40),
            Font = new Font("Segoe UI", 9)
        };
        this.Controls.Add(label);

        textBox = new TextBox
        {
            Location = new Point(20, 70),
            Size = new Size(350, 25),
            Font = new Font("Segoe UI", 9)
        };
        this.Controls.Add(textBox);

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(200, 110),
            Size = new Size(80, 30)
        };
        okButton.Click += (_, _) => Input = textBox.Text;
        this.Controls.Add(okButton);

        var cancelButton = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            Location = new Point(290, 110),
            Size = new Size(80, 30)
        };
        this.Controls.Add(cancelButton);

        this.AcceptButton = okButton;
        this.CancelButton = cancelButton;
    }
}