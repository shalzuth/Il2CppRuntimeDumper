﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Il2CppRuntime.Il2Cpp
{
    public unsafe class Il2CppType : Il2CppBase
    {
        public UInt64 Value;
        public String Name;
        public Byte Rank;
        public Boolean ByRef;
        public Il2CppTypeEnum Type;
        public UInt16 Attributes;
        public static Dictionary<IntPtr, Il2CppType> CachedTypes = new Dictionary<IntPtr, Il2CppType>();
        public Il2CppType(IntPtr ptr) : base(ptr)
        {
            Ptr = ptr;
            Name = Marshal.PtrToStringAnsi(Il2Cpp.il2cpp_type_get_name(Ptr));
            ByRef = ((*(Byte*)(ptr + 0xA)) & 0x80) == 0x80;
            Rank = *(Byte*)(ptr + 0x8);
            Attributes = *(UInt16*)(ptr + 8);
            Type = (Il2CppTypeEnum)(*(Byte*)(ptr + 0xA));
            if (!CachedTypes.ContainsKey(Ptr)) CachedTypes.Add(Ptr, this);
        }
        public IntPtr PtrVal => *(IntPtr*)Ptr;
        public Il2CppClass GetClass()
        {
            return Il2CppClass.Generate(Il2Cpp.il2cpp_class_from_type(Ptr));
        }
        public static Il2CppType GenerateType(IntPtr Ptr)
        {
            if (CachedTypes.ContainsKey(Ptr)) return CachedTypes[Ptr];
            return new Il2CppType(Ptr);
        }
    }
    public enum Il2CppTypeEnum : Byte
    {
        IL2CPP_TYPE_END = 0x00,
        IL2CPP_TYPE_VOID = 0x01,
        IL2CPP_TYPE_BOOLEAN = 0x02,
        IL2CPP_TYPE_CHAR = 0x03,
        IL2CPP_TYPE_I1 = 0x04,
        IL2CPP_TYPE_U1 = 0x05,
        IL2CPP_TYPE_I2 = 0x06,
        IL2CPP_TYPE_U2 = 0x07,
        IL2CPP_TYPE_I4 = 0x08,
        IL2CPP_TYPE_U4 = 0x09,
        IL2CPP_TYPE_I8 = 0x0a,
        IL2CPP_TYPE_U8 = 0x0b,
        IL2CPP_TYPE_R4 = 0x0c,
        IL2CPP_TYPE_R8 = 0x0d,
        IL2CPP_TYPE_STRING = 0x0e,
        IL2CPP_TYPE_PTR = 0x0f,
        IL2CPP_TYPE_BYREF = 0x10,
        IL2CPP_TYPE_VALUETYPE = 0x11,
        IL2CPP_TYPE_CLASS = 0x12,
        IL2CPP_TYPE_VAR = 0x13,
        IL2CPP_TYPE_ARRAY = 0x14,
        IL2CPP_TYPE_GENERICINST = 0x15,
        IL2CPP_TYPE_TYPEDBYREF = 0x16,
        IL2CPP_TYPE_I = 0x18,
        IL2CPP_TYPE_U = 0x19,
        IL2CPP_TYPE_FNPTR = 0x1b,
        IL2CPP_TYPE_OBJECT = 0x1c,
        IL2CPP_TYPE_SZARRAY = 0x1d,
        IL2CPP_TYPE_MVAR = 0x1e,
        IL2CPP_TYPE_CMOD_REQD = 0x1f,
        IL2CPP_TYPE_CMOD_OPT = 0x20,
        IL2CPP_TYPE_INTERNAL = 0x21,
        IL2CPP_TYPE_MODIFIER = 0x40,
        IL2CPP_TYPE_SENTINEL = 0x41,
        IL2CPP_TYPE_PINNED = 0x45,
        IL2CPP_TYPE_ENUM = 0x55
    }
}
