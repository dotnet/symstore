using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.SymbolStore.Client
{
    public class SymbolCache
    {
        private string _location;

        public SymbolCache(string location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            if (string.IsNullOrWhiteSpace(location))
                throw new ArgumentException($"Argument '{nameof(location)}' cannot be an empty string.");

            _location = location;
            if (!Directory.Exists(_location))
                Directory.CreateDirectory(location);
        }

        #region PDB Functions
        public string GetPdbFromCache(string pdbName, Guid guid, int age)
        {
            pdbName = Path.GetFileName(pdbName);
            string fullPath = GetCacheLocation(pdbName, guid, age);
            return GetFile(fullPath);
        }

        public string StorePdb(Stream stream, string pdbName, Guid guid, int age)
        {
            pdbName = Path.GetFileName(pdbName);
            if (string.IsNullOrWhiteSpace(pdbName))
                throw new ArgumentException($"Invalid PDB filename {pdbName}.");

            string fullPath = GetCacheLocation(pdbName, guid, age);
            return StoreFile(stream, fullPath);
        }
        
        public bool RemovePdbFromCache(string pdbName, Guid guid, int age)
        {
            pdbName = Path.GetFileName(pdbName);
            string fullPath = GetCacheLocation(pdbName, guid, age);

            return RemoveFromCache(fullPath);
        }
        #endregion

        #region PEFiles
        public string GetPEFileFromCache(string filename, int timestamp, int filesize)
        {
            filename = Path.GetFileName(filename);
            string fullPath = GetCacheLocation(filename, timestamp, filesize);
            return GetFile(fullPath);
        }

        public string StorePEFile(Stream stream, string filename, int timestamp, int filesize)
        {
            filename = Path.GetFileName(filename);
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException($"Invalid PE file name {filename}.");

            string fullPath = GetCacheLocation(filename, timestamp, filesize);
            return StoreFile(stream, fullPath);
        }
        
        public bool RemovePEFileFromCache(string filename, int timestamp, int filesize)
        {
            filename = Path.GetFileName(filename);
            string fullPath = GetCacheLocation(filename, timestamp, filesize);

            return RemoveFromCache(fullPath);
        }
        #endregion

        #region Helpers
        private static string GetFile(string fullPath)
        {
            FileInfo fi = new FileInfo(fullPath);

            // Locking the FileSemaphore here ensures that we are done copying the file
            // in case another thread or process is currently writing to it.
            if (fi.Exists)
                using (FileSemaphore.LockFile(fullPath))
                    return fullPath;

            return null;
        }

        private static string StoreFile(Stream stream, string fullPath)
        {
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);

            using (FileSemaphore.LockFile(fullPath))
            {
                try
                {
                    using (FileStream fs = File.OpenWrite(fullPath))
                        stream.CopyTo(fs);
                }
                catch
                {
                    try
                    {
                        if (File.Exists(fullPath))
                            File.Delete(fullPath);
                    }
                    catch
                    {
                    }

                    throw;
                }
            }

            return fullPath;
        }

        private static bool RemoveFromCache(string fullPath)
        {
            if (!File.Exists(fullPath))
                return true;

            try
            {
                // Lock the file to ensure it's done copying in another thread/process
                using (FileSemaphore.LockFile(fullPath))
                    File.Delete(fullPath);

                return true;
            }
            catch
            {
                return File.Exists(fullPath);
            }
        }

        private string GetCacheLocation(string filename, int timestamp, int imagesize)
        {
            string indexPath = StoreQueryBuilder.GetPEFileIndexPath(filename, timestamp, imagesize);
            if (Path.DirectorySeparatorChar != '/')
                indexPath.Replace('/', Path.DirectorySeparatorChar);

            return Path.Combine(_location, indexPath);
        }

        private string GetCacheLocation(string pdbSimpleName, Guid guid, int age)
        {
            string indexPath = StoreQueryBuilder.GetWindowsPdbQueryString(pdbSimpleName, guid, age);
            if (Path.DirectorySeparatorChar != '/')
                indexPath.Replace('/', Path.DirectorySeparatorChar);

            return Path.Combine(_location, indexPath);
        }
        #endregion
    }
}
