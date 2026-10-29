using System;
using System.Collections.Generic;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    // Procedurally synthesized placeholder sound set. Clips are generated once at startup;
    // replace a slot by assigning a real AudioClip to Clips[id] before Initialize returns.
    public sealed class HardwareAudio : MonoBehaviour
    {
        public const int Rate=44100;
        public static HardwareAudio Instance {get;private set;}
        public readonly Dictionary<string,AudioClip> Clips=new Dictionary<string,AudioClip>();
        public string LastPlayed {get;private set;}="";
        public readonly Dictionary<string,int> PlayCount=new Dictionary<string,int>();
        private readonly Dictionary<string,float> cooldowns=new Dictionary<string,float>();
        private readonly Dictionary<string,float> minimumGap=new Dictionary<string,float>{{"hover",.06f},{"product",.16f},{"coin",.045f},{"click",.04f},{"defect",.4f}};
        private readonly List<AudioSource> pool=new List<AudioSource>();
        private AudioSource hum,rattle;
        public float Master=1,Sfx=1,Ambient=.6f;
        private const string Prefs="AssemblyZero.Hardware.Audio";
        public void Initialize()
        {
            Instance=this;
            Master=PlayerPrefs.GetFloat(Prefs+".Master",1);Sfx=PlayerPrefs.GetFloat(Prefs+".Sfx",1);Ambient=PlayerPrefs.GetFloat(Prefs+".Ambient",.6f);
            if(FindAnyObjectByType<AudioListener>()==null)gameObject.AddComponent<AudioListener>();
            Synthesize();
            for(var i=0;i<12;i++){var s=gameObject.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=0;pool.Add(s);}
            hum=Loop("hum");rattle=Loop("rattle");
        }
        private AudioSource Loop(string id){var s=gameObject.AddComponent<AudioSource>();s.clip=Clips[id];s.loop=true;s.volume=0;s.playOnAwake=false;s.Play();return s;}
        private void OnDestroy(){if(Instance==this)Instance=null;foreach(var c in Clips.Values)if(c!=null)Destroy(c);}
        public static void Play(string id,float volume=1,float pitch=1)=>Instance?.PlayInternal(id,volume,pitch);
        private void PlayInternal(string id,float volume,float pitch)
        {
            if(!Clips.TryGetValue(id,out var clip))return;
            var now=Time.unscaledTime;if(minimumGap.TryGetValue(id,out var gap)&&cooldowns.TryGetValue(id,out var last)&&now-last<gap)return;cooldowns[id]=now;
            LastPlayed=id;PlayCount[id]=PlayCount.TryGetValue(id,out var c)?c+1:1;
            AudioSource source=null;foreach(var s in pool)if(!s.isPlaying){source=s;break;}
            if(source==null)source=pool[0];
            source.pitch=pitch;source.volume=Mathf.Clamp01(volume*Sfx*Master);source.clip=clip;source.Play();
        }
        // Ambient loops follow factory activity; paused games fade to silence.
        public void SetActivity(float working,int speed,bool paused)
        {
            if(hum==null)return;var target=paused?0:Ambient*Master;
            hum.volume=Mathf.MoveTowards(hum.volume,target*(.12f+.25f*Mathf.Clamp01(working)),Time.unscaledDeltaTime*.6f);
            hum.pitch=Mathf.MoveTowards(hum.pitch,1+.04f*Mathf.Log(Mathf.Max(1,speed),2),Time.unscaledDeltaTime);
            rattle.volume=Mathf.MoveTowards(rattle.volume,target*.18f*Mathf.Clamp01(working),Time.unscaledDeltaTime*.6f);
            rattle.pitch=Mathf.MoveTowards(rattle.pitch,Mathf.Min(1.6f,.9f+.1f*speed),Time.unscaledDeltaTime);
        }
        public void CycleLevel(string channel)
        {
            float Next(float v)=>v>=.99f?0:v>=.49f?1:.5f;
            if(channel=="Master")Master=Next(Master);else if(channel=="Sfx")Sfx=Next(Sfx);else Ambient=Next(Ambient);
            PlayerPrefs.SetFloat(Prefs+".Master",Master);PlayerPrefs.SetFloat(Prefs+".Sfx",Sfx);PlayerPrefs.SetFloat(Prefs+".Ambient",Ambient);PlayerPrefs.Save();Play("click");
        }

        // ---- Synthesis ----
        private System.Random random=new System.Random(20261028);
        private float Noise()=>(float)(random.NextDouble()*2-1);
        private void Add(string id,float seconds,Func<float,float> sample,bool loop=false)
        {
            if(Clips.ContainsKey(id))return;
            var count=Mathf.RoundToInt(seconds*Rate);var data=new float[count];
            for(var i=0;i<count;i++)data[i]=Mathf.Clamp(sample(i/(float)Rate),-1,1);
            // Short fade at both ends prevents clicks on one-shots.
            var fade=loop?0:Mathf.Min(64,count/4);for(var i=0;i<fade;i++){var k=i/(float)fade;data[i]*=k;data[count-1-i]*=k;}
            var clip=AudioClip.Create("Hardware "+id,count,1,Rate,false);clip.SetData(data,0);Clips[id]=clip;
        }
        private static float Sine(float hz,float t)=>Mathf.Sin(2*Mathf.PI*hz*t);
        private static float Decay(float t,float rate)=>Mathf.Exp(-t*rate);
        private void Synthesize()
        {
            Add("click",.05f,t=>.5f*Sine(1800,t)*Decay(t,90)+.25f*Noise()*Decay(t,300));
            Add("hover",.03f,t=>.18f*Sine(2600,t)*Decay(t,120));
            Add("pop",.12f,t=>.5f*Sine(400+5000*t,t)*Decay(t,30));
            Add("thud",.28f,t=>.8f*Sine(95-140*t,t)*Decay(t,14)+.35f*Noise()*Decay(t,40));
            Add("ready",.6f,t=>.35f*Sine(880,t)*Decay(t,6)+(t>.09f?.3f*Sine(1320,t-.09f)*Decay(t-.09f,6):0));
            Add("product",.06f,t=>.32f*Mathf.Sign(Sine(1250,t))*Decay(t,70));
            Add("stamp",.18f,t=>.6f*Sine(210,t)*Decay(t,25)+.4f*Noise()*Decay(t,60));
            Add("coin",.1f,t=>.28f*(Sine(3100,t)+.6f*Sine(4650,t))*Decay(t,40));
            Add("cash",1.15f,t=>
            {
                var drawer=t<.12f?.35f*Noise()*Decay(t,30):0;
                var bell=t>.08f?.32f*(Sine(2093,t-.08f)+.55f*Sine(2637,t-.08f)+.3f*Sine(4186,t-.08f))*Decay(t-.08f,4.5f):0;
                var jingle=0f;for(var k=0;k<6;k++){var start=.18f+k*.11f;if(t>start)jingle+=.12f*Sine(3300+k*410,t-start)*Decay(t-start,35);}
                return drawer+bell+jingle;
            });
            Add("alarm",.9f,t=>.28f*Mathf.Sign(Sine(((int)(t*6))%2==0?660:880,t))*(.6f+.4f*Sine(6,t))*Decay(t,1.6f));
            Add("defect",.32f,t=>.3f*(2*((t*110)%1)-1)*Decay(t,7));
            Add("penalty",.4f,t=>.7f*Sine(70-40*t,t)*Decay(t,8)+.2f*Noise()*Decay(t,20));
            Add("event",.7f,t=>.3f*Sine(523,t)*Decay(t,5)+(t>.15f?.3f*Sine(659,t-.15f)*Decay(t-.15f,5):0)+(t>.3f?.3f*Sine(784,t-.3f)*Decay(t-.3f,4):0));
            Add("whoosh",.35f,t=>.3f*Noise()*Mathf.Sin(Mathf.PI*t/.35f));
            // Loops use whole numbers of cycles over their length to stay seamless.
            Add("hum",2,t=>.25f*Sine(60,t)+.15f*Sine(120,t)+.08f*Sine(180,t)+.03f*Noise(),true);
            Add("rattle",1,t=>{var phase=(t*12)%1;return .25f*Noise()*Decay(phase,45)+.05f*Sine(240,t);},true);
        }
    }
}
