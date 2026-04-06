// CodeAnalyzer.WinForms/ProgressForm.cs
namespace CodeAnalyzer.WinForms;

public partial class ProgressForm : Form
{
    private TextBox logBox;

    public ProgressForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Прогресс анализа";
        this.Size = new System.Drawing.Size(600, 400);
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimizeBox = false;
        this.MaximizeBox = false;

        logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new System.Drawing.Font("Consolas", 9),
            BackColor = System.Drawing.Color.Black,
            ForeColor = System.Drawing.Color.LimeGreen
        };
        this.Controls.Add(logBox);
    }

    public void AddLog(string message)
    {
        if (logBox.InvokeRequired)
        {
            logBox.Invoke(new Action<string>(AddLog), message);
        }
        else
        {
            logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            logBox.SelectionStart = logBox.Text.Length;
            logBox.ScrollToCaret();
        }
    }
}