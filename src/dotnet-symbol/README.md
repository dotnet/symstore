# Symbol downloader dotnet cli extension #

This tool can download all the files needed for debugging (symbols, modules, SOS and DAC for the coreclr module given) for any given core dump, minidump or any supported platform's file formats like ELF, MachO, Windows DLLs, PDBs and portable PDBs.
      
    Usage: dotnet symbol [options] <FILES>
    
    Arguments:
      <FILES>   List of files. Can contain wildcards.

    Options:
      --microsoft-symbol-server                         Add 'http://msdl.microsoft.com/download/symbols' symbol server path (default).
      --internal-server                                 Add 'http://symweb.corp.microsoft.com' internal symbol server path.
      --server-path <symbol server path>                Add a http server path.
      --authenticated-server-path <pat> <server path>   Add a http PAT authenticated server path.
      --cache-directory <file cache directory>          Add a cache directory.
      --recurse-subdirectories                          Process input files in all subdirectories.
      --symbols                                         Download the symbol files (.pdb, .dbg, .dwarf).
      --modules                                         Download the module files (.dll, .so, .dylib).
      --debugging                                       Download the special debugging modules (DAC, DBI, SOS).
      --windows-pdbs                                    Force the downloading of the Windows PDBs when Portable PDBs are also available.
      -o, --output <output directory>                   Set the output directory. Otherwise, write next to the input file (default).
      -d, --diagnostics                                 Enable diagnostic output.
      -h, --help                                        Show help information.

## Install ##

This is a dotnet global tool "extension" supported only in [.NET Core 2.1](https://www.microsoft.com/net/download/). The latest version of the downloader can be installed with the following command.

    dotnet tool install --global dotnet-symbol

## Examples ##

This will attempt to download all the modules, symbols and SOS/DAC files needed to debug the core dump including the managed assemblies and their PDBs if Linux/ELF core dump or Windows minidump:

    dotnet symbol coredump.4507

To download the symbol files for a specific assembly:

    dotnet symbol --symbols --cache-directory c:\temp\symcache --server-path http://symweb --output c:\temp\symout System.Threading.dll

Downloads all the symbol files for the shared runtime:

    dotnet symbol --symbols --output /tmp/symbols /usr/share/dotnet/shared/Microsoft.NETCore.App/2.0.3/*

After the symbols are downloaded to `/tmp/symbols` they can be copied back to the above runtime directory so the native debuggers like lldb or gdb can find them, but the copy needs to be superuser:

	sudo cp /tmp/symbols/* /usr/share/dotnet/shared/Microsoft.NETCore.App/2.0.3

To verify a symbol package on a local VSTS symbol server:

    dotnet symbol --authenticated-server-path x349x9dfkdx33333livjit4wcvaiwc3v4wjyvnq https://mikemvsts.artifacts.visualstudio.com/defaultcollection/_apis/Symbol/symsrv coredump.45634

## Notes ##

Core dumps generated with gdb (generate-core-file command) or gcore (utility that comes with gdb) do not currently work with this utility (issue [#47](https://github.com/dotnet/symstore/issues/47)).

The best way to generate core dumps on Linux (not supported on Windows or OSX) is to use the [createdump](https://github.com/dotnet/coreclr/blob/master/Documentation/botr/xplat-minidump-generation.md) facility that is part of .NET Core 2.0 and greater. It can be setup to automatically generate a "minidump" like ELF core dump when your .NET Core app crashes. The normal Linux system core generation also works just fine but they are usually a lot larger than necessary.


