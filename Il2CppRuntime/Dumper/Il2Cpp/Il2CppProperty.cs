using System;
using System.Runtime.InteropServices;
using Mono.Cecil;
namespace Il2CppRuntime.Il2Cpp
{
    public class Il2CppProperty : Il2CppBase
    {
        public String Name;
        public PropertyAttributes Flags;
        public Il2CppMethod getMethod;
        public Il2CppMethod setMethod;
        public IntPtr ClassPtr;
        public Il2CppClass Class { get { return Il2CppClass.Generate(ClassPtr); } }
        public Il2CppProperty(IntPtr ptr) : base(ptr)
        {
            Ptr = ptr;
            Name = Marshal.PtrToStringAnsi(Il2Cpp.il2cpp_property_get_name(Ptr));
            Flags = (PropertyAttributes)Il2Cpp.il2cpp_property_get_flags(Ptr);
            var getMethodPtr = Il2Cpp.il2cpp_property_get_get_method(Ptr);
            if (getMethodPtr != IntPtr.Zero)
            {
                getMethod = new Il2CppMethod(getMethodPtr);
                ClassPtr = Il2Cpp.il2cpp_class_from_type(getMethod.ReturnType.Ptr);
            }
            var setMethodPtr = Il2Cpp.il2cpp_property_get_set_method(Ptr);
            if (setMethodPtr != IntPtr.Zero)
            {
                setMethod = new Il2CppMethod(setMethodPtr);
                ClassPtr = setMethod.Parameters[0].ClassPtr;
            }
        }
    }
}
