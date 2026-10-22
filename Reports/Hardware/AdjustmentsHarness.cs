// Fallback domain checks when the Unity Editor cannot start.
// Uses Unity's compiler/references and NUnit, but a DataContract JSON field round-trip
// in place of native JsonUtility. This does NOT certify Unity JSON or PlayMode.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using NUnit.Framework;

namespace UnityEngine
{
    public static class JsonUtility
    {
        public static string ToJson(object value)
        {
            using(var stream=new MemoryStream()){new DataContractJsonSerializer(value.GetType()).WriteObject(stream,value);return Encoding.UTF8.GetString(stream.ToArray());}
        }
        public static T FromJson<T>(string value)
        {
            using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(value)))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
    }
}

public static class AdjustmentsHarness
{
    public static int Main()
    {
        var passed=0;var failed=0;
        foreach(var type in new[]{typeof(AssemblyZero.Tests.HardwareBusinessTests),typeof(AssemblyZero.Tests.EP04BusinessTests)})
        foreach(var method in type.GetMethods().OrderBy(m=>m.Name))
        {
            var cases=method.GetCustomAttributes(typeof(TestCaseAttribute),false).Cast<TestCaseAttribute>().ToArray();
            var arguments=cases.Length>0?cases.Select(c=>c.Arguments):method.IsDefined(typeof(TestAttribute),false)?new[]{Array.Empty<object>()}:Array.Empty<object[]>();
            foreach(var args in arguments)
            {
                var name=method.Name+"("+string.Join(",",args.Select(a=>a.ToString()))+")";
                try{method.Invoke(Activator.CreateInstance(type),args);passed++;Console.WriteLine("PASS "+name);}
                catch(Exception e){failed++;Console.WriteLine("FAIL "+name+"\n"+(e.InnerException??e));}
            }
        }
        Console.WriteLine("Passed="+passed+" Failed="+failed+"; fallback serializer, no Unity native runtime or rendering.");
        foreach(var flow in new Func<string>[] {
            ()=>AssemblyZero.Unity.EP04.HardwareWalkthrough.Run(false),
            ()=>AssemblyZero.Unity.EP04.HardwareWalkthrough.Run(true),
            ()=>AssemblyZero.Unity.EP04.HardwareWalkthrough.RunMake(),
            ()=>AssemblyZero.Unity.EP04.HardwareWalkthrough.RunRecovery()})
        {
            try{Console.WriteLine("PASS walkthrough: "+flow());}
            catch(Exception e){failed++;Console.WriteLine("FAIL walkthrough: "+e);}
        }
        return failed==0?0:1;
    }
}
