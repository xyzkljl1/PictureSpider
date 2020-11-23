using System;
using System.Windows.Forms;

namespace PixivAss
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (BlockSyncContext.Enter())
                Application.Run(new MainWindow());
        }
    }
}
