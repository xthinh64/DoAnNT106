namespace OnlineChessGame
{
    partial class LoginForm
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
            this.card = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblSub = new System.Windows.Forms.Label();
            this.lblUser = new System.Windows.Forms.Label();
            this.txtUser = new System.Windows.Forms.TextBox();
            this.lblPass = new System.Windows.Forms.Label();
            this.txtPass = new System.Windows.Forms.TextBox();
            this.pnlConnection = new System.Windows.Forms.Panel();
            this.lblServer = new System.Windows.Forms.Label();
            this.txtServer = new System.Windows.Forms.TextBox();
            this.btnConnection = new System.Windows.Forms.Button();
            this.chkShowPassword = new System.Windows.Forms.CheckBox();
            this.lblError = new System.Windows.Forms.Label();
            this.btnLogin = new System.Windows.Forms.Button();
            this.btnRegister = new System.Windows.Forms.Button();
            this.btnThemeToggle = new System.Windows.Forms.Button();
            this.pnlConnection.SuspendLayout();
            this.card.SuspendLayout();
            this.SuspendLayout();
            //
            // card
            //
            this.card.BackColor = System.Drawing.Color.White;
            this.card.Controls.Add(this.btnThemeToggle);
            this.card.Controls.Add(this.btnRegister);
            this.card.Controls.Add(this.btnLogin);
            this.card.Controls.Add(this.chkShowPassword);
            this.card.Controls.Add(this.lblError);
            this.card.Controls.Add(this.btnConnection);
            this.card.Controls.Add(this.pnlConnection);
            this.card.Controls.Add(this.txtPass);
            this.card.Controls.Add(this.lblPass);
            this.card.Controls.Add(this.txtUser);
            this.card.Controls.Add(this.lblUser);
            this.card.Controls.Add(this.lblSub);
            this.card.Controls.Add(this.lblTitle);
            this.card.Location = new System.Drawing.Point(32, 32);
            this.card.Name = "card";
            this.card.Size = new System.Drawing.Size(560, 516);
            this.card.TabIndex = 0;
            //
            // lblTitle
            //
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 22F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.lblTitle.Location = new System.Drawing.Point(40, 24);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(480, 44);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Đăng nhập";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lblSub
            //
            this.lblSub.Font = new System.Drawing.Font("Segoe UI", 11.5F);
            this.lblSub.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblSub.Location = new System.Drawing.Point(40, 72);
            this.lblSub.Name = "lblSub";
            this.lblSub.Size = new System.Drawing.Size(480, 24);
            this.lblSub.TabIndex = 1;
            this.lblSub.Text = "Chào mừng bạn đến với Cờ vua & Caro Online.";
            this.lblSub.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lblUser
            //
            this.lblUser.AutoSize = true;
            this.lblUser.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblUser.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblUser.Location = new System.Drawing.Point(40, 112);
            this.lblUser.Name = "lblUser";
            this.lblUser.Size = new System.Drawing.Size(110, 20);
            this.lblUser.TabIndex = 2;
            this.lblUser.Text = "Tên đăng nhập";
            //
            // txtUser
            //
            this.txtUser.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtUser.Font = new System.Drawing.Font("Segoe UI", 13F);
            this.txtUser.Location = new System.Drawing.Point(40, 140);
            this.txtUser.Name = "txtUser";
            this.txtUser.MaxLength = 50;
            this.txtUser.AutoSize = false;
            this.txtUser.PlaceholderText = "Nhập tên đăng nhập";
            this.txtUser.Size = new System.Drawing.Size(480, 36);
            this.txtUser.TabIndex = 0;
            this.txtUser.AccessibleName = "Tên đăng nhập";
            //
            // lblPass
            //
            this.lblPass.AutoSize = true;
            this.lblPass.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblPass.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblPass.Location = new System.Drawing.Point(40, 192);
            this.lblPass.Name = "lblPass";
            this.lblPass.Size = new System.Drawing.Size(74, 20);
            this.lblPass.TabIndex = 4;
            this.lblPass.Text = "Mật khẩu";
            //
            // txtPass
            //
            this.txtPass.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtPass.Font = new System.Drawing.Font("Segoe UI", 13F);
            this.txtPass.Location = new System.Drawing.Point(40, 220);
            this.txtPass.Name = "txtPass";
            this.txtPass.UseSystemPasswordChar = true;
            this.txtPass.MaxLength = 128;
            this.txtPass.AutoSize = false;
            this.txtPass.PlaceholderText = "Nhập mật khẩu";
            this.txtPass.Size = new System.Drawing.Size(480, 36);
            this.txtPass.TabIndex = 1;
            this.txtPass.AccessibleName = "Mật khẩu";
            //
            // pnlConnection
            //
            this.pnlConnection.Controls.Add(this.txtServer);
            this.pnlConnection.Controls.Add(this.lblServer);
            this.pnlConnection.Location = new System.Drawing.Point(40, 492);
            this.pnlConnection.Name = "pnlConnection";
            this.pnlConnection.Size = new System.Drawing.Size(480, 80);
            this.pnlConnection.TabIndex = 6;
            this.pnlConnection.Visible = false;
            //
            // lblServer
            //
            this.lblServer.AutoSize = true;
            this.lblServer.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblServer.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblServer.Location = new System.Drawing.Point(0, 0);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(193, 20);
            this.lblServer.TabIndex = 6;
            this.lblServer.Text = "Máy chủ xác thực (host:port)";
            //
            // txtServer
            //
            this.txtServer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtServer.Font = new System.Drawing.Font("Segoe UI", 13F);
            this.txtServer.Location = new System.Drawing.Point(0, 28);
            this.txtServer.Name = "txtServer";
            this.txtServer.AccessibleName = "Máy chủ xác thực";
            this.txtServer.AutoSize = false;
            this.txtServer.Size = new System.Drawing.Size(480, 36);
            this.txtServer.TabIndex = 0;
            //
            // btnConnection
            //
            this.btnConnection.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConnection.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnConnection.FlatAppearance.BorderSize = 0;
            this.btnConnection.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnConnection.Location = new System.Drawing.Point(40, 452);
            this.btnConnection.Name = "btnConnection";
            this.btnConnection.Size = new System.Drawing.Size(240, 32);
            this.btnConnection.TabIndex = 5;
            this.btnConnection.Text = "Cấu hình kết nối";
            this.btnConnection.Click += new System.EventHandler(this.BtnConnection_Click);
            //
            // chkShowPassword
            //
            this.chkShowPassword.AutoSize = true;
            this.chkShowPassword.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.chkShowPassword.Location = new System.Drawing.Point(40, 268);
            this.chkShowPassword.Name = "chkShowPassword";
            this.chkShowPassword.Size = new System.Drawing.Size(180, 24);
            this.chkShowPassword.TabIndex = 2;
            this.chkShowPassword.Text = "Hiện mật khẩu";
            this.chkShowPassword.UseVisualStyleBackColor = true;
            this.chkShowPassword.CheckedChanged += new System.EventHandler(this.ChkShowPassword_CheckedChanged);
            //
            // lblError
            //
            this.lblError.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblError.ForeColor = System.Drawing.Color.FromArgb(220, 38, 38);
            this.lblError.Location = new System.Drawing.Point(40, 300);
            this.lblError.Name = "lblError";
            this.lblError.AccessibleName = "Trạng thái xác thực";
            this.lblError.Size = new System.Drawing.Size(480, 44);
            this.lblError.TabIndex = 9;
            this.lblError.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnLogin
            //
            this.btnLogin.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnLogin.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLogin.FlatAppearance.BorderSize = 0;
            this.btnLogin.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLogin.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnLogin.ForeColor = System.Drawing.Color.White;
            this.btnLogin.Location = new System.Drawing.Point(40, 352);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(480, 48);
            this.btnLogin.TabIndex = 3;
            this.btnLogin.Text = "Đăng nhập";
            this.btnLogin.UseVisualStyleBackColor = false;
            this.btnLogin.Click += new System.EventHandler(this.BtnLogin_Click);
            //
            // btnRegister
            //
            this.btnRegister.BackColor = System.Drawing.Color.Transparent;
            this.btnRegister.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRegister.FlatAppearance.BorderSize = 0;
            this.btnRegister.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRegister.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnRegister.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnRegister.Location = new System.Drawing.Point(40, 408);
            this.btnRegister.Name = "btnRegister";
            this.btnRegister.Size = new System.Drawing.Size(480, 32);
            this.btnRegister.TabIndex = 4;
            this.btnRegister.Text = "Chưa có tài khoản? Đăng ký ngay";
            this.btnRegister.UseVisualStyleBackColor = false;
            this.btnRegister.Click += new System.EventHandler(this.BtnRegister_Click);
            //
            // btnThemeToggle
            //
            this.btnThemeToggle.BackColor = System.Drawing.Color.FromArgb(224, 238, 255);
            this.btnThemeToggle.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnThemeToggle.FlatAppearance.BorderSize = 0;
            this.btnThemeToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnThemeToggle.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnThemeToggle.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnThemeToggle.Location = new System.Drawing.Point(304, 452);
            this.btnThemeToggle.Name = "btnThemeToggle";
            this.btnThemeToggle.Size = new System.Drawing.Size(216, 32);
            this.btnThemeToggle.TabIndex = 7;
            this.btnThemeToggle.Text = "Giao diện sáng";
            this.btnThemeToggle.UseVisualStyleBackColor = false;
            this.btnThemeToggle.Click += new System.EventHandler(this.BtnThemeToggle_Click);
            //
            // LoginForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(243, 245, 249);
            this.ClientSize = new System.Drawing.Size(624, 580);
            this.AcceptButton = this.btnLogin;
            this.AutoScroll = true;
            this.Controls.Add(this.card);
            this.ActiveControl = this.txtUser;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "LoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Cờ vua & Caro Online — Đăng nhập";
            this.Load += new System.EventHandler(this.LoginForm_Load);
            this.Resize += new System.EventHandler(this.LoginForm_Resize);
            this.pnlConnection.ResumeLayout(false);
            this.pnlConnection.PerformLayout();
            this.card.ResumeLayout(false);
            this.card.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel card;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSub;
        private System.Windows.Forms.Label lblUser;
        private System.Windows.Forms.TextBox txtUser;
        private System.Windows.Forms.Label lblPass;
        private System.Windows.Forms.TextBox txtPass;
        private System.Windows.Forms.Panel pnlConnection;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.TextBox txtServer;
        private System.Windows.Forms.Button btnConnection;
        private System.Windows.Forms.CheckBox chkShowPassword;
        private System.Windows.Forms.Label lblError;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Button btnRegister;
        private System.Windows.Forms.Button btnThemeToggle;
    }
}
