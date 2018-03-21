// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.SymbolStore;
using Microsoft.SymbolStore.KeyGenerators;
using Microsoft.SymbolStore.SymbolStores;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SymClient
{
    public class Program
    {
        struct ServerInfo
        {
            public Uri Uri;
            public string PersonalAccessToken;
            public bool InternalSymwebServer;
        }

        private readonly static HashSet<string> s_validExtensions = new HashSet<string>(new string[] { "", ".exe", ".dll", ".pdb", ".so", ".dbg", ".dylib", ".dwarf" });
        private readonly static HashSet<string> s_validSourceExtensions = new HashSet<string>(new string[] { ".cs", ".vb", ".h", ".cpp", ".inl" });

        private readonly List<string> InputFilePaths = new List<string>();
        private readonly List<string> CacheDirectories = new List<string>();
        private readonly List<ServerInfo> SymbolServers = new List<ServerInfo>();
        private string OutputDirectory;
        private bool OutputByInputFile;
        private bool Subdirectories;
        private bool Symbols;
        private bool Debugging;
        private bool Modules;
        private bool ForceWindowsPdbs;
        private bool Packages;
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
                    case "-ms":
                    case "--microsoft-symbol-server":
                        Uri.TryCreate("http://msdl.microsoft.com/download/symbols/", UriKind.Absolute, out uri);
                        program.SymbolServers.Add(new ServerInfo {Uri = uri, PersonalAccessToken = null});
                        break;

                    case "-mi":
                    case "--ms-internal-server":
                        Uri.TryCreate("http://symweb.corp.microsoft.com/", UriKind.Absolute, out uri);
                        program.SymbolServers.Add(new ServerInfo {Uri = uri, PersonalAccessToken = null, InternalSymwebServer = true});
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
                                tracer.Error("Invalid server path '{0}'", args[i]);
                                goto usage;
                            }
                            Uri.TryCreate(serverPath, UriKind.Absolute, out uri);
                            program.SymbolServers.Add(new ServerInfo {Uri = uri, PersonalAccessToken = personalAccessToken});
                        }
                        else
                            goto usage;
                        break;

                    case "-o":
                    case "--output-directory":
                        if (++i < args.Length)
                            program.OutputDirectory = args[i];
                        else
                            goto usage;
                        break;

                    case "-oi":
                    case "--output-by-inputfile":
                        program.OutputByInputFile = true;
                        break;

                    case "-c":
                    case "--cache-directory":
                        if (++i < args.Length)
                            program.CacheDirectories.Add(args[i]);
                        else
                            goto usage;
                        break;

                    case "-e":
                    case "--add-valid-extension":
                        if (++i < args.Length)
                            s_validExtensions.Add(args[i]);
                        else
                            goto usage;
                        break;

                    case "-se":
                    case "--add-source-extension":
                        if (++i < args.Length)
                            s_validSourceExtensions.Add(args[i]);
                        else
                            goto usage;
                        break;

                    case "-p":
                    case "--packages":
                        program.Packages = true;
                        break;

                    case "-r":
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
                    case "--diag":
                        tracer.Enabled = true;
                        break;

                    case "-vd":
                    case "--verbose-diag":
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
                            tracer.Error("Invalid option {0}", inputFile);
                            goto usage;
                        }
                        program.InputFilePaths.Add(inputFile);
                        break;
                }
            }
            if (program.OutputDirectory != null)
            {
                Directory.CreateDirectory(program.OutputDirectory);
                tracer.WriteLine("Writing files to {0}", program.OutputDirectory);
            }
            if (program.OutputByInputFile) {
                tracer.WriteLine("Writing files next to input file");
            }
            foreach (ServerInfo server in program.SymbolServers)
            {
                tracer.WriteLine("Reading from {0}", server.Uri);
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
            Console.WriteLine(@"
SymClient [options] <files>
-ms|--microsoft-symbol-server                       Add 'http://msdl.microsoft.com/download/symbols' symbol server path
-mi|--ms-internal-server                            Add 'http://symweb.corp.microsoft.com' symbol server path
-s |--server-path <symbol server path>              Add a http server path
-as|--authenticated-server-path <pat> <server path> Add a http PAT authenticated server path
-c |--cache-directory <file cache directory>        Add a cache directory
-o |--output-directory <output directory>           Set the output directory
-oi|--output-by-inputfile                           Write symbol file next to input file
-p |--packages                                      Input files are nuget packages
-ae|--add-source-extension <ext>                    Add source file extension
-e |--add-valid-extension <ext>                     Add file extension to be indexed in package
-r |--recurse-subdirectories                        Process input files in all subdirectories
   |--symbols                                       Get the symbol files (.pdb, .dbg, .dwarf)
   |--modules                                       Get the module files (.dll, .so, .dylib)
   |--debugging                                     Get the special debugging modules (DAC, DBI, SOS)
-w |--force-windows-pdbs                            Force downloading of the Windows PDBs
-d |--diag                                          Enable diagnostic output
-vd|--verbose-diag                                  Enable diagnostic and verbose diagnostic output
-h |--help                                          This help message");
        }

        internal async Task DownloadFiles()
        {
            using (SymbolStore symbolStore = BuildSymbolStore())
            {
                bool verifyPackages = Packages && OutputDirectory == null;

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
                    if (verifyPackages)
                    {
                        // If we are verifing symbol packages then only check the identity and clr keys.
                        flags = KeyTypeFlags.IdentityKey | KeyTypeFlags.ClrKeys; 
                    }
                    else
                    {
                        // The default is to download everything
                        flags = KeyTypeFlags.IdentityKey | KeyTypeFlags.SymbolKey | KeyTypeFlags.ClrKeys;
                    }
                }
                if (ForceWindowsPdbs)
                {
                    flags |= KeyTypeFlags.ForceWindowsPdbs;
                }

                foreach (SymbolStoreKeyWrapper wrapper in GetKeys(flags).Distinct())
                {
                    SymbolStoreKey key = wrapper.Key;
                    Tracer.Information("Key: {0} - {1}{2}", key.Index, key.FullPathName, key.IsClrSpecialFile ? "*" : "");

                    if (symbolStore != null)
                    {
                        using (SymbolStoreFile symbolFile = await symbolStore.GetFile(key, CancellationToken.None))
                        {
                            if (symbolFile != null)
                            {
                                await WriteFile(symbolFile, wrapper);
                            }
                            // If there is no output directory verify the file exists in the symbol store
                            if (OutputDirectory == null)
                            {
                                Tracer.WriteLine("Key {0}found {1} - {2}", symbolFile != null ? "" : "NOT ", key.Index, key.FullPathName);
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

        private IEnumerable<SymbolStoreKeyWrapper> GetKeys(KeyTypeFlags flags)
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
                throw new ArgumentException("Input files not found");
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
            if (Packages)
            {
                // The package file needs to be opened for read/write so the zip archive can be created in update mode.
                using (Stream inputStream = File.Open(inputFile, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    // This needs to be update mode so streams created below are seekable which is required by
                    // the key generation code. Because of this the zip archives should not be disposed which
                    // would attempt to write any changes to the input file. It isn't neccesary either because
                    // the actual file stream is disposed.
                    ZipArchive archive = new ZipArchive(inputStream, ZipArchiveMode.Update, leaveOpen: true);

                    // For each entry in the nuget package
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        // Only files (no directories) with the proper extensions are processed
                        if (ShouldIndex(entry.FullName))
                        {
                            using (Stream zipFileStream = entry.Open())
                            {
                                SymbolStoreFile file = new SymbolStoreFile(zipFileStream, entry.FullName);
                                yield return new FileKeyGenerator(Tracer, file);
                            }
                        }
                    }
                }
            }
            else
            {
                using (Stream inputStream = File.Open(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    SymbolStoreFile file = new SymbolStoreFile(inputStream, inputFile);
                    string extension = Path.GetExtension(inputFile);
                    if (s_validSourceExtensions.Contains(extension))
                    {
                        yield return new SourceFileKeyGenerator(Tracer, file);
                    }
                    else
                    {
                        yield return new FileKeyGenerator(Tracer, file);
                    }
                }
            }
        }

        private static bool ShouldIndex(string fullName)
        {
            if (fullName.EndsWith("/") || fullName.EndsWith("_.pdb"))
            {
                return false;
            }
            string extension = Path.GetExtension(fullName);
            return extension != null && s_validExtensions.Contains(extension);
        }

        private async Task WriteFile(SymbolStoreFile file, SymbolStoreKeyWrapper wrapper)
        {
            if (OutputDirectory != null) 
            {
                await WriteFileToDirectory(file.Stream, wrapper.Key.FullPathName, OutputDirectory);
            }
            if (OutputByInputFile && wrapper.InputFile != null)
            {
                await WriteFileToDirectory(file.Stream, wrapper.Key.FullPathName, Path.GetDirectoryName(wrapper.InputFile));
            }
        }

        private async Task WriteFileToDirectory(Stream stream, string fileName, string destinationDirectory)
        {
            stream.Position = 0;
            string destination = Path.Combine(destinationDirectory, Path.GetFileName(fileName));
            if (File.Exists(destination)) {
                Tracer.Warning("Writing: {0} already exists", destination);
            }
            else
            {
                Tracer.WriteLine("Writing: {0}", destination);
                using (Stream destinationStream = File.OpenWrite(destination))
                {
                    await stream.CopyToAsync(destinationStream);
                }
            }
        }
    }
}
