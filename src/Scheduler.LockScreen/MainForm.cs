using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Scheduler.LockScreen;

public partial class MainForm : Form
{
    // 引入 LockWorkStation 函数
    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    public MainForm()
    {
        InitializeComponent();

        LockWorkStation();
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        Close();
    }
}
