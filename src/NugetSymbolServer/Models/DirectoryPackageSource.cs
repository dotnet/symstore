// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NugetSymbolServer.Service.Models
{
    public class DirectoryPackageSourceOptions
    {
        public string SourcePath { get; set; }
    }

    /// <summary>
    /// Scans a directory and adds all the packages it finds to the package store
    /// </summary>
    public class DirectoryPackageSource : IPackageSource
    {
        Task _scanDirectoryTask;

        public DirectoryPackageSource(IOptions<DirectoryPackageSourceOptions> options,
                                      IPackageStore packageStore,
                                      ILoggerFactory loggerFactory)
        {
            _scanDirectoryTask = ScanForPackages(options.Value.SourcePath, 
                                                 packageStore,
                                                 loggerFactory.CreateLogger(GetType().FullName));
        }

        public Task EnsurePackagesProcessed()
        {
            return _scanDirectoryTask;
        }

        async Task ScanForPackages(string sourcePath, IPackageStore packageStore, ILogger logger)
        {
            IEnumerable<Task> tasks = Directory.EnumerateFiles(sourcePath).Select( file => Task.Factory.StartNew( async () =>
            {
                if (Path.GetExtension(file) != ".zip" && Path.GetExtension(file) != ".nupkg")
                {
                    return;
                }
                try
                {
                    logger.LogInformation("Processing Package: " + file);
                    await packageStore.AddPackage(file);
                }
                catch (IOException e)
                {
                    logger.LogWarning("Error processing package " + file + Environment.NewLine +
                                      e.Message);
                }
            }));
            await Task.WhenAll(tasks.ToArray());
        }
    }
}
