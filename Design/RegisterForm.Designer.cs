namespace OnlineChessGame
{
    partial class RegisterForm
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
            this.lblDisplay = new System.Windows.Forms.Label();
            this.txtDisplay = new System.Windows.Forms.TextBox();
            this.lblPass = new System.Windows.Forms.Label();
            this.txtPass = new System.Windows.Forms.TextBox();
            this.lblConfirm = new System.Windows.Forms.Label();
            this.txtConfirm = new System.Windows.Forms.TextBox();
            this.chkShowPassword = new System.Windows.Forms.CheckBox();
            this.lblError = new System.Windows.Forms.Label();
            this.btnRegister = new System.Windows.Forms.Button();
            this.btnBack = new System.Windows.Forms.Button();
            this.btnThemeToggle = new System.Windows.Forms.Button();
            this.card.SuspendLayout();
            this.SuspendLayout();
            //
            // card
            //
            this.card.BackColor = System.Drawing.Color.White;
            this.card.Controls.Add(this.btnThemeToggle);
            this.card.Controls.Add(this.btnBack);
            this.card.Controls.Add(this.btnRegister);
            this.card.Controls.Add(this.chkShowPassword);
            this.card.Controls.Add(this.lblError);
            this.card.Controls.Add(this.txtConfirm);
            this.card.Controls.Add(this.lblConfirm);
            this.card.Controls.Add(this.txtPass);
            this.card.Controls.Add(this.lblPass);
            this.card.Controls.Add(this.txtDisplay);
            this.card.Controls.Add(this.lblDisplay);
            this.card.Controls.Add(this.txtUser);
            this.card.Controls.Add(this.lblUser);
            this.card.Controls.Add(this.lblSub);
            this.card.Controls.Add(this.lblTitle);
            this.card.Location = new System.Drawing.Point(32, 32);
            this.card.Name = "card";
            this.card.Size = new System.Drawing.Size(560, 624);
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
            this.lblTitle.Text = "Tạo tài khoản";
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
            this.lblSub.Text = "Tạo tài khoản để bắt đầu chơi Cờ vua và Caro.";
            this.lblSub.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lblUser
            //
            this.lblUser.AutoSize = true;
            this.lblUser.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblUser.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblUser.Location = new System.Drawing.Point(40, 108);
            this.lblUser.Name = "lblUser";
            this.lblUser.Size = new System.Drawing.Size(110, 20);
            this.lblUser.TabIndex = 2;
            this.lblUser.Text = "Tên đăng nhập";
            //
            // txtUser
            //
            this.txtUser.AccessibleName = "Tên đăng nhập";
            this.txtUser.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtUser.Font = new System.Drawing.Font("Segoe UI", 13F);
            this.txtUser.Location = new System.Drawing.Point(40, 132);
            this.txtUser.Name = "txtUser";
            this.txtUser.MaxLength = 50;
            this.txtUser.AutoSize = false;
            this.txtUser.PlaceholderText = "Nhập tên đăng nhập";
            this.txtUser.Size = new System.Drawing.Size(480, 36);
            this.txtUser.TabIndex = 0;
            //
            // lblDisplay
            //
            this.lblDisplay.AutoSize = true;
            this.lblDisplay.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblDisplay.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblDisplay.Location = new System.Drawing.Point(40, 344);
            this.lblDisplay.Name = "lblDisplay";
            this.lblDisplay.Size = new System.Drawing.Size(252, 20);
            this.lblDisplay.TabIndex = 4;
            this.lblDisplay.Text = "Tên hiển thị (tùy chọn)";
            //
            // txtDisplay
            //
            this.txtDisplay.AccessibleName = "Tên hiển thị (tùy chọn)";
            this.txtDisplay.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtDisplay.Font = new System.Drawing.Font("Segoe UI", 13F);
            this.txtDisplay.Location = new System.Drawing.Point(40, 368);
            this.txtDisplay.Name = "txtDisplay";
            this.txtDisplay.MaxLength = 100;
            this.txtDisplay.AutoSize = false;
            this.txtDisplay.PlaceholderText = "Để trống để dùng tên đăng nhập";
            this.txtDisplay.Size = new System.Drawing.Size(480, 36);
            this.txtDisplay.TabIndex = 4;
            //
            // lblPass
            //
            this.lblPass.AutoSize = true;
            this.lblPass.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblPass.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblPass.Location = new System.Drawing.Point(40, 176);
            this.lblPass.Name = "lblPass";
            this.lblPass.Size = new System.Drawing.Size(74, 20);
            this.lblPass.TabIndex = 6;
            this.lblPass.Text = "Mật khẩu";
            //
            // txtPass
            //
            this.txtPass.AccessibleName = "Mật khẩu";
            this.txtPass.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtPass.Font = new System.Drawing.Font("Segoe UI", 13F);
            this.txtPass.Location = new System.Drawing.Point(40, 200);
            this.txtPass.Name = "txtPass";
            this.txtPass.UseSystemPasswordChar = true;
            this.txtPass.MaxLength = 128;
            this.txtPass.AutoSize = false;
            this.txtPass.PlaceholderText = "Từ 8 đến 128 ký tự";
            this.txtPass.Size = new System.Drawing.Size(480, 36);
            this.txtPass.TabIndex = 1;
            //
            // lblConfirm
            //
            this.lblConfirm.AutoSize = true;
            this.lblConfirm.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblConfirm.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            this.lblConfirm.Location = new System.Drawing.Point(40, 244);
            this.lblConfirm.Name = "lblConfirm";
            this.lblConfirm.Size = new System.Drawing.Size(142, 20);
            this.lblConfirm.TabIndex = 8;
            this.lblConfirm.Text = "Xác nhận mật khẩu";
            //
            // txtConfirm
            //
            this.txtConfirm.AccessibleName = "Xác nhận mật khẩu";
            this.txtConfirm.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtConfirm.Font = new System.Drawing.Font("Segoe UI", 13F);
            this.txtConfirm.Location = new System.Drawing.Point(40, 268);
            this.txtConfirm.Name = "txtConfirm";
            this.txtConfirm.UseSystemPasswordChar = true;
            this.txtConfirm.MaxLength = 128;
            this.txtConfirm.AutoSize = false;
            this.txtConfirm.PlaceholderText = "Nhập lại mật khẩu";
            this.txtConfirm.Size = new System.Drawing.Size(480, 36);
            this.txtConfirm.TabIndex = 2;
            //
            // chkShowPassword
            //
            this.chkShowPassword.AutoSize = true;
            this.chkShowPassword.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.chkShowPassword.Location = new System.Drawing.Point(40, 312);
            this.chkShowPassword.Name = "chkShowPassword";
            this.chkShowPassword.Size = new System.Drawing.Size(180, 24);
            this.chkShowPassword.TabIndex = 3;
            this.chkShowPassword.Text = "Hiện mật khẩu";
            this.chkShowPassword.UseVisualStyleBackColor = true;
            this.chkShowPassword.CheckedChanged += new System.EventHandler(this.ChkShowPassword_CheckedChanged);
            //
            // lblError
            //
            this.lblError.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblError.ForeColor = System.Drawing.Color.FromArgb(220, 38, 38);
            this.lblError.Location = new System.Drawing.Point(40, 412);
            this.lblError.Name = "lblError";
            this.lblError.AccessibleName = "Trạng thái xác thực";
            this.lblError.Size = new System.Drawing.Size(480, 44);
            this.lblError.TabIndex = 10;
            this.lblError.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnRegister
            //
            this.btnRegister.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnRegister.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRegister.FlatAppearance.BorderSize = 0;
            this.btnRegister.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRegister.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.btnRegister.ForeColor = System.Drawing.Color.White;
            this.btnRegister.Location = new System.Drawing.Point(40, 464);
            this.btnRegister.Name = "btnRegister";
            this.btnRegister.Size = new System.Drawing.Size(480, 48);
            this.btnRegister.TabIndex = 5;
            this.btnRegister.Text = "Tạo tài khoản";
            this.btnRegister.UseVisualStyleBackColor = false;
            this.btnRegister.Click += new System.EventHandler(this.BtnRegister_Click);
            //
            // btnBack
            //
            this.btnBack.BackColor = System.Drawing.Color.Transparent;
            this.btnBack.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnBack.FlatAppearance.BorderSize = 0;
            this.btnBack.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBack.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnBack.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnBack.Location = new System.Drawing.Point(40, 520);
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(480, 32);
            this.btnBack.TabIndex = 6;
            this.btnBack.Text = "Đã có tài khoản? Đăng nhập";
            this.btnBack.UseVisualStyleBackColor = false;
            this.btnBack.Click += new System.EventHandler(this.BtnBack_Click);
            //
            // btnThemeToggle
            //
            this.btnThemeToggle.BackColor = System.Drawing.Color.FromArgb(224, 238, 255);
            this.btnThemeToggle.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnThemeToggle.FlatAppearance.BorderSize = 0;
            this.btnThemeToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnThemeToggle.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnThemeToggle.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnThemeToggle.Location = new System.Drawing.Point(304, 568);
            this.btnThemeToggle.Name = "btnThemeToggle";
            this.btnThemeToggle.Size = new System.Drawing.Size(216, 32);
            this.btnThemeToggle.TabIndex = 7;
            this.btnThemeToggle.Text = "Giao diện sáng";
            this.btnThemeToggle.UseVisualStyleBackColor = false;
            this.btnThemeToggle.Click += new System.EventHandler(this.BtnThemeToggle_Click);
            //
            // RegisterForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(243, 245, 249);
            this.ClientSize = new System.Drawing.Size(624, 688);
            this.AcceptButton = this.btnRegister;
            this.AutoScroll = true;
            this.Controls.Add(this.card);
            this.ActiveControl = this.txtUser;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "RegisterForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Cờ vua & Caro Online — Đăng ký";
            this.Load += new System.EventHandler(this.RegisterForm_Load);
            this.Resize += new System.EventHandler(this.RegisterForm_Resize);
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
        private System.Windows.Forms.Label lblDisplay;
        private System.Windows.Forms.TextBox txtDisplay;
        private System.Windows.Forms.Label lblPass;
        private System.Windows.Forms.TextBox txtPass;
        private System.Windows.Forms.Label lblConfirm;
        private System.Windows.Forms.TextBox txtConfirm;
        private System.Windows.Forms.CheckBox chkShowPassword;
        private System.Windows.Forms.Label lblError;
        private System.Windows.Forms.Button btnRegister;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnThemeToggle;
    }
}
