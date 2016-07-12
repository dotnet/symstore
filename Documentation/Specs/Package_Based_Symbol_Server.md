# Zip Package Based Symbol Server #

A zip package based symbol server is a network service that implements the [Simple Symbol Query Protocol](Simple_Symbol_Query_Protocol.md) (SSQP) using a set of zip compressed files with specific contents to define the files that are available for download and the clientKey/filenames that address them. This specification defines the format of the zip packages. Although this format is intended to be fully compatible with NuGet package and NuGet symbol package specifications, the zip packages are not required to qualify under either standard.

## The zip package format ##

Each symbol package is a compressed container of files in the zip format. At the root of the container there must be one file named 'symbol\_index.json'. There may be an arbitrary number of other files in the container either at the root level or in arbitrarily nested sub-containers. The symbol\_index.json is a json map where the key is the clientKey encoded as a string and the value is the path of a file within the zip container encoded as a string. The path is a filename, preceded by 0 or more container names using '/' as the separator. Each path indicated in the mapping must point to a valid file within the zip archive. For example:

    {
        "12387532" : "debug_info.txt",
        "09safnf82asddasdqwd998vds" : "MyProgram.exe",
        "12-09" : "Content/localized/en-us/data.xml",
        "312&312-123*&^ndw" : "foo"
    }

## Implementing the service ##

In order to implement the [Simple Symbol Query Protocol](Simple_Symbol_Query_Protocol.md) the service must search within each package's symbol\_index.json for a map entry that uses clientKey as the key. There should be at most one such entry per-package. If more than one package defines an entry with the same clientKey the implementation may choose one of them arbitrarily. If the clientKey isn't located the implementation may return a 404, or it may fallback to other implementation-specific techniques to satisfy the request. If the filename requested in the client's query does not match the filename in the map entry's path, the service implementation should return an HTTP error, 404 is recommended. Otherwise the service should extract the file from the package return it in the HTTP response.


It is suggested, but not required, that implementations use caching to lower the processing time required to respond to client requests. The format and protocol have been designed in such a way that it is possible to pre-compute all valid client request URIs and the binary content that should be served back.

## Combining with other sources of clientKeys ##

It is possible to run an SSQP service that uses more than one data source to determine the total set of clientKey/filename requests it is able to respond to. For example most existing NuGet symbol service implementations compute their own mappings for files in specific portions of a NuGet symbol package if the files are one of a few well-known formats. This specification explicitly allows for these other data sources to be integrated as long as mappings in symbol\_index.json are given precedence whenever they are present.