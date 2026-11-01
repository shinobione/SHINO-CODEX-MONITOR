# SHINO // CODEX MONITOR — Roadmap

**Mise à jour : 19 octobre 2026 · V5.1.0 publiée officiellement : [Release v5.1.0](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases/tag/v5.1.0).**

Périmètre immuable : utilitaire **local Windows** surveillant les **quotas Codex de l'abonnement ChatGPT** et, si observables, les *tours principaux* Codex. Ni chat classique, ni facturation API, ni autre projet SHINO.

## Statut des jalons

| Version | État | Livrables et critères |
|---|---|---|
| **V5.0.0** | **Release stable publiée** | Quotas 5h / 7j s'ils sont exposés, resets, fraîcheur, origine Codex CLI, fenêtre épinglable, tray et historique local. |
| **V5.1.0-preview.1** | **Preview validée par l'utilisateur** | Widget unique avec activité Codex, icône animée en cours, coche à fin confirmée, durée/modèle/effort si exposés, journaux locaux lus en lecture seule. Menus et identité de version rationalisés ; ZIP portable distinct de V5.0. |
| **V5.1.0 stable** | **Publiée le 19/10/2026** | [ZIP Windows officiel](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases/download/v5.1.0/SHINO_CODEX_MONITOR_V5_1_WINDOWS.zip), installateur dédié, CI de `main` verte ; tests terrain complémentaires restant dans la roadmap. |
| **V5.2 (hypothèse)** | **À étudier** | Si possible, intégration **documentée** aux événements Codex Desktop / app-server afin de synchroniser le tour visible à l'écran, sans attachement clandestin, jetons, cookies ni endpoints privés. |

## V5.1 — Critères avant fusion/publication

- [x] Structure de collecte isolée : quotas via CLI, activité via session locale indicative ; lecture seule.
- [x] Identité **5.1.0-preview.1** distincte de V5.0, script de build Preview, aucun installateur V5.0 embarqué.
- [x] Affichage tout-en-un, statuts explicites, roue vectorielle désactivable, notification configurable, icône de fenêtre.
- [x] Filtrage des sous-agents quand `thread_source` est présent ; support des tours longs et de `duration_ms` cohérent.
- [x] Test automatique sur CI Windows (fixtures synthétiques, audits statiques, build natif) — dernière vérification : [CI Windows réussie](https://github.com/shinobione/SHINO-CODEX-MONITOR/actions/runs/39252417243) (101 audits Python, 98 tests C# des quotas, 46 tests C# des tâches).
- [ ] Sur **le même compte et au même instant**, comparer la valeur des quotas au tableau officiel (pas de comparaison instrumentée fournie).
- [x] Fonctionnement réel de la Preview confirmé par l'utilisateur (« tout est OK »), publication autorisée.
- [ ] Couvrir par des mesures instrumentées 3 commandes utilisateur (courte, >10 min, sous-agent) et comparer aux événements locaux : validation détaillée non publiée.
- [ ] Confirmer réception de notifications Windows sur la V5.1 finale ; mécanisme dépendant de Windows.
- [ ] Valider quatre dispositions, DPI 100/125/150/200 %, écran secondaire, redimensionnement, veille/reprise, hors ligne, mémoire et CPU.
- [ ] Vérifier mise à niveau de préférences depuis V5.0, retour arrière, aucun secret ou prompt dans diagnostics.
- [x] Validation utilisateur de la Preview et autorisation explicite de fusion/publication reçues le 19/10/2026.

## Risques et limites à ne pas masquer

- `session_meta`, `task_started`, `task_complete`, `duration_ms` : formats locaux **non garantis** pour toutes les versions de Codex Desktop.
- Voir un journal qui bouge n'est **pas** la preuve que Codex travaille ; une absence de journal n'est **pas** la preuve qu'il a terminé. Plusieurs onglets et sous-agents brouillent la corrélation.
- La source peut ne pas identifier la commande de l'onglet actif. Montrer **NON VÉRIFIÉ** plutôt qu'un faux « FIN » ou une fausse durée.
- Une bulle `NotifyIcon.ShowBalloonTip` envoyée ne prouve pas son affichage par Windows. L'EXE portable n'est pas signé.
- Ne jamais inventer les limites, heures de reset, crédits ou pourcentages. Pas d'appel payant à un modèle pour collecter les quotas.

## Revue comparative — AI Limits (Rust) (1er novembre 2026)

**Statut : observations documentaires, non implémentées.** Référence : [napxlexn/ailimits](https://github.com/napxlexn/ailimits), version `0.7.0` indiquée dans son `Cargo.toml` à la date de l'examen. L'étude a porté sur le code Rust des fournisseurs, du scheduler, des tendances et de la géométrie taskbar, ainsi que sur nos `CodexReader.cs`, `Models.cs`, `QuotaTrayIcon.cs` et `AlertRules.cs`. Aucune installation, benchmark comparatif ni validation terrain effectuée.

### Pistes à retenir, par ordre de priorité

| Priorité | Piste / constat | Mise en œuvre envisagée et critère de validation |
|---|---|---|
| **P1** | **Détails des crédits de réinitialisation** : notre parser lit déjà `rateLimitResetCredits.availableCount`, mais pas les détails optionnels. | Étudier la lecture seule des crédits fournis par l'app-server (`credits[].status`, `expiresAt`, `title`, `description`) ; afficher une expiration **uniquement si présente** et valide. Aucune action de consommation ; tester absence/null, liste partielle, crédits expirés et valeurs contradictoires. Pour les sondages de fond, examiner `excludeResetCreditDetails` afin d'éviter les lectures détaillées inutiles. |
| **P1** | **Fraîcheur robuste** : notre code possède déjà un statut d'ancienneté et un backoff, calculés notamment depuis l'heure système. | Étudier un ancrage monotone **pendant l'exécution** (avec horodatage UTC conservé pour l'affichage/persistance), y compris après mise en veille, reprise, changement NTP/fuseau/DST et redémarrage. Une horloge incohérente, une réponse manquante ou un échec réseau ne doivent jamais rendre un relevé ancien « Direct ». |
| **P2** | **Actualisation adaptative** : notre rafraîchissement et son backoff existent ; AI Limits suspend ses polls en cas d'inactivité/verrouillage. | Évaluer la pause lorsque la session Windows est inactive ou verrouillée, puis reprise contrôlée au déverrouillage/retour utilisateur ; garder l'actualisation manuelle, un timeout et un backoff borné. Mesurer appels, latence de reprise et CPU/RAM avant/après. Ne jamais faire passer un cache pour un relevé frais. |
| **P2 / option** | **Prévision d'épuisement (« burn rate »)** : régression sur un historique court de consommation mesurée. | Option désactivée par défaut, explicitement libellée **estimation** et jamais confondue avec le reset officiel. Minimum d'échantillons frais de **la même fenêtre**, exclusion des resets, des données périmées, des tendances non croissantes et des prévisions postérieures au reset. Tester les baisses abruptes, l'activité intermittente et les petits échantillons. |
| **P3 / étude UX** | **Mini-panneau taskbar** indépendant du widget principal : AI Limits utilise une fenêtre superposée près de la zone de notification et revient au tray quand l'espace manque. | Ne le retenir qu'après validation sur Windows 11, auto-hide, DPI 100–200 %, trois écrans, plein écran/jeux et barre saturée ; repli explicite sur l'icône tray. Ne pas confondre ce panneau avec notre option actuelle « Afficher dans la barre des tâches ». |
| **P3 / étude UX** | **Densité adaptative** : niveaux Compact / Medium / Expanded et réduction de largeur sans superposition dans AI Limits. | Comparer à nos quatre dispositions existantes ; améliorer uniquement les cas de chevauchement ou de lisibilité mesurés, sans créer une cinquième refonte ni perdre les libellés « restant », reset et fraîcheur. |

### Décisions de non-adoption

- **Conserver C# WinForms** pour l'existant ; ne pas migrer vers Rust sur la seule base des mesures publiées par AI Limits (~3 Mo d'exécutable, 45 Mo de RAM privée, 0,012 % d'un cœur dans son environnement). Un éventuel changement de pile exigerait un benchmark équivalent **sur notre application et notre PC cible**.
- **Conserver les quotas via le CLI Codex / app-server `account/rateLimits/read`**, déjà en place : ne pas copier la lecture de `~/.codex/auth.json` ni l'appel direct à `chatgpt.com/backend-api/wham/usage`, endpoint non documenté utilisé par AI Limits. Aucun cookie, jeton ou mot de passe à extraire ; ne pas confondre abonnement Codex, chat classique et API facturée.
- **Ne pas afficher `≈0 %` comme un nouveau quota mesuré après l'heure présumée de reset** : conserver la dernière valeur, son âge et l'état **Ancienne donnée / À vérifier** jusqu'à un nouveau relevé valide.
- **Ne pas adopter la mise à jour silencieuse par défaut** : garder le consentement explicite avant installation, contrôle d'intégrité, sauvegarde/retour arrière et aucun autostart imposé.
- **Respecter la licence GPL-3.0-or-later d'AI Limits** : s'inspirer des principes sans copier du code Rust dans un projet redistribué sans analyse des obligations de licence.

### Ordonnancement et preuves attendues

Ces pistes sont **indépendantes des correctifs de fiabilité de l'activité Codex** : ne pas retarder un P0 en cours (lecture JSONL progressive, visibilité des missions réelles) pour une amélioration cosmétique. Avant de cocher un point : tests unitaires de source absente, 401/403, données anciennes, fenêtres multiples et resets ; comparaison au tableau Codex officiel sur **le même compte et au même instant** lorsque possible ; tests de veille/reprise/hors ligne, DPI/écrans multiples et benchmark CPU/RAM/I/O. Distinguer tests synthétiques et observation terrain. Pas de Release implicite.

**Sources consultées :** [architecture AI Limits](https://github.com/napxlexn/ailimits/blob/master/docs/en/ARCHITECTURE.md) · [collecteur Codex](https://github.com/napxlexn/ailimits/blob/master/src/providers/codex.rs) · [scheduler](https://github.com/napxlexn/ailimits/blob/master/src/monitor/scheduler.rs) · [projection d'usage](https://github.com/napxlexn/ailimits/blob/master/src/monitor/trend.rs) · [géométrie taskbar](https://github.com/napxlexn/ailimits/blob/master/src/platform/taskbar_geom.rs) · [schéma OpenAI app-server](https://github.com/openai/codex/blob/main/codex-rs/app-server-protocol/src/protocol/v2/account.rs).

## Actions prioritaires

1. **Publié :** V5.1.0 sur GitHub, tag `v5.1.0`, ZIP avec SHA-256 publié ; [workflow de publication](https://github.com/shinobione/SHINO-CODEX-MONITOR/actions/runs/39255792594) et [CI de `main`](https://github.com/shinobione/SHINO-CODEX-MONITOR/actions/runs/39255792674) réussis.
2. Valider sur la machine utilisateur la corrélation du **tour utilisateur complet** (non un sous-agent), puis traiter les erreurs documentées dans la même PR.
3. Après publication, tester l'assistant de mise à jour V5.0 → V5.1 sur le PC Windows et documenter les écarts.

Références : [Release V5.1.0](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases/tag/v5.1.0) · [PR #11 fusionnée](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/11) · [CHANGELOG.md](CHANGELOG.md) · [README.md](README.md).
