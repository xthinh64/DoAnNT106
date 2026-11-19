using OnlineChessGame.Chess;

namespace OnlineChessGame;

internal sealed class ChessDifficultyDialog : Form
{
    public ChessDifficulty? SelectedDifficulty { get; private set; }

    public ChessDifficultyDialog()
    {
        Text = "Chọn độ khó Stockfish";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 390);
        BackColor = ThemeColors.Background;

        var title = new Label
        {
            Text = "Chọn trình độ máy",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = ThemeColors.TextPrimary,
            AutoSize = true,
            Location = new Point(24, 20)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "Bạn có thể đổi mức độ bằng cách quay lại sảnh và mở ván mới.",
            Font = new Font("Segoe UI", 10F),
            ForeColor = ThemeColors.TextSecondary,
            AutoSize = true,
            Location = new Point(26, 58)
        };
        Controls.Add(subtitle);

        int y = 96;
        foreach (ChessDifficulty difficulty in ChessDifficulty.All)
        {
            var button = new Button
            {
                Text = $"{difficulty.DisplayName}  •  Elo khoảng {difficulty.DisplayElo}\r\n{difficulty.Description}",
                Tag = difficulty,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeColors.Surface,
                ForeColor = ThemeColors.TextPrimary,
                Size = new Size(470, 58),
                Location = new Point(24, y),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = ThemeColors.Border;
            button.Click += (_, _) =>
            {
                SelectedDifficulty = (ChessDifficulty)button.Tag;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(button);
            y += 66;
        }
    }
}
