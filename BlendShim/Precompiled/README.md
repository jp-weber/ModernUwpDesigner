### Why are these precompiled binaries here?

Because they are signed.

### How do I know they if they match the source?

The binaries are compiled with `/BREPRO` (Reproducible Build) and PDB symbols are included. You can verify the source code matches the binaries by using a tool like [WinDbg](https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/) to load the PDB files and check that PDB signature match and that the source code lines up with the disassembly of the binaries, or by compiling the source code on the same compiler (MSVC 14.44) and comparing files' bytes.