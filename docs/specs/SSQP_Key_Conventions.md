# SSQP Key conventions #

When using [SSQP](Simple_Symbol_Server_Protocol) it is critical that the content publishers and content consumers agree what keys should correspond to which files. Although any publisher-consumer pair is free to create private agreements, using a standard key format offers the widest compatibility.


## Key formatting basic rules
Unless otherwise specified:

- Bytes: Convert to characters by splitting the byte into the most significant 4 bits and 4 least significant bits, each of which has value 0-15. Convert each of those chunks to the corresponding lower case hexadecimal character. Last concatenate the two characters putting the most significant bit chunk first. For example 0 => '00', 1 => '01', 45 => '2d', 185 => 'b9'
- Byte sequences: Convert to characters by converting each byte as above and then concatenating the characters. For example 2,45,4 => '022d04'
- Multi-byte integers: Convert to characters by first converting it to a big-endian byte sequence next convert the sequence as above and finally trim all leading '0' characters. Example 3,559,453,162 => 'd428f1ea', 114 => '72'
- strings: Convert all the characters to lower-case
- guid: The guid consists of a 4 byte integer, two 2 byte integers, and a sequence of 8 bytes. It is formatted by converting each portion to hex characters without trimming leading '0' characters on the integers, then concatenate the results. Example: { 0x097B72F6, 0x390A, 0x04FC, { 0x87, 0x8E, 0x5A, 0x2D, 0x63, 0xB6, 0xCC, 0x4B } } => '097b72f6390a04fc878e5a2d63b6cc4b'

## Key formats


### PE-timestamp-filesize
This key references Windows Portable Executable format files which commonly have .dll or .exe suffixes. The key is computed by extracting the Timestamp (4 byte integer) and SizeOfImage (4 byte integer) fields from the COFF header in PE image. The key is formatted:

`<filename>/<Timestamp><SizeOfImage>/<filename>`

Example:
	
**File name:** `Foo.exe`

**COFF header Timestamp field:** `0x542d5742`

**COFF header SizeOfImage field:** `0x32000`

**Lookup key:** `foo.exe/542d574200032000/foo.exe`


### PDB-Signature-Age

This applies to Microsoft C++ Symbol Format, commonly called PDB and using files with the .pdb file extension. The key is computed by extracting the Signature (guid) and Age (4 byte integer) values from the guid stream within MSF container. The final key is formatted:

`<filename>/<Signature><Age>/<filename>`

Example:

**File name:** `Foo.pdb`

**Signature field:** `{ 0x497B72F6, 0x390A, 0x44FC, { 0x87, 0x8E, 0x5A, 0x2D, 0x63, 0xB6, 0xCC, 0x4B } }`

**Age field:** `0x1`

**Lookup key**: `foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pdb`


### Portable-Pdb-Signature

This applies to Microsoft .Net portable PDB format files, commonly using the suffix .pdb. The Portable PDB format uses the same key format as Windows PDBs, except that 0xffffffff (UInt32.MaxValue) is used for the age. In other words, the key is computed by extracting the Signature (guid) from debug metadata header and combining it with 'ffffffff'. The final key is formatted: 

`<filename>/<guid>ffffffff/<filename>`
 
Example:
	
**File name:** `Foo.pdb`

**Signature field:** `{ 0x497B72F6, 0x390A, 0x44FC { 0x87, 0x8E, 0x5A, 0x2D, 0x63, 0xB6, 0xCC, 0x4B } }`

**Lookup key:** `foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bffffffff/foo.pdb`


### ELF-buildid

This applies to any ELF format files that have been stripped of debugging information, commonly using the .so suffix or no suffix. The key is computed by reading the 20 byte sequence of the ELF Note section that is named “GNU” and that has note type PRPSINFO (3). The final key is formatted:

`elf-buildid/<note_byte_sequence>/<file_name>`

Example:

**File name:** `foo.so`

**Build note bytes:** `0x18, 0x0a, 0x37, 0x3d, 0x6a, 0xfb, 0xab, 0xf0, 0xeb, 0x1f, 0x09, 0xbe, 0x1b, 0xc4, 0x5b, 0xd7, 0x96, 0xa7, 0x10, 0x85`

**Lookup key:** `elf-buildid/180a373d6afbabf0eb1f09be1bc45bd796a71085/foo.so`


### ELF-buildid-sym

This applies to any ELF format files that have not been stripped of debugging information, commonly ending in ‘.so.dbg’ or ‘.dbg’. The key is computed by reading the 20 byte sequence of the ELF Note section that is named “GNU” and that has note type PRPSINFO (3). The file name is not used in the index because there are cases where all we have is the build id. The final key is formatted:

`elf-buildid-sym/<note_byte_sequence>/_.debug`

Example:

**File name:** `foo.so.dbg`

**Build note bytes:** `0x18, 0x0a, 0x37, 0x3d, 0x6a, 0xfb, 0xab, 0xf0, 0xeb, 0x1f, 0x09, 0xbe, 0x1b, 0xc4, 0x5b, 0xd7, 0x96, 0xa7, 0x10, 0x85`

**Lookup key:** `elf-buildid-sym/180a373d6afbabf0eb1f09be1bc45bd796a71085/_.debug`


### Mach-uuid
This applies to any MachO format files that have been stripped of debugging information, commonly ending in 'dylib'. The key is computed by reading the uuid byte sequence of the MachO LC_UUID load command. The final key is formatted:

`mach-uuid/<uuid_bytes>/<file_name>`

Example:

**File name:** `foo.dylib`

**Uuid bytes:** `0x49, 0x7B, 0x72, 0xF6, 0x39, 0x0A, 0x44, 0xFC, 0x87, 0x8E, 0x5A, 0x2D, 0x63, 0xB6, 0xCC, 0x4B`

**Lookup key:** `mach-uuid/497b72f6390a44fc878e5a2d63b6cc4b/foo.dylib`


### Mach-uuid-sym

This applies to any MachO format files that have not been stripped of debugging information, commonly ending in '.dylib.dwarf'. The key is computed by reading the uuid byte sequence of the MachO LC_UUID load command. The final key is formatted:

`mach-uuid-sym/<uuid_bytes>/_.dwarf`

Example:

**File name:** `foo.dylib.dwarf`

**Uuid bytes:** `0x49, 0x7B, 0x72, 0xF6, 0x39, 0x0A, 0x44, 0xFC, 0x87, 0x8E, 0x5A, 0x2D, 0x63, 0xB6, 0xCC, 0x4B`

**Lookup key:** `mach-uuid-sym/497b72f6390a44fc878e5a2d63b6cc4b/_.dwarf`


### SHA1

This applies to any file, but is commonly used on sources. The key is computed by calculating a SHA1 hash, then formatting the 20 byte hash sequence prepended with “sha1-“

Example:

**File name:** `Foo.cs`

**Sha1 hash bytes:** `0x49, 0x7B, 0x72, 0xF6, 0x39, 0x0A, 0x44, 0xFC, 0x87, 0x8E, 0x5A, 0x2D, 0x63, 0xB6, 0xCC, 0x4B, 0x0C, 0x2D, 0x99, 0x84`

**Lookup key:** `foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984/foo.cs`
