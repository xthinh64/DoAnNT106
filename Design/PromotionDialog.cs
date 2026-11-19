namespace OnlineChessGame;

internal sealed class PromotionDialog : Form
{
    public char? SelectedPiece { get; private set; }

    public PromotionDialog()
    {
        Text = "Phong cấp tốt";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(360, 150);
        BackColor = ThemeColors.Background;

        Controls.Add(new Label
        {
            Text = "Chọn quân muốn phong cấp",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = ThemeColors.TextPrimary,
            AutoSize = true,
            Location = new Point(26, 18)
        });

        (char Code, string Text)[] options =
        [
            ('q', "♛ Hậu"),
            ('r', "♜ Xe"),
            ('b', "♝ Tượng"),
            ('n', "♞ Mã")
        ];

        for (int i = 0; i < options.Length; i++)
        {
            var option = options[i];
            var button = new Button
            {
                Text = option.Text,
                Tag = option.Code,
                Font = new Font("Segoe UI Symbol", 11F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeColors.Surface,
                ForeColor = ThemeColors.TextPrimary,
                Size = new Size(76, 48),
                Location = new Point(18 + i * 85, 72),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = ThemeColors.Border;
            button.Click += (_, _) =>
            {
                SelectedPiece = (char)button.Tag;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(button);
        }
    }
}
