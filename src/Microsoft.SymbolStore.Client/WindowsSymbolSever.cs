using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.SymbolStore.Client
{
    public class SymbolServerResult
    {
        public Stream Stream { get; set; }
        public long ContentLength { get; set; }
        public bool Compressed { get; set; }

        public SymbolServerResult(Stream result, long contentLength, bool compressed)
        {
            Stream = result;
            ContentLength = contentLength;
            Compressed = compressed;
        }
        public SymbolServerResult(WebResponse response, bool compressed)
        {
            Stream = response.GetResponseStream();
            ContentLength = response.ContentLength;
            Compressed = compressed;
        }

        public SymbolServerResult(string fileName)
        {
            Stream = File.OpenRead(fileName);
            ContentLength = new FileInfo(fileName).Length;
            Compressed = false;
        }
    }

    public interface ISymbolServer
    {
        bool IsRemoteServer { get; }
        bool IsReachable();
        bool IsReachable(int timeout);


        SymbolServerResult FindBinary(string filename, int imageSize, int buildTimeStamp);
        Task<SymbolServerResult> FindBinaryAsync(string filename, int imageSize, int buildTimeStamp);
        SymbolServerResult FindPdb(string pdbName, Guid guid, int age);
        Task<SymbolServerResult> FindPdbAsync(string pdbName, Guid guid, int age);
    }

    public class WindowsSymbolSever : ISymbolServer
    {
        private string _path = null;
        private bool _isServer = false;

        public bool IsRemoteServer
        {
            get
            {
                return _isServer;
            }
        }

        public WindowsSymbolSever(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            path = path.Trim();
            bool isServer = false;
            if (path.StartsWith("srv*", StringComparison.OrdinalIgnoreCase))
            {
                isServer = true;
                path = path.Substring(4);
            }

            if (!isServer && path.StartsWith("http:", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
                isServer = true;


            _path = path;
            _isServer = isServer;
        }

        public bool IsReachable()
        {
            return IsReachable(700);
        }

        public bool IsReachable(int timeout)
        {
            try
            {
                string path = _path;
                Uri uri = new Uri(path);
                string host = uri.DnsSafeHost;

                if (!_isServer)
                    throw new InvalidOperationException("Cannot use IsReachable if this is not a remote server.");
                Task<IPAddress[]> addresses = System.Net.Dns.GetHostAddressesAsync(uri.DnsSafeHost);

                if (!addresses.Wait(700))
                    return false;

                IPAddress[] result = addresses.Result;
                return result != null && result.Length > 0;
            }
            catch
            {
                return false;
            }
        }


        public SymbolServerResult FindBinary(string filename, int imageSize, int buildTimeStamp)
        {
            return FindBinaryAsync(filename, imageSize, buildTimeStamp).Result;
        }

        public SymbolServerResult FindPdb(string pdbName, Guid guid, int age)
        {
            return FindPdbAsync(pdbName, guid, age).Result;
        }

        public async Task<SymbolServerResult> FindBinaryAsync(string filename, int imageSize, int buildTimeStamp)
        {
            string indexPath = $"{filename}/{buildTimeStamp.ToString("x")}{imageSize.ToString("x")}/{filename}";
            if (_isServer)
                return await TryGetFileFromServer(indexPath);

            indexPath = indexPath.Replace('/', '\\');
            string fullPath = Path.Combine(_path, indexPath);

            if (File.Exists(fullPath))
            {
                try
                {
                    return new SymbolServerResult(fullPath);
                }
                catch
                {
                    Trace();
                }
            }

            return null;
        }

        public async Task<SymbolServerResult> FindPdbAsync(string pdbName, Guid guid, int age)
        {
            string indexPath = $"{pdbName}/{guid.ToString().Replace("-", "")}{age.ToString("x")}/{pdbName}";
            if (_isServer)
                return await TryGetFileFromServer(indexPath);

            indexPath = indexPath.Replace('/', '\\');
            string fullPath = Path.Combine(_path, indexPath);

            if (File.Exists(fullPath))
            {
                try
                {
                    return new SymbolServerResult(fullPath);
                }
                catch
                {
                    Trace();
                }
            }

            return null;
        }

        private async Task<SymbolServerResult> TryGetFileFromServer(string indexPath)
        {
            int lastSlash = indexPath.LastIndexOf('/');
            string redirectPath = indexPath.Substring(0, lastSlash + 1) + "file.ptr";
            string compressedSigPath = indexPath.Substring(0, indexPath.Length - 1) + "_";

            Task<WebResponse> redirect = GetPhysicalFileFromServer(redirectPath);
            Task<WebResponse> compressed = GetPhysicalFileFromServer(compressedSigPath);
            Task<WebResponse> file = GetPhysicalFileFromServer(indexPath);

            WebResponse response = await redirect;
            if (response != null)
            {
                string fileData = "";
                using (response)
                {
                    using (Stream stream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream))
                        fileData = reader.ReadToEnd().Trim();
                }

                if (fileData.StartsWith("PATH:"))
                    fileData = fileData.Substring(5);

                if (!fileData.StartsWith("MSG:") && File.Exists(fileData))
                {
                    try
                    {
                        return new SymbolServerResult(fileData);
                    }
                    catch
                    {
                        Trace();
                    }
                }
            }

            response = await compressed;
            if (response != null)
                return new SymbolServerResult(response, true);

            response = await file;
            if (response != null)
                return new SymbolServerResult(response, false);

            return null;
        }

        private void Trace()
        {
            throw new NotImplementedException();
        }

        private async Task<WebResponse> GetPhysicalFileFromServer(string indexPath)
        {
            string fullUri = $"{_path}/{indexPath}";
            try
            {
                HttpWebRequest request = WebRequest.CreateHttp(fullUri);
                request.Headers["User-Agent"] = "Microsoft-Symbol-Server/6.13.0009.1140";
                return await request.GetResponseAsync();
            }
            catch (WebException)
            {
            }

            return null;
        }
    }
}
