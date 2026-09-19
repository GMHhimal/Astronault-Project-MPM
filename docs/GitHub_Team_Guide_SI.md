# 👥 GitHub Team Guide — Astronault-Project-MPM (Beginners සඳහා, සිංහල)

මේ guide එක team එකේ **4 දෙනාටම**. මුල සිට අග දක්වා පිළිවෙලට කරන්න.

| ID | නම | කොටස | Branch නම |
|---|---|---|---|
| IT23306172 | Seniduni H.M.D.P | Rocket Assembly and Launch | `feature/rocket-assembly` |
| IT23404564 | Lakshan H.M.H | 360° Spacecraft Explorer | `feature/spacecraft-explorer` |
| IT23152632 | Himal G.M.H | Moon Landing Challenge | `feature/moon-landing` |
| IT23187146 | Jalitha U.K.C | Rocket Evolution Timeline | `feature/rocket-timeline` |

---

## 0. මූලික වචන (ඉස්සෙල්ලා මේක තේරුම් ගන්න)

| වචනය | තේරුම |
|---|---|
| **Repository (repo)** | Project එකේ සියලු files + history එක තියෙන තැන |
| **Clone** | GitHub repo එක ඔයාගේ computer එකට download කරන එක (history එකත් එක්ක) |
| **Commit** | "Save point" එකක් — වෙනස් කළ දේවල් පණිවිඩයක් එක්ක සුරකින එක (local) |
| **Push** | ඔයාගේ commits GitHub එකට යවන එක |
| **Pull** | අනිත් අය push කරපු අලුත් වෙනස්කම් ඔයාගේ computer එකට ගන්න එක |
| **Branch** | Project එකේ වෙනම "පාරක්" — ඔයාගේ වැඩ අනිත් අයට බාධා නොවී කරන්න |
| **Pull Request (PR)** | "මගේ branch එක `main` එකට එකතු කරන්න" කියලා ඉල්ලන එක, review කරලා merge කරනවා |
| **Merge conflict** | දෙන්නෙක් එකම file එකේ එකම තැන වෙනස් කළාම Git එකට තනියම තීරණය කරන්න බැරි වෙන එක |
| **Git LFS** | Images, audio, 3D models වගේ ලොකු files ගබඩා කරන විශේෂ ක්‍රමය |

---

## 1. එක පාරක් විතරක් කරන Setup (සෑම සාමාජිකයෙක්ම)

### 1.1 Install කරන්න
1. **Git** — https://git-scm.com/downloads (Windows: default options තියලා Next Next).
2. **Git LFS** — Git for Windows එකේ දැනටමත් ඇතුලත් වෙනවා. Check කරන්න:
   ```bash
   git lfs version
   ```
   Error ආවොත්: https://git-lfs.com එකෙන් install කරන්න.
3. **GitHub Desktop** (ආරම්භකයින්ට ලේසියි, optional) — https://desktop.github.com
4. **Unity Hub** + **Unity 6000.3.22f1** (✅ හැමෝම **හරියටම එකම version** එක පාවිච්චි කරන්න. වෙනස් version එකකින් open කළොත් ගොඩක් files වෙනස් වෙනවා.)

### 1.2 Git එකට ඔයා කවුද කියලා කියන්න
Terminal / Git Bash open කරලා (ඔයාගේ නම සහ GitHub email එක දාන්න):
```bash
git config --global user.name "Himal G.M.H"
git config --global user.email "your-github-email@example.com"
git lfs install
```

### 1.3 Repo owner — collaborators invite කරන්න
Repo එක හැදුව කෙනා (owner) විතරයි මේක කරන්නේ:
1. GitHub → repo එක → **Settings** → **Collaborators** → **Add people**
2. අනිත් 3 දෙනාගේ GitHub usernames දාලා invite කරන්න.
3. අනිත් අය **email එකෙන් / GitHub notifications එකෙන් invite එක Accept** කරන්න.

### 1.4 `main` branch එක ආරක්ෂා කරන්න (owner, recommended)
**Settings → Branches → Add branch ruleset / protection rule** → `main`:
- ✅ *Require a pull request before merging*
- ✅ *Require approvals*: 1

දැන් කාටවත් කෙලින්ම `main` එකට push කරලා project එක කඩන්න බැහැ.

### 1.5 Repo එක Clone කරන්න
```bash
cd D:/UnityProjects          # ඔයා කැමති folder එකක්
git clone https://github.com/<owner-username>/Astronault-Project-MPM.git
cd Astronault-Project-MPM
git lfs pull                 # ලොකු files (models, textures, audio) බාගන්න
```

> ❌ **GitHub "Download ZIP" පාවිච්චි කරන්න එපා.** ZIP එකේ LFS files එන්නේ නැහැ — models/textures වෙනුවට 131 bytes "pointer" text files විතරයි එන්නේ. (අපේ project ZIP එකේ `Moon.fbx` එක ඒ විදිහයි.)

### 1.6 Unity settings (සෑම කෙනාම එක පාරක් check කරන්න)
**Edit → Project Settings → Editor**:
- **Version Control → Mode:** `Visible Meta Files`
- **Asset Serialization → Mode:** `Force Text` (අපේ project එකේ දැනටමත් ✅)

> `.meta` files **කවදාවත් delete කරන්න එපා**, commit කරන්න අමතක කරන්නත් එපා. ඒවා නැති වුණොත් scripts/materials connections කැඩෙනවා.

---

## 2. හැමදාම වැඩ කරන විදිහ (Daily Workflow) ⭐

```
 main  ──●────────●──────────●────────●──►   (හැමවෙලේම වැඩ කරන project එක)
          \      ↑ PR merge   \      ↑
 feature/  ●──●──●              ●──●──●        (ඔයාගේ වැඩ)
```

### පියවර A — ඔයාගේ branch එක හදන්න (පළමු වතාවට විතරයි)
```bash
git checkout main
git pull
git checkout -b feature/moon-landing
git push -u origin feature/moon-landing
```

### පියවර B — වැඩ පටන් ගන්න කලින් හැමදාම
```bash
git checkout feature/moon-landing
git pull                          # ඔයාගේ branch එකේ අලුත්ම දේ
git merge origin/main             # main එකේ අනිත් අයගේ merge වුණ වැඩ ගන්න
```
(`git fetch` කලින් run කරන්න: `git fetch` → `git merge origin/main`)

### පියවර C — Unity එකේ වැඩ කරන්න
- **ඔයාගේ folder එකේ සහ ඔයාගේ scene එකේ විතරක්** වැඩ කරන්න (Section 3 බලන්න).
- Unity එකේ **Ctrl+S** (scene save) සහ **File → Save Project** කරන්න.

### පියවර D — Commit + Push (දවසකට කීප වතාවක්)
```bash
git status                        # මොනවද වෙනස් වුණේ බලන්න
git add Assets/_Project/Features/MoonLanding Assets/_Project/Scenes/MoonLanding_Scene.unity Assets/_Project/Scenes/MoonLanding_Scene.unity.meta
git commit -m "MoonLanding: add dust VFX and landing evaluator"
git push
```

✅ **හොඳ commit message:** `MoonLanding: fix lander upside-down scale`
❌ **නරක:** `update`, `asdf`, `final final 2`

> 💡 `git add .` භාවිතා කරන්න පුළුවන්, හැබැයි **ඊට කලින් `git status` බලලා** ඔයාට අදාළ නැති files (වෙන කෙනෙක්ගේ scene, `ProjectSettings/`) වෙනස් වෙලාද කියලා බලන්න. වැරදීමකින් වෙනස් වුණ file එකක් reset කරන්න:
> ```bash
> git restore ProjectSettings/QualitySettings.asset
> ```

### පියවර E — Pull Request එකක් හදන්න (feature එකක් ඉවර වුණාම)
1. GitHub → repo → **"Compare & pull request"** (කහ පාට bar එක) හෝ **Pull requests → New**.
2. `base: main` ← `compare: feature/moon-landing`
3. Title + මොකක්ද කළේ කියලා කෙටි විස්තරයක්, screenshot එකක් දාන්න.
4. **Reviewers** ට team member කෙනෙක් select කරන්න.
5. Reviewer: **Files changed** බලලා → **Approve** → **Merge pull request**.
6. Merge වුණාට පස්සේ **හැමෝම** `git fetch` + `git merge origin/main` කරන්න.

---

## 3. Unity Merge Conflicts වළක්වා ගන්න රන් නීති 🏆

Unity scenes/prefabs merge කරන්න **ගොඩක් අමාරුයි**. ඒ නිසා conflict **එන්න නොදී ඉන්න** එක තමයි හොඳම ක්‍රමය.

| # | නීතිය |
|---|---|
| 1 | **එක කෙනාට එක scene එකයි.** වෙන කෙනෙක්ගේ scene එක open කරලා save කරන්න එපා. (Unity එකේ open කරලා බැලුවත් "Save?" ඇහුවොත් **Don't Save**.) |
| 2 | **එක කෙනාට එක folder එකයි:** `Assets/_Project/Features/<ඔයාගේ-කොටස>/` |
| 3 | **Shared files** වෙනස් කරන්න කලින් team group එකේ කියන්න: `ProjectSettings/`, `Packages/manifest.json`, `Assets/Settings/` (URP), Build Profiles scene list, `MainMenu/HUD` scene. එක වෙලාවකට **එක්කෙනයි**. |
| 4 | **Package එකක් add කරන්නේ නම්** team එකට කියලා, එක්කෙනෙක් කරලා PR එකක් දාන්න. |
| 5 | **Push කරන්න කලින් pull**, වැඩ පටන් ගන්න කලින් pull. |
| 6 | **ලොකු වෙනස්කම් එකපාර එපා** — පොඩි commits, නිතර push. |
| 7 | Scene එකේ වැඩ කරන දේවල් **prefabs** වලට දාන්න — prefab එකක් වෙනස් කරද්දී scene file එක වෙනස් වෙන්නේ නැහැ. |
| 8 | `Library/`, `Temp/`, `Logs/`, `UserSettings/` **කවදාවත් commit කරන්න එපා** (`.gitignore` එකෙන් දැනටමත් block කරලා ✅). |

---

## 4. Git LFS — ලොකු files

අපේ `.gitattributes` එකෙන් මේ files **ස්වයංක්‍රීයව LFS** එකට යනවා: `png jpg jpeg tga psd exr hdr wav mp3 ogg fbx obj blend glb gltf mp4 ttf zip unitypackage` + Moon Landing terrain/meshes.

```bash
git lfs ls-files        # LFS එකේ තියෙන files බලන්න
git lfs pull            # LFS files බාගන්න (model එකක් pink/missing නම්)
```

⚠️ **GitHub LFS storage/bandwidth එකට limit එකක් තියෙනවා.** Repo owner: **Settings → Billing / LFS usage** එකෙන් usage බලන්න. ඒ නිසා:
- ඕනෑවට වඩා ලොකු textures (8K) push කරන්න එපා — 2K/4K ඇති.
- එකම ලොකු file එක නැවත නැවත වෙනස් කරලා push කරන්න එපා.
- 100 MB ට වඩා ලොකු file එකක් **LFS නැතුව** push කරන්න බැහැ (GitHub reject කරනවා).

**අලුත් file type එකක් LFS එකට දාන්න** (e.g. `.tif`):
```bash
git lfs track "*.tif"
git add .gitattributes
git commit -m "Track tif with LFS"
```

---

## 5. Merge Conflict එකක් ආවොත් මොකද කරන්නේ? 😰

`git merge origin/main` කරද්දී මෙහෙම ආවොත්:
```
CONFLICT (content): Merge conflict in Assets/_Project/Scenes/MoonLanding_Scene.unity
```

### 5.1 කලබල වෙන්න එපා — මුලින්ම බලන්න
```bash
git status        # "both modified" කියලා තියෙන files තමයි conflict
```

### 5.2 Case 1: ඔයාගේ file එකක් (ඔයාගේ scene / script)
ඔයාගේ version එක තියාගන්න:
```bash
git checkout --ours Assets/_Project/Scenes/MoonLanding_Scene.unity
git add Assets/_Project/Scenes/MoonLanding_Scene.unity
```

### 5.3 Case 2: වෙන කෙනෙක්ගේ file එකක් (ඔයා වැරදීමකින් වෙනස් කළ)
ඒ අයගේ version එක ගන්න:
```bash
git checkout --theirs Assets/_Project/Scenes/RocketAssembly_Scene.unity
git add Assets/_Project/Scenes/RocketAssembly_Scene.unity
```

### 5.4 Case 3: `.cs` script එකක — දෙන්නගෙම වෙනස්කම් ඕන
VS Code / Visual Studio එකේ file එක open කරන්න:
```
<<<<<<< HEAD
   ඔයාගේ code
=======
   අනිත් කෙනාගේ code
>>>>>>> origin/main
```
හරි code එක තියලා `<<<<<<<`, `=======`, `>>>>>>>` lines **මකන්න**, ඊට පස්සේ:
```bash
git add <file>
```

### 5.5 ඔක්කොම resolve කළාට පස්සේ
```bash
git commit -m "Merge main into feature/moon-landing"
git push
```

### 5.6 හැම දෙයක්ම අවුල් වුණා — merge එක අත්හරින්න
```bash
git merge --abort
```
Project එක merge එකට කලින් තිබුණ තත්වයට එනවා. ඊට පස්සේ team එකෙන් උදව් ඉල්ලන්න. 🙂

### 5.7 (Advanced, optional) Unity Smart Merge
Unity එකේ `UnityYAMLMerge` tool එක scene conflicts ගොඩක් auto විසඳනවා. `.gitattributes` එකේ දැනටමත් `merge=unityyamlmerge` දාලා තියෙනවා. Enable කරන්න ඔයාගේ **local** `.git/config` එකට (Windows path එක ඔයාගේ Unity install එකට ගැලපෙන්න වෙනස් කරන්න):
```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```
Configure නොකළත් කමක් නැහැ — Git එකේ සාමාන්‍ය merge එක වෙනවා.

---

## 6. වැරදීම් නිවැරදි කරගන්න (Undo cheat-sheet)

| ප්‍රශ්නය | Command |
|---|---|
| File එකක වෙනස්කම් අත්හරින්න (commit නොකළ) | `git restore <file>` |
| `git add` කරපු file එකක් unstage | `git restore --staged <file>` |
| අන්තිම commit message එක වෙනස් කරන්න (push නොකළ) | `git commit --amend -m "new message"` |
| Push කරපු commit එකක් ආපහු හරවන්න (ආරක්ෂිත) | `git revert <commit-id>` |
| මම දැන් ඉන්නේ කොයි branch එකේද? | `git branch` |
| History බලන්න | `git log --oneline --graph -15` |
| වැඩ ටිකක් තාවකාලිකව පැත්තකට | `git stash` → පස්සේ `git stash pop` |

> ⛔ `git push --force` සහ `git reset --hard` **team එකෙන් අහන්නේ නැතුව කරන්න එපා** — අනිත් අයගේ වැඩ නැති වෙන්න පුළුවන්.

---

## 7. GitHub Desktop එකෙන් (Command line බය නම්)

1. **File → Clone repository** → repo එක තෝරන්න.
2. උඩ **Current branch → New branch** → `feature/moon-landing`.
3. Unity එකේ වැඩ කරලා save කරන්න → GitHub Desktop එකේ වම් පැත්තේ වෙනස් වුණ files පේනවා. **ඔයාට අදාළ නැති files uncheck** කරන්න.
4. පහළ **Summary** එකේ message එක ලියලා **Commit to feature/moon-landing**.
5. **Push origin**.
6. **Branch → Update from main** (අනිත් අයගේ වැඩ ගන්න).
7. **Branch → Create pull request**.

(GitHub Desktop එක LFS support කරනවා — වෙනම කිසිම දෙයක් කරන්න ඕන නැහැ.)

---

## 8. Moon Landing කොටස project එකට push කරන හැටි (Himal)

```bash
git checkout -b feature/moon-landing        # (දැනටමත් තියෙනවා නම්: git checkout feature/moon-landing)
# ZIP එකේ Assets/, docs/, .gitattributes project root එකට copy කරන්න
# Unity open → Astronaut Project ▸ Moon Landing ▸ Build Level → Play කරලා test
git add .gitattributes docs Assets/_Project/Features/MoonLanding Assets/_Project/Scenes/MoonLanding_Scene.unity
git add ProjectSettings/EditorBuildSettings.asset     # scene එක build list එකට add වුණ නිසා (team එකට කියන්න)
git status                                          # වෙන අයගේ files නැති බව check
git commit -m "MoonLanding: complete level (terrain, lander physics, VFX, SFX, HUD, scoring)"
git push -u origin feature/moon-landing
```
ඊට පස්සේ GitHub එකේ **Pull Request** එකක් හදලා team member කෙනෙක්ට review කරන්න දෙන්න.

> `.gitattributes` එක වෙනස් කළ නිසා, merge වුණාට පස්සේ **අනිත් අයත්** `git pull` → `git lfs pull` කරන්න ඕන.

---

## 9. Team එකට Checklist ✅

- [ ] හැමෝම Unity **6000.3.22f1**
- [ ] හැමෝම `git lfs install` කළා
- [ ] Clone කළේ `git clone` එකෙන් (ZIP නෙවෙයි)
- [ ] `main` protect කළා, PR + 1 approval
- [ ] එක කෙනාට එක branch / scene / folder
- [ ] Shared files වෙනස් කරන්න කලින් group එකේ කිව්වා
- [ ] වැඩ පටන් ගන්න කලින් **pull**, ඉවර වෙලා **commit + push**
- [ ] `.meta` files commit කරනවා
