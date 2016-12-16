﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KevinHelper
{
    [Serializable()]
    public class KFile
    {
        public string Name { get; set; }
        public long? Size { get; set; }
        public bool IsFolder { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Path { get; set; }
        public string Checksum { get; set; }
        public int CountFile { get; set; }

        List<KFile> _children;
        public List<KFile> Children
        {
            get
            {
                _children = _children ?? new List<KFile>();
                return _children;
            }
            set
            {
                _children = value;
            }
        }

        public KFile Parent { get; set; }
        public bool Highlight { get; set; }

    }
    public class JobWalkDirectories
    {
        public List<string> exceptionFol { get; set; }
        public List<SearchPath> searchFols { get; set; }
        public bool m_bSystem { get; set; }
        public string[] m_ext { get; set; }

        public Action<string> ProgressChanged
        {
            set
            {
                progressIndicator = new Progress<string>(value);
            }
        }
        public Action JobCompleted;

        public event Action<JobWalkDirectories, FileInfo> GotAFile;
        public event Action<JobWalkDirectories, string> GotAFolder;

        private ManualResetEvent _pauseEvent = new ManualResetEvent(true);
        private IProgress<string> progressIndicator;
        private CancellationTokenSource cancellationTokenSource;

        public JobWalkDirectories()
        {
            exceptionFol = new List<string>();
            m_ext = new string[] { ".*" };
            m_bSystem = true;
        }

        public async Task WalkThroughAsync()
        {
            cancellationTokenSource = new CancellationTokenSource();
            await Task.Run(() =>
            {
                foreach (SearchPath item in searchFols)
                {
                    if (item.m_subFolder)
                        WalkDirectories(item.folder, 0);
                    else
                        WalkDirectories(item.folder);
                }
            });

            if (JobCompleted != null) JobCompleted();
        }
        public void Cancel()
        {
            cancellationTokenSource.Cancel();
            Resume();
        }
        public void Pause()
        {
            _pauseEvent.Reset();
        }
        public void Resume()
        {
            _pauseEvent.Set();
        }

        /// <summary>
        /// Get all file in dir.
        /// </summary>
        /// <param name="subFolder">Specify if subfolder can be gotten</param>
        /// <param name="exceptionFol"> list of folders that can be omitted</param>
        private void WalkDirectories(string sPath, int level)
        {
            _pauseEvent.WaitOne();
            if (cancellationTokenSource.Token.IsCancellationRequested) return;

            if (GotAFolder != null)
                GotAFolder(this, sPath);

            bool valid = true;

            // Get Sub Folders.
            try
            {
                if (level < 3 && progressIndicator != null) progressIndicator.Report(sPath);

                String[] subFolders = Directory.GetDirectories(sPath);
                foreach (string dir in subFolders)
                {
                    valid = (File.GetAttributes(dir) & FileAttributes.System) == 0 || this.m_bSystem;
                    if (!valid) continue;

                    foreach (string item in exceptionFol)
                        if (item == dir)
                        {
                            valid = false;
                            break;
                        }

                    if (valid) WalkDirectories(dir, level + 1);
                }
                // Get files in  folder.
                WalkDirectories(sPath);
            }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Get all file in dir, only top-level.
        /// </summary>
        private void WalkDirectories(string sPath)
        {
            _pauseEvent.WaitOne();
            if (cancellationTokenSource.Token.IsCancellationRequested) return;

            DirectoryInfo dir = new DirectoryInfo(sPath);
            bool matchfiletype = false;

            foreach (FileInfo file in dir.GetFiles())
            {
                if ((file.Attributes & FileAttributes.System) != 0 && !this.m_bSystem) continue;

                matchfiletype = false;
                // Check where file is matched with pattern.
                foreach (string ext in this.m_ext)
                {
                    if (ext == ".*" || ext.Equals(file.Extension, StringComparison.OrdinalIgnoreCase))
                    {
                        matchfiletype = true;
                        break;
                    }
                }

                if (matchfiletype && GotAFile != null)
                    GotAFile(this, file);
            }
        }

        public class SearchPath
        {
            public string folder { get; set; }
            public bool m_subFolder { get; set; }
        }

        public static async Task<KFile> LoadFolderAsync(string sPath)
        {
            return await Task.Run(() => LoadFolder(sPath));
        }
        private static KFile LoadFolder(string sPath)
        {
            KFile file = new KFile
            {
                Name = Path.GetFileName(sPath),
                Path = sPath,
                IsFolder = true,
                CreatedDate = Directory.GetCreationTime(sPath),
                ModifiedDate = Directory.GetLastWriteTime(sPath)
            };

            try
            {
                foreach (string dir in Directory.GetDirectories(sPath))
                {
                    var child = LoadFolder(dir);
                    child.Parent = file;
                    file.Children.Add(child);
                }
                // Get files in folder.
                file.Children.AddRange(
                    Directory.GetFiles(sPath).Select(f => new KFile
                    {
                        Name = Path.GetFileName(f),
                        Path = f,
                        IsFolder = false,
                        CreatedDate = File.GetCreationTime(f),
                        ModifiedDate = File.GetLastWriteTime(f),
                        CountFile = 1,
                        Parent = file,
                        Size = new FileInfo(f).Length
                    })
                );

                file.Size = file.Children.Sum(i => i.Size);
                file.CountFile = file.Children.Sum(i => i.CountFile);
            }
            catch (UnauthorizedAccessException) { }

            return file;
        }
    }

}
