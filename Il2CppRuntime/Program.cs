using System.Text;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Mono.Cecil;

namespace Il2CppRuntime
{
    public unsafe static class Program
    {
        static void Main()
        {
            if (Process.GetCurrentProcess().ProcessName == typeof(Program).Namespace)
            {
                var dll = AssemblyDefinition.ReadAssembly(Assembly.GetEntryAssembly().Location);
                var rand = Guid.NewGuid().ToString().Replace("-", "");
                dll.Name.Name += rand;
                dll.MainModule.Name += rand;
                dll.MainModule.Types.ToList().ForEach(t => t.Namespace += rand);
                var dllBytes = new Byte[0];
                using (var newDll = new MemoryStream())
                {
                    dll.Write(newDll);
                    dllBytes = newDll.ToArray();
                }
                NativeNetSharp.Inject("gameprocname", dllBytes);
            }
            else
            {
                try
                {
                    NativeNetSharp.AllocConsole();
                    var standardOutput = new StreamWriter(new FileStream(new SafeFileHandle(NativeNetSharp.GetStdHandle(-11), true), FileAccess.Write), Encoding.GetEncoding(437)) { AutoFlush = true };
                    Console.SetOut(standardOutput);
                    Console.WriteLine("C# DLL loaded");
                    Il2Cpp.Il2Cpp.il2cpp_thread_attach(Il2Cpp.Il2Cpp.il2cpp_domain_get());
                    Il2Cpp.Il2Cpp.InitAssemblies();
                    Il2Cpp.Dumper.Dump();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message.ToString());
                }
            }
        }
        [DllImport("kernel32")] static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);
        [DllImport("kernel32")] static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);
        [DllImport("kernel32")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32")] static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
        [DllImport("kernel32")] static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, Int32 dwSize, Int32 flAllocationType, Int32 flProtect);
        [DllImport("kernel32")] static extern IntPtr GetModuleHandle(string lpModuleName);
        public static void JmpPatch(IntPtr originalPtr, IntPtr replacement)
        {
            var origCodeLoc = Marshal.ReadIntPtr(originalPtr);
            var jmpToNew = new List<Byte>();
            jmpToNew.AddRange(new Byte[] { 0x49, 0xBB }); // mov r11, replacement
            jmpToNew.AddRange(BitConverter.GetBytes(replacement.ToInt64()));
            jmpToNew.AddRange(new Byte[] { 0x41, 0xFF, 0xE3 }); // jmp r11
            var origCode = new byte[0x12];
            Marshal.Copy(origCodeLoc, origCode, 0, origCode.Length);
            var jmpToOrig = new List<Byte>();
            jmpToOrig.AddRange(origCode);
            jmpToOrig.AddRange(new Byte[] { 0x49, 0xBB }); // mov r11, replacement
            jmpToOrig.AddRange(BitConverter.GetBytes((origCodeLoc + origCode.Length).ToInt64()));
            jmpToOrig.AddRange(new Byte[] { 0x41, 0xFF, 0xE3 }); // jmp r11
            var newFuncLocation = VirtualAllocEx(GetCurrentProcess(), IntPtr.Zero, 0x100, 0x3000, 0x40); // Marshal.Alloc doesn't work here?
            Marshal.Copy(jmpToOrig.ToArray(), 0, newFuncLocation, jmpToOrig.ToArray().Length);

            VirtualProtect(origCodeLoc, (UIntPtr)jmpToNew.ToArray().Length, (UInt32)0x40, out UInt32 old);
            Marshal.Copy(jmpToNew.ToArray(), 0, origCodeLoc, jmpToNew.ToArray().Length);
            FlushInstructionCache(GetCurrentProcess(), origCodeLoc, (UIntPtr)jmpToNew.ToArray().Length);
            VirtualProtect(origCodeLoc, (UIntPtr)jmpToNew.ToArray().Length, old, out UInt32 _);

            Marshal.WriteIntPtr(originalPtr, newFuncLocation);
        }
        public static void JmpUnPatch(IntPtr originalPtr, IntPtr replacement)
        {
            // todo
        }
        unsafe public static void Hook(IntPtr original, IntPtr target)
        {
            IntPtr originalPtr = original;
            IntPtr* targetVarPointer = &originalPtr;
            JmpPatch((IntPtr)targetVarPointer, target);
        }
    }
}
