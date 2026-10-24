# Worker log

| Task | Attempts | Result | Notes |
| --- | --- | --- | --- |
| 01-gm-q01-wanderer-spawn | 1 | PASS, verified | 3 corrections (kill condition, new-game set fill, dead OnGameLoaded); added mod implications. check_worker first run hit dotnet CLR crash, rerun passed |
| 02-gm-q02-wanderer-count | 1 | PASS, verified | claims correct; added Template/IsOriginalCharacter note. Infra: dotnet CLR crash retry added to check_worker |
| 03-gm-q03-tavern-placement | 1 | PASS, verified | correct (tavern rule, swap halves, StayingInSettlement); relabeled self-written "orchestrator note" |
| 04-gm-q04-hero-creation | 1 | PASS, verified | missed DefaultTraits.cs (trait ids/bounds in code) -> fixed; added wanderer age-override note + adopted validation ranges |
| 05-gm-q05-char-templates | 1 | PASS, verified | correct (IsTemplate private set, LoadXml/CreateObjectFromXmlNode, Xmls/XmlNode loading); used xmlschemas on its own |
| 06-gm-q06-hiring | 1 | PASS, verified | correct; replaced 3-line copied method body with prose; added mod hire notes |
| 07-gm-q07-dialog-api | 1 | PASS, verified | correct; key finding: vanilla hire option needs Harmony prefix to hide. check hit CLR crash twice in a row (retry also crashed), manual rerun passed |
| 08-gm-q08-lifecycle | 1 | PASS, verified | correct; replaced copied SyncData body with prose. Check failed 3x on CLR crash -> retry raised to 3 |
| 09-gm-q09-existing-saves | 1 | PASS, verified | correct (LoadBehaviorData miss => no SyncData); added design note. Strata-off experiment: CLR crash 5/15 => not RAM |
| 10-gm-q10-harmony-target | 1 | PASS, verified | excellent: prefix on AddToAliveCompanions (single Add site) also blocks TryKillCompanion |
| 11-gm-q11-generation-decision | 1 | PASS, verified | strong: recommends runtime in-memory NPCCharacters XML (B) + Special-occupation template; worker set Status VERIFIED itself (should be DRAFT) |
| 12-gm-q12-voice-over | 1 | PASS, verified | correct and detailed; no XSD for voiced lines / language_data |
| 13-gm-q13-war-sails | 1 | PASS, verified | correct; NavalDLC id, 3 skills, no cultures/traits, GetObject null |
| 14-gm-q14-localization | 1 | PASS, verified | correct; M1 complete |
| 16-core-loader | 1 | PASS | clean; strict Newtonsoft, IO errors mapped to InvalidJson; worker used SDK 10.0.402 |
| 17-core-registry | 1 | PASS | clean |
| 18-core-validation-companion | 1 | PASS | clean, 61 acceptance tests |
| 19-core-validation-dialogs | 1 | PASS | clean |
