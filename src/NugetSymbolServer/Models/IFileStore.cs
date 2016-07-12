// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System.IO;
using System.Threading.Tasks;

namespace NugetSymbolServer.Service.Models
{
    public interface IFileStore
    {
        Task<FileReference> AddFile(Stream fileData, string relativeFilePath);
    }
}