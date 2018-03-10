# Dotnet cli Symbols Downloader Utility #

This is currently very preliminary and not finished or supported.

This tool can download all the files needed for debugging (symbols, modules, SOS and DAC for the coreclr module given) for any given core dump, minidump or any supported platform's file formats like ELF, MachO, Windows PE and PDBs and portable PDBs.
      
    Usage: dotnet symbols [options] <FILES>
    
    Arguments:
      <FILES>   List of files. Can contain wildcards.
    
    Options:
      -ms, --microsoft-symbol-serverAdd 'http://msdl.microsoft.com/download/symbols' symbol server path (default).
      -mi, --ms-internal-server Add 'http://symweb.corp.microsoft.com' symbol server path.
      -s, --server-path <symbol server path>Add a http server path.
      -as, --authenticated-server-path <pat> <server path>  Add a http PAT authenticated server path.
      -c, --cache-directory <file cache directory>  Add a cache directory.
      -o, --output-directory <output directory> Set the output directory. Otherwise, write next to the input file (default).
      -r, --recurse-subdirectories  Process input files in all subdirectories.
      -y, --symbols-onlyDownload only the symbol files.
      -w, --force-windows-pdbs  Force downloading of the Windows PDBs.
      -d, --diagEnable diagnostic output.
      -h, --helpShow help information.

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
