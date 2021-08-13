using System;
namespace Il2CppRuntime.Il2Cpp
{
    public class Il2CppBase
    {
        public IntPtr Ptr { get; set; }
        public Il2CppBase(IntPtr ptr) => Ptr = ptr;
    }
}
