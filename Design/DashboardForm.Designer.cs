namespace OnlineChessGame
{
    partial class DashboardForm
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
            this.header = new System.Windows.Forms.Panel();
            this.lblAvatar = new System.Windows.Forms.Label();
            this.lblName = new System.Windows.Forms.Label();
            this.pnlElo = new System.Windows.Forms.Panel();
            this.lblElo = new System.Windows.Forms.Label();
            this.btnThemeToggle = new System.Windows.Forms.Button();
            this.btnLogout = new System.Windows.Forms.Button();
            this.lblModeHeader = new System.Windows.Forms.Label();
            this.cardChess = new System.Windows.Forms.Panel();
            this.pnlTagChess = new System.Windows.Forms.Panel();
            this.lblTagChess = new System.Windows.Forms.Label();
            this.lblTitleChess = new System.Windows.Forms.Label();
            this.lblDescChess = new System.Windows.Forms.Label();
            this.btnSoloChess = new System.Windows.Forms.Button();
            this.btnRankedChess = new System.Windows.Forms.Button();
            this.cardCaro = new System.Windows.Forms.Panel();
            this.pnlTagCaro = new System.Windows.Forms.Panel();
            this.lblTagCaro = new System.Windows.Forms.Label();
            this.lblTitleCaro = new System.Windows.Forms.Label();
            this.lblDescCaro = new System.Windows.Forms.Label();
            this.btnSoloCaro = new System.Windows.Forms.Button();
            this.btnRankedCaro = new System.Windows.Forms.Button();
            this.queuePanel = new System.Windows.Forms.Panel();
            this.lblQueueStatus = new System.Windows.Forms.Label();
            this.lblQueueTimer = new System.Windows.Forms.Label();
            this.pbQueue = new System.Windows.Forms.ProgressBar();
            this.btnCancelQueue = new System.Windows.Forms.Button();
            this.lblHistHeader = new System.Windows.Forms.Label();
            this.cardHistory = new System.Windows.Forms.Panel();
            this.header.SuspendLayout();
            this.pnlElo.SuspendLayout();
            this.cardChess.SuspendLayout();
            this.pnlTagChess.SuspendLayout();
            this.cardCaro.SuspendLayout();
            this.pnlTagCaro.SuspendLayout();
            this.queuePanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // header
            // 
            this.header.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.header.Controls.Add(this.btnLogout);
            this.header.Controls.Add(this.btnThemeToggle);
            this.header.Controls.Add(this.pnlElo);
            this.header.Controls.Add(this.lblName);
            this.header.Controls.Add(this.lblAvatar);
            this.header.Dock = System.Windows.Forms.DockStyle.Top;
            this.header.Location = new System.Drawing.Point(0, 0);
            this.header.Name = "header";
            this.header.Size = new System.Drawing.Size(1120, 76);
            this.header.TabIndex = 0;
            // 
            // lblAvatar
            // 
            this.lblAvatar.BackColor = System.Drawing.Color.White;
            this.lblAvatar.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblAvatar.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.lblAvatar.Location = new System.Drawing.Point(28, 15);
            this.lblAvatar.Name = "lblAvatar";
            this.lblAvatar.Size = new System.Drawing.Size(46, 46);
            this.lblAvatar.TabIndex = 0;
            this.lblAvatar.Text = "P";
            this.lblAvatar.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblName
            // 
            this.lblName.AutoSize = true;
            this.lblName.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold);
            this.lblName.ForeColor = System.Drawing.Color.White;
            this.lblName.Location = new System.Drawing.Point(86, 17);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(95, 25);
            this.lblName.TabIndex = 1;
            this.lblName.Text = "Kỳ thủ VN";
            // 
            // pnlElo
            // 
            this.pnlElo.BackColor = System.Drawing.Color.FromArgb(40, 255, 255, 255);
            this.pnlElo.Controls.Add(this.lblElo);
            this.pnlElo.Location = new System.Drawing.Point(86, 42);
            this.pnlElo.Name = "pnlElo";
            this.pnlElo.Size = new System.Drawing.Size(100, 22);
            this.pnlElo.TabIndex = 2;
            // 
            // lblElo
            // 
            this.lblElo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblElo.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblElo.ForeColor = System.Drawing.Color.White;
            this.lblElo.Location = new System.Drawing.Point(0, 0);
            this.lblElo.Name = "lblElo";
            this.lblElo.Size = new System.Drawing.Size(100, 22);
            this.lblElo.TabIndex = 0;
            this.lblElo.Text = "Elo: 1200";
            this.lblElo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnThemeToggle
            // 
            this.btnThemeToggle.BackColor = System.Drawing.Color.FromArgb(224, 238, 255);
            this.btnThemeToggle.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnThemeToggle.FlatAppearance.BorderSize = 0;
            this.btnThemeToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnThemeToggle.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnThemeToggle.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnThemeToggle.Location = new System.Drawing.Point(806, 20);
            this.btnThemeToggle.Name = "btnThemeToggle";
            this.btnThemeToggle.Size = new System.Drawing.Size(160, 36);
            this.btnThemeToggle.TabIndex = 3;
            this.btnThemeToggle.Text = "Theme: Sang";
            this.btnThemeToggle.UseVisualStyleBackColor = false;
            this.btnThemeToggle.Click += new System.EventHandler(this.BtnThemeToggle_Click);
            // 
            // btnLogout
            // 
            this.btnLogout.BackColor = System.Drawing.Color.FromArgb(220, 38, 38);
            this.btnLogout.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLogout.FlatAppearance.BorderSize = 0;
            this.btnLogout.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLogout.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnLogout.ForeColor = System.Drawing.Color.White;
            this.btnLogout.Location = new System.Drawing.Point(982, 20);
            this.btnLogout.Name = "btnLogout";
            this.btnLogout.Size = new System.Drawing.Size(110, 36);
            this.btnLogout.TabIndex = 4;
            this.btnLogout.Text = "Dang xuat";
            this.btnLogout.UseVisualStyleBackColor = false;
            this.btnLogout.Click += new System.EventHandler(this.BtnLogout_Click);
            // 
            // lblModeHeader
            // 
            this.lblModeHeader.AutoSize = true;
            this.lblModeHeader.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblModeHeader.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblModeHeader.Location = new System.Drawing.Point(28, 98);
            this.lblModeHeader.Name = "lblModeHeader";
            this.lblModeHeader.Size = new System.Drawing.Size(189, 20);
            this.lblModeHeader.TabIndex = 1;
            this.lblModeHeader.Text = "CHON TRO CHOI THI DAU";
            // 
            // cardChess
            // 
            this.cardChess.BackColor = System.Drawing.Color.White;
            this.cardChess.Controls.Add(this.btnRankedChess);
            this.cardChess.Controls.Add(this.btnSoloChess);
            this.cardChess.Controls.Add(this.lblDescChess);
            this.cardChess.Controls.Add(this.lblTitleChess);
            this.cardChess.Controls.Add(this.pnlTagChess);
            this.cardChess.Location = new System.Drawing.Point(28, 124);
            this.cardChess.Name = "cardChess";
            this.cardChess.Size = new System.Drawing.Size(600, 175);
            this.cardChess.TabIndex = 2;
            // 
            // pnlTagChess
            // 
            this.pnlTagChess.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.pnlTagChess.Controls.Add(this.lblTagChess);
            this.pnlTagChess.Location = new System.Drawing.Point(24, 16);
            this.pnlTagChess.Name = "pnlTagChess";
            this.pnlTagChess.Size = new System.Drawing.Size(160, 24);
            this.pnlTagChess.TabIndex = 0;
            // 
            // lblTagChess
            // 
            this.lblTagChess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTagChess.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblTagChess.ForeColor = System.Drawing.Color.White;
            this.lblTagChess.Location = new System.Drawing.Point(0, 0);
            this.lblTagChess.Name = "lblTagChess";
            this.lblTagChess.Size = new System.Drawing.Size(160, 24);
            this.lblTagChess.TabIndex = 0;
            this.lblTagChess.Text = "8x8 • Chien thuat FIDE";
            this.lblTagChess.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTitleChess
            // 
            this.lblTitleChess.AutoSize = true;
            this.lblTitleChess.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitleChess.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblTitleChess.Location = new System.Drawing.Point(24, 46);
            this.lblTitleChess.Name = "lblTitleChess";
            this.lblTitleChess.Size = new System.Drawing.Size(256, 32);
            this.lblTitleChess.TabIndex = 1;
            this.lblTitleChess.Text = "Co Vua (Chess 8x8)";
            // 
            // lblDescChess
            // 
            this.lblDescChess.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblDescChess.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblDescChess.Location = new System.Drawing.Point(24, 80);
            this.lblDescChess.Name = "lblDescChess";
            this.lblDescChess.Size = new System.Drawing.Size(552, 34);
            this.lblDescChess.TabIndex = 2;
            this.lblDescChess.Text = "Ban co 8x8 tieu chuan quoc te. Dau tri cung Vua, Hau, Xe, Tuong, Ma va Tot.";
            // 
            // btnSoloChess
            // 
            this.btnSoloChess.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnSoloChess.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSoloChess.FlatAppearance.BorderSize = 0;
            this.btnSoloChess.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSoloChess.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnSoloChess.ForeColor = System.Drawing.Color.White;
            this.btnSoloChess.Location = new System.Drawing.Point(24, 118);
            this.btnSoloChess.Name = "btnSoloChess";
            this.btnSoloChess.Size = new System.Drawing.Size(269, 42);
            this.btnSoloChess.TabIndex = 3;
            this.btnSoloChess.Text = "Dau voi May (Solo)";
            this.btnSoloChess.UseVisualStyleBackColor = false;
            this.btnSoloChess.Click += new System.EventHandler(this.BtnSoloChess_Click);
            // 
            // btnRankedChess
            // 
            this.btnRankedChess.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnRankedChess.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRankedChess.FlatAppearance.BorderSize = 0;
            this.btnRankedChess.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRankedChess.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnRankedChess.ForeColor = System.Drawing.Color.White;
            this.btnRankedChess.Location = new System.Drawing.Point(307, 118);
            this.btnRankedChess.Name = "btnRankedChess";
            this.btnRankedChess.Size = new System.Drawing.Size(269, 42);
            this.btnRankedChess.TabIndex = 4;
            this.btnRankedChess.Text = "Tim tran Dau Rank";
            this.btnRankedChess.UseVisualStyleBackColor = false;
            this.btnRankedChess.Click += new System.EventHandler(this.BtnRankedChess_Click);
            // 
            // cardCaro
            // 
            this.cardCaro.BackColor = System.Drawing.Color.White;
            this.cardCaro.Controls.Add(this.btnRankedCaro);
            this.cardCaro.Controls.Add(this.btnSoloCaro);
            this.cardCaro.Controls.Add(this.lblDescCaro);
            this.cardCaro.Controls.Add(this.lblTitleCaro);
            this.cardCaro.Controls.Add(this.pnlTagCaro);
            this.cardCaro.Location = new System.Drawing.Point(28, 319);
            this.cardCaro.Name = "cardCaro";
            this.cardCaro.Size = new System.Drawing.Size(600, 175);
            this.cardCaro.TabIndex = 3;
            // 
            // pnlTagCaro
            // 
            this.pnlTagCaro.BackColor = System.Drawing.Color.FromArgb(2, 132, 199);
            this.pnlTagCaro.Controls.Add(this.lblTagCaro);
            this.pnlTagCaro.Location = new System.Drawing.Point(24, 16);
            this.pnlTagCaro.Name = "pnlTagCaro";
            this.pnlTagCaro.Size = new System.Drawing.Size(160, 24);
            this.pnlTagCaro.TabIndex = 0;
            // 
            // lblTagCaro
            // 
            this.lblTagCaro.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTagCaro.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblTagCaro.ForeColor = System.Drawing.Color.White;
            this.lblTagCaro.Location = new System.Drawing.Point(0, 0);
            this.lblTagCaro.Name = "lblTagCaro";
            this.lblTagCaro.Size = new System.Drawing.Size(160, 24);
            this.lblTagCaro.TabIndex = 0;
            this.lblTagCaro.Text = "15x15 • 5 Quan Thang";
            this.lblTagCaro.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTitleCaro
            // 
            this.lblTitleCaro.AutoSize = true;
            this.lblTitleCaro.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitleCaro.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblTitleCaro.Location = new System.Drawing.Point(24, 46);
            this.lblTitleCaro.Name = "lblTitleCaro";
            this.lblTitleCaro.Size = new System.Drawing.Size(325, 32);
            this.lblTitleCaro.TabIndex = 1;
            this.lblTitleCaro.Text = "Co Caro (Gomoku 15x15)";
            // 
            // lblDescCaro
            // 
            this.lblDescCaro.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblDescCaro.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblDescCaro.Location = new System.Drawing.Point(24, 80);
            this.lblDescCaro.Name = "lblDescCaro";
            this.lblDescCaro.Size = new System.Drawing.Size(552, 34);
            this.lblDescCaro.TabIndex = 2;
            this.lblDescCaro.Text = "Ban co 15x15 kinh dien. Choi quan X va O, noi 5 quan thang hang ngang, doc hoac cheo.";
            // 
            // btnSoloCaro
            // 
            this.btnSoloCaro.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnSoloCaro.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSoloCaro.FlatAppearance.BorderSize = 0;
            this.btnSoloCaro.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSoloCaro.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnSoloCaro.ForeColor = System.Drawing.Color.White;
            this.btnSoloCaro.Location = new System.Drawing.Point(24, 118);
            this.btnSoloCaro.Name = "btnSoloCaro";
            this.btnSoloCaro.Size = new System.Drawing.Size(269, 42);
            this.btnSoloCaro.TabIndex = 3;
            this.btnSoloCaro.Text = "Dau voi May (Solo)";
            this.btnSoloCaro.UseVisualStyleBackColor = false;
            this.btnSoloCaro.Click += new System.EventHandler(this.BtnSoloCaro_Click);
            // 
            // btnRankedCaro
            // 
            this.btnRankedCaro.BackColor = System.Drawing.Color.FromArgb(2, 132, 199);
            this.btnRankedCaro.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRankedCaro.FlatAppearance.BorderSize = 0;
            this.btnRankedCaro.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRankedCaro.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnRankedCaro.ForeColor = System.Drawing.Color.White;
            this.btnRankedCaro.Location = new System.Drawing.Point(307, 118);
            this.btnRankedCaro.Name = "btnRankedCaro";
            this.btnRankedCaro.Size = new System.Drawing.Size(269, 42);
            this.btnRankedCaro.TabIndex = 4;
            this.btnRankedCaro.Text = "Tim tran Dau Rank";
            this.btnRankedCaro.UseVisualStyleBackColor = false;
            this.btnRankedCaro.Click += new System.EventHandler(this.BtnRankedCaro_Click);
            // 
            // queuePanel
            // 
            this.queuePanel.BackColor = System.Drawing.Color.White;
            this.queuePanel.Controls.Add(this.btnCancelQueue);
            this.queuePanel.Controls.Add(this.pbQueue);
            this.queuePanel.Controls.Add(this.lblQueueTimer);
            this.queuePanel.Controls.Add(this.lblQueueStatus);
            this.queuePanel.Location = new System.Drawing.Point(28, 514);
            this.queuePanel.Name = "queuePanel";
            this.queuePanel.Size = new System.Drawing.Size(600, 140);
            this.queuePanel.TabIndex = 4;
            this.queuePanel.Visible = false;
            // 
            // lblQueueStatus
            // 
            this.lblQueueStatus.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold);
            this.lblQueueStatus.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblQueueStatus.Location = new System.Drawing.Point(0, 16);
            this.lblQueueStatus.Name = "lblQueueStatus";
            this.lblQueueStatus.Size = new System.Drawing.Size(600, 26);
            this.lblQueueStatus.TabIndex = 0;
            this.lblQueueStatus.Text = "Dang tim doi thu phu hop...";
            this.lblQueueStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblQueueTimer
            // 
            this.lblQueueTimer.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblQueueTimer.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblQueueTimer.Location = new System.Drawing.Point(0, 44);
            this.lblQueueTimer.Name = "lblQueueTimer";
            this.lblQueueTimer.Size = new System.Drawing.Size(600, 22);
            this.lblQueueTimer.TabIndex = 1;
            this.lblQueueTimer.Text = "Thoi gian cho: 00:00";
            this.lblQueueTimer.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pbQueue
            // 
            this.pbQueue.Location = new System.Drawing.Point(36, 74);
            this.pbQueue.MarqueeAnimationSpeed = 25;
            this.pbQueue.Name = "pbQueue";
            this.pbQueue.Size = new System.Drawing.Size(528, 16);
            this.pbQueue.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.pbQueue.TabIndex = 2;
            // 
            // btnCancelQueue
            // 
            this.btnCancelQueue.BackColor = System.Drawing.Color.FromArgb(220, 38, 38);
            this.btnCancelQueue.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancelQueue.FlatAppearance.BorderSize = 0;
            this.btnCancelQueue.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancelQueue.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnCancelQueue.ForeColor = System.Drawing.Color.White;
            this.btnCancelQueue.Location = new System.Drawing.Point(220, 98);
            this.btnCancelQueue.Name = "btnCancelQueue";
            this.btnCancelQueue.Size = new System.Drawing.Size(160, 32);
            this.btnCancelQueue.TabIndex = 3;
            this.btnCancelQueue.Text = "HUY TIM TRAN";
            this.btnCancelQueue.UseVisualStyleBackColor = false;
            this.btnCancelQueue.Click += new System.EventHandler(this.BtnCancelQueue_Click);
            // 
            // lblHistHeader
            // 
            this.lblHistHeader.AutoSize = true;
            this.lblHistHeader.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblHistHeader.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblHistHeader.Location = new System.Drawing.Point(656, 98);
            this.lblHistHeader.Name = "lblHistHeader";
            this.lblHistHeader.Size = new System.Drawing.Size(206, 20);
            this.lblHistHeader.TabIndex = 5;
            this.lblHistHeader.Text = "LICH SU THI DAU GAN NHAT";
            // 
            // cardHistory
            // 
            this.cardHistory.BackColor = System.Drawing.Color.White;
            this.cardHistory.Location = new System.Drawing.Point(656, 124);
            this.cardHistory.Name = "cardHistory";
            this.cardHistory.Size = new System.Drawing.Size(436, 550);
            this.cardHistory.TabIndex = 6;
            // 
            // DashboardForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(243, 245, 249);
            this.ClientSize = new System.Drawing.Size(1120, 750);
            this.Controls.Add(this.cardHistory);
            this.Controls.Add(this.lblHistHeader);
            this.Controls.Add(this.queuePanel);
            this.Controls.Add(this.cardCaro);
            this.Controls.Add(this.cardChess);
            this.Controls.Add(this.lblModeHeader);
            this.Controls.Add(this.header);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "DashboardForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Chess & Caro Online - Sanh chinh & Chon tro choi";
            this.Load += new System.EventHandler(this.DashboardForm_Load);
            this.header.ResumeLayout(false);
            this.header.PerformLayout();
            this.pnlElo.ResumeLayout(false);
            this.cardChess.ResumeLayout(false);
            this.cardChess.PerformLayout();
            this.pnlTagChess.ResumeLayout(false);
            this.cardCaro.ResumeLayout(false);
            this.cardCaro.PerformLayout();
            this.pnlTagCaro.ResumeLayout(false);
            this.queuePanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Panel header;
        private System.Windows.Forms.Label lblAvatar;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.Panel pnlElo;
        private System.Windows.Forms.Label lblElo;
        private System.Windows.Forms.Button btnThemeToggle;
        private System.Windows.Forms.Button btnLogout;
        private System.Windows.Forms.Label lblModeHeader;
        private System.Windows.Forms.Panel cardChess;
        private System.Windows.Forms.Panel pnlTagChess;
        private System.Windows.Forms.Label lblTagChess;
        private System.Windows.Forms.Label lblTitleChess;
        private System.Windows.Forms.Label lblDescChess;
        private System.Windows.Forms.Button btnSoloChess;
        private System.Windows.Forms.Button btnRankedChess;
        private System.Windows.Forms.Panel cardCaro;
        private System.Windows.Forms.Panel pnlTagCaro;
        private System.Windows.Forms.Label lblTagCaro;
        private System.Windows.Forms.Label lblTitleCaro;
        private System.Windows.Forms.Label lblDescCaro;
        private System.Windows.Forms.Button btnSoloCaro;
        private System.Windows.Forms.Button btnRankedCaro;
        private System.Windows.Forms.Panel queuePanel;
        private System.Windows.Forms.Label lblQueueStatus;
        private System.Windows.Forms.Label lblQueueTimer;
        private System.Windows.Forms.ProgressBar pbQueue;
        private System.Windows.Forms.Button btnCancelQueue;
        private System.Windows.Forms.Label lblHistHeader;
        private System.Windows.Forms.Panel cardHistory;
    }
}
