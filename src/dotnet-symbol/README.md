# Dotnet cli Symbol Downloader Utility #

This is currently very preliminary and not finished or supported.

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
      --symbols-only                                    Download only the symbol files.
      --windows-pdbs                                    Force downloading of the Windows PDBs.
      -o, --output <output directory>                   Set the output directory. Otherwise, write next to the input file (default).
      -d, --diagnostics                                 Enable diagnostic output.
      -h, --help                                        Show help information.

## Examples ##

This will attempt to download all the modules, symbols and SOS/DAC files needed to debug the core dump including the managed assemblies and their PDBs if Linux/ELF core dump or Windows minidump:

    dotnet symbol coredump.4507

To download the symbol files for a specific assembly:

    dotnet symbol --symbols-only --cache-directory c:\temp\symcache --server-path http://symweb --output c:\temp\symout System.Threading.dll

Downloads all the symbol files for the shared runtime:

    dotnet symbol --symbols-only --output /tmp/symbols /usr/share/dotnet/shared/Microsoft.NETCore.App/2.0.0-preview3-25510-01/*

To verify a symbol package on a local VSTS symbol server:

    dotnet symbol --authenticated-server-path x349x9dfkdx33333livjit4wcvaiwc3v4wjyvnq https://mikemvsts.artifacts.visualstudio.com/defaultcollection/_apis/Symbol/symsrv coredump.45634
