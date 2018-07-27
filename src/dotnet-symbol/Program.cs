// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using dotnet.symbol.Properties;
using Microsoft.SymbolStore;
using Microsoft.SymbolStore.KeyGenerators;
using Microsoft.SymbolStore.SymbolStores;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace dotnet.symbol
{
    public class Program
    {
        struct ServerInfo
        {
            public Uri Uri;
            public string PersonalAccessToken;
            public bool InternalSymwebServer;
        }

        private readonly List<string> InputFilePaths = new List<string>();
        private readonly List<string> CacheDirectories = new List<string>();
        private readonly List<ServerInfo> SymbolServers = new List<ServerInfo>();
        private string OutputDirectory;
        private bool Subdirectories;
        private bool Symbols;
        private bool Debugging;
        private bool Modules;
        private bool ForceWindowsPdbs;
        private ITracer Tracer;

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                goto usage;
            }
            var program = new Program();
            var tracer = new Tracer();
            program.Tracer = tracer;

            for (int i = 0; i < args.Length; i++)
            {
                string personalAccessToken = null;
                Uri uri;
                switch (args[i])
                {
                    case "--microsoft-symbol-server":
                        Uri.TryCreate("http://msdl.microsoft.com/download/symbols/", UriKind.Absolute, out uri);
                        program.SymbolServers.Add(new ServerInfo {Uri = uri, PersonalAccessToken = null});
                        break;

                    case "--internal-server":
                        Uri.TryCreate("http://symweb.corp.microsoft.com/", UriKind.Absolute, out uri);
                        program.SymbolServers.Add(new ServerInfo {Uri = uri, PersonalAccessToken = null, InternalSymwebServer = true});
                        break;

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
                            program.SymbolServers.Add(new ServerInfo {Uri = uri, PersonalAccessToken = personalAccessToken});
                        }
                        else
                            goto usage;
                        break;

                    case "-o":
                    case "--output":
                        if (++i < args.Length)
                            program.OutputDirectory = args[i];
                        else
                            goto usage;
                        break;

                    case "--cache-directory":
                        if (++i < args.Length)
                            program.CacheDirectories.Add(args[i]);
                        else
                            goto usage;
                        break;

                    case "--recurse-subdirectories":
                        program.Subdirectories = true;
                        break;

                    case "--modules":
                        program.Modules = true;
                        break;

                    case "--symbols":
                        program.Symbols = true;
                        break;

                    case "--debugging":
                        program.Debugging = true;
                        break;

                    case "--windows-pdbs":
                        program.ForceWindowsPdbs = true;
                        break;

                    case "-d":
                    case "--diagnostics":
                        tracer.Enabled = true;
                        tracer.EnabledVerbose = true;
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
                        program.InputFilePaths.Add(inputFile);
                        break;
                }
            }
            // Default to public Microsoft symbol server
            if (program.SymbolServers.Count == 0)
            {
                Uri.TryCreate("http://msdl.microsoft.com/download/symbols/", UriKind.Absolute, out Uri uri);
                program.SymbolServers.Add(new ServerInfo {Uri = uri, PersonalAccessToken = null});
            }
            foreach (ServerInfo server in program.SymbolServers)
            {
                tracer.WriteLine(Resources.DownloadFromUri, server.Uri);
            }
            if (program.OutputDirectory != null)
            {
                Directory.CreateDirectory(program.OutputDirectory);
                tracer.WriteLine(Resources.WritingFilesToOutput, program.OutputDirectory);
            }
            try
            {
                program.DownloadFiles().GetAwaiter().GetResult();
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
            Console.WriteLine();
            Console.WriteLine(Resources.UsageOptions);
        }

        internal async Task DownloadFiles()
        {
            using (SymbolStore symbolStore = BuildSymbolStore())
            {
                foreach (SymbolStoreKeyWrapper wrapper in GetKeys().Distinct())
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

            foreach (ServerInfo server in ((IEnumerable<ServerInfo>)SymbolServers).Reverse())
            {
                if (server.InternalSymwebServer)
                {
                    store = new SymwebHttpSymbolStore(Tracer, store, server.Uri, server.PersonalAccessToken);
                }
                else
                {
                    store = new HttpSymbolStore(Tracer, store, server.Uri, server.PersonalAccessToken);
                }
            }

            foreach (string cache in ((IEnumerable<string>)CacheDirectories).Reverse())
            {
                store = new CacheSymbolStore(Tracer, store, cache);
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

        private IEnumerable<SymbolStoreKeyWrapper> GetKeys()
        {
            var inputFiles = InputFilePaths.SelectMany((string file) =>
            {
                string directory = Path.GetDirectoryName(file);
                string pattern = Path.GetFileName(file);
                return Directory.EnumerateFiles(string.IsNullOrWhiteSpace(directory) ? "." : directory, pattern,
                    Subdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            });

            if (!inputFiles.Any())
            {
                throw new ArgumentException(Resources.NoInputFiles);
            }

            foreach (string inputFile in inputFiles)
            {
                foreach (KeyGenerator generator in GetKeyGenerators(inputFile))
                {
                    KeyTypeFlags flags = KeyTypeFlags.None;
                    if (Symbols)
                    {
                        flags |= KeyTypeFlags.SymbolKey;
                    }
                    if (Modules)
                    {
                        flags |= KeyTypeFlags.IdentityKey;
                    }
                    if (Debugging)
                    {
                        flags |= KeyTypeFlags.ClrKeys;
                    }
                    if (flags == KeyTypeFlags.None)
                    {
                        if (generator.IsDump())
                        {
                            // The default for dumps is to download everything
                            flags = KeyTypeFlags.IdentityKey | KeyTypeFlags.SymbolKey | KeyTypeFlags.ClrKeys;
                        }
                        else
                        {
                            // Otherwise the default is just the symbol files
                            flags = KeyTypeFlags.SymbolKey;
                        }
                    }
                    if (ForceWindowsPdbs)
                    {
                        flags |= KeyTypeFlags.ForceWindowsPdbs;
                    }
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
                yield return new FileKeyGenerator(Tracer, file);
            }
        }

        private async Task WriteFile(SymbolStoreFile file, SymbolStoreKeyWrapper wrapper)
        {
            if (OutputDirectory != null) 
            {
                await WriteFileToDirectory(file.Stream, wrapper.Key.FullPathName, OutputDirectory);
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
            if (File.Exists(destination))
            {
                Tracer.Warning(Resources.FileAlreadyExists, destination);
            }
            else
            {
                Tracer.WriteLine(Resources.WritingFile, destination);
                using (Stream destinationStream = File.OpenWrite(destination))
                {
                    await stream.CopyToAsync(destinationStream);
                }
            }
        }
    }
}
