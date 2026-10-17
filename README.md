# SHINO // CODEX MONITOR

Widget Windows local pour afficher les **quotas Codex de l'abonnement ChatGPT**, et non les limites du chat classique ni les quotas de facturation API. Application C# WinForms discrète, trois dispositions, jauges colorées, resets, historique local et alertes.

> **État :** V4.7.0 candidate — téléchargement GitHub vérifié et installation après double confirmation. Dernière Release publique validée : v4.5.0 ; V4.5.1 est encore en PR. Compilation et essais Windows V4.7 non exécutés. Binaires non signés.

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

## Avertissement de sécurité au démarrage (V4.7.0)

Après vérification du SHA-256 et réussite des autotests, l’installateur retire, **sur le seul exécutable installé**, le marquage Internet éventuel (`Unblock-File`). Il ne désactive aucune protection Windows, ne modifie pas les clés de sécurité et n’accorde aucune confiance à l’éditeur : **le binaire reste non signé**. Si l’opération échoue, un avertissement explicite est affiché. L’intégrité SHA-256 contenue dans le même ZIP ne constitue pas une preuve d’authenticité de l’éditeur. Télécharger uniquement la Release du dépôt officiel.

## V4.7 : mise à jour assistée

Clic droit > **Vérifier les mises à jour** > confirmer **téléchargement** > confirmer **installation**.
L'archive provient de GitHub Releases et son SHA-256 doit correspondre à `assets[].digest` dans l'API GitHub.
Un deuxième contrôle vérifie les chemins du ZIP et le SHA-256 de l'exécutable.
En l'absence de digest ou en cas de fichier inattendu, réseau interrompu ou incohérence : l'installation est refusée.
L'installateur reprend la sauvegarde binaire et conserve les JSON locaux. Une console temporaire peut afficher
le résultat de l'installation. Ne jamais exécuter le téléchargement en mode silencieux.

**Limites :** cela vérifie l'intégrité de ce que GitHub a servi, pas une signature numérique de l'éditeur.
Le processus doit encore être éprouvé de bout en bout sur un PC Windows (veille, réseau, réinstallations et rollback).

## V4.7 Circular Dashboard (candidate)

Dans l'icône système → **Disposition → Circulaire · 2 roues**, affiche les quotas 5 h et 7 jours comme deux anneaux.
Le pourcentage au centre signifie **quota restant**, jamais consommé. Les réinitialisations affichées
proviennent exclusivement de Codex CLI. L'état, l'origine et l'âge du relevé restent visibles.
Donnée absente : « — » ; donnée périmée : anneau gris et pourcentage marqué « * ».

**Couleurs selon le pourcentage entier affiché** : 76–100 violet (`#A477FF`),
51–75 turquoise (`#51D7AF`), 26–50 doré (`#E5B64D`),
11–25 orange (`#F28C38`), 0–10 rouge (`#EE4B55`).
La même échelle colore l'icône système et les anciennes barres.
Ces cinq nouvelles couleurs ont leurs propres clés dans le JSON, sans effacer les anciennes.
Une installation neuve propose le mode circulaire par défaut ; les dispositions déjà enregistrées sont respectées.

V4.7 reste une **candidate**, basée sur la branche V4.6. Pas de Release publique avant la compilation
Windows, les tests de rendu (DPI, écrans) et la validation de l'utilisateur.

## V4.7.1 — Apparence & disposition (candidate)

Clic droit sur le tray → **Apparence** : opacité 70/80/90/96/100 %, choix de quatre fonds sombres ou couleur personnalisée via `ColorDialog`. Les fonds trop clairs sont refusés afin de conserver le contraste des états et des resets. Les préférences sont persistées dans le `settings.json` local ; les anciennes dispositions et données ne sont pas modifiées.

Le mode **Micro** réserve une marge au bouton de fermeture ; le mode **Circulaire** réduit la fenêtre à 540 × 278 et agrandit les anneaux. Version affichée dans le diagnostic : version réelle du binaire. Cette version n'est pas encore publiée : tests de rendu sous Windows nécessaires.

### Réinitialisation lorsque Codex indique « usage bloqué »

Dans la V4.7.1 candidate, le menu **Réinitialisations** reste fonctionnel si `ordinaryUsageAllowed=false` et si les quotas sont lus directement et récemment : le message de blocage apparaît en pied de widget, mais la date et/ou le compte à rebours du reset restent affichés sous chaque fenêtre **uniquement si** `resetsAt` a réellement été communiqué par Codex. Sinon : « Reset non communiqué ». Un relevé ancien n'affiche pas son ancien reset comme une donnée fraîche. Les choix « Date/heure + décompte », « Date/heure uniquement », « Décompte uniquement » et « Masquées » restent indépendants du statut bloqué.
