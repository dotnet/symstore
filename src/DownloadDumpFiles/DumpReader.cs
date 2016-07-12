// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using FileFormats;
using FileFormats.MachO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DownloadDumpFiles
{
    public class DumpReader
    {
        Stream _dumpStream;
        public DumpReader(Stream dumpStream)
        {
            _dumpStream = dumpStream;
        }

        public IEnumerable<IDumpModule> GetDumpModules()
        {
            StreamAddressSpace dumpDataSource = new StreamAddressSpace(_dumpStream);
            Func<IAddressSpace, IEnumerable<IDumpModule>>[] parsers = new Func<IAddressSpace, IEnumerable<IDumpModule>>[]
            {
                TryParseMachODump
            };
            foreach(var parser in parsers)
            {
                IEnumerable<IDumpModule> modules = parser(dumpDataSource);
                if(modules != null)
                {
                    return modules;
                }
            }

            throw new Exception("Dump did not match any supported format");
        }

        IEnumerable<IDumpModule> TryParseMachODump(IAddressSpace dumpDataSource)
        {
            MachCore core = new MachCore(dumpDataSource);
            if(!core.IsValidCoreFile)
            {
                return null;
            }
            return core.LoadedImages.Select(i => new MachDumpModule(i));
        }
    }

    public class MachDumpModule : IDumpModule
    {
        MachLoadedImage _loadedImage;
        public MachDumpModule(MachLoadedImage loadedImage)
        {
            _loadedImage = loadedImage;
        }

        public string GetBinaryLookupKey()
        {
            string fileName = Uri.EscapeDataString(_loadedImage.Path.Split('/').Last());
            string uuid = string.Concat(_loadedImage.Image.Uuid.Select(b => b.ToString("x2")));
            return fileName + "/mach-uuid-" + uuid + "/" + fileName;
        }

        public string GetSymbolsLookupKey()
        {
            string fileName = Uri.EscapeDataString(_loadedImage.Path.Split('/').Last() + ".dwarf");
            string uuid = string.Concat(_loadedImage.Image.Uuid.Select(b => b.ToString("x2")));
            return fileName + "/mach-uuid-sym-" + uuid + "/" + fileName;
        }
    }

    public interface IDumpModule
    {
        string GetBinaryLookupKey();
        string GetSymbolsLookupKey();
    }
}
