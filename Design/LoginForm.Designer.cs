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
            this.lblServer = new System.Windows.Forms.Label();
            this.txtServer = new System.Windows.Forms.TextBox();
            this.chkRemember = new System.Windows.Forms.CheckBox();
            this.lblError = new System.Windows.Forms.Label();
            this.btnLogin = new System.Windows.Forms.Button();
            this.btnRegister = new System.Windows.Forms.Button();
            this.btnThemeToggle = new System.Windows.Forms.Button();
            this.card.SuspendLayout();
            this.SuspendLayout();
            // 
            // card
            // 
            this.card.BackColor = System.Drawing.Color.White;
            this.card.Controls.Add(this.btnThemeToggle);
            this.card.Controls.Add(this.btnRegister);
            this.card.Controls.Add(this.btnLogin);
            this.card.Controls.Add(this.lblError);
            this.card.Controls.Add(this.chkRemember);
            this.card.Controls.Add(this.txtServer);
            this.card.Controls.Add(this.lblServer);
            this.card.Controls.Add(this.txtPass);
            this.card.Controls.Add(this.lblPass);
            this.card.Controls.Add(this.txtUser);
            this.card.Controls.Add(this.lblUser);
            this.card.Controls.Add(this.lblSub);
            this.card.Controls.Add(this.lblTitle);
            this.card.Location = new System.Drawing.Point(50, 40);
            this.card.Name = "card";
            this.card.Size = new System.Drawing.Size(480, 580);
            this.card.TabIndex = 0;
            // 
            // lblTitle
            // 
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 22F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.lblTitle.Location = new System.Drawing.Point(0, 36);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(480, 44);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Chess & Caro Online";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblSub
            // 
            this.lblSub.Font = new System.Drawing.Font("Segoe UI", 11.5F);
            this.lblSub.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblSub.Location = new System.Drawing.Point(0, 80);
            this.lblSub.Name = "lblSub";
            this.lblSub.Size = new System.Drawing.Size(480, 24);
            this.lblSub.TabIndex = 1;
            this.lblSub.Text = "Nen tang thi dau co truc tuyen chuyen nghiep";
            this.lblSub.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblUser
            // 
            this.lblUser.AutoSize = true;
            this.lblUser.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblUser.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblUser.Location = new System.Drawing.Point(40, 120);
            this.lblUser.Name = "lblUser";
            this.lblUser.Size = new System.Drawing.Size(110, 20);
            this.lblUser.TabIndex = 2;
            this.lblUser.Text = "Ten dang nhap";
            // 
            // txtUser
            // 
            this.txtUser.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtUser.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.txtUser.Location = new System.Drawing.Point(40, 145);
            this.txtUser.Name = "txtUser";
            this.txtUser.PlaceholderText = "Nhap ten tai khoan";
            this.txtUser.Size = new System.Drawing.Size(400, 27);
            this.txtUser.TabIndex = 3;
            // 
            // lblPass
            // 
            this.lblPass.AutoSize = true;
            this.lblPass.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblPass.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblPass.Location = new System.Drawing.Point(40, 195);
            this.lblPass.Name = "lblPass";
            this.lblPass.Size = new System.Drawing.Size(74, 20);
            this.lblPass.TabIndex = 4;
            this.lblPass.Text = "Mat khau";
            // 
            // txtPass
            // 
            this.txtPass.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtPass.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.txtPass.Location = new System.Drawing.Point(40, 220);
            this.txtPass.Name = "txtPass";
            this.txtPass.PasswordChar = '•';
            this.txtPass.PlaceholderText = "Nhap mat khau";
            this.txtPass.Size = new System.Drawing.Size(400, 27);
            this.txtPass.TabIndex = 5;
            // 
            // lblServer
            // 
            this.lblServer.AutoSize = true;
            this.lblServer.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblServer.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblServer.Location = new System.Drawing.Point(40, 270);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(193, 20);
            this.lblServer.TabIndex = 6;
            this.lblServer.Text = "Dia chi May chu (Server IP)";
            // 
            // txtServer
            // 
            this.txtServer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtServer.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.txtServer.Location = new System.Drawing.Point(40, 295);
            this.txtServer.Name = "txtServer";
            this.txtServer.Size = new System.Drawing.Size(400, 27);
            this.txtServer.TabIndex = 7;
            this.txtServer.Text = "127.0.0.1:8888";
            // 
            // chkRemember
            // 
            this.chkRemember.AutoSize = true;
            this.chkRemember.Cursor = System.Windows.Forms.Cursors.Hand;
            this.chkRemember.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.chkRemember.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.chkRemember.Location = new System.Drawing.Point(40, 335);
            this.chkRemember.Name = "chkRemember";
            this.chkRemember.Size = new System.Drawing.Size(222, 19);
            this.chkRemember.TabIndex = 8;
            this.chkRemember.Text = "Ghi nho tai khoan va thiet lap ket noi";
            this.chkRemember.UseVisualStyleBackColor = true;
            // 
            // lblError
            // 
            this.lblError.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblError.ForeColor = System.Drawing.Color.FromArgb(220, 38, 38);
            this.lblError.Location = new System.Drawing.Point(40, 365);
            this.lblError.Name = "lblError";
            this.lblError.Size = new System.Drawing.Size(400, 20);
            this.lblError.TabIndex = 9;
            this.lblError.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnLogin
            // 
            this.btnLogin.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnLogin.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLogin.FlatAppearance.BorderSize = 0;
            this.btnLogin.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLogin.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnLogin.ForeColor = System.Drawing.Color.White;
            this.btnLogin.Location = new System.Drawing.Point(40, 395);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(400, 48);
            this.btnLogin.TabIndex = 10;
            this.btnLogin.Text = "DANG NHAP";
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
            this.btnRegister.Location = new System.Drawing.Point(40, 455);
            this.btnRegister.Name = "btnRegister";
            this.btnRegister.Size = new System.Drawing.Size(400, 32);
            this.btnRegister.TabIndex = 11;
            this.btnRegister.Text = "Chua co tai khoan? Dang ky ngay";
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
            this.btnThemeToggle.Location = new System.Drawing.Point(280, 525);
            this.btnThemeToggle.Name = "btnThemeToggle";
            this.btnThemeToggle.Size = new System.Drawing.Size(160, 34);
            this.btnThemeToggle.TabIndex = 12;
            this.btnThemeToggle.Text = "Theme: Sang";
            this.btnThemeToggle.UseVisualStyleBackColor = false;
            this.btnThemeToggle.Click += new System.EventHandler(this.BtnThemeToggle_Click);
            // 
            // LoginForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(243, 245, 249);
            this.ClientSize = new System.Drawing.Size(580, 680);
            this.Controls.Add(this.card);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "LoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Chess & Caro Online - Dang nhap";
            this.Load += new System.EventHandler(this.LoginForm_Load);
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
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.TextBox txtServer;
        private System.Windows.Forms.CheckBox chkRemember;
        private System.Windows.Forms.Label lblError;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Button btnRegister;
        private System.Windows.Forms.Button btnThemeToggle;
    }
}
