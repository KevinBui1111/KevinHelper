using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace KevinHelper
{
    class ListComparer
    {
        public Func<IKParent, IKParent, int> compareFile { get; set; }
        public CancellationToken cancellationToken { get; set; }

        public IKParent CompareFolder(IKParent source, IKParent destination)
        {
            //string foldergroup = GetRelativePath(source, rootfolder);
            IKParent res = source.CreateNewInstance();
            CompareListChild(source, destination, true, res);
            CompareListChild(source, destination, false, res);

            return res;
        }
        void CompareListChild(IKParent source, IKParent destination, bool isFolder, IKParent res)
        {
            List<IKParent> srcSubFolders = source.Children.Where(c => c.IsFolder == isFolder).ToList();
            List<IKParent> desSubFolders = destination.Children.Where(c => c.IsFolder == isFolder).ToList();
            srcSubFolders.Sort();
            desSubFolders.Sort();

            #region Compare Folders.

            int iSrc = 0, iDes = 0, compare;
            while (iSrc < srcSubFolders.Count && iDes < desSubFolders.Count)
            {
                if (cancellationToken != null && cancellationToken.IsCancellationRequested) return;

                IKParent resItem;
                compare = srcSubFolders[iSrc].CompareTo(desSubFolders[iDes]);
                if (compare < 0) //src be add new.
                {
                    resItem = srcSubFolders[iSrc++];
                    resItem.operation = Operation.NEW;
                }
                else if (compare > 0) // des be delete.
                {
                    resItem = desSubFolders[iDes++];
                    resItem.operation = Operation.DELETE;
                }
                else
                {
                    if (isFolder)
                        resItem = CompareFolder(srcSubFolders[iSrc], desSubFolders[iDes]);
                    else if (compareFile != null)
                    {
                        int result = compareFile(srcSubFolders[iSrc], desSubFolders[iDes]);
                        resItem = srcSubFolders[iSrc];
                        resItem.operation = result == 0 ? Operation.NOCHANGE :
                            result == 1 ? Operation.CHANGED : Operation.UNKNOWN;
                    }
                    else
                    {
                        resItem = srcSubFolders[iSrc];
                        resItem.operation = Operation.NOCHANGE;
                    }

                    ++iSrc;
                    ++iDes;
                }

                if (res.operation == Operation.NOCHANGE && resItem.operation != Operation.NOCHANGE)
                    res.operation = Operation.CHANGED;

                resItem.Parent = res;
                res.Children.Add(resItem);
            }

            if (iSrc == srcSubFolders.Count) // add remain folder in snap2 to newfolders
                for (int i = iDes; i < desSubFolders.Count; ++i)
                {
                    desSubFolders[i].operation = Operation.DELETE;
                    desSubFolders[i].Parent = res;
                    res.Children.Add(desSubFolders[i]);
                    res.operation = Operation.CHANGED;
                }

            else // add remain folders in snap1 to deletefolders.
                for (int i = iSrc; i < srcSubFolders.Count; ++i)
                {
                    srcSubFolders[i].operation = Operation.NEW;
                    srcSubFolders[i].Parent = res;
                    res.Children.Add(srcSubFolders[i]);
                    res.operation = Operation.CHANGED;
                }

            #endregion
        }
    }
}