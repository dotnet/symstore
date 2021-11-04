// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Hosting;

namespace NugetSymbolServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = 
                Host.CreateDefaultBuilder(args)
                    .ConfigureServices(sp => sp.AddSingleton<IConfigurationSource>(new MemoryConfigurationSource()))
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseStartup<Startup>();
                    })
                    .Build();

            host.Run();
        }
    }
}
