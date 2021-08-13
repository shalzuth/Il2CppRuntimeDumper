using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Mono.Cecil;

namespace Il2CppRuntime.Il2Cpp
{
    public unsafe class Il2CppMethod : Il2CppBase
    {
        public String Name;
        public MethodAttributes Flags;
        public MethodImplAttributes ImplFlags;
        public Il2CppType ReturnType;
        public IntPtr ReturnClassPtr;
        public Il2CppClass ReturnClass { get { return Il2CppClass.Generate(ReturnClassPtr); } }
        public Boolean IsGeneric;
        public Il2CppGenericContainer GenericContainer;
        public List<Il2Cpp_Method_Parameter> Parameters = new List<Il2Cpp_Method_Parameter>();
        public Il2CppMethod(IntPtr ptr) : base(ptr)
        {
            Ptr = ptr;
            Name = Marshal.PtrToStringAnsi(Il2Cpp.il2cpp_method_get_name(Ptr));
            ReturnType = new Il2CppType(Il2Cpp.il2cpp_method_get_return_type(Ptr));
            ReturnClassPtr = Il2Cpp.il2cpp_class_from_type(ReturnType.Ptr);
            if (!Il2CppClass.CachedClasses.ContainsKey(ReturnClassPtr)) Il2CppClass.ClassesToAdd.Add(ReturnClassPtr);
            var flags = 0u;
            Flags = (MethodAttributes)Il2Cpp.il2cpp_method_get_flags(Ptr, ref flags);
            ImplFlags = (MethodImplAttributes)flags;
            IsGeneric = Il2Cpp.il2cpp_method_is_generic(ptr);
            var param_count = Il2Cpp.il2cpp_method_get_param_count(Ptr);
            for (var i = 0u; i < param_count; i++) Parameters.Add(new Il2Cpp_Method_Parameter(Il2Cpp.il2cpp_method_get_param(Ptr, i), Marshal.PtrToStringAnsi(Il2Cpp.il2cpp_method_get_param_name(Ptr, i))));

            if (IsGeneric)
            {
                GenericContainer = new Il2CppGenericContainer(*(IntPtr*)(Ptr + 7 * 8));
                //Console.WriteLine(genBase.ToString("X"));
                var container = new Il2CppGenericContainer(GenericContainer.Ptr);
                while (Il2Cpp.ContainersPtrGuess == IntPtr.Zero)
                {
                    var prevContainer = new Il2CppGenericContainer(container.Ptr - 0x10);
                    if (prevContainer.ParamStart + prevContainer.Count != container.ParamStart) throw new ArithmeticException();
                    if (prevContainer.ParamStart == 0) Il2Cpp.ContainersPtrGuess = prevContainer.Ptr;
                    container = prevContainer;
                }
                // if (m.Name == "Instantiate" && m.Parameters.Count == 2)
            }
        }
    }
    public class Il2Cpp_Method_Parameter : Il2CppBase
    {
        public string Name { get; set; }
        public IntPtr ClassPtr { get; set; }
        public Il2CppClass Class { get { return Il2CppClass.Generate(ClassPtr); } }
        internal Il2Cpp_Method_Parameter(IntPtr ptr, string name) : base(ptr)
        {
            Ptr = ptr;
            Name = name;
            ClassPtr = Il2Cpp.il2cpp_class_from_type(Ptr);
            if (!Il2CppClass.CachedClasses.ContainsKey(ClassPtr)) Il2CppClass.ClassesToAdd.Add(ClassPtr);
        }
    }
}
