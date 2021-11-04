// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.IO;
using System.Threading.Tasks;

namespace NugetSymbolServer.Service.Models
{
    public interface IFileStore
    {
        Task<FileReference> AddFile(Stream fileData, string relativeFilePath);
    }
}