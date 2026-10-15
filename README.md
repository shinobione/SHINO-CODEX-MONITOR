# SHINO // CODEX MONITOR

Widget Windows local pour afficher les **quotas Codex de l'abonnement ChatGPT**, et non les limites du chat classique ni les quotas de facturation API. Application C# WinForms discrète, trois dispositions, jauges colorées, resets, historique local et alertes.

> **État :** V4.5.0 source candidate. Version publique stable actuelle : v4.4.0. Compilation et tests Windows V4.5 à effectuer avant une nouvelle Release. Les binaires ne sont pas signés.

## Confidentialité

- Le programme lit les quotas via le **Codex CLI installé et déjà authentifié** (méthode `account/rateLimits/read`).
- Aucun mot de passe, cookie ou jeton Codex n'est demandé, copié dans le projet ou envoyé vers GitHub.
- Les fichiers `settings.json` et `history.jsonl` restent sur votre PC : `%LOCALAPPDATA%\SHINO-CODEX-MONITOR`.
- La CI utilise des données **simulées** pour les autotests, jamais de session utilisateur.
- Le dépôt ne contient que les sources, scripts, icônes, tests et documentation ; voir [SECURITY.md](SECURITY.md).

## Télécharger et installer

1. Dans **Actions → Build Windows**, lancer le workflow si nécessaire, puis télécharger son artefact Windows (s'il est vert).
2. Décompresser l'artefact, puis `SHINO_CODEX_MONITOR_V4_5_WINDOWS.zip`.
3. Fermer le widget déjà installé et lancer `INSTALLER-V4.5.bat`.
4. L'installateur vérifie le SHA-256, exécute **45 autotests Windows** et installe en profil utilisateur seulement s'ils réussissent.
5. Codex CLI doit déjà être connecté au compte ChatGPT voulu. Aucune clé API nécessaire.

Ou, depuis les sources, exécuter `PREPARER-ET-INSTALLER-V4.5.bat` sur Windows 10/11 avec .NET Framework 4.x disponible.

## Compiler / tester

- Build Windows : `BUILD-RELEASE-V4.5.bat`.
- Autotests du binaire : `SHINO-CODEX-MONITOR.exe --self-test`.
- Audit statique portable : `python -m unittest discover -s tests -p "test_*.py"`.
- CI : `.github/workflows/build-windows.yml` (compilation sur Windows, autotests et artefact).
- Publication GitHub Releases : tag `v4.5.x` **après validation du build**. Aucune release n'est annoncée tant que sa CI n'est pas consultée.

## Désinstaller

Menu Démarrer → « Désinstaller SHINO Codex Monitor », ou « Applications installées » Windows. La désinstallation demande confirmation et conserve par défaut les données locales.

## Limites

La disponibilité des fenêtres et resets dépend des données réellement renvoyées par Codex. Les relevés périmés doivent apparaître comme tels. Cette application est communautaire et **non officielle**, sans affiliation avec OpenAI. Les binaires ne sont pas signés ; vérifier la provenance des artefacts.

Voir [README-FR.txt](README-FR.txt) pour la procédure complète.

**Licence :** source visible publiquement pour l'instant ; aucune licence de réutilisation n'a encore été choisie par le propriétaire. Public ne signifie pas automatiquement « open source sous licence ».

## Vérifier les mises à jour (V4.5)

Clic droit sur l'icône `%` → **Vérifier les mises à jour**. L'application effectue
un appel HTTPS en lecture seule vers l'API publique GitHub Releases du dépôt.
Elle compare la version de l'exécutable à la dernière Release **stable**,
sans lire le compte Codex, sans jeton GitHub et sans téléchargement automatique.
Si une version plus récente existe, tu peux ouvrir sa page officielle.
Le programme actuel continue de fonctionner si GitHub est indisponible.

**Publication :** la V4.5 n'est pas publiée tant que sa CI Windows ne passe pas et
qu'un tag `v4.5.x` n'a pas été créé.
