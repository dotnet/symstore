// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NugetSymbolServer.Service.Models
{
    public class Package : IDisposable
    {
        Dictionary<string, FileReference> _files;

        public async Task Init(FileStream packageStream, string packageRelativeDir, IFileStore cachedFileStorage)
        {
            ZipArchive archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
            _files = new Dictionary<string, FileReference>();
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if(entry.FullName.EndsWith("/"))
                {
                    continue; // skip directories
                }
                using (Stream zipFileStream = entry.Open())
                {
                    var fileStorePath = Path.Combine(packageRelativeDir, entry.FullName);
                    FileReference fileRef = await cachedFileStorage.AddFile(zipFileStream, fileStorePath);
                    _files.Add(entry.FullName, fileRef);
                }
            }
        }

        public IEnumerable<FileReference> Files { get { return _files.Values; } }

        public FileReference GetFile(string relativePath)
        {
            FileReference fileRef;
            if(_files.TryGetValue(relativePath, out fileRef))
            {
                return fileRef.Clone();
            }
            return null;
        }

        public void Dispose()
        {
            foreach(FileReference fileRef in _files.Values)
            {
                fileRef.Dispose();
            }
            _files = null;
        }
    }

    public class PackageStore : IPackageStore, IDisposable
    {
        Dictionary<string, Package> _packages = new Dictionary<string, Package>();
        IFileStore _cachedFileStorage;
        int _counter;

        public PackageStore(IFileStore cachedFileStorage)
        {
            _cachedFileStorage = cachedFileStorage;
            _counter = 0;
        }

        private string GetUniquePackageCacheStorageDir(string packageFilePath)
        {
            int uniqueId = Interlocked.Increment(ref _counter);
            return Path.Combine(Path.GetFileNameWithoutExtension(packageFilePath), uniqueId.ToString());
        }

        public async Task AddPackage(string packageFilePath)
        {
            Package p = new Package();
            try
            {
                using (FileStream packageFileStream = File.OpenRead(packageFilePath))
                {
                    await p.Init(packageFileStream, GetUniquePackageCacheStorageDir(packageFilePath), _cachedFileStorage);
                }
                lock (this)
                {
                    if (_packages.ContainsKey(packageFilePath))
                    {
                        throw new Exception("Package at the same path can't be added twice");
                    }
                    OnPackageAdded(p);
                    _packages.Add(packageFilePath, p);
                    p = null;
                }
            }
            finally
            {
                if(p != null)
                {
                    p.Dispose();
                }
            }
        }

        protected virtual void OnPackageAdded(Package p) { }

        public void RemovePackage(string packageFilePath)
        {
            lock(this)
            {
                Package p;
                if(_packages.TryGetValue(packageFilePath, out p))
                {
                    _packages.Remove(packageFilePath);
                    OnPackageRemoved(p);
                    p.Dispose();
                }
            }
        }

        protected virtual void OnPackageRemoved(Package p) { }

        public void Dispose()
        {
            lock(this)
            {
                if (_packages != null)
                {
                    foreach (Package p in _packages.Values)
                    {
                        p.Dispose();
                    }
                }
                _packages = null;
                _cachedFileStorage = null;
            }
        }
    }
}
