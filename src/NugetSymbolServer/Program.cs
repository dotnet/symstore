// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
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
