using Org.BouncyCastle.Bcpg.OpenPgp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    public static class Util
    {

        public static T Next<T>(this T src) where T : struct
        {
            if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argument {0} is not an Enum", typeof(T).FullName));

            T[] Arr = (T[])Enum.GetValues(src.GetType());
            int j = Array.IndexOf<T>(Arr, src) + 1;
            return j >= Arr.Length ? Arr.First() : Arr[j];
        }

        public static int MainThreadId = -1;
        public static void SetMainThreadId()
        {
            // 主线程似乎总是1？
            MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }
        // 注意同一个context不代表同一个线程，只有主线程可以固定用threadId判断
        public static bool IsMainThread()
        {
            return System.Threading.Thread.CurrentThread.ManagedThreadId == MainThreadId;
        }

        //返回扩展名
        public static string GetExtFromURL(string url)
        {
            var uri=new Uri(url);
            var filename = uri.Segments.Last();
            var ext = new FileInfo(filename).Extension;
            if(ext.StartsWith("."))
                ext = ext.Substring(1);
            return ext;
        }
        //string -> bytes,每个字符转2个byte
        //C#中的string是utf16，即由双字节char组成
        //对于umappable character如0xd83d,在Bytes/String互转时会自动被替换为0xfffd(�)，EF中以varchar(string)/long text(char[])存入时也会被自动替换，非常坑爹
        //而Directionary/File获取本地文件时会返回原本的字符，操作文件时也必须使用原字符
        //因此存储文件名时，用该函数转成byte[](不替换非法字符)，读出来时再转回
        public static byte[] String2Bytes(string str)
        {
            byte[] ret = new byte[str.Length * 2];
            for(int i = 0; i < str.Length; i++)
            {
                var b = BitConverter.GetBytes(str[i]);
                ret[i * 2] = b[0];
                ret[i * 2 + 1] = b[1];
            }
            return ret;
        }
        public static string Bytes2String(byte[] bytes)
        {
            var ret = new char[bytes.Length / 2];
            for (int i = 0; i < bytes.Length; i += 2)
                ret[i/2] += BitConverter.ToChar(bytes, i);
            return new string(ret);
        }
        public static void ClearEmptyFolders(string dir)
        {
            int ct = 0;
            do
            {
                ct = 0;
                foreach (var subdir in Directory.EnumerateDirectories(dir,"*",new EnumerationOptions { RecurseSubdirectories = true, ReturnSpecialDirectories=false }).ToList())
                    if (Directory.GetFileSystemEntries(subdir).Length == 0)
                    {
                        Directory.Delete(subdir);
                        ct++;
                    }
            }
            while (ct > 0);
        }
        public static void ReplaceInvalidCharInFilename(ref string filename)
        {
            //注意和GetInvalidPathChars的区别
            foreach (var c in Path.GetInvalidFileNameChars())
                filename = filename.Replace(c, '_');
            filename = filename.Trim(' ');
        }
        // 不会修改this的值！！
        public static string ReplaceInvalidCharInFilenameWithReturnValue(this string filename)
        {
            ReplaceInvalidCharInFilename(ref filename);
            return filename;
        }
        public static void TouchDir(params string[] dirs)
        {
            foreach (var dir in dirs)
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
        }

        public static HashSet<string> imageExtensions = new HashSet<string>{
                ".jpeg", ".jpg", ".png", ".gif",".webp",".bmp",".jfif",".jpe",".avif"
            };
        public static bool IsImage(this string ext)
        {
            return imageExtensions.Contains(ext.ToLower());
        }

        public static HashSet<string> videoExtensions = new HashSet<string>{
                ".avi", ".mp4", ".divx", ".wmv",".rmvb",".mkv"
            };
        public static bool IsVideo(this string ext)
        {
            return videoExtensions.Contains(ext.ToLower());
        }

        public static HashSet<string> zipExtensions = new HashSet<string>{
                ".zip", ".rar", ".gz", ".7z",".tar"
            };
        public static bool IsZip(this string ext)
        {
            return zipExtensions.Contains(ext.ToLower());
        }
    }
}
