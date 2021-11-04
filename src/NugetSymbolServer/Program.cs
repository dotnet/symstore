// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;

namespace NugetSymbolServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .ConfigureServices(sp => sp.AddSingleton<IConfigurationSource>(new MemoryConfigurationSource()))
                .UseKestrel()
                .UseStartup<Startup>()
                .Build();

            
            host.Run();
        }
    }
}
