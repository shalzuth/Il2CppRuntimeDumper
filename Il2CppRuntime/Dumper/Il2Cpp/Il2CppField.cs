using System;
using System.Runtime.InteropServices;
using Mono.Cecil;

namespace Il2CppRuntime.Il2Cpp
{
    public unsafe class Il2CppField : Il2CppBase
    {
        public String Name;
        public FieldAttributes Flags;
        public UInt32 Offset;
        public Il2CppType Type;
        public IntPtr ClassPtr { get; set; }
        public Il2CppClass Class { get { return Il2CppClass.Generate(ClassPtr); } }
        public Il2CppField(IntPtr ptr) : base(ptr)
        {
            Ptr = ptr;
            Name = Marshal.PtrToStringAnsi(Il2Cpp.il2cpp_field_get_name(Ptr));
            Flags = (FieldAttributes)Il2Cpp.il2cpp_field_get_flags(Ptr);
            Type = new Il2CppType(Il2Cpp.il2cpp_field_get_type(Ptr));
            ClassPtr = Il2Cpp.il2cpp_class_from_type(Type.Ptr);
            Offset = Il2Cpp.il2cpp_field_get_offset(Ptr);
            if (!Il2CppClass.CachedClasses.ContainsKey(ClassPtr)) Il2CppClass.ClassesToAdd.Add(ClassPtr);
        }
        /*
        unsafe public Il2Cpp_Object GetValue() => GetValue(IntPtr.Zero);
        unsafe public Il2Cpp_Object GetValue(IntPtr obj)
        {
            IntPtr returnval;
            if (HasFlag(Il2Cpp_BindingFlags.FIELD_STATIC))
                returnval = Il2Cpp.il2cpp_field_get_value_object(Ptr, IntPtr.Zero);
            else
                returnval = Il2Cpp.il2cpp_field_get_value_object(Ptr, obj);
            if (returnval != IntPtr.Zero)
                return new Il2Cpp_Object(returnval, GetReturnType());
            return null;
        }

        unsafe public void SetValue(IntPtr value) => SetValue(IntPtr.Zero, value);
        unsafe public void SetValue(IntPtr obj, IntPtr value)
        {
            if (HasFlag(Il2Cpp_BindingFlags.FIELD_STATIC))
                Il2Cpp.il2cpp_field_static_set_value(Ptr, value);
            else
                Il2Cpp.il2cpp_field_set_value(obj, Ptr, value);
        }*/
    }
}
