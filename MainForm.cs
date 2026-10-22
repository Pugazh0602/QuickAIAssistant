using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QuickAIAssistant
{
    internal sealed class MainForm : Form
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0x4A21;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_NOREPEAT = 0x4000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly TextBox promptBox = new TextBox();
        private readonly TextBox resultBox = new TextBox();
        private readonly Label selectedLabel = new Label();
        private readonly Button runButton = new Button();
        private readonly Button insertButton = new Button();
        private readonly Button settingsButton = new Button();
        private readonly GroqClient groq = new GroqClient();
        private string capturedText = "";
        private string capturedImageDataUrl = "";
        private IntPtr targetWindow = IntPtr.Zero;
        private bool registered;

        public MainForm()
        {
            Text = "Quick AI";
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(590, 440);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(26, 28, 34);
            ForeColor = Color.White;

            var title = new Label { Text = "QUICK AI", Left = 16, Top = 12, Width = 250, Height = 28, Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.White };
            settingsButton.Text = "Settings"; settingsButton.SetBounds(490, 12, 82, 28);
            settingsButton.Click += (s, e) => { using (var f = new SettingsForm()) f.ShowDialog(this); };

            selectedLabel.SetBounds(16, 48, 555, 22);
            selectedLabel.ForeColor = Color.LightGray;
            selectedLabel.Text = "Selected text/image will appear here.";

            var promptLabel = new Label { Text = "What do you want Quick AI to do?", Left = 16, Top = 76, Width = 400, Height = 20 };
            promptBox.SetBounds(16, 99, 556, 55); promptBox.Multiline = true;
            promptBox.Text = "Explain this simply with an example.";
            promptBox.ScrollBars = ScrollBars.Vertical;

            runButton.Text = "Ask Groq"; runButton.SetBounds(16, 163, 100, 32);
            runButton.Click += async (s, e) => await RunRequestAsync();
            insertButton.Text = "Insert response"; insertButton.SetBounds(124, 163, 125, 32); insertButton.Enabled = false;
            insertButton.Click += (s, e) => InsertResponse();
            var closeButton = new Button { Text = "Close", Left = 257, Top = 163, Width = 75, Height = 32 };
            closeButton.Click += (s, e) => Hide();
            var resultLabel = new Label { Text = "Response", Left = 16, Top = 205, Width = 300, Height = 20 };
            resultBox.SetBounds(16, 228, 556, 194); resultBox.Multiline = true; resultBox.ReadOnly = true;
            resultBox.ScrollBars = ScrollBars.Both; resultBox.WordWrap = true;

            Controls.AddRange(new Control[] { title, settingsButton, selectedLabel, promptLabel, promptBox, runButton, insertButton, closeButton, resultLabel, resultBox });

            tray.Icon = SystemIcons.Application;
            tray.Text = "Quick AI — Ctrl+Alt+Space";
            tray.Visible = true;
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open (Ctrl+Alt+Space)", null, (s, e) => ShowPrompt());
            menu.Items.Add("Settings", null, (s, e) => { using (var f = new SettingsForm()) f.ShowDialog(); });
            menu.Items.Add("Exit", null, (s, e) => ExitApp());
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => ShowPrompt();

            Shown += (s, e) =>
            {
                Hide();
                registered = RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, Keys.Space);
                if (!registered)
                    tray.ShowBalloonTip(4000, "Quick AI", "Could not register Ctrl+Alt+Space. Another app may use it.", ToolTipIcon.Warning);
                if (string.IsNullOrWhiteSpace(AppSettings.GetApiKey()))
                    tray.ShowBalloonTip(3500, "Quick AI", "Open Settings and add your Groq API key.", ToolTipIcon.Info);
            };
            FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID) ShowPrompt();
            base.WndProc(ref m);
        }

        private void ShowPrompt()
        {
            // Capture selection before taking focus. Clipboard is restored after reading.
            targetWindow = GetForegroundWindow();
            CaptureClipboardContent();
            if (string.IsNullOrWhiteSpace(promptBox.Text)) promptBox.Text = "Explain this simply with an example.";
            selectedLabel.Text = !string.IsNullOrWhiteSpace(capturedText)
                ? "Captured text: " + TrimForLabel(capturedText)
                : (!string.IsNullOrWhiteSpace(capturedImageDataUrl) ? "Captured clipboard image." : "No clipboard text/image detected. Copy/select content first.");
            PositionNearCenter();
            Show(); WindowState = FormWindowState.Normal; TopMost = true; Activate();
            promptBox.Focus(); promptBox.SelectAll();
        }

        private static string TrimForLabel(string text)
        {
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length > 95 ? text.Substring(0, 95) + "…" : text;
        }

        private void PositionNearCenter()
        {
            Rectangle r = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(r.Left + (r.Width - Width) / 2, r.Top + (r.Height - Height) / 2);
        }

        private void CaptureClipboardContent()
        {
            capturedText = ""; capturedImageDataUrl = "";
            try
            {
                // This reads clipboard content; it does not automatically copy a highlighted selection.
                // Ctrl+C is sent to the previously active window before the clipboard is read.
                IDataObject oldData = null;
                try { oldData = Clipboard.GetDataObject(); } catch { }
                try
                {
                    if (targetWindow != IntPtr.Zero) SetForegroundWindow(targetWindow);
                    SendKeys.SendWait("^c");
                    System.Threading.Thread.Sleep(180);
                    if (Clipboard.ContainsText()) capturedText = Clipboard.GetText(TextDataFormat.UnicodeText);
                    else if (Clipboard.ContainsImage())
                    {
                        using (Image image = Clipboard.GetImage())
                        using (var ms = new MemoryStream())
                        {
                            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            capturedImageDataUrl = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                        }
                    }
                }
                finally
                {
                    // Avoid overwriting newer clipboard data if the clipboard changed; restore best-effort only when it did not.
                    // Clipboard restoration is intentionally omitted to avoid clobbering a user's copied content.
                }
            }
            catch { }
        }

        private async Task RunRequestAsync()
        {
            runButton.Enabled = false; insertButton.Enabled = false;
            resultBox.Text = "Thinking…";
            try
            {
                string answer = await groq.AskAsync(AppSettings.GetApiKey(), promptBox.Text, capturedText, capturedImageDataUrl);
                resultBox.Text = answer;
                insertButton.Enabled = !string.IsNullOrWhiteSpace(answer);
            }
            catch (Exception ex) { resultBox.Text = "Request failed:\r\n" + ex.Message; }
            finally { runButton.Enabled = true; }
        }

        private void InsertResponse()
        {
            if (string.IsNullOrWhiteSpace(resultBox.Text)) return;
            string answer = resultBox.Text;
            Hide();
            if (targetWindow != IntPtr.Zero) SetForegroundWindow(targetWindow);
            System.Threading.Thread.Sleep(180);
            try
            {
                Clipboard.SetText(answer);
                SendKeys.SendWait("^v");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not insert response: " + ex.Message, "Quick AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExitApp()
        {
            tray.Visible = false;
            if (registered) UnregisterHotKey(Handle, HOTKEY_ID);
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (registered) UnregisterHotKey(Handle, HOTKEY_ID);
                tray.Visible = false; tray.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
