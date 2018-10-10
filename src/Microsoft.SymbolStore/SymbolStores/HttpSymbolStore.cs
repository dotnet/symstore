// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SymbolStore.SymbolStores
{
    /// <summary>
    /// Basic http symbol store. The request can be authentication with a PAT for VSTS symbol stores.
    /// </summary>
    public class HttpSymbolStore : SymbolStore
    {
        private readonly HttpClient _client;
        private readonly HttpClient _authenticatedClient;
        private bool _clientFailure;

        /// <summary>
        /// For example, https://dotnet.myget.org/F/dev-feed/symbols.
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Create an instance of a http symbol store
        /// </summary>
        /// <param name="backingStore">next symbol store or null</param>
        /// <param name="symbolServerUri">symbol server url</param>
        /// <param name="personalAccessToken">optional PAT or null if no authentication</param>
        public HttpSymbolStore(ITracer tracer, SymbolStore backingStore, Uri symbolServerUri, string personalAccessToken = null)
            : base(tracer, backingStore)
        {
            Uri = symbolServerUri ?? throw new ArgumentNullException(nameof(symbolServerUri));
            if (!symbolServerUri.IsAbsoluteUri || symbolServerUri.IsFile)
            {
                throw new ArgumentException(nameof(symbolServerUri));
            }
            // Normal unauthenticated client
            _client = new HttpClient();

            // If PAT, create authenticated client
            if (!string.IsNullOrEmpty(personalAccessToken))
            {
                var handler = new HttpClientHandler() {
                    AllowAutoRedirect = false
                };
                var client = new HttpClient(handler);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Basic", Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", personalAccessToken))));
                _authenticatedClient = client;
            }
        }

        /// <summary>
        /// Resets the sticky client failure flag. This client instance will now 
        /// attempt to download again instead of automatically failing.
        /// </summary>
        public void ResetClientFailure()
        {
            _clientFailure = false;
        }

        protected override async Task<SymbolStoreFile> GetFileInner(SymbolStoreKey key, CancellationToken token)
        {
            Uri uri = GetRequestUri(key.Index);
            Stream stream = await GetFileStream(uri, token);
            if (stream != null)
            {
                return new SymbolStoreFile(stream, uri.ToString());
            }
            return null;
        }

        protected Uri GetRequestUri(string index)
        {
            if (!Uri.TryCreate(Uri, index, out Uri requestUri))
            {
                throw new ArgumentException(nameof(index));
            }
            if (requestUri.IsFile)
            {
                throw new ArgumentException(nameof(index));
            }
            return requestUri;
        }

        protected async Task<Stream> GetFileStream(Uri requestUri, CancellationToken token)
        {
            // Just return if previous failure
            if (_clientFailure)
            {
                return null;
            }
            try
            {
                HttpClient client = _authenticatedClient ?? _client;
                HttpResponseMessage response = await client.GetAsync(requestUri, token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return await response.Content.ReadAsStreamAsync();
                }
                if (response.StatusCode == HttpStatusCode.Found)
                {
                    response = await _client.GetAsync(response.Headers.Location, token);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return await response.Content.ReadAsStreamAsync();
                    }
                }

                // If the status code isn't some temporary or retryable condition, mark failure
                bool retryable = IsRetryableStatus(response.StatusCode);
                if (!retryable)
                {
                    MarkClientFailure();
                }

                string message = string.Format("HttpSymbolStore: {0} {1} '{2}'", (int)response.StatusCode, response.ReasonPhrase, requestUri);
                if (!retryable || response.StatusCode == HttpStatusCode.NotFound)
                {
                    Tracer.Error(message);
                }
                else 
                {
                    Tracer.Warning(message);
                }

                response.Dispose();
            }
            catch (HttpRequestException ex)
            {
                Tracer.Error("HttpSymbolStore: {0} '{1}'", ex.Message, requestUri);
                MarkClientFailure();
            }
            return null;
        }

        public override void Dispose()
        {
            _client.Dispose();
            if (_authenticatedClient != null)
            {
                _authenticatedClient.Dispose();
            }
            base.Dispose();
        }

        HashSet<HttpStatusCode> s_retryableStatusCodes = new HashSet<HttpStatusCode>
        {
            HttpStatusCode.NotFound,
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout,
        };

        /// <summary>
        /// Returns true if the http status code is temporary or retryable condition.
        /// </summary>
        protected bool IsRetryableStatus(HttpStatusCode status)
        {
            return s_retryableStatusCodes.Contains(status);
        }

        /// <summary>
        /// Marks this client as a failure where any subsequent calls to 
        /// GetFileStream() will return null.
        /// </summary>
        protected void MarkClientFailure()
        {
            _clientFailure = true;
        }
    }
}
