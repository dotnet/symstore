// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NugetSymbolServer.Service.Models
{
    public class FileReference : IDisposable
    {
        FileStoreEntry _entry;

        public FileReference(FileStoreEntry entry)
        {
            _entry = entry;
        }

        public string FilePath { get { return _entry.FilePath; } }

        public FileReference Clone()
        {
            return _entry.AddRef();
        }

        public void Dispose()
        {
            _entry.Release();
            _entry = null;
        }
    }

    public class FileStoreEntry
    {
        int _refCount;

        public FileStoreEntry(string filePath)
        {
            FilePath = filePath;
            _refCount = 0;
        }

        public string FilePath { get; private set; }

        public FileReference AddRef()
        {
            lock (this)
            {
                _refCount++;
                return new FileReference(this);
            }
        }

        public void Release()
        {
            lock (this)
            {
                _refCount--;
                if (_refCount == 0)
                {
                    FileUnreferenced?.Invoke(this, null);
                }
            }
        }

        public event EventHandler FileUnreferenced;
    }

    public class FileStoreOptions
    {
        public string RootPath { get; set; }
    }

    public class FileStore : IFileStore, IDisposable
    {
        string _rootPath;
        Dictionary<string, FileStoreEntry> _entries;

        public FileStore(IOptions<FileStoreOptions> options)
        {
            _rootPath = options.Value.RootPath;
            _entries = new Dictionary<string, FileStoreEntry>();
        }

        public async Task<FileReference> AddFile(Stream fileData, string relativeFilePath)
        {
            string finalFilePath = Path.GetFullPath(Path.Combine(_rootPath, relativeFilePath));
            FileStoreEntry entry = new FileStoreEntry(finalFilePath);
            lock (this)
            {
                if (_entries.ContainsKey(finalFilePath))
                {
                    throw new Exception("Files can't be added to the store more than once");
                }
                entry.FileUnreferenced += Entry_FileUnreferenced;
                _entries.Add(finalFilePath, entry);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(finalFilePath));
            for(int i = 0; i < 10; i++)
            {
                try
                {
                    using (FileStream cachedFileStream = File.OpenWrite(finalFilePath))
                    {
                        await fileData.CopyToAsync(cachedFileStream);
                    }
                }
                catch(IOException)
                {
                    if(i==9) // give up eventually
                    {
                        throw; 
                    }
                }
            }
            
            return entry.AddRef();
        }

        private void Entry_FileUnreferenced(object sender, EventArgs e)
        {
            RemoveFile((FileStoreEntry)sender);
        }

        private void RemoveFile(FileStoreEntry entry)
        {
            _entries.Remove(entry.FilePath);
            File.Delete(entry.FilePath);
        }

        public void Dispose()
        {
            lock(this)
            {
                foreach(FileStoreEntry entry in _entries.Values.ToArray())
                {
                    RemoveFile(entry);
                }
            }
        }
    }
}
