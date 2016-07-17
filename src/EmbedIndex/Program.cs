// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace EmbedIndex
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if(args.Length != 2)
            {
                PrintUsage();
                return;
            }
            string nugetPackagePath = args[0];
            string nugetPackageOutputPath = args[1];

            Directory.CreateDirectory(Path.GetDirectoryName(nugetPackageOutputPath));
            File.Copy(nugetPackagePath, nugetPackageOutputPath, true);
            IndexPackage(nugetPackageOutputPath, GetIndexers());
        }

        private static void PrintUsage()
        {
            Console.WriteLine("EmbedIndex <path_to_existing_nuget_package> <output_path_to_indexed_nuget_package>");
        }

        private static IEnumerable<IFileFormatIndexer> GetIndexers()
        {
            yield return new PETimestampSizeIndexer();
            yield return new PDBSignatureAgeIndexer();
            yield return new SHA1Indexer();
            yield return new ELFBuildIdIndexer();
            yield return new MachOUuidIndexer();
        }

        private static void IndexPackage(string nugetPackagePath, IEnumerable<IFileFormatIndexer> indexers)
        {
            using (FileStream packageStream = File.Open(nugetPackagePath, FileMode.Open))
            {
                using (ZipArchive archive = new ZipArchive(packageStream, ZipArchiveMode.Update))
                {
                    List<Tuple<string, string>> indexEntries = ComputeIndexEntries(archive, indexers);

                    ZipArchiveEntry symbolIndexEntry = archive.GetEntry("symbol_index.json");
                    if (symbolIndexEntry != null)
                    {
                        symbolIndexEntry.Delete();
                    }
                    symbolIndexEntry = archive.CreateEntry("symbol_index.json");
                    using (Stream indexStream = symbolIndexEntry.Open())
                    {
                        SerializeIndex(indexEntries, indexStream);
                    }
                }
            }
        }

        private static List<Tuple<string, string>> ComputeIndexEntries(ZipArchive archive, IEnumerable<IFileFormatIndexer> indexers)
        {
            List<Tuple<string, string>> indexEntries = new List<Tuple<string, string>>();
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!ShouldIndexEntry(entry.FullName))
                {
                    continue;
                }
                using (Stream zipFileStream = entry.Open())
                {
                    IEnumerable<string> keys = ComputeIndexEntries(entry.FullName, zipFileStream, indexers);
                    indexEntries.AddRange(keys.Select(k => new Tuple<string, string>(k, entry.FullName)));
                }
            }

            return indexEntries;
        }

        private static bool ShouldIndexEntry(string archiveRelativePath)
        {
            if (archiveRelativePath.EndsWith("/"))
                return false;
            if (archiveRelativePath.StartsWith("_rels/"))
                return false;
            if (archiveRelativePath.StartsWith("package/"))
                return false;
            if (archiveRelativePath == "[Content_Types].xml")
                return false;
            if (archiveRelativePath == "symbol_index.json")
                return false;
            if (!archiveRelativePath.Contains("/") && archiveRelativePath.EndsWith(".nuspec"))
                return false;

            return true;
        }

        private static void SerializeIndex(List<Tuple<string, string>> indexEntries, Stream indexStream)
        {
            TextWriter writer = new StreamWriter(indexStream);
            writer.WriteLine("[");
            for (int i = 0; i < indexEntries.Count; i++)
            {
                Tuple<string, string> entry = indexEntries[i];
                writer.WriteLine("    {");
                writer.WriteLine("        \"clientKey\" : \"{0}\",", entry.Item1);
                writer.WriteLine("        \"blobPath\" : \"{0}\"", entry.Item2);
                
                if (i != indexEntries.Count - 1)
                {
                    writer.WriteLine("    },");
                }
                else
                {
                    writer.WriteLine("    }");
                }
            }
            writer.WriteLine("]");
            writer.Flush();
        }

        public static IEnumerable<string> ComputeIndexEntries(string archiveRelativePath, Stream fileStream, IEnumerable<IFileFormatIndexer> indexers)
        {
            List<string> keys = new List<string>();

            //the indexers can handle this, but it wastes time and makes debugging worse to throw and
            //catch the exceptions
            if (fileStream.Length == 0)
            {
                return keys;
            }

            foreach (IFileFormatIndexer indexer in indexers)
            {
                fileStream.Position = 0;
                string key = indexer.ComputeIndexKey(archiveRelativePath, fileStream);
                if (key != null)
                {
                    keys.Add(key);
                }
            }
            return keys;
        }
    }
}
