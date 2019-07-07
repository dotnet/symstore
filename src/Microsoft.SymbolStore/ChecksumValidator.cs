﻿using Microsoft.FileFormats.PE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.SymbolStore
{
    class ChecksumValidator
    {
        private const string pdbStreamName = "#Pdb";
        private const uint pdbIdSize = 20;

        internal static void Validate(ITracer tracer, Stream pdbStream, IEnumerable<PdbChecksum> pdbChecksums)
        {
            uint offset = 0;

            byte[] bytes = new byte[pdbStream.Length];
            if (pdbStream.Read(bytes, offset: 0, count: bytes.Length) != bytes.Length)
            {
                //throw ...
            }

            try
            {
                offset = GetPdbStreamOffset(pdbStream);
            }
            catch (Exception ex)
            {
                tracer.Error(ex.Message);
                throw;
            }

            for (int i = 0; i <= pdbIdSize; i++)
            {
                bytes[i + offset] = 0;
            }

            foreach (var checksum in pdbChecksums)
            {
                tracer.Information($"Testing checksum: {checksum}");

                var algorithm = HashAlgorithm.Create(checksum.AlgorithmName);
                if (algorithm != null)
                {
                    var hash = algorithm.ComputeHash(bytes);
                    if (hash.SequenceEqual(checksum.Checksum))
                    {
                        // If any of the checksums are OK, we're good
                        tracer.Information($"Found checksum match {checksum}");
                        return;
                    }
                }
                else
                {
                    throw new InvalidChecksumException($"Unknown hash algorithm: {checksum.AlgorithmName}");
                }
            }
            throw new InvalidChecksumException("PDB checksum mismatch");
        }

        private static uint GetPdbStreamOffset(Stream pdbStream)
        {
            pdbStream.Position = 0;
            using (var reader = new BinaryReader(pdbStream, Encoding.UTF8, leaveOpen: true))
            {
                pdbStream.Seek(4 + // Signature
                               2 + // Version Major
                               2 + // Version Minor
                               4,  // Reserved)
                               SeekOrigin.Begin);

                // skip the version string
                uint versionStringSize = reader.ReadUInt32();

                pdbStream.Seek(versionStringSize, SeekOrigin.Current);

                // storage header
                pdbStream.Seek(2, SeekOrigin.Current);

                // read the stream headers
                ushort streamCount = reader.ReadUInt16();
                uint streamOffset;
                string streamName;

                for (int i = 0; i < streamCount; i++)
                {
                    var pos = pdbStream.Position;
                    streamOffset = reader.ReadUInt32();
                    // stream size
                    pdbStream.Seek(4, SeekOrigin.Current);
                    streamName = reader.ReadNullTerminatedString();

                    if (streamName == pdbStreamName)
                    {
                        // We found it!
                        return streamOffset;
                    }

                    // streams headers are on a four byte alignment
                    if (pdbStream.Position % 4 != 0)
                    {
                        pdbStream.Seek(4 - pdbStream.Position % 4, SeekOrigin.Current);
                    }
                }
            }

            throw new ArgumentException("We have a file with a metadata pdb signature but no pdb stream");
        }
    }

    static class BinaryReaderExtensions
    {
        public static string ReadNullTerminatedString(this BinaryReader stream)
        {
            var builder = new StringBuilder();
            char ch;
            while ((ch = stream.ReadChar()) != 0)
                builder.Append(ch);
            return builder.ToString();
        }
    }
}
