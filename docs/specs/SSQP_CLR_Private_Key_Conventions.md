# SSQP CLR Private Key conventions #

These conventions are private extensions to the normal [SSQP conventions](SSQP_Key_Conventions.md). They fulfill niche scenarios specific to the CLR product and are not expected to be used within any general purpose index generating tool.

## Basic rules ##

The private conventions use the same basic rules for bytes, bytes sequences, integers, strings, etc as described in the standard conventions.

## Key formats ##

### PE-filesize-timestamp-coreclr

This key indexes an sos\*.dll or mscordaccore\*.dll file that should be used to debug a given coreclr.dll. The lookup key is computed similar to PE-timestamp-filesize except the timestamp and filesize values are taken from coreclr.dll rather than the file being indexed.
Example:

**Filename:** mscordaccore.dll

**CoreCLR’s COFF header Timestamp field:** 0x542d5742

**CoreCLR’s COFF header SizeOfImage field:** 0x32000

**Lookup key:** mscordaccore.dll/542d574200032000/mscordaccore.dll


### ELF-buildid-coreclr

This applies to any file named libmscordaccore.so or libsos.so that should be used to debug a given libcoreclr.so. The key is computed similarly to ELF-buildid except the note bytes is retrieved from the libcoreclr.so file and prepended with ‘elf-buildid-coreclr-‘.

Example:

**Filename:** libmscordaccore.so

**Libcoreclr’s build note bytes:** 0x49, 0x7B, 0x72, 0xF6, 0x39, 0x0A, 0x44, 0xFC, 0x87, 0x8E

**Lookup key:** mscordaccore.so/elf-buildid-coreclr-497b72f6390a44fc878e/mscordaccore.so

### Mach-uuid-coreclr

This applies to any file named libmscordaccore.dylib or libsos.dylib that should be used to debug a given libcoreclr.dylib. The key is computed similarly to Mach-uuid except the uuid is retrieved from the libcoreclr.dylib file and prepended with ‘mach-uuid-coreclr-‘

Example:

**Filename:** libmscordaccore.dylb

**Coreclr’s uuid bytes:** 0x49, 0x7B, 0x72, 0xF6, 0x39, 0x0A, 0x44, 0xFC, 0x87, 0x8E, 0x5A, 0x2D, 0x63, 0xB6, 0xCC, 0x4B

**Lookup key:** libmscordaccore.dylib/mach-uuid-coreclr-497b72f6390a44fc878e5a2d63b6cc4b/libmscordaccore.dylib
