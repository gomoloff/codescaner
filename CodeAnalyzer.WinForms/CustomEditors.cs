// CodeAnalyzer.WinForms/CustomEditors.cs
using System.ComponentModel;
using System.Drawing.Design;

namespace CodeAnalyzer.WinForms;

// Редактор списка папок с FolderBrowserDialog
public class FolderListEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        var list = value as List<string> ?? new List<string>();
        using var form = new StringListForm(list, "Выберите папки", true);
        if (form.ShowDialog() == DialogResult.OK)
        {
            return form.Values;
        }
        return value;
    }
}

// Редактор списка строк (для исключений, расширений)
public class StringListEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        var list = value as List<string> ?? new List<string>();
        using var form = new StringListForm(list, "Введите значения (по одному на строку)");
        if (form.ShowDialog() == DialogResult.OK)
        {
            return form.Values;
        }
        return value;
    }
}

// Редактор одной папки
public class FolderPickerEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "Выберите выходную папку";
        dialog.SelectedPath = value?.ToString() ?? "";
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            return dialog.SelectedPath;
        }
        return value;
    }
}

// Форма для редактирования списка строк
public partial class StringListForm : Form
{
    private ListBox listBox;
    private TextBox textBox;
    private bool isFolderMode;

    public List<string> Values
    {
        get; private set;
    }

    public StringListForm(List<string> initial, string title, bool folderMode = false)
    {
        isFolderMode = folderMode;
        Values = new List<string>(initial ?? new List<string>());
        InitializeComponent();
        Text = title;
        LoadValues();
    }

    private void InitializeComponent()
    {
        Size = new Size(400, 300);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        Controls.Add(panel);

        listBox = new ListBox { Dock = DockStyle.Top, Height = 150, BorderStyle = BorderStyle.FixedSingle };
        panel.Controls.Add(listBox);

        textBox = new TextBox { Dock = DockStyle.Top, Height = 25, Margin = new Padding(0, 5, 0, 5) };
        if (isFolderMode) textBox.ReadOnly = true;
        panel.Controls.Add(textBox);

        var btnAdd = new Button { Text = isFolderMode ? "Выбрать папку" : "Добавить", Dock = DockStyle.Top, Height = 30 };
        btnAdd.Click += (s, e) =>
        {
            if (isFolderMode)
            {
                using var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Values.Add(dialog.SelectedPath);
                    LoadValues();
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    Values.Add(textBox.Text.Trim());
                    textBox.Clear();
                    LoadValues();
                }
            }
        };
        panel.Controls.Add(btnAdd);

        var btnRemove = new Button { Text = "Удалить", Dock = DockStyle.Top, Height = 30 };
        btnRemove.Click += (s, e) =>
        {
            if (listBox.SelectedItem != null)
            {
                Values.RemoveAt(listBox.SelectedIndex);
                LoadValues();
            }
        };
        panel.Controls.Add(btnRemove);

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 30 };
        panel.Controls.Add(btnOk);
    }

    private void LoadValues()
    {
        listBox.Items.Clear();
        foreach (var item in Values) listBox.Items.Add(item);
    }
}