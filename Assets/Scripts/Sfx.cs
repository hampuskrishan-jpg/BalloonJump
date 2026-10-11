using UnityEngine;

// Enkla ljudeffekter som räknas fram i kod.
public class Sfx : MonoBehaviour
{
    const int SR = 44100;
    AudioSource one, wind;
    public AudioClip pop, coin, jump, win, die, shield, buy, brake, screech, splash, rocket, whoosh, gem, landWood, landStone;

    void Awake()
    {
        one = gameObject.AddComponent<AudioSource>();
        one.playOnAwake = false;
        wind = gameObject.AddComponent<AudioSource>();
        wind.playOnAwake = false;
        wind.loop = true;
        wind.volume = 0f;

        var rnd = new System.Random(7);
        float Noise() => (float)rnd.NextDouble() * 2f - 1f;
        float Sin(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        float Tone(float f, float t) => Sin(f, t) * 0.7f + Sin(f * 2f, t) * 0.2f + Sin(f * 3f, t) * 0.1f;

        // ballongen spricker: en kort skarp smäll (högpassat brus) och ett litet fladder av gummit
        float popPrev = 0f;
        pop = Make("pang", 0.25f, t =>
        {
            float n = Noise();
            float hp = n - popPrev; popPrev = n;                       // högpass = skarp, knastrig smäll
            float crack = hp * Mathf.Exp(-t * 90f) * 1.6f;
            float flap = Noise() * Mathf.Exp(-t * 35f) * 0.25f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 38f * t));
            return crack + flap;
        });
        coin = Make("mynt", 0.28f, t => Tone(t < 0.07f ? 988f : 1319f, t) * Mathf.Exp(-t * 11f) * 0.35f);
        jump = Make("hopp", 0.28f, t => Mathf.Sin(2f * Mathf.PI * (280f * t + 900f * t * t)) * (1f - t / 0.28f) * 0.4f);
        brake = Make("broms", 0.4f, t => Mathf.Sin(2f * Mathf.PI * (500f * t - 400f * t * t)) * (1f - t / 0.4f) * 0.3f);
        buy = Make("kop", 0.3f, t => Tone(t < 0.1f ? 660f : 880f, t) * Mathf.Exp(-t * 8f) * 0.35f);
        shield = Make("skold", 0.6f, t => Mathf.Sin(2f * Mathf.PI * 1100f * t + 3f * Sin(28f, t)) * Mathf.Exp(-t * 5f) * 0.25f);
        die = Make("fall", 1.0f, t => Mathf.Sin(2f * Mathf.PI * (760f * t - 330f * t * t)) * (1f - t) * 0.35f);
        screech = Make("skrik", 0.6f, t => Mathf.Sin(2f * Mathf.PI * (1800f + 600f * Sin(9f, t)) * t) * (1f - t / 0.6f) * 0.25f + Noise() * 0.05f * (1f - t / 0.6f));
        // plask: brus i vattnets frekvenser som sjunker ihop, plus några bubblor
        float s1 = 0f, s2 = 0f;
        float[] bubT = new float[8], bubF = new float[8];
        for (int i = 0; i < 8; i++) { bubT[i] = 0.08f + (float)rnd.NextDouble() * 0.7f; bubF[i] = 350f + (float)rnd.NextDouble() * 500f; }
        splash = Make("plask", 1.3f, t =>
        {
            float n = Noise();
            s1 += (n - s1) * 0.35f; s2 += (s1 - s2) * 0.35f;              // mjukare brus
            float body = (s1 - s2 * 0.6f) * Mathf.Min(1f, t * 60f) * Mathf.Exp(-t * 3.2f) * 1.6f;
            float bubbles = 0f;
            for (int i = 0; i < 8; i++)
            {
                float bt = t - bubT[i];
                if (bt > 0f && bt < 0.08f) bubbles += Mathf.Sin(2f * Mathf.PI * bubF[i] * (1f + bt * 6f) * bt) * Mathf.Exp(-bt * 45f) * 0.12f;
            }
            return body + bubbles;
        });

        // landning på trä (brygga/flotte): dov duns och ett ihåligt knackljud från plankorna
        landWood = Make("landning_tra", 0.6f, t =>
            Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 18f) * 0.6f
            + (Mathf.Sin(2f * Mathf.PI * 210f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 395f * t) * 0.3f) * Mathf.Exp(-t * 30f) * 0.35f
            + Noise() * Mathf.Exp(-t * 60f) * 0.25f);

        // landning på gatsten: duns och skrap från skorna
        float g1 = 0f;
        landStone = Make("landning_sten", 0.6f, t =>
        {
            g1 += (Noise() - g1) * 0.2f;
            float thud = Mathf.Sin(2f * Mathf.PI * 60f * t) * Mathf.Exp(-t * 28f) * 0.7f;
            float grit = g1 * Mathf.Exp(-t * 35f) * 0.9f;
            float scuff = t > 0.12f && t < 0.4f ? Noise() * 0.12f * Mathf.Sin(Mathf.PI * (t - 0.12f) / 0.28f) : 0f;
            return thud + grit + scuff;
        });
        float rlp = 0f;
        rocket = Make("raket", 1.2f, t => { rlp += (Noise() - rlp) * 0.15f; return (rlp * 1.6f + Sin(70f, t) * 0.2f) * Mathf.Min(1f, t * 10f) * Mathf.Min(1f, (1.2f - t) * 3f); });
        whoosh = Make("sus", 0.5f, t => Mathf.Sin(2f * Mathf.PI * (200f * t + 700f * t * t)) * Mathf.Sin(Mathf.PI * t / 0.5f) * 0.3f);
        gem = Make("diamant", 0.5f, t => (Tone(1568f, t) + Tone(2093f, t) * (t > 0.08f ? 1f : 0f)) * Mathf.Exp(-t * 7f) * 0.2f);
        float[] notes = { 523f, 659f, 784f, 1046f };
        win = Make("vinst", 1.1f, t =>
        {
            int n = Mathf.Min(3, (int)(t / 0.13f));
            float lt = t - n * 0.13f;
            return Tone(notes[n], t) * Mathf.Exp(-lt * (n == 3 ? 3f : 10f)) * 0.3f;
        });

        float lp = 0f;
        wind.clip = Make("vind", 3f, t =>
        {
            lp += (Noise() - lp) * 0.04f;
            float fade = Mathf.Min(1f, Mathf.Min(t, 3f - t) * 8f);
            return lp * 2.5f * fade;
        });
    }

    static AudioClip Make(string name, float len, System.Func<float, float> f)
    {
        int n = (int)(len * SR);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)SR), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, SR, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Riktiga inspelade ballongsmällar (CC0). Inspelningen innehåller tre smällar som delas upp här.
    AudioClip[] pops = new AudioClip[0];
    public AudioClip Pop => pops.Length > 0 ? pops[Random.Range(0, pops.Length)] : pop;

    void Start()
    {
        pops = SplitRecording(Resources.Load<AudioClip>("Ljud/ballong_pang"));
        LoadVoices();
        Debug.Log("Ballongsmällar från inspelningen: " + pops.Length);
    }

    static AudioClip[] SplitRecording(AudioClip rec)
    {
        if (rec == null || rec.channels != 1 || !rec.LoadAudioData()) return new AudioClip[0];
        var data = new float[rec.samples];
        if (!rec.GetData(data, 0)) return new AudioClip[0];
        int sr = rec.frequency;
        float peak = 0f;
        foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak < 0.01f) return new AudioClip[0];
        // hitta början på varje smäll: en stark topp minst en kvarts sekund efter förra starka toppen
        var onsets = new System.Collections.Generic.List<int>();
        int lastStrong = -sr;
        for (int i = 0; i < data.Length; i++)
        {
            if (Mathf.Abs(data[i]) < peak * 0.35f) continue;
            if (i - lastStrong > sr / 4) onsets.Add(Mathf.Max(0, i - sr / 400));
            lastStrong = i;
        }
        var clips = new System.Collections.Generic.List<AudioClip>();
        for (int k = 0; k < onsets.Count; k++)
        {
            int start = onsets[k];
            int end = Mathf.Min(data.Length, start + (int)(sr * 0.5f));
            if (k + 1 < onsets.Count) end = Mathf.Min(end, onsets[k + 1] - sr / 100);
            int len = end - start;
            if (len < sr / 20) continue;
            var seg = new float[len];
            int fade = Mathf.Min(len / 3, sr / 30);
            for (int i = 0; i < len; i++)
            {
                float g = i > len - fade ? (len - i) / (float)fade : 1f;
                seg[i] = data[start + i] * g / peak * 0.9f;
            }
            var c = AudioClip.Create("ballongsmall" + k, len, 1, sr, false);
            c.SetData(seg, 0);
            clips.Add(c);
        }
        return clips.ToArray();
    }

    // Gubbens röst och kroppsljud: riktiga inspelningar (CC0, se Resources/Ljud/LICENS_ljud.txt)
    AudioSource voice;
    AudioClip gasp, ow, scream1, scream2, groan, breath, thudGround, thudWood, relief, sigh, cheer1, cheer2;
    AudioClip[] jumps = new AudioClip[0];
    public AudioClip JumpGrunt => jumps.Length > 0 ? jumps[Random.Range(0, jumps.Length)] : jump;
    public AudioClip Relief => Random.value < 0.5f && relief != null ? relief : sigh;   // lättnadens suck
    public AudioClip Cheer => Random.value < 0.5f && cheer1 != null ? cheer1 : cheer2;  // "Yes!" / "Yeah!"
    public AudioClip Fright => Random.value < 0.5f ? (gasp != null ? gasp : pop) : (ow != null ? ow : pop);
    public AudioClip Scream => Random.value < 0.5f ? scream1 : scream2;
    public AudioClip Groan => groan;
    public AudioClip Breath => breath;
    public AudioClip ThudGround => thudGround != null ? thudGround : landStone;
    public AudioClip ThudWood => thudWood != null ? thudWood : landWood;

    void LoadVoices()
    {
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        gasp = Resources.Load<AudioClip>("Ljud/flamtning");
        ow = Resources.Load<AudioClip>("Ljud/aj");
        scream1 = Resources.Load<AudioClip>("Ljud/skrik_fall1");
        scream2 = Resources.Load<AudioClip>("Ljud/skrik_fall2");
        groan = Resources.Load<AudioClip>("Ljud/ston");
        breath = Resources.Load<AudioClip>("Ljud/flamtar");
        thudGround = Resources.Load<AudioClip>("Ljud/duns_mark");
        thudWood = Resources.Load<AudioClip>("Ljud/duns_tra");
        relief = Resources.Load<AudioClip>("Ljud/lattnad");
        var js = new System.Collections.Generic.List<AudioClip>();
        foreach (var n in new[] { "hopp1", "hopp2", "hopp3" }) { var c = Resources.Load<AudioClip>("Ljud/" + n); if (c != null) js.Add(c); }
        jumps = js.ToArray();
        sigh = Resources.Load<AudioClip>("Ljud/suck");
        cheer1 = Resources.Load<AudioClip>("Ljud/jubel_yes");
        cheer2 = Resources.Load<AudioClip>("Ljud/jubel_yeah");
    }

    // Röstkanalen: ett ljud i taget, kan tystas direkt (skriket slutar när han slår i marken)
    public void PlayVoice(AudioClip c, float vol = 1f)
    {
        if (c == null || voice == null) return;
        voice.Stop();
        voice.clip = c;
        voice.volume = vol;
        voice.Play();
    }

    public void StopVoice()
    {
        if (voice != null) voice.Stop();
    }

    public void Play(AudioClip c, float vol = 1f)
    {
        if (c != null) one.PlayOneShot(c, vol);
    }

    public void SetWind(float amount)
    {
        if (!wind.isPlaying) wind.Play();
        wind.volume = Mathf.MoveTowards(wind.volume, Mathf.Clamp01(amount) * 0.5f, Time.unscaledDeltaTime);
        wind.pitch = 0.8f + Mathf.Clamp01(amount) * 0.5f;
    }
}
