using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Il2CppRuntime.Il2Cpp
{
    public unsafe static class Il2Cpp
    {
        //public const String UnityDllName = "UserAssembly";
        public const String UnityDllName = "GameAssembly";
        public static IntPtr Domain = IntPtr.Zero;
        public static IntPtr corlib = IntPtr.Zero;
        public static IntPtr ContainersPtrGuess = IntPtr.Zero;
        public static IntPtr AssemblyGetTypesPtr = IntPtr.Zero;
        public static Dictionary<IntPtr, Il2CppAssembly> Assemblies = new Dictionary<IntPtr, Il2CppAssembly>();
        public static void InitAssemblies()
        {
            Domain = il2cpp_domain_get();
            //il2cpp_thread_attach(Domain);
            corlib = il2cpp_get_corlib();
            IntPtr* param = null;
            var returnedException = IntPtr.Zero;
            var ac = il2cpp_class_from_name(corlib, "System", "AppDomain");
            Console.WriteLine(ac.ToString("X"));
            var getAssemblies = il2cpp_class_get_method_from_name(ac, "GetAssemblies", 0);
            var getCurDomain = il2cpp_class_get_method_from_name(ac, "getCurDomain", 0);
            var currentDomain = il2cpp_runtime_invoke(getCurDomain, IntPtr.Zero, (void**)param, ref returnedException);
            var gotAssemblies = il2cpp_runtime_invoke(getAssemblies, currentDomain, (void**)param, ref returnedException);

            var size = 0u;
            var assemblies = il2cpp_domain_get_assemblies(Domain, ref size);

            var assemblyClass = Il2Cpp.il2cpp_class_from_name(corlib, "System.Reflection", "Assembly");
            var assemblyLoad = Il2Cpp.il2cpp_class_get_method_from_name(assemblyClass, "Load", 1);
            AssemblyGetTypesPtr = Il2Cpp.il2cpp_class_get_method_from_name(assemblyClass, "GetTypes", 0);
            for (var i = 0u; i < size; i++)
            {
                var image = il2cpp_assembly_get_image(assemblies[i]);
                var dll = Marshal.PtrToStringAnsi(il2cpp_image_get_name(image));
                var assembly = *(IntPtr*)((UInt64)gotAssemblies + 32 + i * 8);
                Assemblies.Add(image, new Il2CppAssembly(assembly, assemblies[i]));
            }
            while (Il2CppClass.ClassesToAdd.Count > 0)
            {
                var toAdd = Il2CppClass.ClassesToAdd.ToList();
                Il2CppClass.ClassesToAdd.Clear();
                foreach (var c in toAdd) Il2CppClass.Generate(c);
            }
            if (ContainersPtrGuess != IntPtr.Zero)
            {
                var prevContainer = new Il2CppGenericContainer(ContainersPtrGuess);
                Il2CppGenericContainer.Generics.Add(prevContainer);
                var containerOffset = ContainersPtrGuess + 0x10;
                while (true)
                {
                    var container = new Il2CppGenericContainer(containerOffset);
                    containerOffset += 0x10;
                    if (container.IsMethod >= 2) break;
                    if (container.ParamStart != prevContainer.ParamStart + prevContainer.Count) break;
                    Il2CppGenericContainer.Generics.Add(container);
                    prevContainer = container;
                }
            }
            // Console.WriteLine("generics : " + Il2CppGenericContainer.Generics.Count.ToString("X"));
        }
        public static Dictionary<String, IntPtr> CachedAssemblies = new Dictionary<String, IntPtr>();
        public static IntPtr GetImage(String dllName)
        {
            if (CachedAssemblies.ContainsKey(dllName)) return CachedAssemblies[dllName];
            var domain = il2cpp_domain_get();
            var size = 0u;
            var assemblies = il2cpp_domain_get_assemblies(domain, ref size);
            for (var i = 0u; i < size; i++)
            {
                var image = il2cpp_assembly_get_image(assemblies[i]);
                var dll = Marshal.PtrToStringAnsi(il2cpp_image_get_name(image));
                if (dll == dllName)
                {
                    CachedAssemblies.Add(dllName, image);
                    return image;
                }

            }
            return IntPtr.Zero;
        }
        public static Dictionary<String, IntPtr> CachedMethod = new Dictionary<String, IntPtr>();
        public static IntPtr GetIl2CppMethod(IntPtr clazz, string methodName)
        {
            var cacheName = clazz.ToString("X") + "," + methodName;
            if (CachedMethod.ContainsKey(cacheName)) return CachedMethod[cacheName];
            var iter = IntPtr.Zero;
            IntPtr method;
            while ((method = il2cpp_class_get_methods(clazz, ref iter)) != IntPtr.Zero)
            {
                if (Marshal.PtrToStringAnsi(il2cpp_method_get_name(method)) != methodName) continue;
                CachedMethod.Add(cacheName, method);
                return method;
            }
            CachedMethod.Add(cacheName, IntPtr.Zero);
            //Console.WriteLine("throw exception");
            return IntPtr.Zero;
        }
        public static IntPtr GetIl2CppMethod(IntPtr clazz, bool isGeneric, string methodName, string returnTypeName, params string[] argTypes)
        {
            var cacheName = clazz.ToString("X") + "," + methodName + "," + returnTypeName + String.Join(",", argTypes);
            if (CachedMethod.ContainsKey(cacheName)) return CachedMethod[cacheName];
            var methodsSeen = 0;
            var lastMethod = IntPtr.Zero;
            var iter = IntPtr.Zero;
            IntPtr method;
            while ((method = il2cpp_class_get_methods(clazz, ref iter)) != IntPtr.Zero)
            {
                if (Marshal.PtrToStringAnsi(il2cpp_method_get_name(method)) != methodName) continue;
                if (il2cpp_method_get_param_count(method) != argTypes.Length) continue;
                if (il2cpp_method_is_generic(method) != isGeneric) continue;
                var returnType = il2cpp_method_get_return_type(method);
                var returnTypeNameActual = Marshal.PtrToStringAnsi(il2cpp_type_get_name(returnType));
                if (returnTypeNameActual != returnTypeName) continue;
                methodsSeen++;
                lastMethod = method;
                var badType = false;
                for (var i = 0; i < argTypes.Length; i++)
                {
                    var paramType = il2cpp_method_get_param(method, (uint)i);
                    var typeName = Marshal.PtrToStringAnsi(il2cpp_type_get_name(paramType));
                    if (typeName != argTypes[i])
                    {
                        badType = true;
                        break;
                    }
                }
                if (badType) continue;
                CachedMethod.Add(cacheName, method);
                return method;
            }
            Console.WriteLine("guessing method : " + methodName);
            if (methodsSeen > 0) return lastMethod;
            Console.WriteLine("throw exception");
            return IntPtr.Zero;
        }
        public static Dictionary<String, IntPtr> CachedClasses = new Dictionary<String, IntPtr>();
        public static IntPtr GetIl2CpClass(String dll, String namespaze, String clazz)
        {
            var cacheName = dll + "," + namespaze + "," + clazz;
            if (CachedClasses.ContainsKey(cacheName)) return CachedClasses[cacheName];
            var val = Il2Cpp.il2cpp_class_from_name(Il2Cpp.GetImage(dll), namespaze, clazz);
            CachedClasses.Add(cacheName, val);
            return IntPtr.Zero;
        }


        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_init(IntPtr domain_name);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_init_utf16(IntPtr domain_name);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_shutdown();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_set_config_dir(IntPtr config_path);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_set_data_dir(IntPtr data_path);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_set_temp_dir(IntPtr temp_path);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_set_commandline_arguments(int argc, IntPtr argv, IntPtr basedir);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_set_commandline_arguments_utf16(int argc, IntPtr argv, IntPtr basedir);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_set_config_utf16(IntPtr executablePath);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_set_config(IntPtr executablePath);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_set_memory_callbacks(IntPtr callbacks);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_get_corlib();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_add_internal_call(IntPtr name, IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_resolve_icall([MarshalAs(UnmanagedType.LPStr)] string name);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_alloc(uint size);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_free(IntPtr ptr);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_array_class_get(IntPtr element_class, uint rank);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_array_length(IntPtr array);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_array_get_byte_length(IntPtr array);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_array_new(IntPtr elementTypeInfo, ulong length);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_array_new_specific(IntPtr arrayTypeInfo, ulong length);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_array_new_full(IntPtr array_class, ref ulong lengths, ref ulong lower_bounds);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_bounded_array_class_get(IntPtr element_class, uint rank, bool bounded);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_array_element_size(IntPtr array_class);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_assembly_get_image(IntPtr assembly);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_enum_basetype(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_is_generic(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_is_inflated(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_is_assignable_from(IntPtr klass, IntPtr oklass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_is_subclass_of(IntPtr klass, IntPtr klassc, bool check_interfaces);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_has_parent(IntPtr klass, IntPtr klassc);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_from_il2cpp_type(IntPtr type);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_from_name(IntPtr image, [MarshalAs(UnmanagedType.LPStr)] string namespaze, [MarshalAs(UnmanagedType.LPStr)] string name);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_from_system_type(IntPtr type);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_element_class(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_events(IntPtr klass, ref IntPtr iter);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_fields(IntPtr klass, ref IntPtr iter);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_nested_types(IntPtr klass, ref IntPtr iter);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_interfaces(IntPtr klass, ref IntPtr iter);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_properties(IntPtr klass, ref IntPtr iter);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_property_from_name(IntPtr klass, IntPtr name);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_field_from_name(IntPtr klass, [MarshalAs(UnmanagedType.LPStr)] string name);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_methods(IntPtr klass, ref IntPtr iter);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_method_from_name(IntPtr klass, [MarshalAs(UnmanagedType.LPStr)] string name, int argsCount);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_name(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_namespace(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_parent(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_declaring_type(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_class_instance_size(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_class_num_fields(IntPtr enumKlass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_is_valuetype(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_class_value_size(IntPtr klass, ref uint align);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_is_blittable(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_class_get_flags(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_is_abstract(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_is_interface(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_class_array_element_size(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_from_type(IntPtr type);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_type(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_class_get_type_token(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_has_attribute(IntPtr klass, IntPtr attr_class);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_has_references(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_class_is_enum(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_image(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_class_get_assemblyname(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_class_get_rank(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_class_get_bitmap_size(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_class_get_bitmap(IntPtr klass, ref uint bitmap);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_get_meta_data_pool_memory();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_stats_dump_to_file(IntPtr path);
        //[DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public extern static ulong il2cpp_stats_get_value(IL2CPP_Stat stat);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_domain_get();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_domain_assembly_open(IntPtr domain, IntPtr name);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr* il2cpp_domain_get_assemblies(IntPtr domain, ref uint size);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_exception_from_name_msg(IntPtr image, IntPtr name_space, IntPtr name, IntPtr msg);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_get_exception_argument_null(IntPtr arg);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_format_exception(IntPtr ex, void* message, int message_size);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_format_stack_trace(IntPtr ex, void* output, int output_size);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_unhandled_exception(IntPtr ex);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_field_get_flags(IntPtr field);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_field_get_name(IntPtr field);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_field_get_parent(IntPtr field);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_field_get_offset(IntPtr field);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_field_get_type(IntPtr field);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_field_get_value(IntPtr obj, IntPtr field, void* value);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_field_get_value_object(IntPtr field, IntPtr obj);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_field_has_attribute(IntPtr field, IntPtr attr_class);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_field_set_value(IntPtr obj, IntPtr field, void* value);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_field_static_get_value(IntPtr field, void* value);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_field_static_set_value(IntPtr field, void* value);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_field_set_value_object(IntPtr instance, IntPtr field, IntPtr value);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_gc_collect(int maxGenerations);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_gc_collect_a_little();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_gc_disable();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_gc_enable();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_gc_is_disabled();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern long il2cpp_gc_get_used_size();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern long il2cpp_gc_get_heap_size();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_gc_wbarrier_set_field(IntPtr obj, out IntPtr targetAddress, IntPtr gcObj);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_gchandle_new(IntPtr obj, bool pinned);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_gchandle_new_weakref(IntPtr obj, bool track_resurrection);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_gchandle_get_target(uint gchandle);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_gchandle_free(uint gchandle);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_unity_liveness_calculation_begin(IntPtr filter, int max_object_count, IntPtr callback, IntPtr userdata, IntPtr onWorldStarted, IntPtr onWorldStopped);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_unity_liveness_calculation_end(IntPtr state);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_unity_liveness_calculation_from_root(IntPtr root, IntPtr state);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_unity_liveness_calculation_from_statics(IntPtr state);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_method_get_return_type(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_method_get_declaring_type(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_method_get_name(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_method_get_from_reflection(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_method_get_object(IntPtr method, IntPtr refclass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_method_is_generic(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_method_is_inflated(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_method_is_instance(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_method_get_param_count(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_method_get_param(IntPtr method, uint index);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_method_get_class(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_method_has_attribute(IntPtr method, IntPtr attr_class);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_method_get_flags(IntPtr method, ref uint iflags);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_method_get_token(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_method_get_param_name(IntPtr method, uint index);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_profiler_install(IntPtr prof, IntPtr shutdown_callback);
        // [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public extern static void il2cpp_profiler_set_events(IL2CPP_ProfileFlags events);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_profiler_install_enter_leave(IntPtr enter, IntPtr fleave);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_profiler_install_allocation(IntPtr callback);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_profiler_install_gc(IntPtr callback, IntPtr heap_resize_callback);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_profiler_install_fileio(IntPtr callback);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_profiler_install_thread(IntPtr start, IntPtr end);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_property_get_flags(IntPtr prop);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_property_get_get_method(IntPtr prop);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_property_get_set_method(IntPtr prop);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_property_get_name(IntPtr prop);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_property_get_parent(IntPtr prop);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_object_get_class(IntPtr obj);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_object_get_size(IntPtr obj);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_object_get_virtual_method(IntPtr obj, IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_object_new(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_object_unbox(IntPtr obj);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_value_box(IntPtr klass, IntPtr data);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_monitor_enter(IntPtr obj);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_monitor_try_enter(IntPtr obj, uint timeout);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_monitor_exit(IntPtr obj);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_monitor_pulse(IntPtr obj);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_monitor_pulse_all(IntPtr obj);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_monitor_wait(IntPtr obj);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_monitor_try_wait(IntPtr obj, uint timeout);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern unsafe IntPtr il2cpp_runtime_invoke(IntPtr method, IntPtr obj, void** param, ref IntPtr exc);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        // param can be of Il2CppObject*
        public static extern unsafe IntPtr il2cpp_runtime_invoke_convert_args(IntPtr method, IntPtr obj, void** param, int paramCount, ref IntPtr exc);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_runtime_class_init(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_runtime_object_init(IntPtr obj);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_runtime_object_init_exception(IntPtr obj, ref IntPtr exc);
        // [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public extern static void il2cpp_runtime_unhandled_exception_policy_set(IL2CPP_RuntimeUnhandledExceptionPolicy value);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_string_length(IntPtr str);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern unsafe char* il2cpp_string_chars(IntPtr str);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_string_new(string str);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_string_new_len(string str, uint length);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_string_new_utf16(char* text, int len);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_string_new_wrapper(string str);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_string_intern(string str);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_string_is_interned(string str);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_thread_current();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_thread_attach(IntPtr domain);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_thread_detach(IntPtr thread);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern unsafe void** il2cpp_thread_get_all_attached_threads(ref uint size);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_is_vm_thread(IntPtr thread);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_current_thread_walk_frame_stack(IntPtr func, IntPtr user_data);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_thread_walk_frame_stack(IntPtr thread, IntPtr func, IntPtr user_data);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_current_thread_get_top_frame(IntPtr frame);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_thread_get_top_frame(IntPtr thread, IntPtr frame);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_current_thread_get_frame_at(int offset, IntPtr frame);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_thread_get_frame_at(IntPtr thread, int offset, IntPtr frame);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_current_thread_get_stack_depth();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_thread_get_stack_depth(IntPtr thread);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_type_get_object(IntPtr type);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int il2cpp_type_get_type(IntPtr type);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_type_get_class_or_element_class(IntPtr type);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_type_get_name(IntPtr type);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_type_is_byref(IntPtr type);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_type_get_attrs(IntPtr type);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_type_equals(IntPtr type, IntPtr otherType);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_type_get_assembly_qualified_name(IntPtr type);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_image_get_assembly(IntPtr image);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_image_get_name(IntPtr image);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_image_get_filename(IntPtr image);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_image_get_entry_point(IntPtr image);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern uint il2cpp_image_get_class_count(IntPtr image);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_image_get_class(IntPtr image, uint index);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_capture_memory_snapshot();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_free_captured_memory_snapshot(IntPtr snapshot);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_set_find_plugin_callback(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_register_log_callback(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_debugger_set_agent_options(IntPtr options);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_is_debugger_attached();
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern unsafe void il2cpp_unity_install_unitytls_interface(void* unitytlsInterfaceStruct);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_custom_attrs_from_class(IntPtr klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_custom_attrs_from_method(IntPtr method);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_custom_attrs_get_attr(IntPtr ainfo, IntPtr attr_klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern bool il2cpp_custom_attrs_has_attr(IntPtr ainfo, IntPtr attr_klass);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr il2cpp_custom_attrs_construct(IntPtr cinfo);
        [DllImport(UnityDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern void il2cpp_custom_attrs_free(IntPtr ainfo);
    }
}
