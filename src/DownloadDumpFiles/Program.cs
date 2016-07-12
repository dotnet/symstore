// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using FileFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DownloadDumpFiles
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                PrintUsage();
                return;
            }
            string dumpFilePath = args[0];
            string symbolServerPath = args[1];
            string cachePath = args[2];
            Program p = new Program(dumpFilePath, symbolServerPath, cachePath);
            p.DownloadFiles().Wait();
        }

        static void PrintUsage()
        {
            Console.WriteLine("DownloadDumpFiles <dump_file_path> <symbol_server_url> <file_cache>");
        }


        string _dumpFilePath;
        string _symbolServerPath;
        string _cachePath;

        public Program(string dumpFilePath, string symbolServerPath, string cachePath)
        {
            _dumpFilePath = dumpFilePath;
            _symbolServerPath = symbolServerPath;
            _cachePath = cachePath;
        }

        protected virtual void WriteLine(string line)
        {
            Console.WriteLine(line);
        }

        public async Task DownloadFiles()
        {
            SymbolServerClient client = new SymbolServerClient(_cachePath, _symbolServerPath);
            await Task.WhenAll(GetLookupKeys(_dumpFilePath).Select(k => DownloadFile(client, k)));
        }

        private async Task DownloadFile(SymbolServerClient client, string lookupKey)
        {
            try
            {
                string path = await client.GetFilePath(lookupKey);
                WriteLine("SUCCESS: " + path);
            }
            catch(Exception e)
            {
                WriteLine("FAIL: " + lookupKey + ": " + e.Message);
            }
        }

        private IEnumerable<string> GetLookupKeys(string dumpFilePath)
        {
            using (Stream dump = File.OpenRead(dumpFilePath))
            {
                DumpReader reader = new DumpReader(dump);
                foreach (IDumpModule module in reader.GetDumpModules())
                {
                    yield return module.GetBinaryLookupKey();
                    yield return module.GetSymbolsLookupKey();
                }
            }
        }

        
    }
}
