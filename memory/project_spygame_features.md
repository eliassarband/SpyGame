---
name: project-spygame-features
description: Features added to SpyGame in the major update session (2026-05-19)
metadata:
  type: project
---

Major feature update implemented 2026-05-19:

**New Screens:**
- ResultPage: post-timer voting + spy guess mechanic (full Spyfall flow)
- TutorialPage: game rules shown on first launch
- StatisticsPage: win/loss stats per category, last 5 games
- CustomWordsPage: add/delete custom words per category

**New Models:** GameResult (DB), SessionManager (in-memory score across rounds)

**Game Features:**
- SpiesKnowEachOther toggle in SetupPage → shows partners in RevealPage
- Timer ProgressBar (color: green → yellow → red)
- After timer ends → ResultPage (not SetupPage directly)
- Custom words stored in WordItem table with IsCustom=true flag

**Android/Market:**
- Min SDK lowered from API 27 → API 21 (Android 5.0)
- APK format for Bazaar/Myket
- Keystore signing instructions in csproj comments

**Why:** User requested all features added + market readiness for Bazaar/Myket.
**How to apply:** When suggesting further changes, build on these new pages/models.
