using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HeavyDuck.Dnd.Forms
{
    public partial class InsiderLoginDialog : Form
    {
        public InsiderLoginDialog()
        {
            InitializeComponent();
        }

        public string Email
        {
            get { return email_box.Text; }
            set { email_box.Text = value; }
        }

        public string Password
        {
            get { return password_box.Text; }
            set { password_box.Text = value; }
        }
    }
}
