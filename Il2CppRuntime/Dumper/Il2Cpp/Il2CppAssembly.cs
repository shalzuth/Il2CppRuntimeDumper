using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Il2CppRuntime.Il2Cpp
{
    public unsafe class Il2CppAssembly : Il2CppBase
    {
        public String Name;
        public List<Il2CppClass> Classes = new List<Il2CppClass>();

        public static IntPtr AssemblyGetTypesPtr = IntPtr.Zero;
        public Il2CppAssembly(IntPtr ptr, IntPtr assemblyPtr) : base(ptr)
        {
            Ptr = ptr;
            var imagePtr = Il2Cpp.il2cpp_assembly_get_image(assemblyPtr);
            Name = Marshal.PtrToStringAnsi(Il2Cpp.il2cpp_image_get_name(imagePtr));

            IntPtr* param = null;
            var returnedException = IntPtr.Zero;
            var assemblyTypesPtr = Il2Cpp.il2cpp_runtime_invoke(Il2Cpp.AssemblyGetTypesPtr, ptr, (void**)param, ref returnedException);
            var numTypes = (Int32)Il2Cpp.il2cpp_array_length(assemblyTypesPtr);
            for (var j = 0u; j < numTypes; j++)
            {
                var classPtr = Il2Cpp.il2cpp_class_from_system_type(*(IntPtr*)((UInt64)assemblyTypesPtr + 32 + j * 8));
                Classes.Add(Il2CppClass.Generate(classPtr));
            }
        }
    }
}
