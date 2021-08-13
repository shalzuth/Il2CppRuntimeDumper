using System;
using System.Collections.Generic;
using Mono.Cecil;
using System.Runtime.InteropServices;

namespace Il2CppRuntime.Il2Cpp
{
    public unsafe class Il2CppClass : Il2CppBase
    {
        public IntPtr NamePtr;
        public String Name;
        public String Namespace;
        public TypeAttributes Flags;
        public List<Il2CppMethod> Methods = new List<Il2CppMethod>();
        public List<Il2CppField> Fields = new List<Il2CppField>();
        public List<Il2CppBase> Events = new List<Il2CppBase>();
        public List<Il2CppProperty> Properties = new List<Il2CppProperty>();
        public List<IntPtr> InterfacePtrs = new List<IntPtr>();
        public List<IntPtr> NestedTypePtrs = new List<IntPtr>();
        public IntPtr Image;
        public IntPtr BaseClassPtr = IntPtr.Zero;
        public Il2CppClass BaseClass { get { return Il2CppClass.Generate(BaseClassPtr); } }
        public IntPtr DeclaringTypePtr = IntPtr.Zero;
        public Il2CppClass DeclaringType { get { return Il2CppClass.Generate(DeclaringTypePtr); } }
        public Il2CppType Type;
        public Int32 GenericIndex = 0;
        //public IntPtr GenericClassPtr = IntPtr.Zero;
        //public Il2CppClass GenericClass { get { return Il2CppClass.Generate(GenericClassPtr); } }

        public static List<IntPtr> ClassesToAdd = new List<IntPtr>();
        public static Dictionary<IntPtr, Il2CppClass> CachedClasses = new Dictionary<IntPtr, Il2CppClass>();
        public static Int32 GenericOffset = Int32.MaxValue;
        public static Boolean NotFirstGeneric = false;
        public Il2CppClass(IntPtr ptr) : base(ptr)
        {
            Ptr = ptr;
            NamePtr = Il2Cpp.il2cpp_class_get_name(ptr);
            Name = Marshal.PtrToStringAnsi(Il2Cpp.il2cpp_class_get_name(ptr));
            Namespace = Marshal.PtrToStringAnsi(Il2Cpp.il2cpp_class_get_namespace(ptr));
            Flags = (TypeAttributes)Il2Cpp.il2cpp_class_get_flags(ptr);
            Image = Il2Cpp.il2cpp_class_get_image(ptr);
            Type = new Il2CppType(Il2Cpp.il2cpp_class_get_type(ptr));
            DeclaringTypePtr = Il2Cpp.il2cpp_class_get_declaring_type(ptr);
            if (DeclaringTypePtr != IntPtr.Zero && !Il2CppClass.CachedClasses.ContainsKey(DeclaringTypePtr)) Il2CppClass.ClassesToAdd.Add(DeclaringTypePtr);
            BaseClassPtr = Il2Cpp.il2cpp_class_get_parent(ptr);
            if (BaseClassPtr != IntPtr.Zero && !Il2CppClass.CachedClasses.ContainsKey(BaseClassPtr)) Il2CppClass.ClassesToAdd.Add(BaseClassPtr);
            var isGeneric = Il2Cpp.il2cpp_class_is_generic(ptr);
            if (isGeneric)
            {
                if (NotFirstGeneric)
                {
                    if (GenericOffset == Int32.MaxValue)
                    {
                        for (var i = 0x80; i < 0x100; i += 8)
                        {
                            var qq = *(Int32*)(ptr + i);
                            if (*(Int32*)(ptr + i) == 1)
                            {
                                GenericOffset = i;
                                break;
                            }
                        }
                    }
                    if (GenericOffset == Int32.MaxValue) throw new Exception("can't find generic");
                    GenericIndex = *(Int32*)(ptr + GenericOffset); // e8 for gunfire, //c8 for genshin // f0 for naraka
                }
                NotFirstGeneric = true;
               // Console.WriteLine(Namespace + " : " + Name + " : " + GenericIndex + " : " + Ptr.ToString("X"));
            //    GenericClassPtr = BaseClassPtr;
            }
            var classChildPtr = IntPtr.Zero;
            var iter = IntPtr.Zero;
            //while ((classChildPtr = Il2Cpp.il2cpp_class_get_nested_types(ptr, ref iter)) != IntPtr.Zero)
            {
                //NestedTypePtrs.Add(classChildPtr);
                //if (!Il2Cpp_Class.CachedClasses.ContainsKey(classChildPtr)) Il2Cpp_Class.ClassesToAdd.Add(classChildPtr);
            }
            iter = IntPtr.Zero;
            while ((classChildPtr = Il2Cpp.il2cpp_class_get_methods(ptr, ref iter)) != IntPtr.Zero) Methods.Add(new Il2CppMethod(classChildPtr));
            iter = IntPtr.Zero;
            while ((classChildPtr = Il2Cpp.il2cpp_class_get_fields(ptr, ref iter)) != IntPtr.Zero) Fields.Add(new Il2CppField(classChildPtr));
            iter = IntPtr.Zero;
            while ((classChildPtr = Il2Cpp.il2cpp_class_get_properties(ptr, ref iter)) != IntPtr.Zero) Properties.Add(new Il2CppProperty(classChildPtr));
            iter = IntPtr.Zero;
            while ((classChildPtr = Il2Cpp.il2cpp_class_get_interfaces(ptr, ref iter)) != IntPtr.Zero) InterfacePtrs.Add(classChildPtr);
            iter = IntPtr.Zero;
            //while ((classChildPtr = Il2Cpp.il2cpp_class_get_events(ptr, ref iter)) != IntPtr.Zero) Events.Add(new Il2Cpp_Base(classChildPtr));
        }
        public static Il2CppClass Generate(IntPtr ptr)
        {
            if (CachedClasses.ContainsKey(ptr)) return CachedClasses[ptr];
            var c = new Il2CppClass(ptr);
            CachedClasses.Add(ptr, c);
            return c;
        }

        public static IntPtr CreateInstance(Il2CppClass il2CppClass, Il2CppMethod constructor = null, IntPtr[] contructorParams = null)
        {
            int paramCount = contructorParams?.Length ?? 0;
            if (constructor == null && paramCount > 0)
                return IntPtr.Zero;
            IntPtr instance = Il2Cpp.il2cpp_object_new(il2CppClass.Ptr);
            if (constructor == null)
            {
                Il2Cpp.il2cpp_runtime_object_init(instance);
                return instance;
            }
            else
            {
                //constructor.Invoke(instance, contructorParams);
            }
            return instance;
        }
    }
}
