// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
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
        public Startup(IWebHostEnvironment hostingEnvironment, IConfigurationSource hostProvidedConfiguration)
        { 
            Configuration = 
                new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .Add(hostProvidedConfiguration)
                .AddJsonFile("config.json", optional: hostingEnvironment.IsDevelopment())
                .AddJsonFile($"config.{hostingEnvironment.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables()
                .AddInMemoryCollection()
                .Build(); 

 
            LoggingConfiguration = 
                new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
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
                    options.RootPath = Path.Combine(AppContext.BaseDirectory, cachePath);
                })
                .Configure<DirectoryPackageSourceOptions>(options =>
                {
                    string sourcePath = Configuration["PackageSource:SourcePath"];
                    if(sourcePath == null)
                    {
                        throw new Exception("No configuration specified for PackageSource:SourcePath");
                    }
                    sourcePath = Environment.ExpandEnvironmentVariables(sourcePath);
                    options.SourcePath = Path.Combine(AppContext.BaseDirectory, sourcePath);
                })
                .AddSingleton<IFileStore, FileStore>()
                .AddSingleton<ISymbolAccess, PackageBasedSymbolStore>()
                .AddSingleton<IPackageStore>(sp => sp.GetRequiredService<ISymbolAccess>() as IPackageStore)
                .AddSingleton<IPackageSource, DirectoryPackageSource>()
                .AddMvc();
        }

        public void Configure(IApplicationBuilder app)
        {
            //Kick off package processing now, not when the first request comes in
            app.ApplicationServices.GetRequiredService<IPackageSource>().EnsurePackagesProcessed();

            app.UseDeveloperExceptionPage();

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
