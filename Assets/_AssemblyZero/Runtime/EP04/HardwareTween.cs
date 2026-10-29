using System;
using System.Collections.Generic;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    // Minimal unscaled-time tween runner for presentation polish; never touches simulation state.
    public sealed class HardwareTween : MonoBehaviour
    {
        private sealed class Job { public UnityEngine.Object Owner; public float Delay,Duration,Time; public Action<float> Step; public Func<float,float> Ease; public Action Done; }
        private static HardwareTween instance;
        private readonly List<Job> jobs=new List<Job>();
        public static int Active=>instance==null?0:instance.jobs.Count;
        private static HardwareTween Instance
        {
            get
            {
                if(instance==null){var go=new GameObject("Hardware tweens");DontDestroyOnLoad(go);instance=go.AddComponent<HardwareTween>();}
                return instance;
            }
        }
        // Step receives eased progress 0..1. Jobs whose owner is destroyed are dropped silently.
        public static void Run(UnityEngine.Object owner,float duration,Action<float> step,Func<float,float> ease=null,Action done=null,float delay=0)
        {
            if(owner==null)return;Instance.jobs.Add(new Job{Owner=owner,Duration=Mathf.Max(.0001f,duration),Step=step,Ease=ease??OutQuad,Done=done,Delay=delay});
        }
        public static void Cancel(UnityEngine.Object owner){if(instance!=null)instance.jobs.RemoveAll(j=>j.Owner==owner);}
        private void Update()
        {
            var dt=UnityEngine.Time.unscaledDeltaTime;
            for(var i=jobs.Count-1;i>=0;i--)
            {
                var j=jobs[i];if(j.Owner==null){jobs.RemoveAt(i);continue;}
                if(j.Delay>0){j.Delay-=dt;continue;}
                j.Time+=dt;var t=Mathf.Clamp01(j.Time/j.Duration);
                try{j.Step(j.Ease(t));}catch(MissingReferenceException){jobs.RemoveAt(i);continue;}
                if(t>=1){jobs.RemoveAt(i);j.Done?.Invoke();}
            }
        }
        private void OnDestroy(){if(instance==this)instance=null;}
        public static float Linear(float t)=>t;
        public static float OutQuad(float t)=>1-(1-t)*(1-t);
        public static float InQuad(float t)=>t*t;
        public static float InOutQuad(float t)=>t<.5f?2*t*t:1-Mathf.Pow(-2*t+2,2)/2;
        public static float OutBack(float t){const float c=1.70158f;return 1+(c+1)*Mathf.Pow(t-1,3)+c*Mathf.Pow(t-1,2);}
        public static float OutElastic(float t)=>t<=0?0:t>=1?1:Mathf.Pow(2,-10*t)*Mathf.Sin((t*10-.75f)*(2*Mathf.PI/3))+1;
        // Rises then settles: 0 -> 1 -> 0, useful for pulses and flashes.
        public static float Pulse(float t)=>Mathf.Sin(t*Mathf.PI);
        public static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,float t){var u=1-t;return u*u*a+2*u*t*b+t*t*c;}
        public static void PopIn(Transform t,float duration=.35f,float delay=0)
        {
            if(t==null)return;var target=t.localScale;t.localScale=Vector3.zero;
            Run(t,duration,p=>t.localScale=target*p,OutBack,()=>t.localScale=target,delay);
        }
        public static void Punch(Transform t,float amount=.12f,float duration=.25f)=>Punch(t,amount,duration,Vector3.one);
        public static void Punch(Transform t,float amount,float duration,Vector3 baseScale)
        {
            if(t==null)return;Cancel(t);
            Run(t,duration,p=>t.localScale=baseScale*(1+amount*Pulse(p)),Linear,()=>t.localScale=baseScale);
        }
    }
}
