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

## Actions prioritaires

1. **Publié :** V5.1.0 sur GitHub, tag `v5.1.0`, ZIP avec SHA-256 publié ; [workflow de publication](https://github.com/shinobione/SHINO-CODEX-MONITOR/actions/runs/39255792594) et [CI de `main`](https://github.com/shinobione/SHINO-CODEX-MONITOR/actions/runs/39255792674) réussis.
2. Valider sur la machine utilisateur la corrélation du **tour utilisateur complet** (non un sous-agent), puis traiter les erreurs documentées dans la même PR.
3. Après publication, tester l'assistant de mise à jour V5.0 → V5.1 sur le PC Windows et documenter les écarts.

Références : [Release V5.1.0](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases/tag/v5.1.0) · [PR #11 fusionnée](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/11) · [CHANGELOG.md](CHANGELOG.md) · [README.md](README.md).
