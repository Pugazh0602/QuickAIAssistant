using System;
using System.Drawing;
using System.Windows.Forms;

namespace QuickAIAssistant
{
    internal sealed class SettingsForm : Form
    {
        private readonly TextBox keyBox = new TextBox();
        private readonly CheckBox startupBox = new CheckBox();

        public SettingsForm()
        {
            Text = "Quick AI Settings";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(480, 190);
            Font = new Font("Segoe UI", 9F);

            var keyLabel = new Label { Text = "Groq API key", Left = 18, Top = 18, AutoSize = true };
            keyBox.SetBounds(18, 42, 440, 27);
            keyBox.UseSystemPasswordChar = true;
            keyBox.Text = AppSettings.GetApiKey();

            startupBox.Text = "Start Quick AI when I sign in to Windows";
            startupBox.SetBounds(18, 82, 400, 24);
            startupBox.Checked = AppSettings.StartupEnabled;

            var save = new Button { Text = "Save", Left = 286, Top = 132, Width = 80, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Left = 376, Top = 132, Width = 82, DialogResult = DialogResult.Cancel };
            save.Click += (s, e) =>
            {
                try
                {
                    AppSettings.SaveApiKey(keyBox.Text.Trim());
                    AppSettings.SetStartup(startupBox.Checked);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not save settings: " + ex.Message, "Quick AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                }
            };

            Controls.AddRange(new Control[] { keyLabel, keyBox, startupBox, save, cancel });
            AcceptButton = save; CancelButton = cancel;
        }
    }
}
