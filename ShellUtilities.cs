/*
 * [File purpose]
 * Author: Phillip Piper
 * Date: 1 May 2007 7:44 PM
 * 
 * CHANGE LOG:
 * 2009-07-08  JPP  Don't cache the image collections
 * 1 May 2007  JPP  Initial Version
 */

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BrightIdeasSoftware;
using System.IO;

namespace KevinHelper
{
    /// <summary>
    /// This helper class allows listviews and tree views to use image from the system image list.
    /// </summary>
    /// <remarks>Instances of this helper class know how to retrieve icon from the Windows shell for
    /// a given file path. These icons are then added to the imagelist on the given control. ListViews need 
    /// special handling since they have two image lists which need to be kept in sync.</remarks>
    public class SysImageListHelper
    {
        protected ImageList.ImageCollection SmallImageCollection
        {
            get
            {
                if (this.listView != null)
                    return this.listView.SmallImageList.Images;
                return null;
            }
        }
        protected ImageList SmallImageList
        {
            get
            {
                if (this.listView != null)
                    return this.listView.SmallImageList;
                return null;
            }
        }

        /// <summary>
        /// Create a SysImageListHelper that will fetch images for the given listview control.
        /// </summary>
        /// <param name="listView">The listview that will use the images</param>
        /// <remarks>Listviews manage two image lists, but each item can only have one image index.
        /// This means that the image for an item must occur at the same index in the two lists. 
        /// SysImageListHelper instances handle this requirement. However, if the listview already
        /// has image lists installed, they <b>must</b> be of the same length.</remarks>
        public SysImageListHelper(ObjectListView listView)
        {
            if (listView.SmallImageList == null)
            {
                listView.SmallImageList = new ImageList();
                listView.SmallImageList.ColorDepth = ColorDepth.Depth32Bit;
                listView.SmallImageList.ImageSize = new Size(16, 16);
            }

            this.listView = listView;
        }
        protected ObjectListView listView;

        /// <summary>
        /// Return the index of the image that has the Shell Icon for the given file/directory.
        /// </summary>
        /// <param name="path">The full path to the file/directory</param>
        /// <returns>The index of the image or -1 if something goes wrong.</returns>
        public int GetImageIndex(string path, bool? isFolder = null)
        {
            if (isFolder == true || (isFolder == null && Directory.Exists(path)))
                path = Environment.SystemDirectory; // optimization! give all directories the same image
            else
                if (Path.HasExtension(path))
                path = Path.GetExtension(path);

            if (this.SmallImageCollection.ContainsKey(path))
                return this.SmallImageCollection.IndexOfKey(path);

            try
            {
                this.AddImageToCollection(path, ShellUtilities.GetFileIcon(path, true, true));
            }
            catch (ArgumentNullException)
            {
                return -1;
            }

            return this.SmallImageCollection.IndexOfKey(path);
        }

        private void AddImageToCollection(string key, Icon image)
        {
            if (SmallImageList == null)
                return;

            if (SmallImageList.ImageSize == image.Size)
            {
                SmallImageList.Images.Add(key, image);
                return;
            }

            using (Bitmap imageAsBitmap = image.ToBitmap())
            {
                Bitmap bm = new Bitmap(SmallImageList.ImageSize.Width, SmallImageList.ImageSize.Height);
                Graphics g = Graphics.FromImage(bm);
                g.Clear(SmallImageList.TransparentColor);
                Size size = imageAsBitmap.Size;
                int x = Math.Max(0, (bm.Size.Width - size.Width) / 2);
                int y = Math.Max(0, (bm.Size.Height - size.Height) / 2);
                g.DrawImage(imageAsBitmap, x, y, size.Width, size.Height);
                SmallImageList.Images.Add(key, bm);
            }
        }
    }

    /// <summary>
    /// ShellUtilities contains routines to interact with the Windows Shell.
    /// </summary>
    public static class ShellUtilities
    {
        /// <summary>
        /// Return the icon for the given file/directory.
        /// </summary>
        /// <param name="path">The full path to the file whose icon is to be returned</param>
        /// <param name="isSmallImage">True if the small (16x16) icon is required, otherwise the 32x32 icon will be returned</param>
        /// <param name="useFileType">If this is true, only the file extension will be considered</param>
        /// <returns>The icon of the given file, or null if something goes wrong</returns>
        public static Icon GetFileIcon(string path, bool isSmallImage, bool useFileType)
        {
            int flags = SHGFI_ICON;
            if (isSmallImage)
                flags |= SHGFI_SMALLICON;

            int fileAttributes = 0;
            if (useFileType)
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
                if (System.IO.Directory.Exists(path))
                    fileAttributes = FILE_ATTRIBUTE_DIRECTORY;
                else
                    fileAttributes = FILE_ATTRIBUTE_NORMAL;
            }

            SHFILEINFO shfi = new SHFILEINFO();
            IntPtr result = ShellUtilities.SHGetFileInfo(path, fileAttributes, out shfi, Marshal.SizeOf(shfi), flags);
            if (result.ToInt32() == 0)
                return null;
            else
                return Icon.FromHandle(shfi.hIcon);
        }

        #region Native methods

        private const int SHGFI_ICON = 0x00100;     // get icon
        private const int SHGFI_SMALLICON = 0x00001;     // get small icon
        private const int SHGFI_USEFILEATTRIBUTES = 0x00010;     // use passed dwFileAttribute

        private const int FILE_ATTRIBUTE_NORMAL = 0x00080;     // Normal file
        private const int FILE_ATTRIBUTE_DIRECTORY = 0x00010;     // Directory

        private const int MAX_PATH = 260;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public int dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, int dwFileAttributes, out SHFILEINFO psfi, int cbFileInfo, int uFlags);

        #endregion
    }
}