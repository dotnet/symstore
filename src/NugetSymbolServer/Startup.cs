// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NugetSymbolServer.Service.Models;
using System;
using System.IO;

namespace NugetSymbolServer
{
    public class Startup
    {
        public Startup(IHostingEnvironment hostingEnvironment, IConfigurationSource hostProvidedConfiguration)
        { 
            Configuration = 
                new ConfigurationBuilder()
                .SetBasePath(ApplicationEnvironment.ApplicationBasePath)
                .Add(hostProvidedConfiguration)
                .AddJsonFile("config.json", optional: hostingEnvironment.IsDevelopment())
                .AddJsonFile($"config.{hostingEnvironment.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables()
                .AddInMemoryCollection()
                .Build(); 

 
            LoggingConfiguration = 
                new ConfigurationBuilder()
                .SetBasePath(ApplicationEnvironment.ApplicationBasePath)
                .AddJsonFile("logging.json", optional: true)
                .AddJsonFile($"logging.{hostingEnvironment.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }

        public IConfiguration Configuration { get; private set; } 
 
        public IConfiguration LoggingConfiguration { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            
            services
                .AddOptions()
                .Configure<FileStoreOptions>(options =>
                {
                    string cachePath = Configuration["FileCache:CachePath"];
                    if(cachePath == null)
                    {
                        throw new Exception("No configuration specified for FileCache:CachePath");
                    }
                    cachePath = Environment.ExpandEnvironmentVariables(cachePath);
                    options.RootPath = Path.Combine(ApplicationEnvironment.ApplicationBasePath, cachePath);
                })
                .Configure<DirectoryPackageSourceOptions>(options =>
                {
                    string sourcePath = Configuration["PackageSource:SourcePath"];
                    if(sourcePath == null)
                    {
                        throw new Exception("No configuration specified for PackageSource:SourcePath");
                    }
                    sourcePath = Environment.ExpandEnvironmentVariables(sourcePath);
                    options.SourcePath = Path.Combine(ApplicationEnvironment.ApplicationBasePath, sourcePath);
                })
                .AddSingleton<IFileStore, FileStore>()
                .AddSingleton<ISymbolAccess, PackageBasedSymbolStore>()
                .AddSingleton<IPackageStore>(sp => sp.GetRequiredService<ISymbolAccess>() as IPackageStore)
                .AddSingleton<IPackageSource, DirectoryPackageSource>()
                .AddMvc();
        }

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            //Kick off package processing now, not when the first request comes in
            app.ApplicationServices.GetRequiredService<IPackageSource>().EnsurePackagesProcessed();

            app.UseDeveloperExceptionPage();
            loggerFactory.AddConsole(/*LoggingConfiguration*/);
            app.UseMvc(routes => 
             { 
                 routes.MapRoute( 
                     name: "default", 
                     template: "{controller=Home}/{action=Index}/{id?}"); 
             }); 

            app.UseStaticFiles();
        }
    }
}