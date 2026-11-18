namespace OnlineChessGame
{
    public enum AppTheme
    {
        Light,
        DarkGreen
    }

    /// <summary>
    /// Bảng màu và tài nguyên giao diện chung cho toàn bộ ứng dụng.
    /// Hỗ trợ 2 Theme chuẩn:
    /// - Light: Nền trắng xám hiện đại, điểm nhấn Xanh dương hoàng gia.
    /// - DarkGreen: Nền đen sâu (Dark Slate), điểm nhấn Xanh ngọc lục bảo Neon.
    /// </summary>
    public static class ThemeColors
    {
        public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;
        public static event Action? ThemeChanged;

        // === Nền và Bề mặt ===
        public static Color Background { get; private set; }
        public static Color Surface { get; private set; }
        public static Color SurfaceAlt { get; private set; }
        public static Color HeaderBg { get; private set; }

        // === Màu chữ ===
        public static Color TextPrimary { get; private set; }
        public static Color TextSecondary { get; private set; }
        public static Color TextOnPrimary { get; private set; }

        // === Màu thương hiệu & Nút bấm ===
        public static Color Primary { get; private set; }
        public static Color PrimaryHover { get; private set; }
        public static Color PrimaryLight { get; private set; }
        public static Color Accent { get; private set; }

        // === Trạng thái ===
        public static Color Success { get; private set; }
        public static Color Warning { get; private set; }
        public static Color Danger { get; private set; }
        public static Color DangerHover { get; private set; }

        // === Viền & Nhập liệu ===
        public static Color Border { get; private set; }
        public static Color BorderFocus { get; private set; }
        public static Color InputBg { get; private set; }
        public static Color InputFg { get; private set; }

        // === Bàn Cờ Vua (8x8) ===
        public static Color ChessSquareLight { get; private set; }
        public static Color ChessSquareDark { get; private set; }
        public static Color ChessHighlight { get; private set; }
        public static Color ChessTargetDot { get; private set; }
        public static Color ChessCoordText { get; private set; }
        public static Color ChessPieceWhiteFill { get; private set; }
        public static Color ChessPieceWhiteStroke { get; private set; }
        public static Color ChessPieceBlackFill { get; private set; }
        public static Color ChessPieceBlackStroke { get; private set; }

        // === Bàn Cờ Caro (15x15) ===
        public static Color CaroBoardBg { get; private set; }
        public static Color CaroGrid { get; private set; }
        public static Color CaroHoshi { get; private set; }
        public static Color CaroCoordText { get; private set; }
        public static Color CaroPieceX { get; private set; }
        public static Color CaroPieceO { get; private set; }

        // === Font chữ chuẩn Segoe UI ===
        public static readonly Font FontHero = new("Segoe UI", 22F, FontStyle.Bold);
        public static readonly Font FontTitle = new("Segoe UI", 18F, FontStyle.Bold);
        public static readonly Font FontSubtitle = new("Segoe UI", 11.5F, FontStyle.Regular);
        public static readonly Font FontHeading = new("Segoe UI Semibold", 13F, FontStyle.Bold);
        public static readonly Font FontSubheading = new("Segoe UI Semibold", 11F, FontStyle.Bold);
        public static readonly Font FontBody = new("Segoe UI", 10F, FontStyle.Regular);
        public static readonly Font FontSmall = new("Segoe UI", 9F, FontStyle.Regular);
        public static readonly Font FontButton = new("Segoe UI Semibold", 11F, FontStyle.Bold);
        public static readonly Font FontChat = new("Segoe UI", 9.5F, FontStyle.Regular);
        public static readonly Font FontBoardCoord = new("Segoe UI Semibold", 9F, FontStyle.Bold);
        public static readonly Font FontChessPiece = new("Segoe UI Symbol", 32F, FontStyle.Regular);

        static ThemeColors()
        {
            ApplyTheme(AppTheme.Light);
        }

        public static void ToggleTheme()
        {
            ApplyTheme(CurrentTheme == AppTheme.Light ? AppTheme.DarkGreen : AppTheme.Light);
        }

        public static void ApplyTheme(AppTheme theme)
        {
            CurrentTheme = theme;
            if (theme == AppTheme.Light)
            {
                // ============================================
                // THEME 1: Light (Trắng tinh tế & Xanh dương)
                // ============================================
                Background            = Color.FromArgb(243, 245, 249);
                Surface               = Color.White;
                SurfaceAlt            = Color.FromArgb(248, 250, 252);
                HeaderBg              = Color.FromArgb(37, 99, 235);      // Blue 600

                TextPrimary           = Color.FromArgb(15, 23, 42);       // Slate 900
                TextSecondary         = Color.FromArgb(100, 116, 139);    // Slate 500
                TextOnPrimary         = Color.White;

                Primary               = Color.FromArgb(37, 99, 235);      // Blue 600
                PrimaryHover          = Color.FromArgb(29, 78, 216);      // Blue 700
                PrimaryLight          = Color.FromArgb(224, 238, 255);    // Blue 100
                Accent                = Color.FromArgb(2, 132, 199);       // Sky 600

                Success               = Color.FromArgb(22, 163, 74);      // Green 600
                Warning               = Color.FromArgb(217, 119, 6);      // Amber 600
                Danger                = Color.FromArgb(220, 38, 38);      // Red 600
                DangerHover           = Color.FromArgb(185, 28, 28);

                Border                = Color.FromArgb(226, 232, 240);    // Slate 200
                BorderFocus           = Color.FromArgb(37, 99, 235);

                InputBg               = Color.White;
                InputFg               = Color.FromArgb(15, 23, 42);

                // Cờ Vua
                ChessSquareLight      = Color.FromArgb(240, 217, 181);    // Gỗ sồi sáng (Lichess Wood)
                ChessSquareDark       = Color.FromArgb(181, 136, 99);     // Gỗ óc chó trầm
                ChessHighlight        = Color.FromArgb(247, 208, 112);    // Highlight vàng nghệ
                ChessTargetDot        = Color.FromArgb(100, 37, 99, 235); // Chấm nước đi xanh nhạt
                ChessCoordText        = Color.FromArgb(100, 116, 139);
                ChessPieceWhiteFill   = Color.FromArgb(255, 255, 255);
                ChessPieceWhiteStroke = Color.FromArgb(30, 41, 59);
                ChessPieceBlackFill   = Color.FromArgb(30, 41, 59);
                ChessPieceBlackStroke = Color.FromArgb(241, 245, 249);

                // Cờ Caro
                CaroBoardBg           = Color.FromArgb(253, 251, 247);    // Ngà giấy ấm
                CaroGrid              = Color.FromArgb(203, 213, 225);    // Slate 300
                CaroHoshi             = Color.FromArgb(100, 116, 139);
                CaroCoordText         = Color.FromArgb(148, 163, 184);
                CaroPieceX            = Color.FromArgb(37, 99, 235);      // Xanh dương
                CaroPieceO            = Color.FromArgb(220, 38, 38);      // Đỏ ruby
            }
            else
            {
                // ============================================
                // THEME 2: Dark & Green (Đen & Xanh ngọc lục bảo)
                // ============================================
                Background            = Color.FromArgb(11, 15, 25);       // Deep Dark
                Surface               = Color.FromArgb(19, 26, 42);       // Dark Slate Card
                SurfaceAlt            = Color.FromArgb(28, 38, 59);       // Card phụ
                HeaderBg              = Color.FromArgb(15, 23, 38);       // Header tối

                TextPrimary           = Color.FromArgb(248, 250, 252);    // Trắng tinh
                TextSecondary         = Color.FromArgb(148, 163, 184);    // Slate 400
                TextOnPrimary         = Color.FromArgb(11, 15, 25);       // Chữ đen trên nền nút sáng

                Primary               = Color.FromArgb(16, 185, 129);     // Emerald 500 neon
                PrimaryHover          = Color.FromArgb(5, 150, 105);      // Emerald 600
                PrimaryLight          = Color.FromArgb(6, 78, 59);        // Emerald 900
                Accent                = Color.FromArgb(52, 211, 153);     // Emerald 400

                Success               = Color.FromArgb(16, 185, 129);
                Warning               = Color.FromArgb(245, 158, 11);
                Danger                = Color.FromArgb(239, 68, 68);
                DangerHover           = Color.FromArgb(220, 38, 38);

                Border                = Color.FromArgb(39, 51, 75);       // Viền tối
                BorderFocus           = Color.FromArgb(16, 185, 129);

                InputBg               = Color.FromArgb(15, 22, 35);
                InputFg               = Color.FromArgb(248, 250, 252);

                // Cờ Vua
                ChessSquareLight      = Color.FromArgb(43, 62, 50);       // Xanh lục sẫm thanh lịch
                ChessSquareDark       = Color.FromArgb(22, 34, 27);       // Xanh rừng đêm
                ChessHighlight        = Color.FromArgb(16, 185, 129);     // Highlight xanh neon
                ChessTargetDot        = Color.FromArgb(120, 52, 211, 153);// Chấm nước đi ngọc lục bảo
                ChessCoordText        = Color.FromArgb(100, 116, 139);
                ChessPieceWhiteFill   = Color.FromArgb(240, 253, 244);    // Trắng ánh ngọc
                ChessPieceWhiteStroke = Color.FromArgb(15, 23, 42);
                ChessPieceBlackFill   = Color.FromArgb(15, 23, 35);       // Đen đêm
                ChessPieceBlackStroke = Color.FromArgb(52, 211, 153);     // Viền neon xanh lá

                // Cờ Caro
                CaroBoardBg           = Color.FromArgb(15, 22, 35);       // Bàn cờ đen dịu
                CaroGrid              = Color.FromArgb(39, 53, 78);       // Lưới xám xanh
                CaroHoshi             = Color.FromArgb(16, 185, 129);     // Điểm sao ngọc lục bảo
                CaroCoordText         = Color.FromArgb(100, 116, 139);
                CaroPieceX            = Color.FromArgb(16, 185, 129);     // Xanh ngọc neon
                CaroPieceO            = Color.FromArgb(244, 63, 94);      // Hồng đỏ neon
            }

            ThemeChanged?.Invoke();
        }
    }

    /// <summary>
    /// Helper tiện ích vẽ giao diện WinForms hiện đại.
    /// </summary>
    public static class UIHelper
    {
        public static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Button CreateRoundedButton(string text, Color back, Color fore, int w, int h, int radius = 8)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore,
                Font = ThemeColors.FontButton,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.15f);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(back, 0.15f);
            btn.Region = new Region(RoundedRect(new Rectangle(0, 0, w, h), radius));
            return btn;
        }

        public static Panel CreateStyledTextBox(out TextBox textBox, int width, int height = 46, bool isPassword = false, string placeholder = "")
        {
            var panel = new Panel
            {
                Size = new Size(width, height),
                BackColor = ThemeColors.InputBg,
                Padding = new Padding(14, 11, 14, 11)
            };

            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var pen = new Pen(panel.ContainsFocus ? ThemeColors.BorderFocus : ThemeColors.Border, 1.5f);
                e.Graphics.DrawPath(pen, RoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 8));
            };
            panel.Region = new Region(RoundedRect(new Rectangle(0, 0, width, height), 8));

            var tb = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = ThemeColors.FontBody,
                ForeColor = ThemeColors.InputFg,
                BackColor = ThemeColors.InputBg
            };
            if (isPassword) tb.PasswordChar = '•';

            if (!string.IsNullOrEmpty(placeholder))
            {
                tb.Text = placeholder;
                tb.ForeColor = ThemeColors.TextSecondary;
                tb.GotFocus += (s, e) =>
                {
                    if (tb.Text == placeholder)
                    {
                        tb.Text = "";
                        tb.ForeColor = ThemeColors.InputFg;
                        if (isPassword) tb.PasswordChar = '•';
                    }
                };
                tb.LostFocus += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(tb.Text))
                    {
                        tb.Text = placeholder;
                        tb.ForeColor = ThemeColors.TextSecondary;
                        if (isPassword) tb.PasswordChar = '\0';
                    }
                };
                if (isPassword) tb.PasswordChar = '\0';
            }

            tb.Enter += (s, e) => panel.Invalidate();
            tb.Leave += (s, e) => panel.Invalidate();

            textBox = tb;
            panel.Controls.Add(tb);
            return panel;
        }

        public static Button CreateThemeToggleButton(Action onThemeToggled, int width = 160, int height = 36)
        {
            string GetThemeText() => ThemeColors.CurrentTheme == AppTheme.Light 
                ? "Theme: Sang" 
                : "Theme: Den & Xanh";

            Color GetThemeBg() => ThemeColors.CurrentTheme == AppTheme.Light
                ? Color.FromArgb(224, 238, 255)
                : Color.FromArgb(6, 78, 59);

            Color GetThemeFg() => ThemeColors.CurrentTheme == AppTheme.Light
                ? Color.FromArgb(37, 99, 235)
                : Color.FromArgb(52, 211, 153);

            var btn = CreateRoundedButton(GetThemeText(), GetThemeBg(), GetThemeFg(), width, height, 8);
            btn.Font = ThemeColors.FontSmall;
            btn.Click += (s, e) =>
            {
                ThemeColors.ToggleTheme();
                onThemeToggled();
            };
            return btn;
        }
    }

    /// <summary>
    /// Custom Panel với DoubleBuffering mượt mà để chống giật/nhấp nháy khi vẽ GDI+.
    /// </summary>
    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
        }
    }
}
