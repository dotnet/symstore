using Microsoft.FileFormats.PE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.SymbolStore
{
    public class InvalidChecksumException : Exception
    {
        public InvalidChecksumException(string message) : base(message)
        {

        } 
    }

    class ChecksumValidator
    {
        private const string pdbStreamName = "#Pdb";
        private const uint pdbIdSize = 20;
        private readonly ITracer _tracer;

        public ChecksumValidator(ITracer tracer)
        {
            _tracer = tracer;
        }

        internal void Validate(Stream pdbStream, IEnumerable<PdbChecksum> pdbChecksums)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                pdbStream.CopyTo(ms);
                byte[] bytes = ms.ToArray();
                uint offset = 0;

                try
                {
                    offset = GetPdbStreamOffset(pdbStream);
                }
                catch (Exception ex)
                {
                    _tracer.Error(ex.Message);
                    throw;
                }

                for (int i = 0; i <= pdbIdSize; i++)
                {
                    bytes[i + offset] = 0;
                }

                foreach (var checksum in pdbChecksums)
                {
                    _tracer.Information($"Testing checksum: {checksum}");

                    var algorithm = HashAlgorithm.Create(checksum.AlgorithmName);
                    if (algorithm != null)
                    {
                        var hash = algorithm.ComputeHash(bytes);
                        if (hash.SequenceEqual(checksum.Checksum))
                        {
                            // If any of the checksums are OK, we're good
                            _tracer.Information($"Found checksum match {checksum}");
                            return;
                        }
                    }
                    else
                    {
                        throw new ArgumentException("Unknown hash algorithm");
                    }
                }
            }
            throw new InvalidChecksumException("PDB checksum mismatch");
        }

        private static uint GetPdbStreamOffset(Stream pdbStream)
        {
            pdbStream.Position = 0;
            var reader = new BinaryReader(pdbStream);
            {
                uint signature = reader.ReadUInt32();
                pdbStream.Seek(4 + // Signature
                               2 + // Version Major
                               2 + // Version Minor
                               4,  // Reserved)
                               SeekOrigin.Begin);

                // skip the version string
                uint versionStringSize = 0;
                versionStringSize = reader.ReadUInt32();

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
