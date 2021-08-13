using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono;
using Mono.Cecil;
using System.IO;
using System.Runtime.InteropServices;

namespace Il2CppRuntime.Il2Cpp
{
    public unsafe static class Dumper
    {
        public static void Dump()
        {
            Directory.CreateDirectory("DummyDlls");
            var il2CppDummyDll = Il2CppDummyDll.Create();
            var addressAttribute = il2CppDummyDll.MainModule.Types.First(x => x.Name == "AddressAttribute").Methods[0];
            FieldOffsetAttribute = il2CppDummyDll.MainModule.Types.First(x => x.Name == "FieldOffsetAttribute").Methods[0];
            var attributeAttribute = il2CppDummyDll.MainModule.Types.First(x => x.Name == "AttributeAttribute").Methods[0];
            var metadataOffsetAttribute = il2CppDummyDll.MainModule.Types.First(x => x.Name == "MetadataOffsetAttribute").Methods[0];
            var tokenAttribute = il2CppDummyDll.MainModule.Types.First(x => x.Name == "TokenAttribute").Methods[0];
            StringReference = il2CppDummyDll.MainModule.TypeSystem.String;

            var resolver = new AssemblyResolver();
            var moduleParameters = new ModuleParameters { Kind = ModuleKind.Dll, AssemblyResolver = resolver };
            resolver.Register(il2CppDummyDll);
            var assemblyDefs = new Dictionary<IntPtr, AssemblyDefinition>();
            foreach (var assembly in Il2Cpp.Assemblies)
            {
                var assemblyDefinition = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(assembly.Value.Name.Replace(".dll", ""), new Version("4.0.0.0")), assembly.Value.Name, moduleParameters);
                resolver.Register(assemblyDefinition);

                var moduleDefinition = assemblyDefinition.MainModule;
                moduleDefinition.Types.Clear();
                //if (assembly.Value.Name == "System.dll") moduleDefinition.Types.Add(new TypeDefinition("System", "String", Mono.Cecil.TypeAttributes.Public));

                var cachedClasses = Il2CppClass.CachedClasses.Where(cl => cl.Value.Image == assembly.Key).Select(cl => cl.Value);
                var baseClasses = cachedClasses.Where(cl => cl.DeclaringTypePtr == IntPtr.Zero);
                var declaredClasses = cachedClasses.Where(cl => cl.DeclaringTypePtr != IntPtr.Zero);
                //foreach (var c in baseClasses)
                foreach (var c in assembly.Value.Classes.Where(cl => cl.DeclaringTypePtr == IntPtr.Zero))
                //foreach (var ckvp in Il2CppClass.CachedClasses.Where(cl=>cl.Value.Image == assembly.Key &&  cl.DeclaringTypePtr == IntPtr.Zero).Select(cl => cl.Value))
                {
                    var typeDefinition = new TypeDefinition(c.Namespace, c.Name, c.Flags);
                    //Console.WriteLine("init : " + c.Namespace + " : " + c.Name);
                    //if (c.Name == "<>f__AnonymousType0`1") continue;
                    //if (c.Name.Contains("$ArrayType$")) continue;
                    typeDict.Add(c.Type.Ptr, typeDefinition);
                    foreach (var t in c.NestedTypePtrs)
                    {
                        if (typeDict.ContainsKey(t)) continue;
                        var nestedClass = Il2CppClass.Generate(t);
                        var nestedTypeDef = new TypeDefinition(nestedClass.Namespace, nestedClass.Name, nestedClass.Flags);
                        typeDefinition.NestedTypes.Add(nestedTypeDef);
                        typeDict.Add(t, nestedTypeDef);
                    }
                    moduleDefinition.Types.Add(typeDefinition);
                }
                //foreach (var c in declaredClasses)
                foreach (var c in assembly.Value.Classes.Where(cl => cl.DeclaringTypePtr != IntPtr.Zero))
                //    foreach (var c in assembly.Value.Classes.Where(cl => cl.DeclaringTypePtr != IntPtr.Zero))
                //foreach(var c in assembly.Value.Classes)
                //foreach (var ckvp in Il2CppClass.CachedClasses.Where(cl=>cl.Value.Image == assembly.Key))
                {
                    var typeDefinition = new TypeDefinition(c.Namespace, c.Name, c.Flags);
                    typeDict.Add(c.Type.Ptr, typeDefinition);
                    foreach (var t in c.NestedTypePtrs)
                    {
                        if (typeDict.ContainsKey(t)) continue;
                        var nestedClass = Il2CppClass.Generate(t);
                        var nestedTypeDef = new TypeDefinition(nestedClass.Namespace, nestedClass.Name, nestedClass.Flags);
                        typeDefinition.NestedTypes.Add(nestedTypeDef);
                        typeDict.Add(t, nestedTypeDef);
                    }
                    typeDict[c.DeclaringType.Type.Ptr].NestedTypes.Add(typeDefinition);
                }
                assemblyDefs.Add(assembly.Value.Ptr, assemblyDefinition);
            }
            foreach (var assembly in Il2Cpp.Assemblies)
            {
                var assemblyDefinition = assemblyDefs[assembly.Value.Ptr];
                var moduleDefinition = assemblyDefinition.MainModule;
                //foreach (var c in Il2CppClass.CachedClasses.Where(cl => cl.Value.Image == assembly.Key).Select(cl => cl.Value))
                foreach (var c in assembly.Value.Classes)
                {
                    DumpClass(c, moduleDefinition);
                }
                Console.WriteLine("saving " + assembly.Value.Name);
                assemblyDefinition.Write(@"DummyDlls\" + assembly.Value.Name);
            }
            //File.WriteAllText(@"methodLocs.txt", MethodLocs.ToString());
            Console.WriteLine("dumped");
        }
        public class AssemblyResolver : DefaultAssemblyResolver
        {
            public void Register(AssemblyDefinition assembly)
            {
                RegisterAssembly(assembly);
            }
        }

        public static Dictionary<Int32, GenericParameter> genericParameters = new Dictionary<Int32, GenericParameter>();
        public static GenericParameter CreateGenericParameter(Int32 genericParameterIndex, IGenericParameterProvider iGenericParameterProvider)
        {
            if (!genericParameters.TryGetValue(genericParameterIndex, out var genericParameter))
            {
                genericParameter = new GenericParameter("T", iGenericParameterProvider);
                //genericParameter.Attributes = (Mono.Cecil.GenericParameterAttributes)Mono.Cecil.GenericParameterAttributes.NonVariant;
                genericParameters.Add(genericParameterIndex, genericParameter);
                //for (int i = 0; i < param.constraintsCount; ++i)
                {
                    //var il2CppType = il2Cpp.types[metadata.constraintIndices[param.constraintsStart + i]];
                    // genericParameter.Constraints.Add(new GenericParameterConstraint(GetTypeReference((MemberReference)iGenericParameterProvider, il2CppType)));
                }
            }
            else
            {
                //Console.WriteLine("getting 2nd : " + genericParameterIndex);
            }
            return genericParameter;
        }
        public static Dictionary<IntPtr, MethodDefinition> methodDefs = new Dictionary<IntPtr, MethodDefinition>();
        static StringBuilder MethodLocs = new StringBuilder();
        static List<String> MethodLocsAdded = new List<String>();
        static StringBuilder ClassLocs = new StringBuilder();
        static IntPtr BaseAddr = IntPtr.Zero;
        public static MethodDefinition DumpMethod(Il2CppMethod m, TypeDefinition typeDefinition, Boolean dump)
        {
            //var methodDefinition = new MethodDefinition(m.Name, m.Flags, moduleDefinition.ImportReference(m.ReturnType.ByRef ? (TypeReference)new ByReferenceType(typeDict[m.ReturnClass.Ptr]) : typeDict[m.ReturnClass.Ptr]));
            var methodDefinition = new MethodDefinition(m.Name, m.Flags, typeDefinition.Module.ImportReference(typeof(void)));
            methodDefinition.ImplAttributes = m.ImplFlags;
            typeDefinition.Methods.Add(methodDefinition);
            /*var line = typeDefinition.Namespace + "." + typeDefinition.Name + "." + m.Name;
            var mName = line.Replace(".", "_").Replace(" ", "_");
            MethodLocsAdded.Add(mName);
            var count = MethodLocsAdded.Count(l => l == mName);
            if (count > 1) mName += "_" + (count);
            line = mName + " " + ((UInt64)(*(IntPtr*)m.Ptr) - (UInt64)BaseAddr).ToString("X");
            MethodLocs.AppendLine(line);*/
            //Console.WriteLine("fin");
            //var methodDefinition = new MethodDefinition(m.Name, m.Flags, moduleDefinition.ImportReference(typeDict[m.ReturnClassPtr]));
            //var methodDefinition = new MethodDefinition(m.Name, m.Flags, moduleDefinition.ImportReference(typeDict.First().Value));
            //methodDefinition.DeclaringType = typeDefinition;
            // if (methodDefinition.ReturnType.Name.StartsWith("!!"))
            if (m.IsGeneric)
            {
                //Console.WriteLine(typeDefinition.Name + " : " + typeDefinition.Namespace + " : " + m.Name + " : " + m.Ptr.ToString("X") + " : " + m.GenericContainer.Count + " : " + m.GenericContainer.ParamStart);
                for (var i = 0; i < m.GenericContainer.Count; i++)
                {
                    var genericParameter = CreateGenericParameter(m.GenericContainer.ParamStart + i, methodDefinition);
                    methodDefinition.GenericParameters.Add(genericParameter);
                }
                // if (m.Name == "Instantiate" && m.Parameters.Count == 2)
            }
            methodDefinition.ReturnType = GetTypeReferenceWithByRef(methodDefinition, m.ReturnClass.Type);
            foreach (var p in m.Parameters)
            {
                var pType = Il2CppType.GenerateType(p.Class.Type.Ptr);
                var pType2 = Il2CppType.GenerateType(p.Ptr);
                //Console.WriteLine(typeDefinition.Namespace + " : " + typeDefinition.Name + " : " + m.Name + " | " + p.Name + " : " + p.Class.Type.Name + " : " + p.Class.Type.Type + " : " + pType.Name + " : " + pType2.Name + " : " + pType2.Type);
                methodDefinition.Parameters.Add(new ParameterDefinition(p.Name, (Mono.Cecil.ParameterAttributes)pType.Attributes, GetTypeReferenceWithByRef(methodDefinition, p.Class.Type)));
                //Console.WriteLine("done");
                // methodDefinition.Parameters.Add(new ParameterDefinition(p.Name, Mono.Cecil.ParameterAttributes.None, GetTypeReferenceWithByRef(methodDefinition, pType2)));
                continue;
                //Console.WriteLine(typeDefinition.Name + " : " + typeDefinition.Namespace + " : " + m.Name + " : " + p.Name + " : " + m.Ptr.ToString("X"));

                //Console.WriteLine(p.Name + " : " + p.Class.Name);
                // var pType = Il2CppType.GenerateType(p.Class.Type.Ptr);
                var c = typeDict.Count(t => (t.Value.Namespace + "." + t.Value.Name) == pType.Name);
                if (c > 0) methodDefinition.Parameters.Add(new ParameterDefinition(p.Name, Mono.Cecil.ParameterAttributes.None, new ByReferenceType(typeDict.First(t => (t.Value.Namespace + "." + t.Value.Name) == pType.Name).Value)));
                else
                {
                    Console.WriteLine(typeDefinition.Namespace + " : " + typeDefinition.Name + " : " + m.Name + " : " + p.Name + " : " + pType.Name + " : " + pType.Type + " : " + c);
                    if (!typeDict.ContainsKey(pType.Ptr) && (pType.Type == Il2CppTypeEnum.IL2CPP_TYPE_CLASS || pType.Type == Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE))
                    {
                        //Console.WriteLine(typeDefinition.Namespace + " : " + typeDefinition.Name + " : " + m.Name + " : " + p.Name + " : " + pType.Name + " : " + pType.Type);
                        // methodDefinition.Parameters.Add(new ParameterDefinition(p.Name, Mono.Cecil.ParameterAttributes.None, typeDefinition.Module.ImportReference(typeof(Single))));
                    }
                    if (!typeDict.ContainsKey(pType.Ptr))
                    {
                        Console.WriteLine(typeDefinition.Namespace + " : " + typeDefinition.Name + " : " + m.Name + " : " + p.Name + " : " + pType.Name + " : " + pType.Type);
                        // methodDefinition.Parameters.Add(new ParameterDefinition(p.Name, Mono.Cecil.ParameterAttributes.None, typeDefinition.Module.ImportReference(typeof(Single))));
                    }
                    //else
                    // Console.WriteLine(p.Name + " : " + p.Class.Type.Name + " : " + (Mono.Cecil.ParameterAttributes)p.Class.Type.Attributes + " : " + pType.Name + " : " + (Mono.Cecil.ParameterAttributes)pType.Attributes);
                    //methodDefinition.Parameters.Add(new ParameterDefinition(p.Name, (Mono.Cecil.ParameterAttributes)pType.Attributes, GetTypeReferenceWithByRef(methodDefinition, p.Class.Type)));
                    //methodDefinition.Parameters.Add(new ParameterDefinition(p.Name, Mono.Cecil.ParameterAttributes.None, GetTypeReferenceWithByRef(methodDefinition, p.Class.Type)));
                    //methodDefinition.Parameters.Add(new ParameterDefinition(p.Name, Mono.Cecil.ParameterAttributes.None, GetTypeReferenceWithByRef(methodDefinition, pType)));
                    methodDefinition.Parameters.Add(new ParameterDefinition(pType.Name + "_" + p.Name, Mono.Cecil.ParameterAttributes.None, typeDefinition.Module.ImportReference(typeof(Single))));
                }
                if (pType.Name.EndsWith("&"))
                {
                    // var newPType = typeDict.First(t => t.Value.Name == pType.Name.Replace("&", "")).Value;
                    // methodDefinition.Parameters.Add(new ParameterDefinition(p.Name, Mono.Cecil.ParameterAttributes.None, newPType));
                }

            }//if (methodDefinition.HasBody && dump) InitMethodBody(methodDefinition, typeDict[m.ReturnClass.Ptr]);
            methodDefs.Add(m.Ptr, methodDefinition);
            return methodDefinition;
        }
        public static void DumpClass(Il2CppClass c, ModuleDefinition moduleDefinition)
        {
            if (c.Name == "<>f__AnonymousType0`1") return;
            if (c.Name.Contains("$ArrayType$")) return;
            var typeDefinition = typeDict[c.Type.Ptr];
            if (c.BaseClassPtr != IntPtr.Zero)
            {
                //var newBase = typeDict.First(t => t.Value.Name == c.BaseClass.Name && t.Value.Namespace == c.BaseClass.Namespace);
                typeDefinition.BaseType = GetTypeReference(typeDefinition, c.BaseClass.Type);
                //Console.WriteLine(c.Namespace + " :: " + c.Name + " : " + " : " + c.BaseClass.Type.Name);
            }
            if (c.GenericIndex != Int32.MaxValue)
            {
                var genericContainer = Il2CppGenericContainer.Generics[c.GenericIndex];
                //Console.WriteLine("dump class generic : " + c.Namespace + " :: " + c.Name + " : " + " : " + genericContainer.Count + " : " + genericContainer.ParamStart);
                for (var i = 0; i < genericContainer.Count; i++)
                    typeDefinition.GenericParameters.Add(CreateGenericParameter(genericContainer.ParamStart + i, typeDefinition));
            }
            //if (c.DeclaringTypePtr != IntPtr.Zero) typeDefinition.DeclaringType = typeDict[c.DeclaringTypePtr];
            foreach (var f in c.Fields)
            {
                if (c.Name == "ILFixDynamicMethodWrapper" && f.Name.Contains("Gen")) continue;
                //if (c.Name.Contains("TestReporter"))
                //    Console.WriteLine("ptr : " + c.Namespace + " :: " + c.Name + " . " + f.Name + " : " + f.Ptr.ToString("X"));
                var field = new FieldDefinition(f.Name, f.Flags, GetTypeReference(typeDefinition, f.Class.Type));
                if (typeDefinition.IsEnum && field.IsStatic)
                {
                    int result = 0;
                    Il2Cpp.il2cpp_field_static_get_value(f.Ptr, (void*)(&result));
                    //Il2Cpp.Il2Cpp.il2cpp_field_get_value(IntPtr.Zero, f.Ptr, (void*)(&result));
                    //field.Constant = result;
                }
                if (!field.IsLiteral)
                {
                    if (f.Offset >= 0)
                    {
                        var customAttribute = new CustomAttribute(typeDefinition.Module.ImportReference(FieldOffsetAttribute));
                        if (c.Name == "RuntimeTypeHandle" && f.Name == "value") f.Offset = 0;
                        var offset = new Mono.Cecil.CustomAttributeNamedArgument("Offset", new CustomAttributeArgument(StringReference, $"0x{f.Offset:X}"));
                        customAttribute.Fields.Add(offset);
                        field.CustomAttributes.Add(customAttribute);
                    }
                }
                typeDefinition.Fields.Add(field);
            }
            foreach (var m in c.Methods)
            {
                if (c.Name == "ILFixDynamicMethodWrapper" && m.Name.Contains("Gen")) continue;
                var methodDefinition = DumpMethod(m, typeDefinition, typeDefinition.BaseType?.FullName != "System.MulticastDelegate");
            }
            foreach (var p in c.Properties)
            {
                //Console.WriteLine(c.Name + " : " +p.Name);
                var propDefinition = new PropertyDefinition(p.Name, p.Flags, GetTypeReferenceWithByRef(typeDefinition, p.Class.Type));
                if (p.getMethod != null) propDefinition.GetMethod = methodDefs[p.getMethod.Ptr];
                if (p.setMethod != null) propDefinition.SetMethod = methodDefs[p.setMethod.Ptr];
                typeDefinition.Properties.Add(propDefinition);
            }
            foreach (var i in c.InterfacePtrs)
            {
                var inter = Il2CppClass.Generate(i);
                var interfaceTypeRef = GetTypeReference(typeDefinition, inter.Type);
                typeDefinition.Interfaces.Add(new InterfaceImplementation(interfaceTypeRef));
                //var nestedClass = Il2CppClass.Generate(t);
                //DumpClass(nestedClass, moduleDefinition);
            }
            foreach (var t in c.NestedTypePtrs)
            {
                //var nestedClass = Il2CppClass.Generate(t);
                //DumpClass(nestedClass, moduleDefinition);
            }
        }
        public static void DumpStrings()
        {
            //var dataOffset = BaseAddr + 0x5D94000;
            var dataOffset = 0x27300000000u;// BaseAddr + 0x6141400;
            var size = 0x00080000000u;
            var offset = 0u;
            var added = new List<String>();
            //while(offset < 0x3AA400)
            //while(offset < 0x5E08FF)
            while (offset < (dataOffset + size))
            {
                try
                {
                    var ptr = *(UInt64*)(dataOffset + offset);
                    //if (Il2CppClass.CachedClasses.ContainsKey(ptr))
                    if (ptr > dataOffset && ptr < dataOffset + size)
                    {
                        //Console.WriteLine("p : " + ptr.ToString("X"));
                        var namePtr = *(UInt64*)(ptr + 2 * 8);
                        var nameSpacePtr = *(UInt64*)(ptr + 3 * 8);
                        var selfPtr = *(UInt64*)(ptr + 6 * 8);
                        //Console.WriteLine(namePtr.ToString("X"));
                        //Console.WriteLine(nameSpacePtr.ToString("X"));
                        //Console.WriteLine(selfPtr.ToString("X"));
                        if (namePtr > dataOffset && namePtr < dataOffset && nameSpacePtr > dataOffset + size && nameSpacePtr < dataOffset + size && selfPtr == ptr)
                        {
                            var cName = Marshal.PtrToStringAnsi((IntPtr)namePtr);
                            var cNamespace = Marshal.PtrToStringAnsi((IntPtr)nameSpacePtr);
                            var line = cNamespace + "_" + cName;
                            //if (c.Name == "Enumerator") line = c.DeclaringType.Name + "Enumerator";
                            line = line.Replace(".", "_").Replace("*", "Ptr").Replace(" ", "").Replace("`", "").Replace("<", "").Replace(">", "").Replace("[", "Arr").Replace("]", "").Replace(",", "");
                            added.Add(line);
                            var count = added.Count(l => l == line);

                            if (count > 1) line += "_" + (count);
                            ClassLocs.AppendLine(line + " " + ((UInt64)(dataOffset + offset) - (UInt64)BaseAddr).ToString("X"));
                        }
                    }
                }
                catch { }
                offset += 8;
            }
            File.WriteAllText(@"classLocs.txt", ClassLocs.ToString());
        }
        //public static GameObject BaseObject;
        public static Dictionary<IntPtr, TypeDefinition> typeDict = new Dictionary<IntPtr, TypeDefinition>();
        static MethodDefinition FieldOffsetAttribute;
        static TypeReference StringReference;

        public static TypeReference GetTypeReferenceWithByRef(MemberReference memberRef, Il2CppType il2CppType)
        {
            var typeReference = GetTypeReference(memberRef, il2CppType);
            return il2CppType.ByRef ? new ByReferenceType(typeReference) : typeReference;
        }
        public static TypeReference GetTypeReference(MemberReference memberRef, Il2CppType il2CppType)
        {
            var moduleDefinition = memberRef.Module;
            var il2CppClass = il2CppType.GetClass();
            //if (loggin)
            //Console.WriteLine("GetTypeReference : " + il2CppClass.Namespace + " : " + il2CppClass.Name + " : " + il2CppType.Type + " : " + il2CppClass.Ptr.ToString("X") + " : " + il2CppType.Ptr.ToString("X") + " : " + moduleDefinition);
            // Console.WriteLine("GetTypeReference : " + il2CppType.Type + " : " + il2CppType.Name);
            //
            switch (il2CppType.Type)
            {
                case Il2CppTypeEnum.IL2CPP_TYPE_OBJECT:
                    return moduleDefinition.ImportReference(typeof(object));
                case Il2CppTypeEnum.IL2CPP_TYPE_VOID:
                    return moduleDefinition.ImportReference(typeof(void));
                case Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                    return moduleDefinition.ImportReference(typeof(bool));
                case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                    return moduleDefinition.ImportReference(typeof(char));
                case Il2CppTypeEnum.IL2CPP_TYPE_I1:
                    return moduleDefinition.ImportReference(typeof(sbyte));
                case Il2CppTypeEnum.IL2CPP_TYPE_U1:
                    return moduleDefinition.ImportReference(typeof(byte));
                case Il2CppTypeEnum.IL2CPP_TYPE_I2:
                    return moduleDefinition.ImportReference(typeof(short));
                case Il2CppTypeEnum.IL2CPP_TYPE_U2:
                    return moduleDefinition.ImportReference(typeof(ushort));
                case Il2CppTypeEnum.IL2CPP_TYPE_I4:
                    return moduleDefinition.ImportReference(typeof(int));
                case Il2CppTypeEnum.IL2CPP_TYPE_U4:
                    return moduleDefinition.ImportReference(typeof(uint));
                case Il2CppTypeEnum.IL2CPP_TYPE_I:
                    return moduleDefinition.ImportReference(typeof(IntPtr));
                case Il2CppTypeEnum.IL2CPP_TYPE_U:
                    return moduleDefinition.ImportReference(typeof(UIntPtr));
                case Il2CppTypeEnum.IL2CPP_TYPE_I8:
                    return moduleDefinition.ImportReference(typeof(long));
                case Il2CppTypeEnum.IL2CPP_TYPE_U8:
                    return moduleDefinition.ImportReference(typeof(ulong));
                case Il2CppTypeEnum.IL2CPP_TYPE_R4:
                    return moduleDefinition.ImportReference(typeof(float));
                case Il2CppTypeEnum.IL2CPP_TYPE_R8:
                    return moduleDefinition.ImportReference(typeof(double));
                case Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                    return moduleDefinition.ImportReference(typeof(string));
                case Il2CppTypeEnum.IL2CPP_TYPE_TYPEDBYREF:
                    return moduleDefinition.ImportReference(typeof(TypedReference));
                case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                    {
                        return moduleDefinition.ImportReference(typeDict[il2CppType.Ptr]);
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
                    {
                        var etype = *(IntPtr*)((*(IntPtr*)il2CppType.Ptr) + 0);
                        var rank = *(Byte*)((*(IntPtr*)il2CppType.Ptr) + 8);
                        return new ArrayType(GetTypeReference(memberRef, Il2CppType.GenerateType(etype)), rank);
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
                    {
                        //var genericClass = il2CppType.PtrVal;
                        // var il2CppClass = il2CppType.GetClass();
                        var newBase = typeDict.First(t => t.Value.Name == il2CppClass.Name && t.Value.Namespace == il2CppClass.Namespace);
                        var genericInstanceType = new GenericInstanceType(moduleDefinition.ImportReference(newBase.Value));
                        var genClass = *(IntPtr*)il2CppType.Ptr;
                        var genClassInst = *(IntPtr*)(genClass + 8);
                        var argc = *(IntPtr*)genClassInst;
                        var argv = *(IntPtr*)(genClassInst + 8);
                        //Console.WriteLine(il2CppType.Name + " : " + argv.ToString("X") + " : " + argc);
                        for (var i = 0; i < (Int64)argc; i++)
                        {
                            var argTypeTemp = Il2CppType.GenerateType(*(IntPtr*)(argv + i * 8));
                            var argClass = argTypeTemp.GetClass();
                            genericInstanceType.GenericArguments.Add(GetTypeReference(memberRef, argClass.Type));
                        }
                        //var genericContainer = Il2CppGenericContainer.Generics[*(Int32*)il2CppType.Ptr];
                        //var pointers = il2Cpp.MapVATR<ulong>(genericInst.type_argv, genericInst.type_argc);
                        //foreach (var pointer in genericContainer.) genericInstanceType.GenericArguments.Add(GetTypeReference(memberReference, oriType));
                        return genericInstanceType;
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                    {
                        var il2CppTypeInner = new Il2CppType(*(IntPtr*)il2CppType.Ptr);
                        //Console.WriteLine(il2CppTypeInner.Name);
                        return new ArrayType(GetTypeReference(memberRef, il2CppTypeInner));
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
                    {
                        //Console.WriteLine("fail IL2CPP_TYPE_VAR");
                        //Console.WriteLine("GetTypeReference : " + il2CppClass.Namespace + " : " + il2CppClass.Name + " : " + il2CppType.Type + " : " + il2CppClass.Ptr.ToString("X") + " : " + il2CppType.Ptr.ToString("X") + " : " + (*(Int32*)il2CppType.Ptr).ToString("X"));
                        var q = CreateGenericParameter(*(Int32*)il2CppType.Ptr, memberRef is MethodDefinition ? ((MethodDefinition)memberRef).DeclaringType : (TypeDefinition)memberRef);
                        return CreateGenericParameter(*(Int32*)il2CppType.Ptr, memberRef is MethodDefinition ? ((MethodDefinition)memberRef).DeclaringType : (TypeDefinition)memberRef);
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
                    {
                        return CreateGenericParameter(*(Int32*)il2CppType.Ptr, (MethodDefinition)memberRef);
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
                    {
                        //return moduleDefinition.ImportReference(typeof(Single));
                        var newBase = typeDict.First(t => t.Value.Name == il2CppClass.Name.Replace("*", "") && t.Value.Namespace == il2CppClass.Namespace);
                        //Console.WriteLine("fail IL2CPP_TYPE_PTR" + " : " + il2CppType.Ptr.ToString("X"));
                        return new PointerType(moduleDefinition.ImportReference(newBase.Value));
                    }
                default:
                    {
                        Console.WriteLine("fail def : " + il2CppType.Type);
                        return moduleDefinition.ImportReference(typeof(Single));
                        //var methodDefinition = (MethodDefinition)memberReference;
                        //return CreateGenericParameter(executor.GetGenericParameteFromIl2CppType(il2CppType), methodDefinition);
                        //return moduleDefinition.ImportReference(typeDict[il2CppClass.Ptr]);
                    }
            }
        }
    }
}
