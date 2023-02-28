# Sample Nuget Symbol Server #

This is a proof of concept implementation of a [Package Based Symbol Server](../../docs/specs/Package_Based_Symbol_Server.md)

## Running the example ##

Step 1 - Build the symstore repo
    
    c:\repos\symstore> build.cmd

Step 2 - Run the symbol server

    c:\repos\symstore> dotnet artifacts\bin\NugetSymbolServer\Debug\net6.0\NugetSymbolServer.dll

    info: NugetSymbolServer.Service.Models.PackageStore[0]
          Adding package c:\repos\symstore\artifacts\Debug\bin\NugetSymbolServer\net6.0\.\nuget_feed\HelloWorld.1.0.0.nupkg
    info: NugetSymbolServer.Service.Models.PackageBasedSymbolStore[0]
          Adding symbol entry helloworld.dll/57847aae8000/helloworld.dll => C:\Users\mikem\AppData\Local\Temp\NugetSymbolServerFileCache\HelloWorld.1.0.0\1\lib\netcoreapp1.0\HelloWorld.dll
    Hosting environment: Production
    Content root path: c:\repos\symstore\artifacts\bin\NugetSymbolServer\Debug\net6.0\
    Now listening on: http://localhost:5000
    Application started. Press Ctrl+C to shut down.

Step 2 - Use your favorite web browser to send an SSQP request

    http://localhost:5000/symbol/HelloWorld.dll/57847aae8000/HelloWorld.dll

or use the dotnet symbol tool (after installing it):

    dotnet symbol --server-path http://localhost:5000 <path>\HelloWorld.dll

Congratulations, you just made an request and downloaded a file. Normally a diagnostic tool would make this request on your behalf, for example when debugging a dump that was running the HelloWorld application.

## Further exploration ##

Now that you have gotten your feet wet lets explore how this worked. In this directory there is a folder called nuget\_feed which contains a sample HelloWorld.nupkg. When the server starts it reads from the config.json to determine which folder has the nuget packages to be served. Currently this points at nuget\_feed, but you can experiment with any folder you like.

*Be careful, if you use specify a relative path it will be appended to the content root
which is [this_directory]\bin\Debug\netcoreapp1.0\. The sample works because it copies
the HelloWorld.nupkg to the right bin directory during the build.*

Once the server runs it scans each package in the folder and unpacks the contents on disk. In each
package the server looks for a file called symbol_index.json. In the example HelloWorld package that file looks like this:

	[
      {
        "clientKey": "HelloWorld.dll/57847aae8000/HelloWorld.dll",
        "blobPath": "lib/netcoreapp1.0/HelloWorld.dll"
      }
    ]

The server uses this information to build an in-memory dictionary that maps the clientKey HelloWorld.dll/57847aae8000/HelloWorld.dll to the file in the HelloWorld.nupkg at relative path lib/netcoreapp1.0/HelloWorld.dll. When a request arrives in the format:

    http://localhost:5000/symbol/{*clientKey}

The server looks up the clientKey and then returns the corresponding file.


## Guide to the code ##

The server is written as a simple ASP.Net Core MVC app and I'll assume you are familiar with the basics of this application type. 

There are 4 main abstractions in the Model directory:

1. IPackageSource - this is the origin of all the packages that should be injested. A real server probably uses some type of upload mechanism, but the DirectoryPackageSource class implementation reads all Nuget packages in the configured directory and adds them to the IPackageStore
2. IPackageStore - this is a simple storage system for packages. The implementation in PackageStore class maintains a list of all the files in the package in memory and inserts each file into the IFileStore
3. IFileStore - this is a storage system for files. When a file is initially added an in-memory FileReference object is created. The FileReference can be cloned as needed, and when the last FileReference is disposed the file is deleted from the store. The FileStore class is a simple implementation of this that backs all the files with on-disk storage.
4. ISymbolAccess - this is the index which stores the mappings between clientKey and the file content. This is implemented with PackageBasedSymbolStore, a derived type of PackageStore. This specialized store reads the symbol_index.json as each package is added so that it can maintain a global index. It also maintains per-package indexes so that it can remove the correct entries from the global index when a package is removed from the store.

In the controller directory there is the symbol endpoint which parses the request into the clientKey and then uses ISymbolAccess to obtain the correct file to serve back. This controller also waits for the IPackageSource to finish the ingestion process before answering any queries, but in a production service its likely that packages are being added and removed in real-time so races between inserts and queries are expected.
