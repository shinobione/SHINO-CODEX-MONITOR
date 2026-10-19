# SHINO // CODEX MONITOR — Roadmap

**Mise à jour : 19 octobre 2026 · branche `feature/v5.1-live-task-monitor` · PR #11 Draft**

Périmètre immuable : utilitaire **local Windows** surveillant les **quotas Codex de l'abonnement ChatGPT** et, si observables, les *tours principaux* Codex. Ni chat classique, ni facturation API, ni autre projet SHINO.

## Statut des jalons

| Version | État | Livrables et critères |
|---|---|---|
| **V5.0.0** | **Release stable publiée** | Quotas 5h / 7j s'ils sont exposés, resets, fraîcheur, origine Codex CLI, fenêtre épinglable, tray et historique local. |
| **V5.1.0-preview.1** | **Branche de test — non fusionnée, non publiée** | Widget unique avec activité Codex, icône animée en cours, coche à fin confirmée, durée/modèle/effort si exposés, journaux locaux lus en lecture seule. Menus et identité de version rationalisés ; ZIP portable distinct de V5.0. |
| **V5.1.0 stable** | **Bloqué en validation terrain** | Passer les critères ci-dessous sur le PC utilisateur ; ne pas publier sur simples tests simulés. |
| **V5.2 (hypothèse)** | **À étudier** | Si possible, intégration **documentée** aux événements Codex Desktop / app-server afin de synchroniser le tour visible à l'écran, sans attachement clandestin, jetons, cookies ni endpoints privés. |

## V5.1 — Critères avant fusion/publication

- [x] Structure de collecte isolée : quotas via CLI, activité via session locale indicative ; lecture seule.
- [x] Identité **5.1.0-preview.1** distincte de V5.0, script de build Preview, aucun installateur V5.0 embarqué.
- [x] Affichage tout-en-un, statuts explicites, roue vectorielle désactivable, notification configurable, icône de fenêtre.
- [x] Filtrage des sous-agents quand `thread_source` est présent ; support des tours longs et de `duration_ms` cohérent.
- [x] Test automatique sur CI Windows (fixtures synthétiques, audits statiques, build natif) — à confirmer pour le **dernier commit**.
- [ ] Sur **le même compte et au même instant**, comparer la valeur des quotas au tableau officiel.
- [ ] Tester avec **Codex Desktop réellement installé** : 3 commandes utilisateur (courte, >10 min, avec sous-agent), comparer démarrage, fin et durée finale, identifier les cas non observables.
- [ ] Vérifier l'absence de notifications intempestives ; notification Windows visible ou état dégradé/explication si bloquée.
- [ ] Valider quatre dispositions, DPI 100/125/150/200 %, écran secondaire, redimensionnement, veille/reprise, hors ligne, mémoire et CPU.
- [ ] Vérifier mise à niveau de préférences depuis V5.0, retour arrière, aucun secret ou prompt dans diagnostics.
- [ ] Vérifier la prévisualisation sur le PC, puis obtenir validation explicite pour **fusion dans main**, **version finale**, **installateur**, **tag/Release publics**.

## Risques et limites à ne pas masquer

- `session_meta`, `task_started`, `task_complete`, `duration_ms` : formats locaux **non garantis** pour toutes les versions de Codex Desktop.
- Voir un journal qui bouge n'est **pas** la preuve que Codex travaille ; une absence de journal n'est **pas** la preuve qu'il a terminé. Plusieurs onglets et sous-agents brouillent la corrélation.
- La source peut ne pas identifier la commande de l'onglet actif. Montrer **NON VÉRIFIÉ** plutôt qu'un faux « FIN » ou une fausse durée.
- Une bulle `NotifyIcon.ShowBalloonTip` envoyée ne prouve pas son affichage par Windows. L'EXE portable n'est pas signé.
- Ne jamais inventer les limites, heures de reset, crédits ou pourcentages. Pas d'appel payant à un modèle pour collecter les quotas.

## Actions prioritaires

1. Installer **aucune** Preview automatiquement : fournir ZIP portable non signé, exécuter les deux autotests et demander retour écran/diagnostic expurgé.
2. Valider sur la machine utilisateur la corrélation du **tour utilisateur complet** (non un sous-agent), puis traiter les erreurs documentées dans la même PR.
3. Avant publication, préparer un **installeur V5.1 dédié**, migration/rollback, tests de mise à jour et source d'authentification officielle si disponible ; demander l'accord de publication.

Référence : [PR #11](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/11) · [CHANGELOG.md](CHANGELOG.md) · [README.md](README.md).
