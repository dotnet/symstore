// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Threading.Tasks;

namespace NugetSymbolServer.Service.Models
{
    public interface IPackageStore
    {
        Task AddPackage(string packageFilePath);
        void RemovePackage(string packageFilePath);
    }
}