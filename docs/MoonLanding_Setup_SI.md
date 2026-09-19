# 🌕 Moon Landing Challenge — Setup Guide (සිංහල)

**කොටස:** Moon Landing Challenge · **සාමාජිකයා:** IT23152632 Himal G.M.H
**Unity:** 6000.3.22f1 (URP 17.3) · **Input:** Input System Package

---

## v3 වෙනස්කම් (Mobile + Guidance + Score update) ⭐ අලුත්ම

### 1. "මේසයක් වගේ" පේන ප්‍රශ්නය fix කළා
- **ප්‍රශ්නයට හේතුව:** Horizon ring එක 30 km වලින් ඉවර වුණා. Lander එක ඉහළ ඉඳන් (150 m+) බලද්දී සඳේ සැබෑ ක්ෂිතිජය (horizon) 30 km ට වඩා දුරින් තියෙනවා. ඒ නිසා ලෝකයේ කෙළවර පේන්න ගත්තා. එතකොට හැම දෙයක්ම මේසයක් උඩ තියෙනවා වගේ පෙනුණා.
- **විසඳුම (සැබෑ ගණිතය):** ක්ෂිතිජයට දුර `d = √(2·R·h)` (R = 1737 km). උපරිම උස 2.5 km නම් d ≈ 93 km. ඒ නිසා horizon එක දැන් **95 km** දක්වා දිගු කළා, සඳේ **වක්‍රතාවයත්** (curvature) එක්කම. දැන් කෙළවර හැමවෙලේම ක්ෂිතිජයට පිටුපස සැඟවෙනවා.
- Terrain එකේ **හතරැස් කොන් කපලා දැම්මා** (Terrain Holes). ක්‍රීඩා ප්‍රදේශය දැන් රවුමක්, horizon එකට බාධාවකින් තොරව එකතු වෙනවා.
- Horizon එකට collider එකක් දැම්මා. Terrain එකෙන් එළියට පියාසර කළත් ඒ මතුපිට වදිනවා.

### 2. Difficulty levels අලුතින්
| | EASY | NORMAL | HARD |
|---|---|---|---|
| Target එකේ හරියටම බහින්න ඕනද? | ❌ ඕනම ආරක්ෂිත තැනක | ❌ (ළඟ නම් ලකුණු වැඩියි) | ✅ **15 m zone එක ඇතුලේම** |
| Guidance | ✅ සම්පූර්ණ | ⚠️ හදිසි ඒවා විතරයි | ❌ නැහැ |
| Engine auto cut | ✅ | ❌ | ❌ |
| SAS | Auto-level | Stabiliser | Manual (Off) |
| Safe vertical speed | 3.0 m/s | 2.2 m/s | 2.0 m/s |
| Score multiplier | ×1.0 | ×1.5 | ×2.5 |

HARD mode එකේ zone එකෙන් එළියේ ආරක්ෂිතව බැස්සොත් **"MISSED THE TARGET"** වෙනවා. Lander එක පුපුරන්නේ නැහැ, ලකුණු ටිකක් ලැබෙනවා.

### 3. Guidance (EASY mode එකේ සම්පූර්ණයෙන්)
Flight computer එකක් lander එක දිහා බලාගෙන ඉඳලා, තිරයේ මැද ලොකු banner එකකින් කළ යුතු දේ කියනවා:
`FULL THRUST NOW!` · `HOLD THRUST` · `RELEASE THE THRUST NOW` · `TILT BACK TO STOP DRIFTING` · `LEVEL THE LANDER` · `CUT THE ENGINE NOW` · `GOOD — HOLD THIS DESCENT`

ඔබන්න ඕන **button එකත් දිලිසෙනවා** (THRUST / lever / CUT). Joystick එකේ **ඊතලයකින්** ඇල කරන්න ඕන දිශාවත් පෙන්නනවා.
- **ගණිතය (viva):** ආරක්ෂිත බැසීමේ වේගය = `0.5 m/s + උසින් 9%` (උපරිම 10 m/s). ලබන වේගය ඊට වඩා වැඩි නම් "thrust", අඩු නම් "release".
- Python simulation එකකින් පරීක්ෂා කළා. Guidance එකට කීකරු වෙන ආධුනිකයෙක් (0.6 s ප්‍රතික්‍රියා කාලයක් එක්ක වුණත්) difficulty තුනේම ≤1.7 m/s වේගයෙන් ආරක්ෂිතව බහිනවා.

### 4. Score — දිනුවත් පැරදුණත් ලකුණු ✅
- **පියාසර කරද්දී:** target එකට ළං වීම, smooth descent, 100 m / 50 m / 20 m / contact light milestones. ලකුණු ලැබෙද්දී තිරයේ `+50 SMOOTH DESCENT` වගේ popups එනවා.
- **අවසානයේ breakdown එක:** Landing bonus, Soft touchdown, Precision, Fuel saved, Level stance, Difficulty bonus.
- **Crash වුණත්:** flight points + closest approach + flight time ලැබෙනවා.
- Best score එක ඕනම ප්‍රතිඵලයකින් save වෙනවා.

### 5. අලුත් Fancy HUD (mobile-first)
- Glass panels, corner brackets, glow. **Orbitron / Rajdhani fonts** (SIL Open Font License, games වල නොමිලේ පාවිච්චි කරන්න පුළුවන්; license files `Fonts/` folder එකේ තියෙනවා).
- Animated score counter, radar sweep, **sink-rate gauge** (කොළ පාට = ආරක්ෂිත, නිල් = guidance target), MASTER ALARM එකේදී රතු screen edges.
- Result screen: තරු එකින් එක pop වෙනවා, score එක count-up වෙනවා, breakdown lines animate වෙනවා.
- Phone notch වලට **Safe Area** support.

### 6. Mobile controls
| Control | කරන දේ |
|---|---|
| **TILT joystick** (වම් පහළ) | Pitch + roll (analog) |
| **YAW ◄ ►** | කැරකීම |
| **THRUST** (ලොකු රවුම, hold) | 100% thrust |
| **THROTTLE lever** (දකුණු කෙළවර) | Throttle එක drag කරලා set කරන්න. කොළ පාට **HOVER** ඉර = lander එක එකම උසක රඳවා ගන්න ඕන throttle එක |
| **CUT** | Engine off |
| **SAS / CAM / II** (radar එකට යටින්) | SAS mode / camera / pause |
| **එක ඇඟිල්ලකින් drag** (හිස් තැනක) | Camera එක කරකවන්න |
| **ඇඟිලි දෙකෙන් pinch** | Zoom |

Scene එක ඇතුලේ විතරක්: **Landscape lock**, **60 FPS**, mobile වලට shadows/terrain සැහැල්ලු කරනවා. Scene එකෙන් එළියට ගියාම team එකේ settings ආපහු restore වෙනවා.

> 💡 Unity Editor එකේ phone එකක් වගේ test කරන්න: Game tab එකේ **Game ▸ Simulator** තෝරලා phone model එකක් (e.g. Galaxy / iPhone) select කරන්න. Mouse click එක touch එකක් විදිහට වැඩ කරනවා.

**Update කරන විදිහ:** Files replace කරලා → `Astronaut Project ▸ Moon Landing ▸ Build Level` (**අනිවාර්යයි**, අලුත් difficulty/guidance/score components scene එකට එකතු වෙන්නේ එතකොට) → Play.

## v2 වෙනස්කම් (Realistic ground update)

- ✅ **"කොටු කොටු" (tiling grid) ප්‍රශ්නය fix කළා.** පරණ regolith texture එකේ ලොකු අඳුරු/එළිය පැල්ලම් තිබුණ නිසා, ඒක සෑම 7 m එකකට සැරයක් repeat වෙනවා පේන්න තිබුණා. දැන් තියෙන්නේ anti-tiling textures දෙකක්. ඒ දෙක noise එකකින් mix වෙනවා, tile sizes 5.3 m / 13.7 m.
- ✅ **2 km පුරා baked macro albedo map එකක්** එකතු කළා. ඒකේ mare පැල්ලම්, අලුත් craters වටේ දීප්තිමත් ejecta, crater rays, අඳුරු crater floors තියෙනවා.
- ✅ Heightmap resolution එක 1025 → **2049** (≈1 m). Craters ~14,000ක් තියෙනවා, rims අක්‍රමවත්, පරණ craters වල පතුල් පැතලියි.
- ✅ Horizon ring එකට shaded craters තියෙන texture එකක්.
- ✅ Rocks/pebbles දෙගුණයක් විතර වැඩි කළා. Color grade එකත් neutral කළා (Moon එක අළු පාටයි, නිල් නෙවෙයි).
- v1 files (පරණ regolith textures/layers) Build Level එකෙන් auto delete වෙනවා.

## 1. මේ package එකේ තියෙන දේවල්

```
Assets/_Project/Features/MoonLanding/
├── Scripts/
│   ├── Core/         MoonLandingEvents, LunarEnvironment (1.62 m/s² gravity), MoonLandingLibrary
│   ├── Lander/       LanderInput, LanderController (physics), LandingEvaluator, LanderDestruction
│   ├── Effects/      MoonVFXFactory, LanderEffects (plume/dust/RCS), SparkEmitter, LightFlashFade
│   ├── Audio/        LanderAudio
│   ├── Camera/       LanderCameraRig (Chase / Cockpit / Tower)
│   ├── UI/           LanderHUD, MoonUI, HoldButton
│   ├── Game/         MoonLandingGameManager (briefing, difficulty, score)
│   └── Environment/  LandingZone, SkyBodyFollower (Earth), BeaconBlink
├── Editor/           ⭐ One-click Level Builder + terrain / rock / material generators
├── Audio/SFX/        SFX 27ක් (අපිම synthesize කළ original sounds — copyright ප්‍රශ්න නැහැ)
└── Textures/Generated/  Regolith, rock, particle, starfield sky, Earth placeholder
.gitattributes        Git LFS rules update කරලා (.glb, .ogg, .tga ... එකතු කළා)
docs/                 මේ guide එක + GitHub team guide
```

සියලුම files **ඔයාගේ `MoonLanding` folder එක ඇතුලේ විතරයි**. අනිත් සාමාජිකයන්ගේ folders, scenes වලට අත ගහන්නේ නැහැ.

---

## 2. Project එකට දාන විදිහ (පියවරෙන් පියවර)

1. **Unity close කරන්න.**
2. ZIP එක extract කරලා, ඇතුලේ තියෙන `Assets`, `docs`, `.gitattributes` project root එකට (`Astronault-Project-MPM/`) copy කරන්න. *"Merge / Replace"* ඇහුවොත් **Yes**.
3. Unity Hub එකෙන් project එක open කරන්න. Scripts compile වෙනකම් ටිකක් ඉන්න.
4. Console එකේ **red errors නැති බව** බලන්න.
5. Top menu එකෙන්: **`Astronaut Project ▸ Moon Landing ▸ Build Level`** → **Build**.
   - Terrain (craters 1100+), rocks 2000+, sky, Earth, lighting, post-processing, lander rig, VFX, audio, HUD, game logic ඔක්කොම හැදිලා **scene එක auto save** වෙනවා.
   - තත්පර 10–40ක් යන්න පුළුවන්.
6. **▶ Play** ඔබන්න. 🚀

> ⚠️ **Build Level ආයෙත් run කළත් කමක් නැහැ** — `MoonLanding_Level` object එක විතරක් අලුතෙන් හැදෙනවා. ඔයාගේ Lander model එක ආරක්ෂිතයි. පරණ `Moon` sphere එකයි `LandingSurface` එකයි **delete කරන්නේ නැහැ**, disable කරනවා විතරයි.

---

## 3. Controls

| ක්‍රියාව | Keyboard | Gamepad | Screen button |
|---|---|---|---|
| Pitch (ඉදිරියට/පස්සට ඇල කරන්න) | W / S | Left stick ↕ | ▲ PITCH / PITCH ▼ |
| Roll (වමට/දකුණට) | A / D | Left stick ↔ | ◄ ROLL / ROLL ► |
| Yaw (කැරකෙන්න) | Q / E | LB / RB | ◄ YAW / YAW ► |
| Throttle වැඩි / අඩු | Shift / Ctrl | RT (analog) / LT | THR + / THR − |
| **Full thrust (hold)** | **Space** | RT full | **THRUST (hold)** |
| Throttle 100% | Z | — | — |
| Engine cut | X | X | CUT |
| SAS mode (Off → Rate damp → Auto level) | F | Y | SAS |
| Camera (Chase → Cockpit → Tower) | C | B | CAM |
| Camera orbit / zoom | Right mouse drag / Scroll | Right stick / D-pad | — |
| Buttons hide/show | H | — | — |
| Pause | Esc / P | Start | II |
| Restart | R | Select | RETRY |

**හරියට land වෙන්න:** vertical speed **< 2.0 m/s**, horizontal **< 1.2 m/s**, tilt **< 12°**, පාද බිම වැදුණාම **X (Engine cut)** ඔබන්න.

---

## 4. Viva එකට — Physics එක කොහොමද වැඩ කරන්නේ?

- **Gravity:** `Physics.gravity = 1.62 m/s²` (Moon). Scene එකෙන් ඉවත් වෙද්දී project default එකට restore වෙනවා → අනිත් අයගේ scenes වලට බලපාන්නේ නැහැ.
- **Thrust:** `F = throttle × 30 kN`, lander එකේ up axis දිගේ `Rigidbody.AddForce`.
- **Fuel burn (Tsiolkovsky):** `ṁ = F / (Isp · g₀)`, Isp = 311 s (Apollo DPS). Fuel අඩු වෙද්දී `Rigidbody.mass` අඩු වෙනවා → lander එක සැහැල්ලු වෙනවා.
- **No air = no drag:** `linearDamping = 0`. එන්ජිමෙන් විතරයි වේගය නවත්වන්න පුළුවන්.
- **RCS attitude control:** angular acceleration pitch/yaw/roll. **SAS** = PD controller (rate damping / auto-level).
- **Landing check:** foot pad colliders 4 + body colliders 2. Impact speed එක ගන්නේ **collision එකට කලින් physics step එකේ velocity එකෙන්** (නිවැරදි අගය).
- **Dust:** Apollo වීඩියෝ වල වගේ, ~35 m ට පහළදී exhaust එක බිමේ වදින තැනින් **flat ballistic sheets** විදිහට (වාතය නැති නිසා වලාකුළු වගේ නෙවෙයි).
- **Sound:** අභ්‍යවකාශයේ ශබ්දය යන්නේ නැහැ — අහන්නේ **cabin එක ඇතුලේ** crew අහන දේ (structure vibration, valves, alarms, NASA Quindar tones 2525/2475 Hz).
- **Scale:** Lander model එක scale 0.05 → උස ≈ 6.7 m (සැබෑ LM ≈ 7 m). ඔයාගේ scene එකේ තිබුණ 0.01 scale එකෙන් LM එක 1.3 m විතරයි වුණේ, සහ X=180° rotation එකෙන් model එක **upside-down** වෙලා තිබුණා — builder එක ඒක auto fix කරනවා.

---

## 5. Visual quality ("UE5 වගේ") සඳහා කළ දේවල්

- Low sun (17°) → crater වල දිගු, තද shadows (Apollo 11 landing වෙලාවේ වගේ), soft shadows, shadow distance 900 m (runtime)
- Regolith **bounce light** (cheap GI), trilight ambient, realtime reflection probe
- ACES tonemapping, bloom, contrast/saturation grade, vignette, film grain, chromatic aberration, camera motion blur, SMAA
- 4-layer terrain (fine regolith, macro variation, rocky slopes, bright fresh ejecta) + normal maps
- 30 km **curved horizon** (Moon radius 1737 km) → terrain එකේ කෙළවරක් පේන්නේ නැහැ
- Earth — real sun light එකෙන් light වෙන නිසා **phase එක නිවැරදියි**
- HDR VFX: engine plume, dust sheets/haze/streaks, RCS puffs, electric sparks (bounce), explosion (flash, fireball, embers, regolith ring, smoke, light), flying physical debris

**තවත් ලස්සන කරන්න (optional):** `Assets/Settings/PC_RPAsset` → *Main Light Shadow Resolution* 4096. (මේක shared file එකක් — team එකට කියලා කරන්න.)

---

## 6. Customize කරන විදිහ

| දේ | කොහෙද |
|---|---|
| Difficulty (උස, වේගය, fuel) | `Systems` object → **MoonLandingGameManager → Difficulties** |
| Main menu scene නම | MoonLandingGameManager → **Hub Scene Name** (දැන් `MainMenu`) — ඔයාලගේ HUD scene නමට වෙනස් කරන්න, ඒ scene එක Build Profiles වල තියෙන්න ඕන |
| Thrust, fuel, Isp, RCS | `LanderRig` → **LanderController** |
| Landing limits | `LanderRig` → **LandingEvaluator** |
| Sounds ඕනම එකක් මාරු කරන්න | `Generated/MoonLandingLibrary.asset` |
| Sound volume | `LanderRig` → **LanderAudio** |
| Camera | Main Camera → **LanderCameraRig** |
| HUD buttons default hide | `Systems` → **LanderHUD → Show Touch Controls** |

### අනිත් scenes වලින් result එක ගන්න (team integration)
```csharp
using Astronaut.MoonLanding;

void OnEnable()  { MoonLandingEvents.ChallengeFinished += OnMoonDone; }
void OnDisable() { MoonLandingEvents.ChallengeFinished -= OnMoonDone; }
void OnMoonDone(MoonLandingResult r) { Debug.Log($"Moon landing: {r.outcome}, score {r.score}, stars {r.stars}"); }

// නැත්නම් ඕනම තැනකදී:
int best = MoonLandingEvents.BestScore;
bool done = MoonLandingEvents.HasCompleted;
```

---

## 7. Troubleshooting

| ප්‍රශ්නය | විසඳුම |
|---|---|
| `Lander model not found` | Git LFS files pull කරලා නැහැ → `git lfs pull`. (**GitHub "Download ZIP" එකෙන් LFS files එන්නේ නැහැ** — ඔයා එවපු ZIP එකේ `Moon.fbx` 131 bytes pointer එකක් විතරයි.) |
| Pink materials | URP active නැහැ → *Project Settings ▸ Graphics* එකේ `PC_RPAsset` තියෙනවද බලන්න |
| Lander එක upside-down | Menu: `Astronaut Project ▸ Moon Landing ▸ Options ▸ Force Flip Lander Model` → Build Level ආයෙත් |
| Buttons click වෙන්නේ නැහැ | Scene එකේ පරණ `StandaloneInputModule` EventSystem එකක් තියෙනවා නම් delete කරන්න (HUD එක අලුත් එකක් හදනවා) |
| Restart / Retry වැඩ නැහැ | Scene එක *File ▸ Build Profiles ▸ Scene List* එකේ තියෙන්න ඕන (builder එක auto add කරනවා) |
| FPS අඩුයි | LanderHUD/terrain: *Terrain ▸ Pixel Error* 3 → 6, rocks ගණන `MoonRockBuilder.cs` එකේ අඩු කරන්න |

---

## 8. ඔයාගෙන් ඕන කරන resources (optional, තවත් realistic කරන්න)

1. **NASA Blue Marble Earth texture** (public domain, equirectangular JPG) → `Assets/_Project/Features/MoonLanding/Textures/T_Earth_BlueMarble.jpg` නමින් දාලා Build Level ආයෙත් run කරන්න. (දැන් තියෙන්නේ procedural placeholder එකක්.)
2. **Apollo 11 radio call-outs** ("Contact light", "The Eagle has landed" — NASA public domain audio) → `MoonLandingLibrary` asset එකේ *Voice Contact Light / Voice Landed* slots වලට drag කරන්න.
3. Higher quality lander model එකක් තියෙනවා නම් — ඒක `Lander` නමින් scene එකට දාලා Build Level.
