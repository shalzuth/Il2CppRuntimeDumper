using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Il2CppRuntime.Il2Cpp
{
    public unsafe class Il2CppObject : Il2CppBase
    {
        public Il2CppType ReturnType;
        public Il2CppObject(IntPtr ptr, Il2CppType returntype) : base(ptr)
        {
            Ptr = ptr;
            ReturnType = returntype;
        }

        public Il2CppType GetReturnType() => ReturnType;

        public IntPtr UnboxIntPtr() => Il2Cpp.il2cpp_object_unbox(Ptr);
        public void* Unbox() => UnboxIntPtr().ToPointer();
        public T UnboxValue<T>() where T : unmanaged => *(T*)Unbox();
        //public string UnboxString() => Il2Cpp.IntPtrToString(Ptr);
    }
}
