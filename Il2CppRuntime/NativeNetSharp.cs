using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using System.IO;
using Microsoft.Win32.SafeHandles;

namespace Il2CppRuntime
{
    public unsafe static class NativeNetSharp
    {
        public delegate dynamic DynamicDelegate(params dynamic[] args);
        static ModuleBuilder moduleBuilder;
        public static IntPtr GetFunctionPointerForNativeCode(Action a)
        {
            var methodInfo = a.Method;
            if (moduleBuilder == null) moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("JitDelegateTypes"), AssemblyBuilderAccess.Run).DefineDynamicModule("JitDelegateTypes");
            var parameters = methodInfo.GetParameters().Select(x => x.ParameterType).ToArray();
            var builder = moduleBuilder.DefineType(methodInfo.Name + "Delegate", TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoClass, typeof(MulticastDelegate));
            builder.DefineConstructor(MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public, CallingConventions.Standard, new Type[] { typeof(Object), typeof(IntPtr) }).SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
            builder.DefineMethod("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual, methodInfo.ReturnType, parameters).SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
            var delegateType = builder.CreateTypeInfo();
            var dlg = Delegate.CreateDelegate(delegateType, null, methodInfo);
            return Marshal.GetFunctionPointerForDelegate<Delegate>(dlg);
        }
        public static T FastCallDelegate<T>(IntPtr functionPtr)
        {
            var wrapper = new List<Byte>();
            wrapper.Add(0x58);          // pop eax  - store the return address
            wrapper.Add(0x59);          // pop ecx  - move the 1st argument to ecx
            wrapper.Add(0x5A);          // pop edx  - move the 2nd argument to edx
            wrapper.Add(0x50);          // push eax - restore the return address
            wrapper.Add(0x68);                                                  // push ...
            wrapper.AddRange(BitConverter.GetBytes(functionPtr.ToInt32()));     // the function address to call
            wrapper.Add(0xC3);                                                  // ret - and jump to
            var wrapperPtr = Marshal.AllocHGlobal(wrapper.Count);
            Marshal.Copy(wrapper.ToArray(), 0, wrapperPtr, wrapper.Count);
            return Marshal.GetDelegateForFunctionPointer<T>(wrapperPtr);
        }
        public static void JmpPatch(IntPtr originalPtr, IntPtr replacement)
        {
            // todo fix
            throw new Exception("JmpPatch not supported");
            var origCodeLoc = Marshal.ReadIntPtr(originalPtr);
            var jmpToNew = new List<Byte>();
            if (Environment.Is64BitProcess)
            {
                jmpToNew.AddRange(new Byte[] { 0x49, 0xBB }); // mov r11, replacement
                jmpToNew.AddRange(BitConverter.GetBytes(replacement.ToInt64()));
                jmpToNew.AddRange(new Byte[] { 0x41, 0xFF, 0xE3 }); // jmp r11
            }
            else
            {
                jmpToNew.Add(0xB8); // mov eax
                jmpToNew.AddRange(BitConverter.GetBytes(replacement.ToInt32()));
                jmpToNew.AddRange(new Byte[] { 0xFF, 0xE0 }); // jmp eax
            }
            var origCode = new byte[0x12];
            Marshal.Copy(origCodeLoc, origCode, 0, origCode.Length);
            var jmpToOrig = new List<Byte>();
            jmpToOrig.AddRange(origCode);
            if (Environment.Is64BitProcess)
            {
                jmpToOrig.AddRange(new Byte[] { 0x49, 0xBB }); // mov r11, replacement
                jmpToOrig.AddRange(BitConverter.GetBytes((origCodeLoc + origCode.Length).ToInt64()));
                jmpToOrig.AddRange(new Byte[] { 0x41, 0xFF, 0xE3 }); // jmp r11
            }
            else
            {

            }
            var newFuncLocation = VirtualAllocEx(GetCurrentProcess(), IntPtr.Zero, 0x100, 0x3000, 0x40);
            Marshal.Copy(jmpToOrig.ToArray(), 0, newFuncLocation, jmpToOrig.ToArray().Length);

            VirtualProtect(origCodeLoc, (UIntPtr)jmpToNew.ToArray().Length, (UInt32)0x40, out UInt32 old);
            Marshal.Copy(jmpToNew.ToArray(), 0, origCodeLoc, jmpToNew.ToArray().Length);
            FlushInstructionCache(GetCurrentProcess(), origCodeLoc, (UIntPtr)jmpToNew.ToArray().Length);
            VirtualProtect(origCodeLoc, (UIntPtr)jmpToNew.ToArray().Length, old, out UInt32 _);

            Marshal.WriteIntPtr(originalPtr, newFuncLocation);
        }
        public static IntPtr baseAddress;
        public static IntPtr procHandle;
        public static Boolean target32Bit = false;
        public static void Inject(String procName, Byte[] exeBytes)
        {
            var CLSID_CLRMetaHost = new Guid("9280188D-0E8E-4867-B30C-7FA83884E8DE").ToByteArray();
            var IID_ICLRMetaHost = new Guid("D332DB9E-B9B3-4125-8207-A14884F53216").ToByteArray();
            var IID_ICLRRuntimeInfo = new Guid("BD39D1D2-BA2F-486A-89B0-B4B0CB466891").ToByteArray();
            var CLSID_CorRuntimeHost = new Guid("CB2F6723-AB3A-11D2-9C40-00C04FA30A3E").ToByteArray();
            var IID_ICorRuntimeHost = new Guid("CB2F6722-AB3A-11D2-9C40-00C04FA30A3E").ToByteArray();
            var _AppDomain = new Guid("05F696DC-2B29-3663-AD8B-C4389CF2A713").ToByteArray();
            var CLSID_CLRRuntimeHost = new Guid("90F1A06E-7712-4762-86B5-7A5EBA6BDB02").ToByteArray();
            var IID_ICLRRuntimeHost = new Guid("90F1A06C-7712-4762-86B5-7A5EBA6BDB02").ToByteArray();

            while (Process.GetProcessesByName(procName).Length == 0) { System.Threading.Thread.Sleep(500); }
            var targetProcess = Process.GetProcessesByName(procName)[0];
            procHandle = OpenProcess(0x43a, false, targetProcess.Id);
            if (procHandle == IntPtr.Zero) throw new Exception("can't open target process. try run as admin");
            baseAddress = GetBaseAddress();
            if (!IsWow64Process(procHandle, out target32Bit)) target32Bit = IntPtr.Size == 4;
            RemoteLoadLibrary("mscoree.dll");
            var ptrSize = target32Bit ? 4 : 8;

            var CLRCreateInstance = GetProcAddress("mscoree.dll", "CLRCreateInstance");
            var safeArray = CreateSafeArray(exeBytes.ToList());
            var metaHost = ExecFunc(CLRCreateInstance, CLSID_CLRMetaHost, IID_ICLRMetaHost, new Byte[0]);
            var runtime = ExecVTable(metaHost, 3 * ptrSize, Encoding.Unicode.GetBytes("v4.0.30319"), IID_ICLRRuntimeInfo, new Byte[0]);
            var runtimeHost = ExecVTable(runtime, 9 * ptrSize, CLSID_CorRuntimeHost, IID_ICorRuntimeHost, new Byte[0]);
            var started = ExecVTable(runtimeHost, 0xA * ptrSize);
            var domain = ExecVTable(runtimeHost, 0xD * ptrSize, new Byte[0]);
            var appDomain = ExecVTable(domain, 0, _AppDomain, new Byte[0]);
            var assembly = ExecVTable(appDomain, 0x2D * ptrSize, BitConverter.GetBytes((UInt64)safeArray), new Byte[0]);
            var method = ExecVTable(assembly, 0x10 * ptrSize, new Byte[0]);
            var variant = new Byte[0x18]; variant[0] = 1;
            var mainReturnVal = new Byte[0x18];
            var methodResult = ExecVTable(method, 0x25 * ptrSize, variant, new Byte[8], new Byte[8], new Byte[8], new Byte[8], mainReturnVal); // todo fix parameters... x86 has more?
            var val = BitConverter.ToUInt64(mainReturnVal, 8);
            //var released = ExecVTable(method, 2 * ptrSize); // not sure how this works if the app is still running.
            //VirtualFreeEx(procHandle, safeArray - 0x10, 0, 0x8000);
            //var stopped = ExecVTable(runtimeHost, 0xB * ptrSize); // not sure how this works if the app is still running.
        }
        [DllImport("kernel32")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32")] static extern IntPtr OpenProcess(Int32 dwDesiredAccess, Boolean bInheritHandle, Int32 dwProcessId);
        [DllImport("kernel32")] static extern IntPtr GetModuleHandle(String lpModuleName);
        //[DllImport("kernel32")] static extern IntPtr GetProcAddress(IntPtr hModule, String procName);
        [DllImport("kernel32")] static extern Boolean FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);
        [DllImport("kernel32")] public static extern Boolean VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, UInt32 flNewProtect, out UInt32 lpflOldProtect);
        [DllImport("kernel32")] static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, Int32 dwSize, UInt32 flAllocationType, UInt32 flProtect);
        [DllImport("kernel32")] static extern Int32 ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In, Out] Byte[] buffer, Int32 size, out Int32 lpNumberOfBytesRead);
        [DllImport("kernel32")] static extern Boolean WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, Byte[] lpBuffer, Int32 nSize, out Int32 lpNumberOfBytesWritten);
        [DllImport("kernel32")] static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, UInt32 dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, UInt32 dwCreationFlags, IntPtr lpThreadId);
        [DllImport("kernel32")] static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);
        [DllImport("kernel32")] static extern Int32 CloseHandle(IntPtr hObject);
        [DllImport("kernel32")] static extern Boolean VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, Int32 dwSize, UInt32 flNewProtect, out UInt32 lpflOldProtect);
        [DllImport("kernel32")] static extern Boolean VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, Int32 dwSize, Int32 dwFreeType);
        [DllImport("kernel32")] static extern Boolean IsWow64Process(IntPtr processHandle, out Boolean wow64Process);
        [DllImport("psapi")] static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, UInt32 cb);
        [DllImport("psapi")] static extern bool EnumProcessModulesEx(IntPtr hProcess, IntPtr[] lphModule, UInt32 cb, out UInt32 lpcbNeeded, UInt32 dwFilterFlag);
        [DllImport("psapi")] static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, UInt32 nSize);
        [DllImport("kernel32")] public static extern IntPtr GetStdHandle(Int32 nStdHandle);
        [DllImport("kernel32")] public static extern int AllocConsole();
        [DllImport("kernel32")] public static extern int FreeConsole();
        [StructLayout(LayoutKind.Sequential)]
        public struct MODULEINFO
        {
            public IntPtr lpBaseOfDll;
            public UInt32 SizeOfImage;
            public IntPtr EntryPoint;
        }
        public static IntPtr CreateSafeArray(List<Byte> bytes)
        {
            var safeArray = VirtualAllocEx(procHandle, IntPtr.Zero, bytes.Count + 0x30, 0x1000, 4);
            var safeArrayBytes = new List<Byte>();
            safeArrayBytes.AddRange(BitConverter.GetBytes((UInt32)0)); // ??
            safeArrayBytes.AddRange(BitConverter.GetBytes((UInt32)0)); // ??
            safeArrayBytes.AddRange(BitConverter.GetBytes((UInt32)0)); // ??
            safeArrayBytes.AddRange(BitConverter.GetBytes((UInt32)0x11)); // arrayType... weird
            safeArrayBytes.AddRange(BitConverter.GetBytes((UInt16)1)); // cDims
            safeArrayBytes.AddRange(BitConverter.GetBytes((UInt16)0x80)); // fFeatures
            safeArrayBytes.AddRange(BitConverter.GetBytes((UInt32)1)); // cbElements
            safeArrayBytes.AddRange(BitConverter.GetBytes((UInt32)0)); // cLocks
            if (!target32Bit) safeArrayBytes.AddRange(BitConverter.GetBytes((UInt32)0)); // ???
            safeArrayBytes.AddRange(target32Bit ? BitConverter.GetBytes((Int32)safeArray + 0x28) : BitConverter.GetBytes((UInt64)safeArray + 0x30)); // pvData
            safeArrayBytes.AddRange(BitConverter.GetBytes((UInt32)bytes.Count)); // rgsabound numElements
            safeArrayBytes.AddRange(BitConverter.GetBytes((UInt32)0)); // rgsabound min
            safeArrayBytes.AddRange(bytes);
            WriteProcessMemory(procHandle, safeArray, safeArrayBytes.ToArray(), safeArrayBytes.Count, out _);
            return safeArray + 0x10;
        }
        public static IntPtr ExecVTable(IntPtr obj, Int32 offset, params Byte[][] args)
        {
            var methodAddr = ReadIntPtr(ReadIntPtr(obj) + offset);
            args = args.Prepend(BitConverter.GetBytes((UInt64)obj)).ToArray();
            return ExecFunc(methodAddr, args);
        }
        public static IntPtr ExecFunc(IntPtr funcAddr, params Byte[][] args)
        {
            var newArgs = new List<IntPtr>();
            foreach (var arg in args)
            {
                if (arg.Length == 8) newArgs.Add((IntPtr)(target32Bit ? BitConverter.ToInt32(arg, 0) : BitConverter.ToInt64(arg, 0))); // todo fix hack for direct args
                else
                {
                    var argLength = arg.Length == 0 ? 0x8 : arg.Length;
                    var argVal = arg;
                    if (arg.Length == 0)
                    {
                        argLength = target32Bit ? 4 : 8;
                        argVal = new byte[argLength];
                    }
                    var temp = VirtualAllocEx(procHandle, IntPtr.Zero, argLength, 0x3000, 0x40);
                    WriteProcessMemory(procHandle, temp, argVal, argLength, out _);
                    newArgs.Add(temp);
                }
            }
            var retVal = ExecFunc(funcAddr, newArgs.ToArray());
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                var argLength = arg.Length == 0 ? (target32Bit ? 4 : 8) : arg.Length;
                var buf = new Byte[argLength];
                if (args[i].Length == 8u) { }
                else
                {
                    ReadProcessMemory(procHandle, newArgs[i], buf, buf.Length, out _);
                    if (args[i].Length != argLength) Array.Resize(ref args[i], argLength);
                    Array.Copy(buf, args[i], argLength);
                    VirtualFreeEx(procHandle, newArgs[i], 0, 0x8000);
                }
            }
            if (retVal == IntPtr.Zero && args.ToList().Last().Length == (target32Bit ? 4u : 8u)) return (IntPtr)(target32Bit ? BitConverter.ToInt32(args.ToList().Last(), 0) : BitConverter.ToInt64(args.ToList().Last(), 0)); // todo fix hack for arg refs
            else return retVal;
        }
        public static IntPtr ExecFunc(IntPtr funcAddr, params IntPtr[] args)
        {
            var asm = new List<Byte>();
            var retVal = VirtualAllocEx(procHandle, IntPtr.Zero, 8, 0x3000, 4);
            WriteProcessMemory(procHandle, retVal, BitConverter.GetBytes(0xdeadbeefcafef00d), 8, out _);
            if (target32Bit)
            {
                for (var i = args.Length - 1; i >= 0; i--)
                {
                    asm.Add(0x68); // push
                    asm.AddRange(BitConverter.GetBytes((UInt32)args[i]));
                }
                asm.Add(0xB8); // mov eax
                asm.AddRange(BitConverter.GetBytes((UInt32)funcAddr));
                asm.AddRange(new Byte[] { 0xFF, 0xD0 }); // call eax
                // todo fix this?
                //asm.AddRange(new Byte[] { 0x83, 0xC4 }); // add esp
                //asm.Add((Byte)(args.Length * 4));
                asm.Add(0xA3); // mov eax to
                asm.AddRange(BitConverter.GetBytes((UInt32)retVal));
            }
            else
            {
                asm.AddRange(new Byte[] { 0x48, 0x83, 0xEC, 0x38 }); // sub rsp 0x38
                for (var i = 0; i < args.Length && i < 4; i++)
                {
                    if (i == 0) asm.AddRange(new Byte[] { 0x48, 0xB9 }); // mov rcx
                    if (i == 1) asm.AddRange(new Byte[] { 0x48, 0xBA }); // mov rdx
                    if (i == 2) asm.AddRange(new Byte[] { 0x49, 0xB8 }); // mov r8
                    if (i == 3) asm.AddRange(new Byte[] { 0x49, 0xB9 }); // mov r9
                    asm.AddRange(BitConverter.GetBytes((UInt64)args[i]));
                }
                for (var i = 4; i < args.Length; i++) // broke need to fix
                {
                    /*asm.Add(0x68);
                    asm.AddRange(BitConverter.GetBytes((UInt32)(UInt64)args[i]));
                    asm.Add(0x68);
                    asm.AddRange(BitConverter.GetBytes(((UInt64)args[i]) >> 32));*/
                }
                asm.AddRange(new Byte[] { 0x48, 0xB8 }); // mov rax
                asm.AddRange(BitConverter.GetBytes((UInt64)funcAddr));

                asm.AddRange(new Byte[] { 0xFF, 0xD0 }); // call rax
                asm.AddRange(new Byte[] { 0x48, 0x83, 0xC4, 0x38 }); // add rsp 0x38

                asm.AddRange(new Byte[] { 0x48, 0xA3 }); // mov rax to retval
                asm.AddRange(BitConverter.GetBytes((UInt64)retVal));
            }
            asm.AddRange(Enumerable.Range(0, 0x20).Select(a => (byte)0x90));
            asm.Add(0xC3); // ret
            var codePtr = VirtualAllocEx(procHandle, IntPtr.Zero, asm.Count, 0x3000, 0x40);
            WriteProcessMemory(procHandle, codePtr, asm.ToArray(), asm.Count, out _);
            var qq = BitConverter.ToString(asm.ToArray()).Replace("-", " ");
            var thread = CreateRemoteThread(procHandle, IntPtr.Zero, 0, codePtr, IntPtr.Zero, 0, IntPtr.Zero);
            WaitForSingleObject(thread, 10000);
            var buf = new Byte[target32Bit ? 4u : 8u];
            ReadProcessMemory(procHandle, retVal, buf, buf.Length, out _);
            VirtualFreeEx(procHandle, retVal, 0, 0x8000);
            VirtualFreeEx(procHandle, codePtr, 0, 0x8000);
            CloseHandle(thread);
            return (IntPtr)(target32Bit ? BitConverter.ToInt32(buf, 0) : BitConverter.ToInt64(buf, 0));
        }
        public static IntPtr ReadIntPtr(IntPtr addr)
        {
            var buf = new Byte[8];
            ReadProcessMemory(procHandle, addr, buf, buf.Length, out _);
            return (IntPtr)(target32Bit ? BitConverter.ToInt32(buf, 0) : BitConverter.ToInt64(buf, 0));
        }
        public static Int32 ReadInt32(IntPtr addr)
        {
            var temp = new Byte[4];
            ReadProcessMemory(procHandle, addr, temp, temp.Length, out _);
            return BitConverter.ToInt32(temp, 0);
        }
        public static IntPtr LoadLibraryA = IntPtr.Zero;
        public static void RemoteLoadLibrary(String dllName)
        {
            if (LoadLibraryA == IntPtr.Zero) LoadLibraryA = GetProcAddress("kernel32.dll", "LoadLibraryA");
            var allocMemAddress = VirtualAllocEx(procHandle, IntPtr.Zero, ((dllName.Length + 1) * Marshal.SizeOf(typeof(char))), 0x3000, 4);
            WriteProcessMemory(procHandle, allocMemAddress, Encoding.Default.GetBytes(dllName), ((dllName.Length + 1) * Marshal.SizeOf(typeof(char))), out _);
            var thread = CreateRemoteThread(procHandle, IntPtr.Zero, 0, LoadLibraryA, allocMemAddress, 0, IntPtr.Zero); WaitForSingleObject(thread, 10000);
            VirtualFreeEx(procHandle, allocMemAddress, 0, 0x8000);
        }
        public static IntPtr GetBaseAddress()
        {
            var ptrs = new IntPtr[1];
            EnumProcessModulesEx(procHandle, ptrs, target32Bit ? 4u : 8u, out _, 3);
            return ptrs[0];
        }
        public static IntPtr GetProcAddress(String dll, String procName)
        {
            var ptrs = new IntPtr[0];
            var q = EnumProcessModulesEx(procHandle, ptrs, 0, out UInt32 bytesNeeded, 3);
            var size = target32Bit ? 4 : 8;
            var moduleCount = bytesNeeded / size;
            ptrs = new IntPtr[moduleCount];
            EnumProcessModulesEx(procHandle, ptrs, bytesNeeded, out _, 3);
            for (var i = 0; i < moduleCount; i++)
            {
                var path = new StringBuilder(260);
                GetModuleFileNameEx(procHandle, ptrs[i], path, 260);

                if (path.ToString().ToLower().Contains(dll.ToLower()))
                {
                    GetModuleInformation(procHandle, ptrs[i], out MODULEINFO info, (uint)(size * ptrs.Length));
                    var e_lfanew = ReadInt32(info.lpBaseOfDll + 0x3C);
                    var ntHeaders = info.lpBaseOfDll + e_lfanew;
                    var optionalHeader = ntHeaders + 0x18;
                    var dataDirectory = optionalHeader + (target32Bit ? 0x60 : 0x70);
                    var exportDirectory = info.lpBaseOfDll + ReadInt32(dataDirectory);
                    var names = info.lpBaseOfDll + ReadInt32(exportDirectory + 0x20);
                    var ordinals = info.lpBaseOfDll + ReadInt32(exportDirectory + 0x24);
                    var functions = info.lpBaseOfDll + ReadInt32(exportDirectory + 0x1C);
                    var numFuncs = ReadInt32(exportDirectory + 0x18);

                    for (var j = 0; j < numFuncs; j++)
                    {
                        var offset = ReadInt32(names + j * 4);
                        var buffer = new Byte[32];
                        ReadProcessMemory(procHandle, info.lpBaseOfDll + offset, buffer, 32, out _);
                        var name = Encoding.UTF8.GetString(buffer);
                        if (name.Contains("\0")) name = name.Substring(0, name.IndexOf("\0"));
                        var ordinal = ReadInt32(ordinals + j * 2) & 0xFFFF;
                        var address = info.lpBaseOfDll + ReadInt32(functions + ordinal * 4);
                        if (name == procName) return address;
                    }
                }
            }
            return IntPtr.Zero;
        }
    }
}