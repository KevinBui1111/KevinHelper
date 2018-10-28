using System;
using System.Collections.Generic;
using System.Text;

namespace KevinHelper
{
    static class FileUtils
    {
        public static string GetRelativePath(string filespec, string folder)
        {
            if (string.IsNullOrEmpty(folder)) return filespec;

            if (filespec.StartsWith(folder))
                return filespec.Substring(folder.Length + (folder.EndsWith(@"\") ? 0 : 1));
            else return "error";
        }
    }
}
