# SHINO // CODEX MONITOR

Widget Windows local pour afficher les **quotas Codex de l'abonnement ChatGPT**, et non les limites du chat classique ni les quotas de facturation API. Application C# WinForms discrète, trois dispositions, jauges colorées, resets, historique local et alertes.

> **État :** V4.4 source, version Windows confirmée fonctionnelle par son utilisateur ; CI publique et téléchargement précompilé restent à valider après création du dépôt. Aucun exécutable officiel signé n'est fourni ici.

## Confidentialité

- Le programme lit les quotas via le **Codex CLI installé et déjà authentifié** (méthode `account/rateLimits/read`).
- Aucun mot de passe, cookie ou jeton Codex n'est demandé, copié dans le projet ou envoyé vers GitHub.
- Les fichiers `settings.json` et `history.jsonl` restent sur votre PC : `%LOCALAPPDATA%\SHINO-CODEX-MONITOR`.
- La CI utilise des données **simulées** pour les autotests, jamais de session utilisateur.
- Le dépôt ne contient que les sources, scripts, icônes, tests et documentation ; voir [SECURITY.md](SECURITY.md).

## Télécharger et installer

1. Dans **Actions → Build Windows**, lancer le workflow si nécessaire, puis télécharger son artefact Windows (s'il est vert).
2. Décompresser l'artefact, puis `SHINO_CODEX_MONITOR_V4_4_WINDOWS.zip`.
3. Fermer le widget déjà installé et lancer `INSTALLER-V4.4.bat`.
4. L'installateur vérifie le SHA-256, exécute **38 autotests Windows** et installe en profil utilisateur seulement s'ils réussissent.
5. Codex CLI doit déjà être connecté au compte ChatGPT voulu. Aucune clé API nécessaire.

Ou, depuis les sources, exécuter `PREPARER-ET-INSTALLER-V4.4.bat` sur Windows 10/11 avec .NET Framework 4.x disponible.

## Compiler / tester

- Build Windows : `BUILD-RELEASE-V4.4.bat`.
- Autotests du binaire : `SHINO-CODEX-MONITOR.exe --self-test`.
- Audit statique portable : `python -m unittest discover -s tests -p "test_*.py"`.
- CI : `.github/workflows/build-windows.yml` (compilation sur Windows, autotests et artefact).
- Publication GitHub Releases : tag `v4.4.x` **après validation du build**. Aucune release n'est annoncée tant que sa CI n'est pas consultée.

## Désinstaller

Menu Démarrer → « Désinstaller SHINO Codex Monitor », ou « Applications installées » Windows. La désinstallation demande confirmation et conserve par défaut les données locales.

## Limites

La disponibilité des fenêtres et resets dépend des données réellement renvoyées par Codex. Les relevés périmés doivent apparaître comme tels. Cette application est communautaire et **non officielle**, sans affiliation avec OpenAI. Les binaires ne sont pas signés ; vérifier la provenance des artefacts.

Voir [README-FR.txt](README-FR.txt) pour la procédure complète.

**Licence :** source visible publiquement pour l'instant ; aucune licence de réutilisation n'a encore été choisie par le propriétaire. Public ne signifie pas automatiquement « open source sous licence ».
