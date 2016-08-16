using System;
using System.IO;
using System.Threading;

namespace Microsoft.SymbolStore.Client
{
    public static class FileSemaphore
    {
        class LockedFile : IDisposable
        {
            private bool _disposed = false;
            private string _lockPath;
            private FileStream _fileStream;

            public LockedFile(string lockPath, FileStream fs)
            {
                _lockPath = lockPath;
                _fileStream = fs;
            }

            #region IDisposable Support
            protected virtual void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        _fileStream.Dispose();
                    }

                    try
                    {
                        File.Delete(_lockPath);
                    }
                    catch (IOException)
                    {
                    }
                    
                    _disposed = true;
                }
            }
            
            ~LockedFile()
            {
               Dispose(false);
            }
            
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            #endregion

        }

        public static IDisposable TryLockFile(string fullPath)
        {
            DirectoryInfo di = new FileInfo(fullPath).Directory;
            if (!di.Exists)
                throw new DirectoryNotFoundException(di.FullName);

            string lockName = fullPath + ".sem";
            while (true)
            {
                try
                {
                    FileStream fs = File.Open(lockName, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                    return new LockedFile(lockName, fs);
                }
                catch (IOException)
                {
                    return null;
                }
            }
        }

        public static IDisposable LockFile(string fullPath)
        {
            IDisposable result = null;
            while ((result = TryLockFile(fullPath)) == null)
                Thread.Sleep(100);

            return result;
        }
    }
}
