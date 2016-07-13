# Simple Symbol Query Protocol (SSQP)#

Frequently when diagnosing computer programs there is a need to retrieve additional information about the program beyond what was required for a computer to run it. This is accomplished by having a network service, the 'symbol server', which provides the additional information on demand. A diagnostic tool such as a debugger or profiler act as symbol server clients, submitting requests for additional information they need. 

The protocol solely consists of a mechanism for the client to provide a key, the 'clientKey',  and a filename, identifying the file it wants. The server then sends back that file. The client may understand the semantics of the key, filename, and the resulting file, but from the perspective of the protocol and the server these items are merely character sequences and binary blobs. 

## Request ##
All requests are [HTTP/1.1](https://tools.ietf.org/html/rfc2616 "HTTP/1.1") messages using the 'get' verb. The client is configured with a valid URI for the service endpoint, and a clientKey + filename it wants to retrieve. The client should request URI <service_endpoint\>/<filename\>/<clientKey\>/<filename\>. 

 For example:

- service endpoint URI: http://www.contuso.com/symbol/server
- clientKey: 12345abcdefg
- filename: debug_info.txt

GET http://www.contuso.com/symbol/server/debug\_info.txt/12345abcdefg/debug\_info.txt

### Encoding and case sensitivity ###
ClientKey and filename need to URL encoded as they may contain characters outside the [URI unreserved character set](https://tools.ietf.org/html/rfc3986#Section-2.3). Neither clientKey nor filename is case-sensitive. Clients are encouraged to normalize to lower-case in the request URI, but the server should perform any clientKey and filename comparisons in a case-insensitive manner regardless.

## Response ##

The response is a standard [HTTP/1.1](https://tools.ietf.org/html/rfc2616 "HTTP/1.1") response including the content of the requested file. Content-Type should be application/octet-stream. 

An http 304 redirection will be honored by the client.

## Relation to the SymSrv protocol ##

This protocol has been deliberately designed so that it is also compatible with SymSrv clients. A typical SymSrv server implementation would expect the client to request a given file up to three times, ones using the original file name (foo.exe), once using a trailing underbar to retrieve a cab compressed version (foo.ex_), and once replacing the file name with file.ptr to allow for limited redirection. The SymSrv server need only respond succesfully to one of those three requests, and the SSQP ensures the original filename form will be a succesful request. Supporting the other request forms is allowed, but not required.

If the client indicates it can support HTTP compression in its request then the server can use that as a functional replacement for symsrv's cab compression scheme. Otherwise the service must be prepared to serve the uncompressed file.

## Rationale for defining an alternative protocol to SymSrv ##
SymSrv allows the server to choose whether to respond a request in the filename form, filename underbar form or file.ptr form. Aside from the performance overhead in needing clients to make up to three HTTP requests per-file, the filename underbar form uses cab compression. This compression format is uncommon outside of the windows ecosystem which makes a fully compatible non-windows SymSrv client significantly more complex to develop.

## Security Warnings ##
Clients consuming the diagnostic files downloaded via SSQP often expect that there is a certain relationship between the key and the data that is downloaded. For example a client may expect requesting key 'sha-1-123878123871238712378519847' guarantees that the data which comes back has the given hash. The server is not required to do any verification of this however. A client that wants to be certain must either:

 - verify this is the case after the data has been downloaded
 - trust the author of the mapping data, every intermediary that handled the data at rest or in transit, and know that none of the trusted parties merge in any clientKey mappings from untrusted parties

There are internet hosted symbol server implementations that DO merge mappings from untrusted and potentially anonymous 3rd parties. In this case it is trivial for an attacker to pose as such a 3rd party and add mappings that remap legitimate clientKeys to arbitrary attacker chosen files. Clients need to assume that ANY data returned by such a service could have been tampered with, regardless of whether the particular key being requested was derived from a trusted source. 

For tools that allow the end-user to specify the SSQP endpoint to connect to, consider the risk that the end-user may change configuration from a service that hosts only trusted content to a service  that hosts untrusted content without realizing the security implications of this choice. Some guides on the web from reputable and independent sources explicitly tell developers to make such configuration changes without any mention of the risks.

Clients are strongly recommended to only work with trusted services, harden against using malicious downloaded files, or implement a scheme to independently verify the integrity and authenticity of downloaded files before using them.