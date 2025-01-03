﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    public class Util
    {
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
    }
}
