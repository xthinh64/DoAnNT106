namespace OnlineChessGame
{
    partial class CaroBoardForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.boardPanel = new System.Windows.Forms.Panel();
            this.cardOpp = new System.Windows.Forms.Panel();
            this.lblAvatarOpp = new System.Windows.Forms.Label();
            this.lblNameOpp = new System.Windows.Forms.Label();
            this.lblEloOpp = new System.Windows.Forms.Label();
            this.pnlTimerOpp = new System.Windows.Forms.Panel();
            this.lblTimerOpp = new System.Windows.Forms.Label();
            this.lblTurn = new System.Windows.Forms.Label();
            this.cardPlayer = new System.Windows.Forms.Panel();
            this.lblAvatarPlayer = new System.Windows.Forms.Label();
            this.lblNamePlayer = new System.Windows.Forms.Label();
            this.lblEloPlayer = new System.Windows.Forms.Label();
            this.pnlTimerPlayer = new System.Windows.Forms.Panel();
            this.lblTimerPlayer = new System.Windows.Forms.Label();
            this.btnDraw = new System.Windows.Forms.Button();
            this.btnSurr = new System.Windows.Forms.Button();
            this.btnLeave = new System.Windows.Forms.Button();
            this.lblChatTitle = new System.Windows.Forms.Label();
            this.btnThemeToggle = new System.Windows.Forms.Button();
            this.rtbChat = new System.Windows.Forms.RichTextBox();
            this.txtChat = new System.Windows.Forms.TextBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.cardOpp.SuspendLayout();
            this.pnlTimerOpp.SuspendLayout();
            this.cardPlayer.SuspendLayout();
            this.pnlTimerPlayer.SuspendLayout();
            this.SuspendLayout();
            // 
            // boardPanel
            // 
            this.boardPanel.BackColor = System.Drawing.Color.FromArgb(253, 251, 247);
            this.boardPanel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.boardPanel.Location = new System.Drawing.Point(24, 24);
            this.boardPanel.Name = "boardPanel";
            this.boardPanel.Size = new System.Drawing.Size(624, 624);
            this.boardPanel.TabIndex = 0;
            this.boardPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.BoardPanel_Paint);
            this.boardPanel.MouseClick += new System.Windows.Forms.MouseEventHandler(this.BoardPanel_Click);
            // 
            // cardOpp
            // 
            this.cardOpp.BackColor = System.Drawing.Color.White;
            this.cardOpp.Controls.Add(this.pnlTimerOpp);
            this.cardOpp.Controls.Add(this.lblEloOpp);
            this.cardOpp.Controls.Add(this.lblNameOpp);
            this.cardOpp.Controls.Add(this.lblAvatarOpp);
            this.cardOpp.Location = new System.Drawing.Point(672, 24);
            this.cardOpp.Name = "cardOpp";
            this.cardOpp.Size = new System.Drawing.Size(380, 76);
            this.cardOpp.TabIndex = 1;
            // 
            // lblAvatarOpp
            // 
            this.lblAvatarOpp.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblAvatarOpp.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold);
            this.lblAvatarOpp.ForeColor = System.Drawing.Color.White;
            this.lblAvatarOpp.Location = new System.Drawing.Point(14, 16);
            this.lblAvatarOpp.Name = "lblAvatarOpp";
            this.lblAvatarOpp.Size = new System.Drawing.Size(44, 44);
            this.lblAvatarOpp.TabIndex = 0;
            this.lblAvatarOpp.Text = "C";
            this.lblAvatarOpp.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblNameOpp
            // 
            this.lblNameOpp.AutoSize = true;
            this.lblNameOpp.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold);
            this.lblNameOpp.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblNameOpp.Location = new System.Drawing.Point(68, 16);
            this.lblNameOpp.Name = "lblNameOpp";
            this.lblNameOpp.Size = new System.Drawing.Size(217, 25);
            this.lblNameOpp.TabIndex = 1;
            this.lblNameOpp.Text = "Computer Caro [Quan O]";
            // 
            // lblEloOpp
            // 
            this.lblEloOpp.AutoSize = true;
            this.lblEloOpp.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblEloOpp.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblEloOpp.Location = new System.Drawing.Point(68, 42);
            this.lblEloOpp.Name = "lblEloOpp";
            this.lblEloOpp.Size = new System.Drawing.Size(55, 15);
            this.lblEloOpp.TabIndex = 2;
            this.lblEloOpp.Text = "Elo: 1200";
            // 
            // pnlTimerOpp
            // 
            this.pnlTimerOpp.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.pnlTimerOpp.Controls.Add(this.lblTimerOpp);
            this.pnlTimerOpp.Location = new System.Drawing.Point(286, 22);
            this.pnlTimerOpp.Name = "pnlTimerOpp";
            this.pnlTimerOpp.Size = new System.Drawing.Size(80, 32);
            this.pnlTimerOpp.TabIndex = 3;
            // 
            // lblTimerOpp
            // 
            this.lblTimerOpp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTimerOpp.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblTimerOpp.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblTimerOpp.Location = new System.Drawing.Point(0, 0);
            this.lblTimerOpp.Name = "lblTimerOpp";
            this.lblTimerOpp.Size = new System.Drawing.Size(80, 32);
            this.lblTimerOpp.TabIndex = 0;
            this.lblTimerOpp.Text = "10:00";
            this.lblTimerOpp.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTurn
            // 
            this.lblTurn.BackColor = System.Drawing.Color.FromArgb(224, 238, 255);
            this.lblTurn.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold);
            this.lblTurn.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.lblTurn.Location = new System.Drawing.Point(672, 112);
            this.lblTurn.Name = "lblTurn";
            this.lblTurn.Size = new System.Drawing.Size(380, 38);
            this.lblTurn.TabIndex = 2;
            this.lblTurn.Text = "LUOT CUA BAN (QUAN X)";
            this.lblTurn.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // cardPlayer
            // 
            this.cardPlayer.BackColor = System.Drawing.Color.White;
            this.cardPlayer.Controls.Add(this.pnlTimerPlayer);
            this.cardPlayer.Controls.Add(this.lblEloPlayer);
            this.cardPlayer.Controls.Add(this.lblNamePlayer);
            this.cardPlayer.Controls.Add(this.lblAvatarPlayer);
            this.cardPlayer.Location = new System.Drawing.Point(672, 162);
            this.cardPlayer.Name = "cardPlayer";
            this.cardPlayer.Size = new System.Drawing.Size(380, 76);
            this.cardPlayer.TabIndex = 3;
            // 
            // lblAvatarPlayer
            // 
            this.lblAvatarPlayer.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.lblAvatarPlayer.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold);
            this.lblAvatarPlayer.ForeColor = System.Drawing.Color.White;
            this.lblAvatarPlayer.Location = new System.Drawing.Point(14, 16);
            this.lblAvatarPlayer.Name = "lblAvatarPlayer";
            this.lblAvatarPlayer.Size = new System.Drawing.Size(44, 44);
            this.lblAvatarPlayer.TabIndex = 0;
            this.lblAvatarPlayer.Text = "P";
            this.lblAvatarPlayer.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblNamePlayer
            // 
            this.lblNamePlayer.AutoSize = true;
            this.lblNamePlayer.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold);
            this.lblNamePlayer.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblNamePlayer.Location = new System.Drawing.Point(68, 16);
            this.lblNamePlayer.Name = "lblNamePlayer";
            this.lblNamePlayer.Size = new System.Drawing.Size(209, 25);
            this.lblNamePlayer.TabIndex = 1;
            this.lblNamePlayer.Text = "Kỳ thủ VN [Quan X]";
            // 
            // lblEloPlayer
            // 
            this.lblEloPlayer.AutoSize = true;
            this.lblEloPlayer.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblEloPlayer.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblEloPlayer.Location = new System.Drawing.Point(68, 42);
            this.lblEloPlayer.Name = "lblEloPlayer";
            this.lblEloPlayer.Size = new System.Drawing.Size(55, 15);
            this.lblEloPlayer.TabIndex = 2;
            this.lblEloPlayer.Text = "Elo: 1200";
            // 
            // pnlTimerPlayer
            // 
            this.pnlTimerPlayer.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.pnlTimerPlayer.Controls.Add(this.lblTimerPlayer);
            this.pnlTimerPlayer.Location = new System.Drawing.Point(286, 22);
            this.pnlTimerPlayer.Name = "pnlTimerPlayer";
            this.pnlTimerPlayer.Size = new System.Drawing.Size(80, 32);
            this.pnlTimerPlayer.TabIndex = 3;
            // 
            // lblTimerPlayer
            // 
            this.lblTimerPlayer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTimerPlayer.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblTimerPlayer.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblTimerPlayer.Location = new System.Drawing.Point(0, 0);
            this.lblTimerPlayer.Name = "lblTimerPlayer";
            this.lblTimerPlayer.Size = new System.Drawing.Size(80, 32);
            this.lblTimerPlayer.TabIndex = 0;
            this.lblTimerPlayer.Text = "10:00";
            this.lblTimerPlayer.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnDraw
            // 
            this.btnDraw.BackColor = System.Drawing.Color.FromArgb(217, 119, 6);
            this.btnDraw.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnDraw.FlatAppearance.BorderSize = 0;
            this.btnDraw.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDraw.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnDraw.ForeColor = System.Drawing.Color.White;
            this.btnDraw.Location = new System.Drawing.Point(672, 252);
            this.btnDraw.Name = "btnDraw";
            this.btnDraw.Size = new System.Drawing.Size(121, 36);
            this.btnDraw.TabIndex = 4;
            this.btnDraw.Text = "Cau hoa";
            this.btnDraw.UseVisualStyleBackColor = false;
            this.btnDraw.Click += new System.EventHandler(this.BtnDraw_Click);
            // 
            // btnSurr
            // 
            this.btnSurr.BackColor = System.Drawing.Color.FromArgb(220, 38, 38);
            this.btnSurr.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSurr.FlatAppearance.BorderSize = 0;
            this.btnSurr.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSurr.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnSurr.ForeColor = System.Drawing.Color.White;
            this.btnSurr.Location = new System.Drawing.Point(801, 252);
            this.btnSurr.Name = "btnSurr";
            this.btnSurr.Size = new System.Drawing.Size(121, 36);
            this.btnSurr.TabIndex = 5;
            this.btnSurr.Text = "Dau hang";
            this.btnSurr.UseVisualStyleBackColor = false;
            this.btnSurr.Click += new System.EventHandler(this.BtnSurr_Click);
            // 
            // btnLeave
            // 
            this.btnLeave.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnLeave.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLeave.FlatAppearance.BorderSize = 0;
            this.btnLeave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLeave.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnLeave.ForeColor = System.Drawing.Color.White;
            this.btnLeave.Location = new System.Drawing.Point(931, 252);
            this.btnLeave.Name = "btnLeave";
            this.btnLeave.Size = new System.Drawing.Size(121, 36);
            this.btnLeave.TabIndex = 6;
            this.btnLeave.Text = "Ve Sanh";
            this.btnLeave.UseVisualStyleBackColor = false;
            this.btnLeave.Click += new System.EventHandler(this.BtnLeave_Click);
            // 
            // lblChatTitle
            // 
            this.lblChatTitle.AutoSize = true;
            this.lblChatTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold);
            this.lblChatTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblChatTitle.Location = new System.Drawing.Point(672, 306);
            this.lblChatTitle.Name = "lblChatTitle";
            this.lblChatTitle.Size = new System.Drawing.Size(189, 25);
            this.lblChatTitle.TabIndex = 7;
            this.lblChatTitle.Text = "Tro chuyen trong tran";
            // 
            // btnThemeToggle
            // 
            this.btnThemeToggle.BackColor = System.Drawing.Color.FromArgb(224, 238, 255);
            this.btnThemeToggle.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnThemeToggle.FlatAppearance.BorderSize = 0;
            this.btnThemeToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnThemeToggle.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnThemeToggle.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnThemeToggle.Location = new System.Drawing.Point(922, 302);
            this.btnThemeToggle.Name = "btnThemeToggle";
            this.btnThemeToggle.Size = new System.Drawing.Size(130, 28);
            this.btnThemeToggle.TabIndex = 8;
            this.btnThemeToggle.Text = "Theme: Sang";
            this.btnThemeToggle.UseVisualStyleBackColor = false;
            this.btnThemeToggle.Click += new System.EventHandler(this.BtnThemeToggle_Click);
            // 
            // rtbChat
            // 
            this.rtbChat.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.rtbChat.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbChat.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.rtbChat.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.rtbChat.Location = new System.Drawing.Point(672, 338);
            this.rtbChat.Name = "rtbChat";
            this.rtbChat.ReadOnly = true;
            this.rtbChat.Size = new System.Drawing.Size(380, 260);
            this.rtbChat.TabIndex = 9;
            this.rtbChat.Text = "";
            // 
            // txtChat
            // 
            this.txtChat.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtChat.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.txtChat.Location = new System.Drawing.Point(672, 614);
            this.txtChat.Name = "txtChat";
            this.txtChat.PlaceholderText = "Nhap tin nhan...";
            this.txtChat.Size = new System.Drawing.Size(302, 24);
            this.txtChat.TabIndex = 10;
            this.txtChat.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TxtChat_KeyDown);
            // 
            // btnSend
            // 
            this.btnSend.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnSend.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSend.FlatAppearance.BorderSize = 0;
            this.btnSend.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSend.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnSend.ForeColor = System.Drawing.Color.White;
            this.btnSend.Location = new System.Drawing.Point(982, 610);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(70, 32);
            this.btnSend.TabIndex = 11;
            this.btnSend.Text = "Gui";
            this.btnSend.UseVisualStyleBackColor = false;
            this.btnSend.Click += new System.EventHandler(this.BtnSend_Click);
            // 
            // CaroBoardForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(243, 245, 249);
            this.ClientSize = new System.Drawing.Size(1076, 672);
            this.Controls.Add(this.btnSend);
            this.Controls.Add(this.txtChat);
            this.Controls.Add(this.rtbChat);
            this.Controls.Add(this.btnThemeToggle);
            this.Controls.Add(this.lblChatTitle);
            this.Controls.Add(this.btnLeave);
            this.Controls.Add(this.btnSurr);
            this.Controls.Add(this.btnDraw);
            this.Controls.Add(this.cardPlayer);
            this.Controls.Add(this.lblTurn);
            this.Controls.Add(this.cardOpp);
            this.Controls.Add(this.boardPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "CaroBoardForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Chess Online - Co Caro";
            this.Load += new System.EventHandler(this.CaroBoardForm_Load);
            this.cardOpp.ResumeLayout(false);
            this.cardOpp.PerformLayout();
            this.pnlTimerOpp.ResumeLayout(false);
            this.cardPlayer.ResumeLayout(false);
            this.cardPlayer.PerformLayout();
            this.pnlTimerPlayer.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Panel boardPanel;
        private System.Windows.Forms.Panel cardOpp;
        private System.Windows.Forms.Label lblAvatarOpp;
        private System.Windows.Forms.Label lblNameOpp;
        private System.Windows.Forms.Label lblEloOpp;
        private System.Windows.Forms.Panel pnlTimerOpp;
        private System.Windows.Forms.Label lblTimerOpp;
        private System.Windows.Forms.Label lblTurn;
        private System.Windows.Forms.Panel cardPlayer;
        private System.Windows.Forms.Label lblAvatarPlayer;
        private System.Windows.Forms.Label lblNamePlayer;
        private System.Windows.Forms.Label lblEloPlayer;
        private System.Windows.Forms.Panel pnlTimerPlayer;
        private System.Windows.Forms.Label lblTimerPlayer;
        private System.Windows.Forms.Button btnDraw;
        private System.Windows.Forms.Button btnSurr;
        private System.Windows.Forms.Button btnLeave;
        private System.Windows.Forms.Label lblChatTitle;
        private System.Windows.Forms.Button btnThemeToggle;
        private System.Windows.Forms.RichTextBox rtbChat;
        private System.Windows.Forms.TextBox txtChat;
        private System.Windows.Forms.Button btnSend;
    }
}
