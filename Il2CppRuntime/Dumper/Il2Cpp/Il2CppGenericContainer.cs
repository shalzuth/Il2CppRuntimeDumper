using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Il2CppRuntime.Il2Cpp
{
    public unsafe class Il2CppGenericContainer : Il2CppBase
    {
        public Int32 OwnerIndex;
        public Int32 Count;
        public Int32 IsMethod;
        public Int32 ParamStart;
        public static List<Il2CppGenericContainer> Generics = new List<Il2CppGenericContainer>();
        public Il2CppGenericContainer(IntPtr ptr) : base(ptr)
        {
            Ptr = ptr;
            OwnerIndex = *(Int32*)Ptr;
            Count = *(Int32*)(Ptr + 4);
            IsMethod = *(Int32*)(Ptr + 8);
            ParamStart = *(Int32*)(Ptr + 12);
        }
    }
}
