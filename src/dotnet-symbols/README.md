# Symbols Downloader Utility #

This tool can download all the files needed for debugging (symbols, modules, SOS and DAC for the coreclr module given) for any given core dump, minidump or any supported platform's file formats like ELF, MachO, Windows PE and PDBs and portable PDBs.
      
    dotnet-symbols [options] <files>

    -ms|--microsoft-symbol-server                       Add 'http://msdl.microsoft.com/download/symbols' symbol server path
    -mi|--ms-internal-server                            Add 'http://symweb.corp.microsoft.com' symbol server path
    -s |--server-path <symbol server path>              Add a http server path
    -as|--authenticated-server-path <pat> <server path> Add a http PAT authenticated server path
    -c |--cache-directory <file cache directory>        Add a cache directory
    -o |--output-directory <output directory>           Set the output directory
    -oi|--output-by-inputfile                           Write symbol file next to input file
    -p |--packages                                      Input files are nuget packages
    -r |--add-source-extension <ext>                    Add source file extension
    -e |--add-valid-extension <ext>                     Add file extension to be indexed in package
    -y |--symbols-only                                  Get only the symbol files
    -w |--force-windows-pdbs                            Force downloading of the Windows PDBs
    -d |--diag                                          Enable diagnostic output
    -vd|--verbose-diag                                  Enable diagnostic and verbose diagnostic output
    -h |--help                                          This help message

The --packages option allows nuget packages to be accepted as input files. To verify that the input files or nuget package has been uploaded to the specified symbol server (-ms, -mi, -s, -as or -ss) don't provide an output path (-o).

## Examples ##

This will attempt to download all the modules, symbols and SOS/DAC files needed to debug the core dump including the managed assemblies and their PDBs if Linux/ELF core dump or Windows minidump:

    dotnet symbols --cache-directory c:\temp\symcache --ms-internal-server --output-directory c:\temp\symout coredump.4507

To download the symbol files for a specific assembly:

    dotnet symbols --symbols-only --cache-directory c:\temp\symcache --server-path http://symweb --output-directory c:\temp\symout System.Threading.dll

Downloads all the symbol files for the shared runtime:

    dotnet symbols --symbols-only --cache-directory ~/symcache --server-path http://msdl.microsoft.com/download/symbols --output-directory /tmp/symbols /usr/share/dotnet/shared/Microsoft.NETCore.App/2.0.0-preview3-25510-01/*

If you run this as superuser (sudo) and change the output directory to the shared frame directory, the symbols can be downloaded side-by-side the runtime modules. Native debuggers like lldb should then automatically load the symbols for a runtime module.

To display the indexes for a specific binary or file:

    dotnet symbols --diag libcoreclr.so

To verify a symbol package on a local VSTS symbol server:

    dotnet symbols --authenticated-server-path x349x9dfkdx33333livjit4wcvaiwc3v4wjyvnq https://mikemvsts.artifacts.visualstudio.com/defaultcollection/_apis/Symbol/symsrv --packages runtime.linux-x64.Microsoft.NETCore.Runtime.CoreCLR.2.0.0.symbols.nupkg