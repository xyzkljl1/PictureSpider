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
            try
            {

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (BlockSyncContext.Enter())
                    Application.Run(new MainWindow());
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("捕获到未处理异常：{0}\r\n异常信息：{1}\r\n异常堆栈：{2}", ex.GetType(), ex.Message, ex.StackTrace));
            }
        }
    }
}
