// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using dotnet.symbols.Properties;
using Microsoft.SymbolStore;
using Microsoft.SymbolStore.KeyGenerators;
using Microsoft.SymbolStore.SymbolStores;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace dotnet.symbols
{
    public class Program
    {
        struct ServerInfo
        {
            public Uri Uri;
            public string PersonalAccessToken;
            public bool InternalSymwebServer;
        }

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                goto usage;
            }
            var inputFilePaths = new List<string>();
            var symbolServers = new List<ServerInfo>();
            var cacheDirectories = new List<string>();
            var tracer = new Tracer();
            string outputDirectory = null;
            bool subdirectories = false;
            bool symbolsOnly = false;
            bool forceWindowsPdbs = false;

            for (int i = 0; i < args.Length; i++)
            {
                string personalAccessToken = null;
                Uri uri;
                switch (args[i])
                {
                    case "-ms":
                    case "--microsoft-symbol-server":
                        Uri.TryCreate("http://msdl.microsoft.com/download/symbols/", UriKind.Absolute, out uri);
                        symbolServers.Add(new ServerInfo {Uri = uri, PersonalAccessToken = null});
                        break;

                    case "-mi":
                    case "--ms-internal-server":
                        Uri.TryCreate("http://symweb.corp.microsoft.com/", UriKind.Absolute, out uri);
                        symbolServers.Add(new ServerInfo {Uri = uri, PersonalAccessToken = null, InternalSymwebServer = true});
                        break;

                    case "-as":
                    case "--authenticated-server-path":
                        if (++i < args.Length)
                            personalAccessToken = args[i];
                        else
                            goto usage;

                        if (string.IsNullOrEmpty(personalAccessToken))
                        {
                            tracer.Error("No personal access token option");
                            goto usage;
                        }
                        goto case "--server-path";

                    case "-s":
                    case "--server-path":
                        if (++i < args.Length)
                        {
                            // Make sure the server Uri ends with "/"
                            string serverPath = args[i].TrimEnd('/') + '/';
                            if (!Uri.TryCreate(serverPath, UriKind.Absolute, out uri) || uri.IsFile)
                            {
                                tracer.Error(Resources.InvalidServerPath, args[i]);
                                goto usage;
                            }
                            Uri.TryCreate(serverPath, UriKind.Absolute, out uri);
                            symbolServers.Add(new ServerInfo {Uri = uri, PersonalAccessToken = personalAccessToken});
                        }
                        else
                            goto usage;
                        break;

                    case "-o":
                    case "--output-directory":
                        if (++i < args.Length)
                            outputDirectory = args[i];
                        else
                            goto usage;
                        break;

                    case "-c":
                    case "--cache-directory":
                        if (++i < args.Length)
                            cacheDirectories.Add(args[i]);
                        else
                            goto usage;
                        break;

                    case "-d":
                    case "--diag":
                        tracer.Enabled = true;
                        tracer.EnabledVerbose = true;
                        break;

                    case "-y":
                    case "--symbols-only":
                        symbolsOnly = true;
                        break;

                    case "-r":
                    case "--recurse-subdirectories":
                        subdirectories = true;
                        break;
                    
                    case "-w":
                    case "--force-windows-pdbs":
                        forceWindowsPdbs = true;
                        break;

                    case "-h":
                    case "-?":
                    case "--help":
                        goto usage;

                    default:
                        string inputFile = args[i];
                        if (inputFile.StartsWith("-") || inputFile.StartsWith("--"))
                        {
                            tracer.Error(Resources.InvalidCommandLineOption, inputFile);
                            goto usage;
                        }
                        inputFilePaths.Add(inputFile);
                        break;
                }
            }
            // Default to public Microsoft symbol server
            if (symbolServers.Count == 0)
            {
                Uri.TryCreate("http://msdl.microsoft.com/download/symbols/", UriKind.Absolute, out Uri uri);
                symbolServers.Add(new ServerInfo {Uri = uri, PersonalAccessToken = null});
            }
            foreach (ServerInfo server in symbolServers)
            {
                tracer.WriteLine(Resources.DownloadFromUri, server.Uri);
            }
            if (outputDirectory != null)
            {
                Directory.CreateDirectory(outputDirectory);
                tracer.WriteLine(Resources.WritingFilesToOutput, outputDirectory);
            }
            Program program = new Program(tracer, subdirectories, inputFilePaths, cacheDirectories, symbolServers, outputDirectory);
            try
            {
                program.DownloadFiles(symbolsOnly, forceWindowsPdbs).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                tracer.Error("{0}{1}", ex.Message, ex.InnerException != null ? " -> " + ex.InnerException.Message : "");
            }
            return;

        usage:
            PrintUsage();
        }

        private static void PrintUsage()
        {
            Console.WriteLine(@"
Usage: dotnet symbols [options] <FILES>

Arguments:
  <FILES>   List of files. Can contain wildcards.

Options:
  -ms, --microsoft-symbol-server                        Add 'http://msdl.microsoft.com/download/symbols' symbol server path (default).
  -mi, --ms-internal-server                             Add 'http://symweb.corp.microsoft.com' symbol server path.
  -s, --server-path <symbol server path>                Add a http server path.
  -as, --authenticated-server-path <pat> <server path>  Add a http PAT authenticated server path.
  -c, --cache-directory <file cache directory>          Add a cache directory.
  -o, --output-directory <output directory>             Set the output directory. Otherwise, write next to the input file (default).
  -r, --recurse-subdirectories                          Process input files in all subdirectories.
  -y, --symbols-only                                    Download only the symbol files.
  -w, --force-windows-pdbs                              Force downloading of the Windows PDBs.
  -d, --diag                                            Enable diagnostic output.
  -h, --help                                            Show help information.");
        }

        private readonly IEnumerable<string> _inputFilePaths;
        private readonly IEnumerable<string> _cacheDirectories;
        private readonly IEnumerable<ServerInfo> _symbolServers;
        private readonly string _outputDirectory;
        private readonly bool _subdirectories;
        private readonly ITracer _tracer;

        Program(ITracer tracer, bool subdirectories, IEnumerable<string> inputFilePaths, IEnumerable<string> cacheDirectories, IEnumerable<ServerInfo> symbolServers, string outputDirectory)
        {
            _tracer = tracer;
            _subdirectories = subdirectories;
            _inputFilePaths = inputFilePaths;
            _cacheDirectories = cacheDirectories;
            _symbolServers = symbolServers;
            _outputDirectory = outputDirectory;
        }

        internal async Task DownloadFiles(bool symbolsOnly, bool forceWindowsPdbs)
        {
            using (SymbolStore symbolStore = BuildSymbolStore())
            {
                KeyTypeFlags flags;
                if (symbolsOnly)
                {
                    flags = KeyTypeFlags.SymbolKey;
                }
                else
                {
                    flags = KeyTypeFlags.IdentityKey | KeyTypeFlags.SymbolKey | KeyTypeFlags.ClrKeys;
                }
                if (forceWindowsPdbs)
                {
                    flags |= KeyTypeFlags.ForceWindowsPdbs;
                }
                foreach (SymbolStoreKeyWrapper wrapper in GetKeys(flags).Distinct())
                {
                    SymbolStoreKey key = wrapper.Key;
                    if (symbolStore != null)
                    {
                        using (SymbolStoreFile symbolFile = await symbolStore.GetFile(key, CancellationToken.None))
                        {
                            if (symbolFile != null)
                            {
                                await WriteFile(symbolFile, wrapper);
                            }
                        }
                    }
                }
            }
        }

        private SymbolStore BuildSymbolStore()
        {
            SymbolStore store = null;

            foreach (ServerInfo server in _symbolServers.Reverse())
            {
                if (server.InternalSymwebServer)
                {
                    store = new SymwebHttpSymbolStore(_tracer, store, server.Uri, server.PersonalAccessToken);
                }
                else {
                    store = new HttpSymbolStore(_tracer, store, server.Uri, server.PersonalAccessToken);
                }
            }

            foreach (string cache in _cacheDirectories.Reverse())
            {
                store = new CacheSymbolStore(_tracer, store, cache);
            }

            return store;
        }

        class SymbolStoreKeyWrapper
        {
            public readonly SymbolStoreKey Key;
            public readonly string InputFile;

            internal SymbolStoreKeyWrapper(SymbolStoreKey key, string inputFile)
            {
                Key = key;
                InputFile = inputFile;
            }

            /// <summary>
            /// Returns the hash of the index.
            /// </summary>
            public override int GetHashCode()
            {
                return Key.GetHashCode();
            }

            /// <summary>
            /// Only the index is compared or hashed. The FileName is already
            /// part of the index.
            /// </summary>
            public override bool Equals(object obj)
            {
                var wrapper = (SymbolStoreKeyWrapper)obj;
                return Key.Equals(wrapper.Key);
            }
        }

        private IEnumerable<SymbolStoreKeyWrapper> GetKeys(KeyTypeFlags flags)
        {
            var inputFiles = _inputFilePaths.SelectMany((string file) => 
            {
                string directory = Path.GetDirectoryName(file);
                string pattern = Path.GetFileName(file);
                return Directory.EnumerateFiles(string.IsNullOrWhiteSpace(directory) ? "." : directory, pattern,
                    _subdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            });

            if (!inputFiles.Any())
            {
                throw new ArgumentException(Resources.NoInputFiles);
            }

            foreach (string inputFile in inputFiles)
            {
                foreach (KeyGenerator generator in GetKeyGenerators(inputFile))
                {
                    foreach(var wrapper in generator.GetKeys(flags).Select((key) => new SymbolStoreKeyWrapper(key, inputFile)))
                    {
                        yield return wrapper;
                    }
                }
            }
        }

        private IEnumerable<KeyGenerator> GetKeyGenerators(string inputFile)
        {
            using (Stream inputStream = File.Open(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SymbolStoreFile file = new SymbolStoreFile(inputStream, inputFile);
                string extension = Path.GetExtension(inputFile);
                yield return new FileKeyGenerator(_tracer, file);
            }
        }

        private async Task WriteFile(SymbolStoreFile file, SymbolStoreKeyWrapper wrapper)
        {
            if (_outputDirectory != null) 
            {
                await WriteFileToDirectory(file.Stream, wrapper.Key.FullPathName, _outputDirectory);
            }
            else
            {
                await WriteFileToDirectory(file.Stream, wrapper.Key.FullPathName, Path.GetDirectoryName(wrapper.InputFile));
            }
        }

        private async Task WriteFileToDirectory(Stream stream, string fileName, string destinationDirectory)
        {
            stream.Position = 0;
            string destination = Path.Combine(destinationDirectory, Path.GetFileName(fileName));
            if (File.Exists(destination)) {
                _tracer.Warning(Resources.FileAlreadyExists, destination);
            }
            else
            {
                _tracer.WriteLine(Resources.WritingFile, destination);
                using (Stream destinationStream = File.OpenWrite(destination))
                {
                    await stream.CopyToAsync(destinationStream);
                }
            }
        }
    }
}
