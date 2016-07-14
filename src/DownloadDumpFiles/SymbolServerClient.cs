// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DownloadDumpFiles
{
    public class SymbolServerClient
    {
        private string _cachePath;
        private string _serverEndpoint;
        private HttpClient _client;

        public SymbolServerClient(string cachePath, string serverEndpoint)
        {
            _cachePath = cachePath;
            _serverEndpoint = serverEndpoint;
            if (!_serverEndpoint.EndsWith("/"))
            {
                _serverEndpoint += "/";
            }
            _client = new HttpClient();
        }

        public async Task<string> GetFilePath(string lookupKey)
        {
            if (!IsKeyValid(lookupKey))
            {
                throw new Exception("Lookup key \'" + lookupKey + "\' is invalid");
            }

            string cachedFile = Path.Combine(_cachePath, lookupKey);
            if (File.Exists(cachedFile))
            {
                return cachedFile;
            }

            HttpResponseMessage response = await _client.GetAsync(_serverEndpoint + lookupKey);
            response.EnsureSuccessStatusCode();
            Directory.CreateDirectory(Path.GetDirectoryName(cachedFile));
            using (Stream cacheStream = File.OpenWrite(cachedFile))
            {
                await response.Content.CopyToAsync(cacheStream);
            }
            return cachedFile;
        }

        private bool IsKeyValid(string lookupKey)
        {
            // SSQP theoretically supports a broader set of keys, but in order to ensure that all the keys
            // play well with the caching scheme we enforce additional requirements (that all current key
            // conventions also meet)

            string[] parts = lookupKey.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                return false;
            }
            for (int i = 0; i < 3; i++)
            {
                foreach (char c in parts[i])
                {
                    if (char.IsLetterOrDigit(c))
                        continue;
                    if (c == '_' || c == '-' || c == '.' || c == '%')
                        continue;
                    return false;
                }
                // we need to support files with . in the name, but
                // we don't want identifiers that are meaningful to the filesystem
                if (parts[i] == "." || parts[i] == "..")
                    return false;
            }

            return true;
        }
    }
}
