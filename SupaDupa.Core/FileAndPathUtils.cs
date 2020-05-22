using System.Collections.Generic;
using System.IO;

namespace SupaDupa.Core
{
    static class FileAndPathUtils
    {
        public static IEnumerable<FileInfo[]> RecurseEnumerateFileBlock(IEnumerable<string> rootFolders)
        {
	        foreach (var rootFolder in rootFolders)
	        {
		        foreach (var fileBlock in RecurseEnumerateFileBlock(new DirectoryInfo(rootFolder)))
		        {
			        yield return fileBlock;
		        }
	        }
        }

        private static IEnumerable<FileInfo[]> RecurseEnumerateFileBlock(DirectoryInfo root)
        {
	        FileInfo[] files;
	        try
	        {
		        files = root.GetFiles();
	        }
	        catch
	        {
		        yield break;
	        }

	        if (files.Length > 0)
	        {
		        yield return files;
	        }

	        IEnumerable<DirectoryInfo> dirs;
	        try
	        {
		        dirs = root.EnumerateDirectories();
	        }
	        catch
	        {
		        yield break;
	        }

	        foreach (var directoryInfo in dirs)
	        {
		        foreach (var fInfo in RecurseEnumerateFileBlock(directoryInfo))
		        {
			        yield return fInfo;
		        }
	        }
        }
    }
}
