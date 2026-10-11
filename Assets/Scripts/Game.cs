using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Ballongfallet: hoppa från en hög kant med din ballong, glid framåt och neråt förbi hindren och landa säkert.
public class Game : MonoBehaviour
{
    public enum State { Shop, Ready, Jumping, Falling, Dying, Dead, Won }
    enum Help { None, Up, Side, Rocket, Shield, Brake }

    public static Game I;
    public const float StreetHalf = 7f;
    // 100 banor. Miljön byts VARJE bana: staden → öknen → bergsbyn → vattengrottan → snöberget → staden …
    // Varje bana är lite svårare än den förra. Bana 100 är finalen i grottan.
    public const int LevelCount = 100;
    public static readonly string[] ThemeNames = { "Staden", "Öknen", "Bergsbyn", "Vattengrottan", "Snöberget" };
    public static readonly string[] ThemeInfo =
    {
        "Gamla gränder med banderoller, tvättlinor och kugghjul",
        "Djup kanjon av röd sandsten och het sol – landa i sanden",
        "Mossiga klippor, trähus och väderkvarnar – landa på byns torg",
        "Mörk grotta med vatten – landa på en brygga eller flotten!",
        "Isiga klippor och fallande istappar – landa i snön",
    };
    public static int Theme(int i) => i == LevelCount - 1 ? 3 : i % 5;
    public static float Diff(int i) => i / (float)(LevelCount - 1);                // 0 = lättast, 1 = svårast
    public static string LevelName(int i) => i == LevelCount - 1 ? "Grottan – Finalen" : ThemeNames[Theme(i)];
    public const int BirdsFrom = 1; // bana 1 är lugn utan fågel; från bana 2 kommer fågeln (snäll i början)
    public static string LevelInfo(int i)
    {
        if (i == LevelCount - 1) return "FINALEN: mörk grotta, giftig rök och de argaste fåglarna – landa på en brygga!";
        return ThemeInfo[Theme(i)];
    }
    // Bana 1 är låg, kort och lugn för att lära sig; bana 100 är väldigt hög och snabb.
    // Höjden växer långsamt i början och snabbare mot slutet.
    public static float Height(int i) => Mathf.Round(40f + 860f * Mathf.Pow(Diff(i), 1.5f)); // 40 m … 900 m
    public static float Speed(int i) => Mathf.Lerp(6f, 22f, Mathf.Pow(Diff(i), 0.8f));     // framåtfart med en ballong – snabbare tidigt
    public static float Ratio(int i) => Mathf.Lerp(2.6f, 1.6f, Diff(i));                   // banans längd / höjd

    // Man börjar med EN ballong. Varje ny ballong blir dyrare – de sista är riktigt svåra att få ihop till.
    static readonly int[] BalloonCost = { 80, 200, 400, 700, 1100, 1600, 2300, 3200, 4500 }; // ballong 2 … 10
    static readonly int[] SteerCost = { 30, 70, 140 };
    const int UpCost = 25, SideCost = 15, ShieldCost = 20, BrakeCost = 12, RocketGems = 2, RocketCoins = 150, GemPrice = 60, MaxItems = 9;
    static readonly float[] HelpDuration = { 0f, 4f, 5f, 3f, 8f, 3f };
    static readonly string[] HelpNames = { "", "UPPFLYGNING", "SIDOTURBO", "RAKET", "SKÖLD", "BROMS" };
    const float LiftMax = 3.2f, LiftDown = 5f, LiftBoost = 9f;   // lite mer upp/ner så det aldrig blir omöjligt

    // sparas mellan spelomgångar
    public int money, gems, maxBalloons = 1, steering, unlocked = 1, level;
    int ups, sides, rockets, shields, brakes;
    readonly float[] best = new float[LevelCount];

    public State state;
    Camera cam;
    Player player;
    Level lvl;
    int builtLevel = -1;
    Sfx sfx;
    float stateTime, shake, bannerTime, bannerDur, lift, helpTime;
    Help active;
    string banner = "", deathTitle = "", deathText = "";
    int runCoins, lastBonus, nextBird;
    readonly List<Bird> birds = new List<Bird>();
    bool paused, god, snapCam = true;
    Vector3 camPos, camLook, deathCamPos;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        Application.targetFrameRate = 60;
        Time.timeScale = 1f;
        Load();

        cam = Camera.main;
        if (cam == null)
        {
            var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = cgo.AddComponent<Camera>();
        }
        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 400f;
        RenderSettings.fog = false;

        sfx = gameObject.AddComponent<Sfx>();
        player = Player.Create(transform);
        level = Mathf.Clamp(level, 0, unlocked - 1);
        page = level / 10;
        ShowShop();

        string shotDir = Arg("-ballongShot");
        if (shotDir != null) StartCoroutine(AutoShot(shotDir));
    }

    // ---------------------------------------------------------------- Spara/ladda

    void Load()
    {
        money = PlayerPrefs.GetInt("bf_money", 0);
        gems = PlayerPrefs.GetInt("bf_gems", 0);
        maxBalloons = Mathf.Clamp(PlayerPrefs.GetInt("bf_balloons", 1), 1, Player.MaxBalloons);
        steering = PlayerPrefs.GetInt("bf_steering", 0);
        ups = PlayerPrefs.GetInt("bf_ups", 0);
        sides = PlayerPrefs.GetInt("bf_sides", 0);
        rockets = PlayerPrefs.GetInt("bf_rockets", 0);
        shields = PlayerPrefs.GetInt("bf_shields", 0);
        brakes = PlayerPrefs.GetInt("bf_brakes", 0);
        unlocked = Mathf.Clamp(PlayerPrefs.GetInt("bf_unlocked", 1), 1, LevelCount);
        level = Mathf.Clamp(PlayerPrefs.GetInt("bf_level", 0), 0, LevelCount - 1);
        touchUI = PlayerPrefs.GetInt("bf_touch", 1) == 1;   // pekkontroller på från början (dra med finger eller mus)
        dragSteer = PlayerPrefs.GetInt("bf_drag", 1) == 1;
        // banorna byttes ut i version 3 – nollställ gamla "bäst"-värden en gång
        if (PlayerPrefs.GetInt("bf_version", 0) < 4) { for (int i = 0; i < best.Length; i++) PlayerPrefs.DeleteKey("bf_best" + i); }
        for (int i = 0; i < best.Length; i++) best[i] = PlayerPrefs.GetFloat("bf_best" + i, 0f);
    }

    void Save()
    {
        if (god) return;
        PlayerPrefs.SetInt("bf_version", 4);
        money = Mathf.Max(0, money);
        gems = Mathf.Max(0, gems);
        PlayerPrefs.SetInt("bf_money", money);
        PlayerPrefs.SetInt("bf_gems", gems);
        PlayerPrefs.SetInt("bf_balloons", maxBalloons);
        PlayerPrefs.SetInt("bf_steering", steering);
        PlayerPrefs.SetInt("bf_ups", ups);
        PlayerPrefs.SetInt("bf_sides", sides);
        PlayerPrefs.SetInt("bf_rockets", rockets);
        PlayerPrefs.SetInt("bf_shields", shields);
        PlayerPrefs.SetInt("bf_brakes", brakes);
        PlayerPrefs.SetInt("bf_unlocked", unlocked);
        PlayerPrefs.SetInt("bf_level", level);
        PlayerPrefs.SetInt("bf_touch", touchUI ? 1 : 0);
        PlayerPrefs.SetInt("bf_drag", dragSteer ? 1 : 0);
        for (int i = 0; i < best.Length; i++) PlayerPrefs.SetFloat("bf_best" + i, best[i]);
        PlayerPrefs.Save();
    }

    // ---------------------------------------------------------------- Flöde

    void EnsureLevel(int idx)
    {
        if (builtLevel == idx && lvl != null) return;
        ClearBirds();
        if (lvl != null) Destroy(lvl.root);
        lvl = LevelBuilder.Build(idx, transform);
        builtLevel = idx;
        snapCam = true;
        Toon.SetupLighting(cam, lvl.env);
    }

    void SelectLevel(int idx)
    {
        if (idx < 0 || idx >= unlocked) return;
        level = idx;
        page = idx / 10;
        EnsureLevel(idx);
        ResetRun();
    }

    static IEnumerator After(float delay, System.Action a)
    {
        yield return new WaitForSeconds(delay);
        a();
    }

    void ResetRun()
    {
        grounded = inWater = false;
        if (sfx != null) sfx.StopVoice();
        foreach (var c in lvl.coins) c.SetActive(true);
        player.ResetFor(lvl.start, maxBalloons);
        runCoins = 0;
        lift = 0f;
        active = Help.None;
        helpTime = 0f;
        nextBird = 0;
        ClearBirds();
    }

    void ClearBirds()
    {
        foreach (var b in birds) if (b != null) Destroy(b.gameObject);
        birds.Clear();
    }

    float resetArmedUntil;

    // ---------------------------------------------------------------- Pekkontroller (mobil)
    bool touchUI;                      // visa knappar på skärmen
    float touchX, touchV;              // styrning från knapparna
    bool pauseTapped, freeTap;         // tryck på pausknappen / på tom yta
    readonly bool[] helpTapped = new bool[5];
    readonly bool[] helpHeld = new bool[5];
    bool heldL, heldR, heldU, heldD;
    bool dragSteer = true;                  // styr genom att dra med fingret (annars knappar)
    int dragId = int.MinValue;              // fingret som styr just nu
    Vector2 dragOrigin, dragNow;            // var fingret sattes ner och var det är nu (GUI-koordinater)
    const float DragFull = 70f, DragDead = 10f;   // så långt man drar för full fart, och dödzon

    float UiScale => Screen.height / 720f;
    float UiWidth => Screen.width / UiScale;
    Rect RPause => new Rect(UiWidth / 2f + 108f, 14f, 58f, 58f);
    Rect RLeft => new Rect(22f, 596f, 112f, 104f);
    Rect RRight => new Rect(146f, 596f, 112f, 104f);
    Rect RUp => new Rect(UiWidth - 262f, 486f, 104f, 100f);
    Rect RDown => new Rect(UiWidth - 262f, 598f, 104f, 100f);
    Rect RHelp(int i) => new Rect(UiWidth / 2f - 212f + i * 86f, 640f, 80f, 62f);

    void ReadTouches()
    {
        bool dragSeen = false;
        pauseTapped = freeTap = false;
        touchX = touchV = 0f;
        heldL = heldR = heldU = heldD = false;
        for (int i = 0; i < 5; i++) helpTapped[i] = helpHeld[i] = false;
        float sc = UiScale;
        bool running = state == State.Falling || state == State.Jumping || state == State.Ready;
        void Pointer(Vector2 px, bool began, int id)
        {
            var g = new Vector2(px.x / sc, (Screen.height - px.y) / sc);   // skärmpunkt → GUI-koordinater
            if (id == dragId)
            {
                // fingret som styr: dra = styr åt sidan och upp/ner, längre drag = snabbare
                dragSeen = true;
                dragNow = g;
                Vector2 d = g - dragOrigin;
                if (state == State.Falling)
                {
                    if (Mathf.Abs(d.x) > DragDead) touchX += Mathf.Clamp((d.x - Mathf.Sign(d.x) * DragDead) / DragFull, -1f, 1f);
                    if (Mathf.Abs(d.y) > DragDead) touchV -= Mathf.Clamp((d.y - Mathf.Sign(d.y) * DragDead) / DragFull, -1f, 1f);
                }
                return;
            }
            if (running && RPause.Contains(g)) { if (began) pauseTapped = true; return; }
            if (touchUI && state == State.Falling)
            {
                for (int i = 0; i < 5; i++)
                    if (RHelp(i).Contains(g)) { helpHeld[i] = true; if (began) helpTapped[i] = true; return; }
            }
            if (touchUI && dragSteer && began && dragId == int.MinValue && (state == State.Falling || state == State.Jumping || state == State.Ready))
            {
                dragId = id;
                dragOrigin = dragNow = g;
                dragSeen = true;
            }
            if (touchUI && !dragSteer && state == State.Falling)
            {
                if (RLeft.Contains(g)) { touchX -= 1f; heldL = true; return; }
                if (RRight.Contains(g)) { touchX += 1f; heldR = true; return; }
                if (RUp.Contains(g)) { touchV += 1f; heldU = true; return; }
                if (RDown.Contains(g)) { touchV -= 1f; heldD = true; return; }
                for (int i = 0; i < 5; i++)
                    if (RHelp(i).Contains(g)) { helpHeld[i] = true; if (began) helpTapped[i] = true; return; }
            }
            if (began) freeTap = true;
        }
        if (Input.touchCount > 0)
        {
            foreach (var t in Input.touches)
                if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled) Pointer(t.position, t.phase == TouchPhase.Began, t.fingerId);
        }
        else if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0))
            Pointer(Input.mousePosition, Input.GetMouseButtonDown(0), -1);
        if (!dragSeen) dragId = int.MinValue;   // fingret släpptes: sluta styra
    }

    // Ritar pekknapparna (och pausknappen) ovanpå spelet
    void DrawTouchControls()
    {
        bool running = state == State.Falling || state == State.Jumping || state == State.Ready;
        if (running)
        {
            var pr = RPause;
            Fill(new Rect(pr.x - 2, pr.y - 2, pr.width + 4, pr.height + 4), Ink);
            Fill(pr, new Color(PanelCol.r, PanelCol.g, PanelCol.b, 0.95f));
            Fill(new Rect(pr.x + 18, pr.y + 14, 8, 30), Color.white);
            Fill(new Rect(pr.x + 32, pr.y + 14, 8, 30), Color.white);
        }
        if (!touchUI || state != State.Falling) return;
        if (dragSteer && dragId != int.MinValue)
        {
            // styrcirkel: var fingret sattes ner och hur långt man dragit
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.22f);
            GUI.DrawTexture(new Rect(dragOrigin.x - DragFull - 10f, dragOrigin.y - DragFull - 10f, (DragFull + 10f) * 2f, (DragFull + 10f) * 2f), texCircle);
            Vector2 d = Vector2.ClampMagnitude(dragNow - dragOrigin, DragFull);
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.DrawTexture(new Rect(dragOrigin.x + d.x - 26f, dragOrigin.y + d.y - 26f, 52f, 52f), texCircle);
            GUI.color = old;
        }
        void Pad(Rect r, string label, bool held, Color col)
        {
            Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            Fill(r, held ? new Color(col.r, col.g, col.b, 0.75f) : new Color(0.1f, 0.08f, 0.06f, 0.42f));
            int fs = sBtnBig.fontSize;
            if (label == "◀" || label == "▶") sBtnBig.fontSize = 72;   // de här tecknen ritas mindre – gör dem lika stora som ▲ ▼
            Text(r, label, sBtnBig, new Color(1f, 1f, 1f, held ? 1f : 0.85f), TextAnchor.MiddleCenter, 2);
            sBtnBig.fontSize = fs;
        }
        if (!dragSteer)
        {
            Pad(RLeft, "◀", heldL, Blue);
            Pad(RRight, "▶", heldR, Blue);
            Pad(RUp, "▲", heldU, Green);
            Pad(RDown, "▼", heldD, Green);
        }
        string[] names = { "Upp", "Sido", "Raket", "Sköld", "Broms" };
        int[] counts = { ups, sides, rockets, shields, brakes };
        for (int i = 0; i < 5; i++)
        {
            var r = RHelp(i);
            bool on = active == (Help)(i + 1);
            Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            Fill(r, on || helpHeld[i] ? new Color(Red.r, Red.g, Red.b, 0.8f) : counts[i] > 0 ? new Color(0.25f, 0.18f, 0.12f, 0.75f) : new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Text(new Rect(r.x, r.y + 4, r.width, 30), names[i], sHud, counts[i] > 0 ? Color.white : new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter, 2);
            Text(new Rect(r.x, r.y + 30, r.width, 28), counts[i].ToString(), sH2, counts[i] > 0 ? Gold : new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleCenter, 2);
        }
    }
    float lastTap = -1f;   // för dubbelklick = raket

    // Nollställer allt: bana 1, en ballong, inga mynt eller saker.
    void ResetProgress()
    {
        for (int i = 0; i < LevelCount; i++) PlayerPrefs.DeleteKey("bf_best" + i);
        foreach (var k in new[] { "bf_money", "bf_gems", "bf_balloons", "bf_steering", "bf_ups", "bf_sides", "bf_rockets", "bf_shields", "bf_brakes", "bf_unlocked", "bf_level" })
            PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        Load();
        level = 0;
        page = 0;
        builtLevel = -1;
        resetArmedUntil = 0f;
        ShowShop();
        Banner("Ny start! En ballong och bana 1.", 2f);
    }

    enum MenuPage { Main, Levels, Store, Bank }
    MenuPage menuPage = MenuPage.Main;

    void ShowStore() { ShowShop(); menuPage = MenuPage.Store; }

    void ShowShop()
    {
        menuPage = MenuPage.Main;
        paused = false;
        Time.timeScale = 1f;
        EnsureLevel(level);
        ResetRun();
        SetState(State.Shop);
        bannerTime = 0f;
        Save();
    }

    void StartLevel()
    {
        paused = false;
        Time.timeScale = 1f;
        EnsureLevel(level);
        ResetRun();
        SetState(State.Ready);
        Banner("BANA " + (level + 1) + ": " + LevelName(level).ToUpper(), 2.5f);
    }

    void SetState(State s)
    {
        state = s;
        stateTime = 0f;
    }

    void Banner(string text, float dur = 2f)
    {
        banner = text;
        bannerTime = bannerDur = dur;
    }

    void Jump()
    {
        SetState(State.Jumping);
        player.falling = true;
        player.vel = new Vector3(1.5f, 4.5f, 4f);
        sfx.PlayVoice(sfx.JumpGrunt, 1f);   // riktigt "hupp!" när han tar sats och hoppar
    }

    // fler ballonger = lugnare fart, men effekten avtar efter fyra ballonger
    float ForwardSpeed()
    {
        int n = Mathf.Max(1, player.Balloons);
        return Speed(level) / (0.85f + 0.15f * Mathf.Min(n, 4) + 0.04f * Mathf.Max(0, n - 4));
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool running = state == State.Falling || state == State.Jumping || state == State.Ready;
        ReadTouches();
        if ((Input.GetKeyDown(KeyCode.Escape) || pauseTapped) && running)
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            return;
        }
        if (paused) return;

        if (state == State.Shop) StoreIcons.Ensure();   // bilderna till affären skapas en gång
        stateTime += dt;
        coinPulse = Mathf.MoveTowards(coinPulse, 0f, Time.unscaledDeltaTime * 2.5f);
        bannerTime -= dt;
        shake = Mathf.Max(0f, shake - dt);

        switch (state)
        {
            case State.Shop:
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) StartLevel();
                if (Input.GetKeyDown(KeyCode.Escape)) menuPage = MenuPage.Main;
                if (menuPage == MenuPage.Levels)
                {
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) SelectLevel(level - 1);
                    if (Input.GetKeyDown(KeyCode.RightArrow)) SelectLevel(level + 1);
                }
                break;
            case State.Ready:
                if (stateTime > 0.3f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || freeTap)) Jump();   // tryck var som helst för att hoppa
                break;
            case State.Jumping:
                player.vel.y -= 18f * dt;
                player.vel.z = Mathf.MoveTowards(player.vel.z, ForwardSpeed(), 12f * dt);
                Move(dt);
                if (player.vel.y < 0f && player.transform.position.y <= lvl.PathY(player.transform.position.z))
                {
                    SetState(State.Falling);
                    Banner("NEDSTIGNING PÅBÖRJAD!", 2.2f);
                }
                break;
            case State.Falling:
                UpdateFall(dt);
                break;
            case State.Dying:
                UpdateDying(dt);
                break;
            case State.Dead:
            case State.Won:
                if (stateTime < 0.5f || god) break;
                if (Input.GetKeyDown(KeyCode.R)) StartLevel();
                else if (Input.GetKeyDown(KeyCode.Tab)) ShowStore();
                else if (Input.GetKeyDown(KeyCode.Escape)) ShowShop();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    if (state == State.Won && level < LevelCount - 1) { SelectLevel(level + 1); StartLevel(); }
                    else if (state == State.Won) ShowShop();
                    else StartLevel();
                }
                break;
        }

        // gubben tittar mot närmaste fågel
        Bird near = null;
        foreach (var b in birds) if (b != null && (near == null || (b.transform.position - player.transform.position).sqrMagnitude < (near.transform.position - player.transform.position).sqrMagnitude)) near = b;
        player.lookTarget = near != null ? near.transform.position : (Vector3?)null;

        bool windy = state == State.Falling || state == State.Dying || state == State.Jumping;
        sfx.SetWind(windy ? player.vel.magnitude / 18f : 0f);
    }

    void UpdateFall(float dt)
    {
        float input = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input += 1f;
        float vertical = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
        input = Mathf.Clamp(input + touchX, -1f, 1f);          // pekknappar ◀ ▶
        vertical = Mathf.Clamp(vertical + touchV, -1f, 1f);   // pekknappar ▲ ▼
        for (int i = 0; i < 5; i++) if (helpTapped[i]) UseHelp((Help)(i + 1));

        if (Pressed(KeyCode.Alpha1, KeyCode.Keypad1)) UseHelp(Help.Up);
        if (Pressed(KeyCode.Alpha2, KeyCode.Keypad2)) UseHelp(Help.Side);
        if (Pressed(KeyCode.Alpha3, KeyCode.Keypad3)) UseHelp(Help.Rocket);
        // dubbelklick (eller dubbeltryck på pekskärm) var som helst på skärmen = raket
        bool tap = freeTap;   // bara tryck på tom yta räknas (inte på knapparna)
        if (tap)
        {
            if (Time.unscaledTime - lastTap < 0.35f) { UseHelp(Help.Rocket); lastTap = -1f; }
            else lastTap = Time.unscaledTime;
        }
        if (Pressed(KeyCode.Alpha4, KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.E)) UseHelp(Help.Shield);
        if (Pressed(KeyCode.Alpha5, KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Q)) UseHelp(Help.Brake);
        UpdateHelp(dt);

        // sidled
        float lateral = 7f + 1.6f * steering;
        float accel = 32f + 10f * steering;
        if (active == Help.Side) { lateral *= 1.8f; accel *= 2f; }
        player.vel.x = Mathf.MoveTowards(player.vel.x, input * lateral, accel * dt);

        // framåt
        float fwd = ForwardSpeed();
        if (vertical < 0f) fwd *= 1.1f;   // nedåtpil/S: sjunker snabbt – nästan ingen extra fart framåt, så man hinner undvika hindren
        if (active == Help.Rocket) fwd *= 2.4f;
        if (player.brakeTime > 0f) fwd *= 0.4f;
        player.vel.z = Mathf.MoveTowards(player.vel.z, fwd, (active == Help.Rocket ? 30f : 12f) * dt);

        // upp/ner runt glidbanan (uppflygning lyfter högt över hindren)
        float maxLift = active == Help.Up ? LiftBoost : LiftMax;
        if (active == Help.Up) lift = Mathf.MoveTowards(lift, LiftBoost, 6f * dt);
        else if (vertical != 0f) lift += vertical * (vertical < 0f ? 11f : 5f) * dt;   // håll in ner: sjunker snabbt
        else lift = Mathf.MoveTowards(lift, 0f, 0.8f * dt);
        if (lift > maxLift) lift = Mathf.MoveTowards(lift, maxLift, 4f * dt);
        lift = Mathf.Max(lift, -LiftDown);   // man kan dyka längre ner än man kan stiga
        var pos = player.transform.position;
        float left = lvl.length - pos.z;
        if (left < 15f) lift = Mathf.MoveTowards(lift, 0f, 4f * dt);

        player.vel.y = -player.vel.z * lvl.slope;
        Move(dt);
        pos = player.transform.position;
        pos.y = Mathf.Max(lvl.floorY, lvl.PathY(pos.z) + lift);
        player.transform.position = pos;

        SpawnBirds(pos.z);

        float prog = Progress();
        if (prog > best[level] && prog < 1f) best[level] = prog;
        if (pos.z >= lvl.length)
        {
            if (!lvl.cave || lvl.OnDock(pos.x)) Land();
            else Splash();
        }
    }

    static bool Pressed(KeyCode a, KeyCode b) => Input.GetKeyDown(a) || Input.GetKeyDown(b);

    // ---------------------------------------------------------------- Hjälpmedel

    int Count(Help h)
    {
        switch (h)
        {
            case Help.Up: return ups;
            case Help.Side: return sides;
            case Help.Rocket: return rockets;
            case Help.Shield: return shields;
            case Help.Brake: return brakes;
        }
        return 0;
    }

    void UseHelp(Help h)
    {
        if (active != Help.None)
        {
            Banner("Bara ett hjälpmedel åt gången!", 1.3f);
            return;
        }
        if (Count(h) <= 0)
        {
            Banner("Du har ingen " + HelpNames[(int)h].ToLower() + " – köp i affären", 1.6f);
            return;
        }
        switch (h)
        {
            case Help.Up: ups--; sfx.Play(sfx.whoosh); break;
            case Help.Side: sides--; sfx.Play(sfx.whoosh); break;
            case Help.Rocket: rockets--; player.rocket = true; sfx.Play(sfx.rocket); shake = 0.2f; break;
            case Help.Shield: shields--; player.shieldTime = HelpDuration[(int)h]; sfx.Play(sfx.shield); break;
            case Help.Brake: brakes--; player.brakeTime = HelpDuration[(int)h]; sfx.Play(sfx.brake); break;
        }
        active = h;
        helpTime = HelpDuration[(int)h];
        Banner(HelpNames[(int)h] + "!", 1f);
    }

    void UpdateHelp(float dt)
    {
        if (active == Help.None) return;
        helpTime -= dt;
        bool shieldGone = active == Help.Shield && player.shieldTime <= 0f;
        if (helpTime <= 0f || shieldGone) EndHelp();
    }

    void EndHelp()
    {
        if (active == Help.Rocket) player.rocket = false;
        if (active == Help.Shield) player.shieldTime = 0f;
        active = Help.None;
        helpTime = 0f;
    }

    // ---------------------------------------------------------------- Fågeln

    void SpawnBirds(float z)
    {
        if (nextBird >= lvl.birdZ.Count || z < lvl.birdZ[nextBird] - 24f) return;
        bool hawk = nextBird < lvl.birdHawk.Count && lvl.birdHawk[nextBird];
        nextBird++;
        // finalen (bana 100) har de allra svåraste fåglarna
        float d = level == LevelCount - 1 ? 1.3f : 1f - Mathf.Pow(1f - Diff(level), 1.8f); // fåglarna blir svåra tidigare
        if (level >= 69)
        {
            // de svåraste banorna: två fåglar i taget, från var sin sida, den andra anfaller lite senare
            int s = Random.value < 0.5f ? -1 : 1;
            birds.Add(Bird.Spawn(lvl.root.transform, player, d, d >= 0.6f, hawk, s, 0f));
            birds.Add(Bird.Spawn(lvl.root.transform, player, d, d >= 0.6f, hawk, -s, Mathf.Lerp(1.1f, 0.7f, Diff(level))));
        }
        else birds.Add(Bird.Spawn(lvl.root.transform, player, d, d >= 0.6f, hawk));
        sfx.Play(sfx.screech);
        Banner(hawk ? "EN HÖK! Den dyker snabbt – flytta dig när den lyser rött!" : "SE UPP! Flytta dig när fågeln lyser rött!", 2.6f);
    }

    // Fågeln laddar upp ett anfall: varna spelaren.
    public void OnBirdWindup()
    {
        if (state != State.Falling) return;
        sfx.Play(sfx.screech, 0.6f);
        Banner("!  FLYTTA DIG  !", 0.7f);
    }

    // Fågeln gav upp: belöning.
    public void OnBirdGone()
    {
        if (state != State.Falling) return;
        money += 5;
        runCoins += 5;
        sfx.Play(sfx.gem);
        Banner("Du lurade fågeln! +5 mynt", 1.8f);
    }

    // ---------------------------------------------------------------- Rörelse, träffar, landning

    float Progress() => Mathf.Clamp01(player.transform.position.z / lvl.length);

    void Move(float dt)
    {
        var p = player.transform.position;
        p += player.vel * dt;
        float lim = StreetHalf - player.HalfWidth;
        if (state != State.Jumping && Mathf.Abs(p.x) > lim)
        {
            p.x = Mathf.Sign(p.x) * lim;
            player.vel.x = 0f;
        }
        float floor = lvl.cave && state == State.Dying ? -2.5f : lvl.floorY;
        if (state == State.Dying && lvl.cave && lvl.OnDock(p.x) && p.z > lvl.length - 6f) floor = lvl.floorY;
        if (p.y < floor)
        {
            p.y = floor;
            player.vel = Vector3.zero;
        }
        player.transform.position = p;
    }

    void Land()
    {
        var p = player.transform.position;
        player.transform.position = new Vector3(p.x, lvl.floorY, p.z);
        player.vel = Vector3.zero;
        player.falling = false;
        EndHelp();
        ClearBirds();
        lastBonus = 20 + 3 * level;
        money += lastBonus;
        best[level] = 1f;
        if (level + 2 > unlocked) unlocked = Mathf.Min(LevelCount, level + 2);
        SetState(State.Won);
        bannerTime = 0f;
        sfx.Play(lvl.cave ? sfx.landWood : sfx.landStone); // riktigt landningsljud, ingen melodi
        player.Celebrate();
        StartCoroutine(WinReaction());
        Save();
    }

    // Gubbens naturliga reaktion efter en lyckad landning
    IEnumerator WinReaction()
    {
        yield return new WaitForSeconds(0.45f);
        if (state != State.Won) yield break;
        sfx.PlayVoice(sfx.Relief, 1f);            // lättnadens suck
        yield return new WaitForSeconds(1.0f);
        if (state != State.Won) yield break;
        sfx.PlayVoice(sfx.Cheer, 1f);             // "Yes!"
        yield return new WaitForSeconds(1.4f);
        if (state != State.Won) yield break;
        sfx.PlayVoice(sfx.Breath, 0.3f);          // flämtar lite efteråt
    }

    void Splash()
    {
        EndHelp();
        Die("PLASK!", "Du kan inte simma – sikta på en brygga eller flotten!");
        player.vel = new Vector3(0f, -2f, player.vel.z * 0.3f);
    }

    bool grounded, inWater;

    // Faller hela vägen ner (kameran följer med), slår i marken och blir liggande.
    void UpdateDying(float dt)
    {
        if (!grounded)
        {
            player.vel.y = Mathf.Max(player.vel.y - 40f * dt, -75f);
            player.vel.x *= 1f - dt * 0.5f;
            player.vel.z *= 1f - dt * 0.6f;
            var p = player.transform.position + player.vel * dt;
            float lim = StreetHalf - 0.6f;
            p.x = Mathf.Clamp(p.x, -lim, lim);
            bool onDock = lvl.cave && lvl.OnDock(p.x) && p.z > lvl.length - 6f;
            float floor = lvl.cave && !onDock ? 0f : lvl.floorY;   // i grottan: vattenytan
            if (p.y <= floor)
            {
                p.y = floor;
                player.transform.position = p;
                Impact(lvl.cave && !onDock);
            }
            else player.transform.position = p;
        }
        else
        {
            if (inWater)
            {
                var p = player.transform.position;
                p.y = Mathf.MoveTowards(p.y, -2.6f, 1.2f * dt);   // sjunker
                player.transform.position = p;
            }
            if (stateTime > (inWater ? 2f : 3.2f))
            {
                SetState(State.Dead);
                Save();
            }
        }
    }

    float dieY, fallHeight;

    void Impact(bool water)
    {
        grounded = true;
        stateTime = 0f;
        Vector3 impactVel = player.vel;
        fallHeight = Mathf.Max(0f, dieY - player.transform.position.y);
        player.vel = Vector3.zero;
        sfx.StopVoice();   // skriket tystnar
        var pos = player.transform.position;
        if (water)
        {
            inWater = true;
            player.dying = false;
            sfx.Play(sfx.splash);
            Dust.Puff(pos + Vector3.up * 0.3f, new Color(0.8f, 0.9f, 1f, 0.35f), 14);   // vattenstänk
        }
        else
        {
            sfx.Play(lvl.cave ? sfx.ThudWood : sfx.ThudGround);
            Color dc = lvl.env == 4 ? new Color(0.95f, 0.97f, 1f, 0.5f)      // snörök
                     : lvl.env == 1 ? new Color(0.85f, 0.65f, 0.45f, 0.4f)   // sandmoln
                     : lvl.cave ? new Color(0.5f, 0.45f, 0.4f, 0.3f) : new Color(0.62f, 0.56f, 0.48f, 0.35f);
            Dust.Puff(pos, dc);
            // kroppen slår i hårt – men fysiken får en fart den klarar av utan att "tunnla" genom marken
            player.LieDown(new Vector3(impactVel.x * 0.5f, Mathf.Max(impactVel.y, -14f), impactVel.z * 0.4f));
            shake = 0.5f;
            deathText = "Du föll " + Mathf.RoundToInt(fallHeight) + " meter.";
            if (fallHeight < 20f) StartCoroutine(AfterFall());   // kort fall: stönar och flämtar. Långt fall: helt stilla.
        }
    }

    // Efter fallet: han stönar och flämtar – han lever, men det gjorde ont
    IEnumerator AfterFall()
    {
        yield return new WaitForSeconds(0.9f);
        if (state != State.Dying && state != State.Dead) yield break;
        sfx.PlayVoice(sfx.Groan, 1f);
        yield return new WaitForSeconds(1.1f);
        if (state != State.Dying && state != State.Dead) yield break;
        sfx.PlayVoice(sfx.Breath, 0.55f);
    }

    void Die(string title, string text)
    {
        SetState(State.Dying);
        grounded = inWater = false;
        dieY = player.transform.position.y;
        player.dying = true;
        sfx.PlayVoice(sfx.Scream, 1f);   // riktigt skrik medan han faller
        deathTitle = title;
        deathText = text;
        deathCamPos = cam.transform.position;
        Banner(title, 2f);
    }

    public void OnHazard(bool deadly)
    {
        if (god || state != State.Falling || player.invuln > 0f) return;
        if (active == Help.Rocket) return; // raketen krossar sig igenom allt
        if (player.shieldTime > 0f)
        {
            EndHelp();
            player.invuln = 1f;
            sfx.Play(sfx.shield);
            Banner("Skölden räddade dig!", 1.4f);
            return;
        }
        shake = 0.35f;
        sfx.Play(sfx.Pop);
        if (deadly) player.PopAll();
        else player.Pop();

        if (player.Balloons == 0)
        {
            EndHelp();
            player.vel = new Vector3(player.vel.x * 0.3f, 2f, player.vel.z * 0.6f);
            if (deadly && lvl.cave) Die("GIFTIG RÖK!", "Röken i grottan dödar direkt – håll dig undan.");
            else if (deadly) Die("FÅGELN TOG DIG!", "Flytta dig åt sidan när fågeln lyser rött!");
            else Die("BALLONGEN SPRACK!", "Du föll ner.");
        }
        else
        {
            player.invuln = 1.5f;
            player.Flinch();   // rycker till av skräck
            StartCoroutine(After(0.12f, () => sfx.PlayVoice(sfx.Fright, 0.9f)));   // flämtar eller "argh!"
            Banner("PANG! " + player.Balloons + (player.Balloons == 1 ? " ballong kvar" : " ballonger kvar"), 1.5f);
        }
    }

    public void OnCoin(Coin c)
    {
        if (state != State.Falling && state != State.Jumping) return;
        if (!c.gameObject.activeSelf) return;
        c.gameObject.SetActive(false);
        money += c.value;
        runCoins += c.value;
        coinPulse = 1f;
        lastGain = c.value;
        sfx.Play(sfx.coin, 0.7f);
    }

    // ---------------------------------------------------------------- Kamera

    void LateUpdate()
    {
        if (lvl == null) return;
        float dt = Time.unscaledDeltaTime;
        if (paused) return;
        Vector3 p = player.transform.position;
        float H = lvl.height;
        Vector3 pos, look;
        float k;
        switch (state)
        {
            case State.Shop:
                if (menuPage == MenuPage.Main)
                {
                    // startmenyn: gubben framifrån med sina ballonger, till vänster i bilden
                    float sway = Mathf.Sin(Time.time * 0.25f) * 0.25f;
                    // kameran stannar inne i gatan (inte i husväggen) och vinklas så att gubben hamnar till vänster
                    pos = new Vector3(Mathf.Max(p.x - 0.7f, -StreetHalf + 0.6f) + sway * 0.4f, p.y + 1.9f, p.z + 6.2f);
                    look = new Vector3(p.x - 2.6f, p.y + 2.15f, p.z);
                    k = 3f;
                }
                else
                {
                    float a = Time.time * 0.15f;
                    pos = new Vector3(1.5f + Mathf.Sin(a) * 1.5f, H + 6f, -16f);
                    look = new Vector3(0f, H - 12f, 35f);
                    k = 2f;
                }
                break;
            case State.Ready:
                pos = p + new Vector3(2.6f, 2.6f, -7.5f);
                look = p + new Vector3(0.5f, 0.9f, 8f);
                k = 2.5f;
                break;
            case State.Won:
                // framifrån: man ser hela gubben (landningen, lättnaden och jublet) ovanför rutan
                pos = p + new Vector3(1.3f, 1.2f, 5.6f);
                look = p + new Vector3(0f, 0.35f, 0f);
                k = 5f;
                break;
            case State.Dying:
            case State.Dead:
                if (!grounded)
                {
                    // följer med honom hela vägen ner – man ser hur långt det är
                    pos = p + new Vector3(2.5f, 5.5f, -6.5f);
                    look = p + new Vector3(0f, -3f, 3f);
                    k = 6f;
                }
                else if (inWater)
                {
                    pos = p + new Vector3(3f, 4.5f, -5f);
                    look = p;
                    k = 2f;
                }
                else
                {
                    // han ligger på marken – kameran tittar på kroppen där den hamnade
                    Vector3 c = player.BodyCenter;
                    pos = c + new Vector3(2.6f, 3.4f, -2.6f);
                    look = c;
                    k = 2f;
                }
                break;
            default:
                // bakom och lite ovanför gubben, tittar framåt längs gatan
                // kameran följer glidbanan, inte upp/ner-styrningen: när du trycker ner syns det att GUBBEN åker neråt i bilden
                float camY = state == State.Falling ? lvl.PathY(p.z) + lift * 0.25f : p.y;
                pos = new Vector3(p.x * 0.6f, camY + 3.4f, p.z - 10.5f); // längre bort: gubben blir en liten siluett i miljön
                look = new Vector3(p.x * 0.7f, camY + 1.2f - lvl.slope * 15f, p.z + 30f);
                k = state == State.Jumping ? 3f : 7f;
                break;
        }
        if (snapCam)
        {
            camPos = pos;
            camLook = look;
            snapCam = false;
        }
        float f = 1f - Mathf.Exp(-k * dt);
        camPos = Vector3.Lerp(camPos, pos, f);
        camLook = Vector3.Lerp(camLook, look, f);
        cam.transform.position = camPos + Random.insideUnitSphere * shake * 0.6f;
        cam.transform.LookAt(camLook);
    }

    // ---------------------------------------------------------------- GUI

    Texture2D texWhite, texCircle, texRing, texVignette;
    GUIStyle sTitle, sH1, sH2, sText, sSmall, sBtn, sBtnBig, sBanner, sRow, sDesc, sHud, sDeath;
    float VW;
    float coinPulse;   // myntmätaren "poppar" när man plockar ett mynt
    int lastGain;
    int btnCounter, pressId = -1;
    int page;
    static readonly Color Gold = new Color(1f, 0.82f, 0.3f), Ink = new Color(0.13f, 0.08f, 0.06f), Gem = new Color(0.55f, 0.9f, 1f);
    static readonly Color PanelCol = new Color(0.16f, 0.1f, 0.07f, 0.86f), BtnCol = new Color(0.36f, 0.25f, 0.17f);
    static readonly Color Red = new Color(0.82f, 0.24f, 0.16f), Green = new Color(0.3f, 0.6f, 0.3f), Blue = new Color(0.22f, 0.45f, 0.75f);

    GUIStyle Style(int size, TextAnchor anchor, FontStyle fs = FontStyle.Bold)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, fontStyle = fs, wordWrap = false, richText = false };
        s.normal.textColor = Color.white;
        return s;
    }

    void InitGui()
    {
        if (sTitle != null) return;
        texWhite = Texture2D.whiteTexture;
        texCircle = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f));
                float aIn = Mathf.Clamp01(29f - d), aOut = Mathf.Clamp01(32f - d);
                Color c = Color.Lerp(Ink, Color.white, aIn);
                c.a = aOut;
                texCircle.SetPixel(x, y, c);
            }
        texCircle.Apply();
        // ram runt porträttet: genomskinligt inuti, mörk ring, panelfärg utanför
        texRing = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64f, 64f)) / 64f;
                Color c;
                if (d < 0.84f) c = new Color(0, 0, 0, 0);
                else if (d < 0.88f) c = Ink;
                else if (d < 0.95f) c = new Color(0.78f, 0.62f, 0.36f);
                else c = new Color(PanelCol.r, PanelCol.g, PanelCol.b, 1f);
                texRing.SetPixel(x, y, c);
            }
        texRing.Apply();
        // vinjett: mörka, filmiska kanter
        texVignette = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float dx = (x + 0.5f) / 64f - 1f, dy = (y + 0.5f) / 64f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                texVignette.SetPixel(x, y, new Color(0.02f, 0.02f, 0.04f, Mathf.SmoothStep(0f, 0.8f, (d - 0.45f) / 0.85f)));
            }
        texVignette.Apply();
        texVignette.wrapMode = TextureWrapMode.Clamp;
        sTitle = Style(60, TextAnchor.MiddleCenter);
        sH1 = Style(28, TextAnchor.MiddleLeft);
        sH2 = Style(20, TextAnchor.MiddleLeft);
        sText = Style(16, TextAnchor.MiddleLeft, FontStyle.Normal);
        sSmall = Style(15, TextAnchor.MiddleCenter, FontStyle.Normal);
        sBtn = Style(21, TextAnchor.MiddleCenter);
        sBtnBig = Style(34, TextAnchor.MiddleCenter);
        sBanner = Style(50, TextAnchor.MiddleCenter);
        sRow = Style(18, TextAnchor.MiddleLeft);
        sDesc = Style(14, TextAnchor.MiddleLeft, FontStyle.Normal);
        sHud = Style(17, TextAnchor.MiddleLeft);
        sDeath = Style(46, TextAnchor.MiddleCenter);
    }

    void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, texWhite);
        GUI.color = old;
    }

    void Panel(Rect r)
    {
        Fill(new Rect(r.x + 5, r.y + 6, r.width, r.height), new Color(0, 0, 0, 0.35f));
        Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Ink);
        Fill(r, PanelCol);
    }

    void Text(Rect r, string t, GUIStyle s, Color col, TextAnchor? anchor = null, int outline = 2)
    {
        var oldA = s.alignment;
        if (anchor.HasValue) s.alignment = anchor.Value;
        s.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, col.a);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0) GUI.Label(new Rect(r.x + dx * outline, r.y + dy * outline + 1, r.width, r.height), t, s);
        s.normal.textColor = col;
        GUI.Label(r, t, s);
        s.alignment = oldA;
    }

    bool Btn(Rect r, string label, Color baseCol, bool enabled = true, GUIStyle style = null)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        Color c = enabled ? (hover ? Color.Lerp(baseCol, Color.white, 0.18f) : baseCol) : new Color(0.32f, 0.29f, 0.27f, 0.95f);
        Fill(new Rect(r.x + 3, r.y + 5, r.width, r.height), new Color(0, 0, 0, 0.4f));
        Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Ink);
        Fill(r, c);
        Fill(new Rect(r.x, r.y, r.width, 3), Color.Lerp(c, Color.white, 0.25f));
        if (label.Length > 0) Text(r, label, style ?? sBtn, enabled ? Color.white : new Color(0.7f, 0.68f, 0.65f), TextAnchor.MiddleCenter);
        // egen klickhantering: en knapp räknas bara om man trycker ner OCH släpper på den
        int id = btnCounter++;
        var e = Event.current;
        if (!enabled) return false;
        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { pressId = id; e.Use(); }
        else if (e.type == EventType.MouseUp && e.button == 0 && pressId == id)
        {
            pressId = -1;
            if (r.Contains(e.mousePosition) && (stateTime > 0.4f || paused)) { e.Use(); return true; }
        }
        return false;
    }

    // Liten myntikon: guldmynt med ett B i mitten (som mynten i banorna)
    void CoinIcon(Rect r)
    {
        var old = GUI.color;
        GUI.color = new Color(0.55f, 0.36f, 0.06f);
        GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), texCircle);
        GUI.color = new Color(0.98f, 0.76f, 0.22f);
        GUI.DrawTexture(new Rect(r.x + r.width * 0.08f, r.y + r.height * 0.08f, r.width * 0.84f, r.height * 0.84f), texCircle);
        GUI.color = old;
        int oldSize = sRow.fontSize;
        sRow.fontSize = Mathf.Max(8, Mathf.RoundToInt(r.height * 0.62f));
        var a = sRow.alignment;
        sRow.alignment = TextAnchor.MiddleCenter;
        sRow.normal.textColor = new Color(0.55f, 0.36f, 0.06f);
        GUI.Label(new Rect(r.x, r.y + 1, r.width, r.height), "B", sRow);
        sRow.alignment = a;
        sRow.fontSize = oldSize;
    }

    void Diamond(Rect r)
    {
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, r.center);
        Fill(new Rect(r.x - 1.5f, r.y - 1.5f, r.width + 3f, r.height + 3f), Ink);
        Fill(r, Gem);
        Fill(new Rect(r.x, r.y, r.width * 0.45f, r.height * 0.45f), Color.white);
        GUI.matrix = m;
    }

    static readonly bool NoHud = Arg("-ballongNoHud") != null;   // för att fotografera banbilderna utan menyer

    void OnGUI()
    {
        InitGui();
        if (NoHud) return;
        btnCounter = 0;
        float s = Screen.height / 720f;
        VW = Screen.width / s;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        GUI.DrawTexture(new Rect(0, 0, VW, 720), texVignette, ScaleMode.StretchToFill);

        switch (state)
        {
            case State.Shop: DrawShop(); break;
            case State.Ready: DrawHud(); DrawReady(); break;
            case State.Jumping:
            case State.Falling:
            case State.Dying: DrawHud(); break;
            case State.Dead: DrawHud(); DrawDead(); break;
            case State.Won: DrawWon(); break;
        }
        if (state == State.Falling || state == State.Jumping || state == State.Dying) DrawSpeedLines();
        if (bannerTime > 0f && banner.Length > 0) DrawBanner();
        if (state != State.Shop && state != State.Won) DrawTouchControls();
        if (paused) DrawPause();
    }

    // ---------------------------------------------------------------- Menyer: startmeny, banor, affären

    void DrawShop()
    {
        if (menuPage == MenuPage.Bank) DrawBankPage();
        else if (menuPage == MenuPage.Levels) DrawLevelsPage();
        else if (menuPage == MenuPage.Store) DrawStorePage();
        else DrawMainMenu();
    }

    void Wallet()
    {
        var w = new Rect(VW - 250f, 18f, 230f, 70f);
        Panel(w);
        Text(new Rect(w.x + 12, w.y + 4, w.width - 56, 34), money.ToString(), sH1, Gold, TextAnchor.MiddleRight);
        CoinIcon(new Rect(w.xMax - 40, w.y + 8, 28, 28));
        Text(new Rect(w.x + 12, w.y + 38, w.width - 56, 28), gems.ToString(), sH2, Gem, TextAnchor.MiddleRight);
        Diamond(new Rect(w.xMax - 32, w.y + 47, 12, 12));
    }

    bool BackButton() => Btn(new Rect(20f, 22f, 170f, 52f), "‹ Tillbaka", BtnCol, true, sBtn);

    // Startmenyn: titel, bild på nästa bana och tre stora knappar
    void DrawMainMenu()
    {
        Text(new Rect(0, 34, VW, 84), "BALLONGFALLET", sTitle, new Color(1f, 0.96f, 0.85f), null, 4);
        Text(new Rect(0, 108, VW, 30), "En fattig gubbe, en ballong och en väldigt lång väg ner.", sH2, Color.white, TextAnchor.MiddleCenter);
        Wallet();

        // de tre knapparna
        float bx = VW / 2f + 80f, bw = 380f;
        if (Btn(new Rect(bx, 186f, bw, 96f), "SPELA", Red, true, sBtnBig)) StartLevel();
        if (Btn(new Rect(bx, 304f, bw, 88f), "BANOR", new Color(0.25f, 0.42f, 0.62f), true, sBtnBig)) menuPage = MenuPage.Levels;
        if (Btn(new Rect(bx, 414f, bw, 88f), "AFFÄREN", Green, true, sBtnBig)) menuPage = MenuPage.Store;
        Text(new Rect(bx, 306f, bw - 16, 30), unlocked + "/" + LevelCount + " öppna", sDesc, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperRight, 1);
        Text(new Rect(bx, 416f, bw - 16, 30), "Uppgraderingar & hjälpmedel", sDesc, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperRight, 1);
        // vilken bana man spelar härnäst
        var nb = new Rect(bx, 520f, bw, 62f);
        Panel(nb);
        Text(new Rect(nb.x + 14, nb.y + 4, nb.width - 28, 30), "Bana " + (level + 1) + ": " + LevelName(level), sH2, Gold);
        Text(new Rect(nb.x + 14, nb.y + 4, nb.width - 28, 30), Height(level).ToString("0") + " m", sH2, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleRight);
        Text(new Rect(nb.x + 14, nb.y + 32, nb.width - 28, 26), LevelInfo(level), sDesc, new Color(0.95f, 0.9f, 0.82f), null, 1);

        // börja om från början (tryck två gånger för att vara säker)
        bool armed = Time.unscaledTime < resetArmedUntil;
        if (Btn(new Rect(20f, 640f, 200f, 44f), armed ? "Säker? Tryck igen!" : "Börja om från början", armed ? Red : BtnCol, true, sRow))
        {
            if (armed) ResetProgress();
            else resetArmedUntil = Time.unscaledTime + 3f;
        }
        Text(new Rect(0, 684, VW, 28), "Styr: dra med fingret/musen eller A/D och W/S   Hjälpmedel: 1–5   Paus: Esc   Starta: Enter",
            sSmall, new Color(1f, 0.95f, 0.85f), null, 1);
    }

    // Banor: 10 kort per sida i två kolumner
    void DrawLevelsPage()
    {
        if (BackButton()) menuPage = MenuPage.Main;
        Wallet();
        int pages = LevelCount / 10;
        page = Mathf.Clamp(page, 0, pages - 1);
        Text(new Rect(0, 8, VW, 72), "BANOR " + (page * 10 + 1) + "–" + (page * 10 + 10), sTitle, Gold, TextAnchor.MiddleCenter, 3);
        Text(new Rect(0, 78, VW, 26), unlocked + " av " + LevelCount + " banor öppna", sH2, Color.white, TextAnchor.MiddleCenter);
        float cw = Mathf.Min(520f, VW / 2f - 70f);
        for (int k = 0; k < 10; k++)
        {
            int i = page * 10 + k;
            int col = k / 5, rowI = k % 5;
            var b = new Rect(VW / 2f + (col == 0 ? -cw - 14f : 14f), 118f + rowI * 86f, cw, 76f);
            LevelCard(b, i);
        }
        if (Btn(new Rect(VW / 2f - 330f, 600f, 140f, 64f), "◀ Förra", BtnCol, page > 0)) page--;
        if (Btn(new Rect(VW / 2f + 190f, 600f, 140f, 64f), "Nästa ▶", BtnCol, page < pages - 1)) page++;
        if (Btn(new Rect(VW / 2f - 170f, 600f, 340f, 64f), "SPELA BANA " + (level + 1), Red, true, sBtn)) StartLevel();
        Text(new Rect(0, 678, VW, 28), "Tryck på en bana för att välja den", sSmall, new Color(1f, 0.95f, 0.85f), null, 1);
    }

    // Affären: kort med 3D-bild på varje sak, namn, beskrivning, hur många man har och pris
    void DrawStorePage()
    {
        if (BackButton()) menuPage = MenuPage.Main;
        Wallet();
        Text(new Rect(0, 12, VW, 76), "AFFÄREN", sTitle, Gold, TextAnchor.MiddleCenter, 3);
        float gap = 16f, cw = Mathf.Min(272f, (VW - 120f - 3f * gap) / 4f), ch = 236f;
        float x0 = VW / 2f - (4f * cw + 3f * gap) / 2f, y0 = 96f;
        Rect Cell(int k) => new Rect(x0 + (k % 4) * (cw + gap), y0 + (k / 4) * (ch + gap), cw, ch);

        // varje sak kan köpas med mynt, med diamanter, eller (inte inkopplat än) med riktiga pengar
        int bp = maxBalloons < Player.MaxBalloons ? BalloonCost[maxBalloons - 1] : -1;
        int r0 = StoreCard(Cell(0), 0, "Extra ballong", "Ett liv till och lugnare fart – men bredare knippe", bp, bp < 0 ? -1 : Mathf.Max(2, bp / 50), bp < 0 ? null : "15 kr", maxBalloons, Player.MaxBalloons, true);
        if (r0 > 0) { if (r0 == 1) money -= bp; else gems -= Mathf.Max(2, bp / 50); maxBalloons++; player.SetBalloons(maxBalloons); Save(); }
        int sp = steering < 3 ? SteerCost[steering] : -1;
        int r1 = StoreCard(Cell(1), 1, "Bättre styrning", "Alltid snabbare åt sidorna", sp, sp < 0 ? -1 : Mathf.Max(1, sp / 50), sp < 0 ? null : "10 kr", steering, 3, true);
        if (r1 > 0) { if (r1 == 1) money -= sp; else gems -= Mathf.Max(1, sp / 50); steering++; Save(); }
        int r2 = StoreCard(Cell(2), 2, "[1] Uppflygning", "Flyg högt över hindren i 4 sekunder", ups < MaxItems ? UpCost : -1, ups < MaxItems ? 1 : -1, ups < MaxItems ? "5 kr" : null, ups, MaxItems, false);
        if (r2 > 0) { if (r2 == 1) money -= UpCost; else gems -= 1; ups++; Save(); }
        int r3 = StoreCard(Cell(3), 3, "[2] Sidoturbo", "Supersnabb åt sidorna i 5 sekunder", sides < MaxItems ? SideCost : -1, sides < MaxItems ? 1 : -1, sides < MaxItems ? "5 kr" : null, sides, MaxItems, false);
        if (r3 > 0) { if (r3 == 1) money -= SideCost; else gems -= 1; sides++; Save(); }
        int r4 = StoreCard(Cell(4), 4, "[3] Raket", "Susa framåt – inget träffar dig i 3 s. Dubbeltryck!", rockets < MaxItems ? RocketCoins : -1, rockets < MaxItems ? RocketGems : -1, rockets < MaxItems ? "10 kr" : null, rockets, MaxItems, false);
        if (r4 > 0) { if (r4 == 1) money -= RocketCoins; else gems -= RocketGems; rockets++; Save(); }
        int r5 = StoreCard(Cell(5), 5, "[4] Sköld", "Tål en träff – även fågeln och giftig rök", shields < MaxItems ? ShieldCost : -1, shields < MaxItems ? 1 : -1, shields < MaxItems ? "5 kr" : null, shields, MaxItems, false);
        if (r5 > 0) { if (r5 == 1) money -= ShieldCost; else gems -= 1; shields++; Save(); }
        int r6 = StoreCard(Cell(6), 6, "[5] Broms", "Fallskärm som saktar in farten i 3 s", brakes < MaxItems ? BrakeCost : -1, brakes < MaxItems ? 1 : -1, brakes < MaxItems ? "5 kr" : null, brakes, MaxItems, false);
        if (r6 > 0) { if (r6 == 1) money -= BrakeCost; else gems -= 1; brakes++; Save(); }
        int r7 = StoreCard(Cell(7), 7, "Diamant", GemPrice + " mynt = 1 diamant", GemPrice, -1, "10 kr", gems, -1, false);
        if (r7 == 1) { money -= GemPrice; gems++; sfx.Play(sfx.gem); Save(); }

        if (Btn(new Rect(VW / 2f - 170f, 600f, 340f, 64f), "SPELA BANA " + (level + 1), Red, true, sBtn)) StartLevel();
        if (Btn(new Rect(VW - 250f, 600f, 230f, 64f), "Köp mynt &\ndiamanter", new Color(0.75f, 0.55f, 0.12f), true, sRow)) { menuPage = MenuPage.Bank; bankNotice = false; }
        if (bankNotice) DrawBankNotice();
    }

    // ---- Köp mynt och diamanter för riktiga pengar.
    // OBS: betalning är INTE inkopplad. Riktiga köp kräver att spelet finns i App Store / Google Play
    // och ett utvecklarkonto som en vuxen äger. Knapparna visar därför bara information.
    bool bankNotice;
    static readonly int[] BankCoins = { 500, 1500, 5000 }, BankGems = { 5, 20, 60 };
    static readonly string[] BankCoinPrice = { "10 kr", "25 kr", "69 kr" }, BankGemPrice = { "10 kr", "35 kr", "89 kr" };
    static readonly string[] BankCoinName = { "Påse med mynt", "Kista med mynt", "Skattkista" }, BankGemName = { "Liten näve", "Stor näve", "Diamantskrin" };

    void DrawBankPage()
    {
        if (BackButton()) menuPage = MenuPage.Store;
        Wallet();
        int tfs = sTitle.fontSize;
        sTitle.fontSize = 44;
        Text(new Rect(0, 18, VW, 64), "KÖP MYNT & DIAMANTER", sTitle, Gold, TextAnchor.MiddleCenter, 3);
        sTitle.fontSize = tfs;
        float gap = 16f, cw = Mathf.Min(260f, (VW - 140f - 2f * gap) / 3f), ch = 210f;
        float x0 = VW / 2f - (3f * cw + 2f * gap) / 2f;
        for (int row = 0; row < 2; row++)
        {
            for (int k = 0; k < 3; k++)
            {
                var c = new Rect(x0 + k * (cw + gap), 100f + row * (ch + 20f), cw, ch);
                bool gemsRow = row == 1;
                Fill(new Rect(c.x + 4, c.y + 5, c.width, c.height), new Color(0, 0, 0, 0.35f));
                Fill(new Rect(c.x - 2, c.y - 2, c.width + 4, c.height + 4), Ink);
                Fill(c, new Color(0.2f, 0.15f, 0.11f, 0.96f));
                Fill(new Rect(c.x, c.y, c.width, 3), gemsRow ? Gem : Gold);
                // bild: en hög mynt eller diamanter, större ju större paket
                var area = new Rect(c.x + 10, c.y + 12, c.width - 20, 92);
                Fill(area, new Color(0.17f, 0.21f, 0.27f));
                int n = k == 0 ? 3 : k == 1 ? 6 : 10;
                for (int j = 0; j < n; j++)
                {
                    float px = area.center.x + ((j % 5) - 2f) * 26f + (j / 5) * 13f, py = area.yMax - 34f - (j / 5) * 22f;
                    if (gemsRow) { var tex = StoreIcons.Get(7); if (tex != null) GUI.DrawTexture(new Rect(px - 22, py - 22, 44, 44), tex, ScaleMode.ScaleAndCrop); }
                    else CoinIcon(new Rect(px - 16, py - 10, 32, 32));
                }
                string amount = gemsRow ? BankGems[k] + " diamanter" : BankCoins[k].ToString("N0").Replace(",", " ") + " mynt";
                Text(new Rect(c.x, c.y + 110, c.width, 28), gemsRow ? BankGemName[k] : BankCoinName[k], sH2, Color.white, TextAnchor.MiddleCenter);
                Text(new Rect(c.x, c.y + 136, c.width, 26), amount, sRow, gemsRow ? Gem : Gold, TextAnchor.MiddleCenter, 1);
                if (k == 2) Text(new Rect(c.x, c.y + 14, c.width - 16, 22), "BÄST VÄRDE", sDesc, new Color(1f, 0.85f, 0.3f), TextAnchor.UpperRight, 1);
                if (Btn(new Rect(c.x + 14, c.yMax - 44, c.width - 28, 34), gemsRow ? BankGemPrice[k] : BankCoinPrice[k], new Color(0.75f, 0.55f, 0.12f), true, sRow))
                    bankNotice = true;
            }
        }
        Text(new Rect(0, 572, VW, 26), "Du kan alltid tjäna mynt gratis genom att spela – och växla till diamanter i affären.", sText, new Color(1f, 0.95f, 0.85f), TextAnchor.MiddleCenter, 1);
        if (bankNotice) DrawBankNotice();
    }

    // Informationsruta: köp med riktiga pengar är inte inkopplat
    void DrawBankNotice()
    {
        Fill(new Rect(0, 0, VW, 720), new Color(0, 0, 0, 0.55f));
        var r = new Rect(VW / 2f - 330f, 190f, 660f, 300f);
        Panel(r);
        Text(new Rect(r.x, r.y + 16, r.width, 40), "Inte påslaget än", sH1, Gold, TextAnchor.MiddleCenter);
        var ws = new GUIStyle(sText) { wordWrap = true, alignment = TextAnchor.UpperCenter };
        Text(new Rect(r.x + 30, r.y + 70, r.width - 60, 140),
            "Köp med riktiga pengar fungerar först när spelet finns i App Store eller Google Play. " +
            "Då behöver en vuxen skapa ett utvecklarkonto och koppla in betalningen.\n\n" +
            "Inget har köpts och inga pengar har dragits.", ws, Color.white, null, 1);
        if (Btn(new Rect(r.center.x - 100f, r.yMax - 70f, 200f, 52f), "OK", Green)) bankNotice = false;
    }

    // Ett kort i affären med upp till tre betalsätt: mynt, diamanter och riktiga pengar (inte inkopplat).
    // Returnerar 1 = köpt med mynt, 2 = köpt med diamanter, 0 = inget köp.
    int StoreCard(Rect c, int icon, string title, string desc, int coinCost, int gemCost, string kr, int have, int max, bool upgrade)
    {
        bool luxury = coinCost < 0 && gemCost >= 0;
        Fill(new Rect(c.x + 4, c.y + 5, c.width, c.height), new Color(0, 0, 0, 0.35f));
        Fill(new Rect(c.x - 2, c.y - 2, c.width + 4, c.height + 4), Ink);
        Fill(c, new Color(0.2f, 0.15f, 0.11f, 0.96f));
        Fill(new Rect(c.x, c.y, c.width, 3), luxury ? Gem : Gold);
        float isz = 100f;
        var ir = new Rect(c.center.x - isz / 2f, c.y + 10f, isz, isz);
        Fill(new Rect(ir.x - 2, ir.y - 2, ir.width + 4, ir.height + 4), new Color(0, 0, 0, 0.5f));
        var tex = StoreIcons.Get(icon);
        if (tex != null) GUI.DrawTexture(ir, tex, ScaleMode.ScaleAndCrop);
        Text(new Rect(c.x + 8, c.y + 114, c.width - 16, 26), title, sH2, Color.white, TextAnchor.MiddleCenter);
        var ds = new GUIStyle(sDesc) { wordWrap = true, alignment = TextAnchor.UpperCenter };
        Text(new Rect(c.x + 10, c.y + 139, c.width - 20, 36), desc, ds, new Color(0.92f, 0.88f, 0.8f), null, 1);
        if (upgrade)
        {
            float dot = max > 5 ? 12f : 16f, step = dot + 5f, w = max * step - 5f;
            for (int k = 0; k < max; k++)
            {
                var old = GUI.color;
                GUI.color = k < have ? Gold : new Color(0.35f, 0.32f, 0.3f);
                GUI.DrawTexture(new Rect(c.center.x - w / 2f + k * step, c.y + 176f, dot, dot), texCircle);
                GUI.color = old;
            }
        }
        else Text(new Rect(c.x, c.y + 172f, c.width, 22), max > 0 ? "Har " + have + " / " + max : "Har " + have, sDesc, Gold, TextAnchor.MiddleCenter, 1);

        // köpknappar
        int result = 0;
        int n = (coinCost >= 0 ? 1 : 0) + (gemCost >= 0 ? 1 : 0) + (kr != null ? 1 : 0);
        float y = c.yMax - 40f, x = c.x + 10f, totalW = c.width - 20f, g = 6f;
        if (n == 0)
        {
            Btn(new Rect(x, y, totalW, 32f), "MAX", BtnCol, false, sRow);
            return 0;
        }
        float bw = (totalW - g * (n - 1)) / n;
        int fs = sRow.fontSize;
        sRow.fontSize = n >= 3 ? 15 : 17;
        if (coinCost >= 0)
        {
            var r = new Rect(x, y, bw, 32f); x += bw + g;
            bool ok = money >= coinCost;
            if (Btn(r, "", Green, ok, sRow)) result = 1;
            CoinIcon(new Rect(r.x + 5, r.y + 6, 20, 20));
            Text(new Rect(r.x + 22, r.y, r.width - 24, r.height), coinCost.ToString(), sRow, ok ? Color.white : new Color(0.7f, 0.68f, 0.65f), TextAnchor.MiddleCenter, 1);
        }
        if (gemCost >= 0)
        {
            var r = new Rect(x, y, bw, 32f); x += bw + g;
            bool ok = gems >= gemCost;
            if (Btn(r, "", Blue, ok, sRow)) result = 2;
            Diamond(new Rect(r.x + 9, r.y + 11, 10, 10));
            Text(new Rect(r.x + 18, r.y, r.width - 20, r.height), gemCost.ToString(), sRow, ok ? Color.white : new Color(0.7f, 0.68f, 0.65f), TextAnchor.MiddleCenter, 1);
        }
        if (kr != null)
        {
            var r = new Rect(x, y, bw, 32f);
            if (Btn(r, kr, new Color(0.75f, 0.55f, 0.12f), true, sRow)) bankNotice = true;   // riktiga pengar: visar bara info
        }
        sRow.fontSize = fs;
        if (result > 0) sfx.Play(sfx.buy);
        return result;
    }

    // ---- Banmenyn: kort med bild på miljön, nummer, namn, höjd och status
    Texture2D[] thumbs;

    Texture2D Thumb(int i)
    {
        if (thumbs == null)
        {
            thumbs = new Texture2D[6];
            for (int t = 0; t < 5; t++) thumbs[t] = Resources.Load<Texture2D>("Banbilder/miljo" + t);
            thumbs[5] = Resources.Load<Texture2D>("Banbilder/finalen");
        }
        return i == LevelCount - 1 ? thumbs[5] : thumbs[Theme(i)];
    }

    void LevelCard(Rect b, int i)
    {
        bool locked = i >= unlocked, selected = i == level, done = best[i] >= 1f;
        bool isNext = !locked && !done && i == unlocked - 1;
        // ram: guld för vald bana
        Fill(new Rect(b.x + 3, b.y + 4, b.width, b.height), new Color(0, 0, 0, 0.35f));
        Fill(new Rect(b.x - 3, b.y - 3, b.width + 6, b.height + 6), selected ? Gold : Ink);
        if (Btn(b, "", locked ? new Color(0.2f, 0.18f, 0.16f) : selected ? new Color(0.42f, 0.3f, 0.18f) : new Color(0.3f, 0.22f, 0.15f), !locked)) SelectLevel(i);
        // bild på miljön
        var th = new Rect(b.x + 4, b.y + 4, 118f, b.height - 8);
        var tex = Thumb(i);
        if (tex != null) GUI.DrawTexture(th, tex, ScaleMode.ScaleAndCrop);
        else Fill(th, new Color(0.3f, 0.35f, 0.4f));
        Fill(new Rect(th.x, th.yMax - 2, th.width, 2), new Color(0, 0, 0, 0.5f));
        if (locked)
        {
            Fill(th, new Color(0.05f, 0.04f, 0.04f, 0.68f));
            Padlock(new Vector2(th.center.x, th.center.y + 4f));
        }
        // nummer i ett runt märke
        var badge = new Rect(th.x + 4, th.y + 4, 30, 30);
        var old = GUI.color;
        GUI.color = locked ? new Color(0.35f, 0.33f, 0.3f) : done ? new Color(0.3f, 0.62f, 0.32f) : Gold;
        GUI.DrawTexture(badge, texCircle);
        GUI.color = old;
        int fs = sRow.fontSize;
        sRow.fontSize = i >= 99 ? 12 : 15;
        Text(badge, (i + 1).ToString(), sRow, Color.white, TextAnchor.MiddleCenter, 1);
        sRow.fontSize = fs;
        // namn och höjd
        float tx = th.xMax + 12f, tw = b.xMax - tx - 10f;
        Color tc = locked ? new Color(0.62f, 0.6f, 0.57f) : Color.white;
        Text(new Rect(tx, b.y + 6, tw, 28), LevelName(i), sH2, tc);
        Text(new Rect(tx, b.y + 6, tw, 28), Height(i).ToString("0") + " m", sRow, locked ? tc : new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleRight);
        // status
        var chip = new Rect(tx, b.y + 42, 0, 24);
        if (locked) Chip(chip, "LÅST", new Color(0.32f, 0.3f, 0.28f));
        else if (done) Chip(chip, "KLARAD", new Color(0.26f, 0.55f, 0.28f));
        else if (best[i] > 0f)
        {
            Chip(chip, "BÄST " + Mathf.RoundToInt(best[i] * 100f) + " %", new Color(0.55f, 0.4f, 0.16f));
            var bar = new Rect(tx + 112f, b.y + 51, Mathf.Max(30f, tw - 116f), 8);
            Fill(bar, new Color(0, 0, 0, 0.45f));
            Fill(new Rect(bar.x, bar.y, bar.width * best[i], bar.height), Gold);
        }
        else Chip(chip, isNext ? "NY BANA!" : "SPELA", isNext ? new Color(0.8f, 0.45f, 0.12f) : new Color(0.25f, 0.42f, 0.62f));
        if (i == LevelCount - 1 && !locked) Text(new Rect(tx, b.y + 42, tw, 24), "FINAL", sRow, Red, TextAnchor.MiddleRight, 1);
    }

    void Chip(Rect r, string text, Color col)
    {
        float w = sDesc.CalcSize(new GUIContent(text)).x * 1.25f + 20f;
        var c = new Rect(r.x, r.y, w, r.height);
        Fill(c, col);
        Fill(new Rect(c.x, c.y, c.width, 2), Color.Lerp(col, Color.white, 0.3f));
        int fs = sRow.fontSize;
        sRow.fontSize = 14;
        Text(c, text, sRow, Color.white, TextAnchor.MiddleCenter, 1);
        sRow.fontSize = fs;
    }

    void Padlock(Vector2 c)
    {
        Color m = new Color(0.85f, 0.82f, 0.75f);
        Fill(new Rect(c.x - 11, c.y - 2, 22, 18), m);                  // låskropp
        Fill(new Rect(c.x - 2, c.y + 3, 4, 7), new Color(0.2f, 0.18f, 0.16f)); // nyckelhål
        Fill(new Rect(c.x - 8, c.y - 14, 3, 13), m);                   // bygel
        Fill(new Rect(c.x + 5, c.y - 14, 3, 13), m);
        Fill(new Rect(c.x - 8, c.y - 16, 16, 3), m);
    }

    bool ShopRow(Rect panel, int i, string title, string desc, int cost, bool gemsCost)
    {
        var row = new Rect(panel.x + 18, panel.y + 58 + i * 52, panel.width - 36, 46);
        Fill(row, new Color(1f, 1f, 1f, 0.06f));
        Text(new Rect(row.x + 12, row.y + 2, row.width - 140, 24), title, sRow, Color.white);
        Text(new Rect(row.x + 12, row.y + 23, row.width - 140, 20), desc, sDesc, new Color(0.92f, 0.88f, 0.8f), null, 1);
        int have = gemsCost ? gems : money;
        bool ok = cost >= 0 && have >= cost;   // man kan bara köpa när man har lika mycket eller mer än det kostar
        string label = cost < 0 ? "MAX"
            : !ok ? "Saknar " + (cost - have)
            : gemsCost ? cost + " diam." : "Köp " + cost;
        if (Btn(new Rect(row.xMax - 118, row.y + 6, 110, 34), label, gemsCost ? Blue : Green, ok, sRow))
        {
            sfx.Play(sfx.buy);
            return true;
        }
        return false;
    }

    void DrawHud()
    {
        if (lvl == null) return;
        // runt porträtt av gubben med ballongerna som liv bredvid (som i äventyrsspelen)
        var hud = new Rect(16, 16, 330, 112);
        Fill(new Rect(hud.x + 4, hud.y + 5, hud.width, hud.height), new Color(0, 0, 0, 0.35f));
        Fill(hud, new Color(PanelCol.r, PanelCol.g, PanelCol.b, 1f));
        var face = new Rect(24, 22, 100, 100);
        if (player.Portrait != null) GUI.DrawTexture(face, player.Portrait, ScaleMode.ScaleAndCrop);
        GUI.DrawTexture(new Rect(face.x - 1, face.y - 1, face.width + 2, face.height + 2), texRing);
        // ballonger som liv – mindre ikoner när man har många
        float bw = maxBalloons <= 6 ? 26f : 16f, step = bw + (maxBalloons <= 6 ? 6f : 3f);
        for (int i = 0; i < maxBalloons; i++)
        {
            bool alive = i < player.Balloons;
            var old = GUI.color;
            GUI.color = alive ? Player.BalloonColors[i % Player.BalloonColors.Length] : new Color(0.35f, 0.33f, 0.32f, 0.8f);
            GUI.DrawTexture(new Rect(136 + i * step, 28, bw, bw * 1.15f), texCircle);
            GUI.color = old;
            Fill(new Rect(136 + i * step + bw * 0.5f - 1f, 28 + bw * 1.15f - 1f, 2, 7), Ink);
        }
        Diamond(new Rect(139, 76, 10, 10));
        Text(new Rect(156, 68, 190, 26), "Diamanter: " + gems, sHud, Gem);
        Text(new Rect(136, 92, 210, 26), "På banan: +" + runCoins + " mynt", sHud, new Color(1f, 0.9f, 0.6f));

        // stor myntmätare uppe i mitten – poppar till när man plockar ett mynt
        var cb = new Rect(VW / 2f - 95f, 14f, 190f, 58f);
        Fill(new Rect(cb.x + 4, cb.y + 5, cb.width, cb.height), new Color(0, 0, 0, 0.35f));
        Fill(new Rect(cb.x - 3, cb.y - 3, cb.width + 6, cb.height + 6), Ink);
        Fill(cb, new Color(PanelCol.r, PanelCol.g, PanelCol.b, 1f));
        float ps = 1f + 0.35f * coinPulse;
        float isz = 40f * ps;
        CoinIcon(new Rect(cb.x + 30 - isz * 0.5f, cb.center.y - isz * 0.5f, isz, isz));
        int oldSize = sH1.fontSize;
        sH1.fontSize = Mathf.RoundToInt(30 * (1f + 0.15f * coinPulse));
        Text(new Rect(cb.x + 58, cb.y, cb.width - 66, cb.height), money.ToString(), sH1, Color.Lerp(Gold, Color.white, coinPulse * 0.6f), TextAnchor.MiddleLeft);
        sH1.fontSize = oldSize;
        if (coinPulse > 0.01f)
            Text(new Rect(cb.xMax - 60, cb.y - 22 - (1f - coinPulse) * 18f, 70, 26), "+" + lastGain, sH2, new Color(1f, 0.95f, 0.6f, coinPulse), TextAnchor.MiddleCenter);

        float y = Mathf.Max(0f, player.transform.position.y - lvl.floorY);
        Text(new Rect(VW - 330, 16, 270, 44), Mathf.CeilToInt(y) + " m kvar", sH1, Color.white, TextAnchor.MiddleRight);
        var bar = new Rect(VW - 46, 70, 20, 500);
        Fill(new Rect(bar.x - 3, bar.y - 3, bar.width + 6, bar.height + 6), Ink);
        Fill(bar, new Color(0.3f, 0.25f, 0.2f, 0.9f));
        float f = Progress();
        Fill(new Rect(bar.x, bar.y, bar.width, bar.height * f), Gold);
        Fill(new Rect(bar.x - 6, bar.yMax - 4, bar.width + 12, 8), Red);
        var old2 = GUI.color;
        GUI.color = player.Balloons > 0 ? Player.BalloonColors[0] : Color.gray;
        GUI.DrawTexture(new Rect(bar.x - 8, bar.y + bar.height * f - 18, 36, 36), texCircle);
        GUI.color = old2;
        Text(new Rect(VW - 120, bar.yMax + 8, 110, 26), "MÅL", sH2, Color.white, TextAnchor.MiddleCenter);

        // aktivt hjälpmedel
        if (active != Help.None)
        {
            var hb = new Rect(VW / 2f - 160f, 600f, 320f, 34f);
            Panel(hb);
            float frac = Mathf.Clamp01(helpTime / HelpDuration[(int)active]);
            Fill(new Rect(hb.x + 4, hb.y + 4, (hb.width - 8) * frac, hb.height - 8), active == Help.Rocket ? Red : Blue);
            Text(hb, HelpNames[(int)active] + "  " + helpTime.ToString("0.0") + " s", sHud, Color.white, TextAnchor.MiddleCenter);
        }

        string line = "[1] Upp " + ups + "    [2] Sido " + sides + "    [3] Raket " + rockets
            + "    [4] Sköld " + shields + "    [5] Broms " + brakes + "        W/S: upp/ner";
        if (!touchUI) Text(new Rect(20, 678, VW - 40, 28), line, sHud, Color.white, null, 2);
    }

    // Serietidningens fartstreck i bildens kanter – fler ju fortare det går
    void DrawSpeedLines()
    {
        float speed = player.vel.magnitude;
        float a = active == Help.Rocket ? 0.3f : 0f; // bara fartstreck när raketen går
        if (a <= 0.02f) return;
        Vector2 c = new Vector2(VW * 0.5f, 330f);
        var m = GUI.matrix;
        for (int i = 0; i < 40; i++)
        {
            float ang = i * 9f + Mathf.Sin(i * 12.9898f) * 4f;
            float phase = Mathf.Repeat(Time.time * (1.4f + (i % 5) * 0.3f) + i * 0.37f, 1f);
            float r0 = 340f + phase * 420f;
            float len = 50f + (i % 4) * 40f;
            GUI.matrix = m;
            GUIUtility.RotateAroundPivot(ang, c);
            Fill(new Rect(c.x + r0, c.y - 2f, len, 4f), new Color(Ink.r, Ink.g, Ink.b, a * (1f - phase)));
        }
        GUI.matrix = m;
    }

    void DrawReady()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
        Text(new Rect(0, 520, VW, 60), "Tryck MELLANSLAG för att hoppa!", sBanner, Color.Lerp(Color.white, Gold, pulse), null, 3);
        Text(new Rect(0, 580, VW, 30), LevelInfo(level), sH2, Color.white, TextAnchor.MiddleCenter);
    }

    void DrawBanner()
    {
        float age = bannerDur - bannerTime;
        float pop = age < 0.15f ? Mathf.Lerp(0.6f, 1.12f, age / 0.15f) : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((age - 0.15f) / 0.2f));
        float alpha = Mathf.Clamp01(bannerTime * 2.5f);
        int oldSize = sBanner.fontSize;
        sBanner.fontSize = Mathf.RoundToInt(50 * pop);
        Text(new Rect(0, 130, VW, 90), banner, sBanner, new Color(1f, 0.96f, 0.82f, alpha), null, 4);
        sBanner.fontSize = oldSize;
    }

    void DrawDead()
    {
        if (state != State.Dead) return;
        var r = new Rect(VW / 2f - 300f, 210f, 600f, 300f);
        Panel(r);
        Text(new Rect(r.x, r.y + 18, r.width, 60), deathTitle, sDeath, Red, TextAnchor.MiddleCenter, 3);
        Text(new Rect(r.x, r.y + 84, r.width, 30), deathText, sH2, Color.white, TextAnchor.MiddleCenter);
        int pct = Mathf.RoundToInt(Progress() * 100f);
        Text(new Rect(r.x, r.y + 116, r.width, 28), "Du kom " + pct + " % av vägen. Du behåller dina " + runCoins + " mynt!", sText, Gold, TextAnchor.MiddleCenter);
        if (Btn(new Rect(r.x + 40, r.y + 190, 245, 64), "Försök igen (R)", Red)) StartLevel();
        if (Btn(new Rect(r.xMax - 285, r.y + 190, 245, 64), "Affären (Tab)", BtnCol)) ShowStore();
    }

    void DrawWon()
    {
        var r = new Rect(VW / 2f - 300f, 420f, 600f, 285f);
        Panel(r);
        Text(new Rect(r.x, r.y + 16, r.width, 70), "BANA KLARAD!", sTitle, new Color(0.6f, 1f, 0.55f), TextAnchor.MiddleCenter, 3);
        Text(new Rect(r.x, r.y + 96, r.width, 32), "Du landade säkert! (Bana " + (level + 1) + ": " + LevelName(level) + ")", sH2, Color.white, TextAnchor.MiddleCenter);
        Text(new Rect(r.x, r.y + 132, r.width, 32), "Mynt: +" + runCoins + "    Landningsbonus: +" + lastBonus, sH2, Gold, TextAnchor.MiddleCenter);
        bool hasNext = level < LevelCount - 1;
        if (hasNext && Btn(new Rect(r.x + 30, r.y + 190, 260, 66), "Nästa bana (Enter)", Green)) { SelectLevel(level + 1); StartLevel(); }
        if (!hasNext) Text(new Rect(r.x, r.y + 160, r.width, 30), "Du har klarat alla 100 banor! Gubben är inte fattig längre!", sText, Color.white, TextAnchor.MiddleCenter);
        if (Btn(new Rect(r.xMax - 290, r.y + 190, 260, 66), "Affären (Tab)", BtnCol)) ShowStore();
    }

    void DrawPause()
    {
        Fill(new Rect(0, 0, VW, 720), new Color(0, 0, 0, 0.45f));
        var r = new Rect(VW / 2f - 200f, 160f, 400f, touchUI ? 400f : 330f);
        Panel(r);
        Text(new Rect(r.x, r.y + 20, r.width, 60), "PAUS", sTitle, Color.white, TextAnchor.MiddleCenter, 3);
        if (Btn(new Rect(r.x + 50, r.y + 100, 300, 56), "Fortsätt", Green)) { paused = false; Time.timeScale = 1f; }
        if (Btn(new Rect(r.x + 50, r.y + 170, 300, 56), "Till menyn", BtnCol)) ShowShop();
        if (Btn(new Rect(r.x + 50, r.y + 240, 300, 56), "Pekkontroller: " + (touchUI ? "PÅ" : "AV"), touchUI ? Blue : BtnCol))
        {
            touchUI = !touchUI;
            Save();
        }
        if (touchUI && Btn(new Rect(r.x + 50, r.y + 310, 300, 56), "Styrning: " + (dragSteer ? "Dra fingret" : "Knappar"), Blue))
        {
            dragSteer = !dragSteer;
            Save();
        }
    }

    // ---------------------------------------------------------------- Automatiska skärmdumpar (för test)

    static string Arg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
        return null;
    }

    IEnumerator Shot(string dir, string name)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
        yield return null;
        yield return null;
    }

    IEnumerator AutoShot(string dir)
    {
        god = true;
        Application.runInBackground = true;
        if (Arg("-ballongTouch") != null) touchUI = true;
        Directory.CreateDirectory(dir);
        maxBalloons = int.TryParse(Arg("-ballongAntal") ?? "3", out int antal) ? Mathf.Clamp(antal, 1, Player.MaxBalloons) : 3;
        money = 120;
        gems = 2;
        ups = 2; sides = 1; rockets = 1; shields = 1; brakes = 1;
        unlocked = 5;
        int.TryParse(Arg("-ballongLevel") ?? "0", out int startLevel);
        level = startLevel;
        page = level / 10;
        EnsureLevel(level);
        ShowShop();
        yield return new WaitForSeconds(2f);
        yield return Shot(dir, "1_meny");
        menuPage = MenuPage.Levels;
        yield return new WaitForSeconds(0.3f);
        yield return Shot(dir, "1b_banor");
        menuPage = MenuPage.Store;
        yield return new WaitForSeconds(0.3f);
        yield return Shot(dir, "1_affar");
        if (Arg("-ballongBank") != null)
        {
            menuPage = MenuPage.Bank; yield return new WaitForSeconds(0.3f); yield return Shot(dir, "1c_bank");
            bankNotice = true; yield return new WaitForSeconds(0.3f); yield return Shot(dir, "1d_info");
            bankNotice = false;
        }
        StartLevel();
        yield return new WaitForSeconds(2.5f);
        yield return Shot(dir, "2_redo");
        Jump();
        yield return new WaitForSeconds(3f);
        if (Arg("-ballongTouch") != null) { dragId = 99; dragOrigin = new Vector2(VW * 0.7f, 420f); dragNow = dragOrigin + new Vector2(-45f, 30f); }
        yield return Shot(dir, "3_fall");
        UseHelp(Help.Rocket);
        yield return new WaitForSeconds(1f);
        yield return Shot(dir, "4_raket");
        EndHelp();
        float mz = lvl.length * 0.5f;
        player.transform.position = new Vector3(0f, lvl.PathY(mz), mz);
        yield return new WaitForSeconds(1.5f);
        yield return Shot(dir, "5_mitten");
        if (lvl.birdZ.Count > 0)
        {
            birds.Add(Bird.Spawn(lvl.root.transform, player, 0.3f, false, Arg("-ballongHok") != null));
            yield return new WaitForSeconds(3.3f);
            yield return Shot(dir, "6_fagel");
        }
        // träfftest: en provvägg rakt framför gubben – den exakta träffzonen ska spräcka en ballong
        {
            var pp = player.transform.position;
            float wz = pp.z + 10f;
            var wall = new GameObject("Provvagg");
            wall.transform.SetParent(lvl.root.transform, false);
            wall.transform.position = new Vector3(pp.x, lvl.PathY(wz) + 2.3f, wz);
            wall.AddComponent<Hazard>();
            var wmb = new MeshBuilder();
            wmb.Box(Vector3.zero, new Vector3(3f, 7f, 0.4f), Color.red);
            var wmesh = Toon.Make("Mesh", wmb.ToMesh("provvagg"), wall.transform);
            wmesh.AddComponent<MeshCollider>().sharedMesh = wmesh.GetComponent<MeshFilter>().sharedMesh;
            int before = player.Balloons;
            god = false;
            player.invuln = 0f;
            float tt = 0f;
            while (tt < 2.5f && player.Balloons == before) { tt += Time.deltaTime; var q = player.transform.position; q.x = pp.x; player.transform.position = q; yield return null; }
            Debug.Log("TRAFFTEST: ballonger före " + before + ", efter " + player.Balloons);
            god = true;
            Destroy(wall);
        }
        if (state == State.Dying)
        {
            // falltest: följ fallet och nedslaget
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "8_faller");
            float ft = 0f;
            while (!grounded && ft < 20f) { ft += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "9_nedslag");
            yield return new WaitForSeconds(1.6f);
            yield return Shot(dir, "9b_ligger");
            Debug.Log("FALLTEST: landade efter " + ft.ToString("0.0") + " s, ligger=" + player.lying);
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
            yield break;
        }
        yield return new WaitForSeconds(0.25f);
        yield return Shot(dir, "7_pang");
        float ez = lvl.length - 20f;
        float ex = lvl.cave ? lvl.docks[0].position.x : 0f;
        player.transform.position = new Vector3(ex, lvl.PathY(ez), ez);
        player.vel.x = 0f;
        float t = 0f;
        while (state != State.Won && state != State.Dead && t < 8f)
        {
            t += Time.deltaTime;
            if (lvl.cave)
            {
                var p = player.transform.position;
                p.x = lvl.docks[0].position.x;
                player.transform.position = p;
            }
            yield return null;
        }
        yield return new WaitForSeconds(0.1f);
        yield return Shot(dir, "8a_landar");
        yield return new WaitForSeconds(0.7f);
        yield return Shot(dir, "8b_lattnad");
        yield return new WaitForSeconds(1.2f);
        yield return Shot(dir, "8c_jubel");
        yield return new WaitForSeconds(2.6f);
        yield return Shot(dir, "8_slut");
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }
}
