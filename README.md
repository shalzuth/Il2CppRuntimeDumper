# Il2CppRuntimeDumper
 This project shows example code of how to dump out il2cpp assemblies from memory. This is useful for apps where the metadata is encrypted.
 
# How does this work
 The method heavily relies on the il2cpp reflection system, and uses il2cpp calls to reconstruct the dll's
 
# Known issues
 Game crashes - hopefully only after it dumps the game
 Invalid DLLs - it's still not perfect, feel free to drop a PR

# Todo
 Add memory location scripts for inclusion into IDA (like il2cppdumper has)
 Fix DLL generation to be 1:1 with il2cppdumper
 Remove lots of bad debug code

# Credits
 knah - AssemblyUnhollower (which this can be used with) & other help
 perfare / Jumboperson - for the original il2cppdumper
 