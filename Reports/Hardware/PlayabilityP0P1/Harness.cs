// Fallback domain checks used while the Unity Editor owns the project lock.
// Uses Unity's compiler/references and NUnit, but a DataContract JSON field round-trip
// in place of native JsonUtility. This does NOT certify Unity JSON, PlayMode or rendering.
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

public static class PlayabilityHarness
{
    public static int Main(string[] filters)
    {
        var passed=0;var failed=0;
        foreach(var type in new[]{typeof(AssemblyZero.Tests.HardwareBusinessTests),typeof(AssemblyZero.Tests.EP04BusinessTests)})
        foreach(var method in type.GetMethods().OrderBy(m=>m.Name))
        {
            if(filters.Length>0&&!filters.Any(f=>method.Name.Contains(f)))continue;
            var cases=method.GetCustomAttributes(typeof(TestCaseAttribute),false).Cast<TestCaseAttribute>().ToArray();
            var arguments=cases.Length>0?cases.Select(c=>c.Arguments):method.IsDefined(typeof(TestAttribute),false)?new[]{Array.Empty<object>()}:Array.Empty<object[]>();
            foreach(var args in arguments)
            {
                var name=method.Name+"("+string.Join(",",args.Select(a=>a.ToString()))+")";var watch=System.Diagnostics.Stopwatch.StartNew();
                try{method.Invoke(Activator.CreateInstance(type),args);passed++;Console.WriteLine("PASS "+name+" "+watch.ElapsedMilliseconds+"ms");}
                catch(Exception e){failed++;var inner=e.InnerException??e;Console.WriteLine("FAIL "+name+"\n  "+inner.GetType().Name+": "+inner.Message+"\n"+inner.StackTrace?.Split('\n').FirstOrDefault(l=>l.Contains("AssemblyZero")));}
            }
        }
        Console.WriteLine("Passed="+passed+" Failed="+failed+"; fallback serializer, no Unity native runtime or rendering.");
        var walkthrough=Type.GetType("AssemblyZero.Unity.EP04.HardwareWalkthrough, AssemblyZero.Unity");
        if(filters.Length==0&&walkthrough!=null)
            foreach(var flow in walkthrough.GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.ReturnType==typeof(string)&&m.GetParameters().Length==0))
            {
                try{Console.WriteLine("PASS walkthrough "+flow.Name+": "+flow.Invoke(null,null));}
                catch(Exception e){failed++;Console.WriteLine("FAIL walkthrough "+flow.Name+": "+(e.InnerException??e));}
            }
        return failed==0?0:1;
    }
}
