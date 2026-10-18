# SHINO // CODEX MONITOR

Widget Windows local pour afficher les **quotas Codex de l'abonnement ChatGPT**, et non les limites du chat classique ni les quotas de facturation API. Application C# WinForms discrète, quatre dispositions, jauges circulaires et colorées, resets, historique local et alertes.

> **Version publique : V5.0.0 — Data Trust & Health (première étape fonctionnelle).** Les nouveautés V4.8 et V4.9 sont incluses. 92/92 audits Python et 98/98 autotests C# ont réussi dans GitHub Actions ; l'utilisateur a confirmé le fonctionnement sur Windows. Les validations multi-DPI, reprise après veille, 0 % bloqué et les protections de téléchargement restent partielles/non testées. Exécutable non signé. Voir [Releases](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases).

## Confidentialité

- Le programme lit les quotas via le **Codex CLI installé et déjà authentifié** (méthode `account/rateLimits/read`).
- Aucun mot de passe, cookie ou jeton Codex n'est demandé, copié dans le projet ou envoyé vers GitHub.
- Les fichiers `settings.json` et `history.jsonl` restent sur votre PC : `%LOCALAPPDATA%\SHINO-CODEX-MONITOR`.
- La CI utilise des données **simulées** pour les autotests, jamais de session utilisateur.
- Le dépôt ne contient que les sources, scripts, icônes, tests et documentation ; voir [SECURITY.md](SECURITY.md).

## Télécharger et installer (Windows)

1. Télécharger le fichier officiel `SHINO_CODEX_MONITOR_V5_0_WINDOWS.zip` depuis la [Release GitHub](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases) correspondante.
2. Décompresser le ZIP, puis lancer `INSTALLER-V5.0.bat` dans le dossier extrait **sans droits administrateur**.
3. L'installateur contrôle l'intégrité SHA-256 de l'exécutable, lance les autotests embarqués (**98 pour V5.0.0**), puis installe sous `%LOCALAPPDATA%\Programs\SHINO Codex Monitor`.
4. Codex CLI doit être installé et déjà authentifié sur le compte ChatGPT concerné ; pas de clé API ni de mot de passe à saisir dans le widget.

Le binaire **n'est pas signé**. Windows/Chrome peuvent afficher des avertissements ; ne désactivez pas Defender, SmartScreen ou Safe Browsing. En cas de blocage de téléchargement, ne forcez pas son exécution : privilégiez l'inspection et une compilation locale depuis les sources officielles. Un scan antivirus négatif ne constitue pas une garantie d'innocuité.

## Compiler / tester depuis les sources

- Compiler sans installer : `BUILD-RELEASE-V5.0.bat` (Windows, compilateur C# .NET Framework disponible).
- Compiler et demander l'installation : `PREPARER-ET-INSTALLER-V5.0.bat`.
- Autotests du binaire : `SHINO-CODEX-MONITOR.exe --self-test`.
- Audit statique : `python -m unittest discover -s tests -p "test_*.py"`.
- Build Windows officiel : `.github/workflows/build-windows.yml`. La publication doit porter sur un commit testé.

## Désinstaller

Menu Démarrer → « Désinstaller SHINO Codex Monitor », ou « Applications installées » Windows. La désinstallation demande confirmation et conserve par défaut les données locales.

## Limites

La disponibilité des fenêtres et resets dépend des données réellement renvoyées par Codex. Les relevés périmés doivent apparaître comme tels. Cette application est communautaire et **non officielle**, sans affiliation avec OpenAI. Les binaires ne sont pas signés ; vérifier la provenance des artefacts.

Voir [README-FR.txt](README-FR.txt) pour la procédure complète.

**Licence :** source visible publiquement pour l'instant ; aucune licence de réutilisation n'a encore été choisie par le propriétaire. Public ne signifie pas automatiquement « open source sous licence ».

## Vérifier les mises à jour

Clic droit sur l'icône `%` → **Vérifier les mises à jour**. L'application effectue
un appel HTTPS en lecture seule vers l'API publique GitHub Releases du dépôt.
Elle compare la version de l'exécutable à la dernière Release **stable**,
sans lire le compte Codex, sans jeton GitHub et sans téléchargement automatique.
Si une version plus récente existe, tu peux ouvrir sa page officielle.
Le programme actuel continue de fonctionner si GitHub est indisponible.

**Publication :** seules les Releases visibles dans la page officielle doivent être considérées comme publiées.

## Avertissement de sécurité au démarrage (V4.7.1)

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

## V4.7 Circular Dashboard

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

Les dispositions restent sélectionnables indépendamment ; aucune valeur simulée ne doit être présentée comme quota réel.

## V4.7.1 — Apparence & disposition

Clic droit sur le tray → **Apparence** : opacité 70/80/90/96/100 %, choix de quatre fonds sombres ou couleur personnalisée via `ColorDialog`. Les fonds trop clairs sont refusés afin de conserver le contraste des états et des resets. Les préférences sont persistées dans le `settings.json` local ; les anciennes dispositions et données ne sont pas modifiées.

Le mode **Micro** réserve une marge au bouton de fermeture ; le mode **Circulaire** réduit la fenêtre à 540 × 278 et agrandit les anneaux. Version affichée dans le diagnostic : version réelle du binaire. Les mises à jour visuelles ont été vérifiées sur Windows ; les variations de DPI et le dernier correctif de reset bloqué restent à confirmer.

### Réinitialisation lorsque Codex indique « usage bloqué »

Dans la V4.7.1, le menu **Réinitialisations** reste fonctionnel si `ordinaryUsageAllowed=false` et si les quotas sont lus directement et récemment : le message de blocage apparaît en pied de widget, mais la date et/ou le compte à rebours du reset restent affichés sous chaque fenêtre **uniquement si** `resetsAt` a réellement été communiqué par Codex. Sinon : « Reset non communiqué ». Un relevé ancien n'affiche pas son ancien reset comme une donnée fraîche. Les choix « Date/heure + décompte », « Date/heure uniquement », « Décompte uniquement » et « Masquées » restent indépendants du statut bloqué.

## V4.7.2 — Identification fiable des mises à jour (candidate)

La V4.7.1 avait reçu un correctif après qu'un premier exécutable 4.7.1 eut déjà été installé localement. Le vérificateur de mises à jour compare les numéros `Major.Minor.Build` et ne peut pas distinguer deux exécutables différents avec **le même numéro**. La V4.7.2 incrémente explicitement ce numéro dans l'assembly, les libellés Windows, le rapport de tests et l'installateur. Les noms ZIP et scripts restent `V4_7` pour assurer la compatibilité avec le téléchargeur vérifié.

Les User-Agent des requêtes GitHub utilisent désormais automatiquement la version de l'exécutable ; aucune collecte Codex, préférence ou historique local n'est modifié.

La candidate V4.7.2 a passé la CI Windows. Le test de détection de mise à jour sur le PC utilisateur reste à faire avant publication. Une nouvelle Release n'est **pas** créée automatiquement.

## V4.8.0 — Desktop Experience (intégré à V5)

- **Redimensionnement des roues** : taille circulaire de 470 à 720 px de large. Glisser la poignée en bas à droite du widget épinglé, ou utiliser **Apparence → Taille des roues** (480, 540, 620, 700 px). Les autres modes conservent leurs dimensions et leur fonctionnement.
- **Aimantation multi-écran** facultative : **Fenêtre → Aimantation aux bords des écrans** ; lors du relâchement après déplacement/redimensionnement, le widget s'aligne à 20 px près des limites utiles de l'écran Windows le plus proche. Désactivée par défaut pour respecter les positions existantes.
- **Opacité affinée** : choix de 70, 75, 80, 85, 90, 95, 96, 98 et 100 %. La couleur de fond et les seuils personnalisés restent disponibles.
- **Notifications par fenêtre** : **Notifications → Fenêtre 5 heures / Fenêtre 7 jours**. Les alertes existantes 50/25/10 % et reset ne s'appliquent qu'aux fenêtres cochées et à deux relevés valides ; aucune alerte fondée sur une simple estimation du compte à rebours.
- **Migration sans perte** : nouvelles clés `CircularWidth`, `SnapToEdges`, `AlertFive` et `AlertWeek` dans `settings.json`, avec valeurs par défaut compatibles. Historique et authentification Codex inchangés.

**Validation :** tests automatisés et validation utilisateur du fonctionnement général. Les scénarios DPI 100/150/200 %, bascule d'écran et reprise après veille doivent encore être vérifiés systématiquement.

## V4.8 — Menu tray réorganisé après revue vidéo

Clic droit sur l'icône de zone de notification : les actions **Afficher / masquer** et **Actualiser maintenant** restent immédiates. Les options sont regroupées par fonction :

| Groupe | Contenu |
| --- | --- |
| **Quotas & données** | Tableau officiel Codex, réinitialisations, fréquence d'actualisation |
| **Apparence** | Disposition, taille des roues, opacité, couleur du fond, couleurs des jauges, **Tray Icon** |
| **Fenêtre** | Épinglage, aimantation, toujours au-dessus, barre des tâches |
| **Notifications** | Activation globale, fenêtres 5 h / 7 j, seuils et resets |
| **Historique** | Consulter, enregistrer, ouvrir le dossier local |
| **Application** | Mise à jour, Releases GitHub, démarrage avec Windows, diagnostic |

**Quitter complètement** reste au premier niveau. Le libellé « Démarrer V4 avec Windows » devient « Démarrer avec Windows », sans activer l'autostart. « Rétablir l'apparence » est clarifié en **« Réinitialiser fond et opacité »**, afin de ne pas suggérer une remise à zéro des autres réglages. La sélection du fond, de l'opacité, de la disposition et des tailles est recalculée lors de l'ouverture des sous-menus, y compris après un redimensionnement manuel. Les paramètres existants et leurs clés JSON restent inchangés.

## V4.9.0 — Recovery & Version Identity (intégré à V5)

- Restaurer un widget enregistré **entièrement sur un écran connecté** : si un moniteur externe a été débranché ou déplacé, les coordonnées sauvegardées sont bornées à la zone utilisable de l'écran Windows le plus proche. Les dispositions du widget et les autres préférences restent inchangées.
- Corriger le libellé initial de la **Tray Icon**, qui indiquait encore V4.7.2 en dur dans le code V4.8. La version initiale vient désormais de l'assembly.
- Quatre autotests C# supplémentaires sur récupération hors écran, deuxième écran à coordonnées négatives, disposition verticale et dimensions ; cinq audits Python ciblés.
- Historique V4.9 conservé dans Git. Les scripts de distribution publique sont ceux de **V5.0**.

## V5 — principes de périmètre

V5 doit rester un moniteur de quotas **Codex ChatGPT local Windows**, sans mélange avec l'API OpenAI, l'utilisation du chat classique ni un tableau de bord hébergé. Objectifs proposés : (1) robustesse du monitoring et indicateurs de source/fraîcheur par fenêtre ; (2) parcours Windows stable, accessibilité/DPI/multi-écran ; (3) mise à jour vérifiée et réversible sans créer de service ou abonnement. Aucune nouvelle source de données non documentée sans audit spécifique et accord. La première tranche V5 livre déjà le rapport local de santé des données ; les suites du MVP sont suivies dans l'issue GitHub #9.

## V5.0.0 — Première tranche « Data Trust & Health »

**Quotas & données → État des données Codex** ouvre un rapport local en lecture seule : origine réelle Codex CLI, état de fraîcheur, âge/heure locale du dernier relevé, échecs, dernier message d'erreur et prochaine tentative. Il décrit séparément les fenêtres 5 h / 7 jours seulement si leurs valeurs ont réellement été communiquées ; ne fabrique ni pourcentage ni reset. Une donnée ancienne est explicitement étiquetée comme ancienne ; l'alerte `Usage bloqué` demeure distincte des resets. Aucune requête payante, aucun accès à auth.json, cookie ou mot de passe n'est ajouté.

**Limites résiduelles de V5.0.0** : le rapport est une boîte de dialogue WinForms, pas encore un panneau dédié. Les DPI multiples, la veille, le reset réel en usage bloqué, la comparaison simultanée avec le tableau officiel Codex et le parcours One-Click Update de V4.7.2 vers V5 demandent encore des essais spécifiques. La suite du produit est suivie dans [GitHub #9](https://github.com/shinobione/SHINO-CODEX-MONITOR/issues/9).

## V5.1 — Live Task Monitor (branche de développement, NON publiée)

**Statut : candidate à valider.** Cette fonction est séparée des quotas officiels et ne
change pas `account/rateLimits/read`. Elle exploite **en lecture seule** les événements
persistés par les sessions Codex locales, sous `%USERPROFILE%\.codex\sessions`
(ou `%CODEX_HOME%\sessions` si défini). Il ne s'agit **pas** d'une API live
officielle documentée : la compatibilité dépend de la version du CLI et de Codex Desktop.

- Clic sur l'indicateur **TASKS** dans le widget ou clic droit tray → **Tâches Codex · LIVE**
  → **Ouvrir le moniteur d'activité** : jusqu'à trois tâches locales récentes, durée,
  projet (nom du dernier dossier seulement), modèle et effort **quand réellement enregistrés**.
- Une roue violette vectorielle indique une activité **observée** ; coche verte après
  `task_complete`, croix rouge si le même événement signale explicitement une erreur,
  orange si `turn_aborted`. Une absence de fin n'est jamais convertie en succès.
  Si le fichier ne reçoit plus d'événement depuis **90 secondes**, l'état devient
  **À vérifier** : cela ne prouve pas que le travail est arrêté.
- Les changements confirmés vers un état terminal peuvent afficher une notification
  Windows, uniquement si un tour était vu comme actif lors du sondage précédent.
  Aucune notification n'est envoyée pour un ancien succès découvert au démarrage.
- Les deux options **Notifier les tâches terminées** et **Animations** sont
  indépendantes des seuils de quota ; les notifications restent soumises à la
  préférence générale. L'animation est arrêtée lorsque le panneau est masqué ;
  la tray conserve une icône statique (jauge quota + petit point de statut).
- Polling local toutes les 5 secondes, lecture bornée à 12 sessions récentes et
  512 Kio par fichier. Aucune écriture des logs Codex, de leur contenu de conversation,
  de jetons ni de cookies. Pas d'appel réseau ni de modèle supplémentaire.
- **Limites** : seules les sessions locales avec fichiers JSONL lisibles sont
  observables ; tâches Cloud et sessions dont les événements sont absents,
  tronqués ou non persistés sont **indisponibles**. Le statut reste indicatif :
  la fraîcheur d'un fichier n'est pas une confirmation indépendante du processus.
  Le temps d'attente d'une validation humaine et l'avancement en pourcentage ne sont
  pas exposés fiablement ici. Aucun 100 % artificiel ni décompte inventé.
- **Tests hors ligne** : `SHINO-CODEX-MONITOR.exe --live-task-self-test` produit
  `V5_TASK_TEST_REPORT.txt` dans le dossier de l'exécutable (13 fixtures synthétiques).
  Compilation Windows : `BUILD-RELEASE-V5.0.ps1` dans cette branche ; le nom du script
  historique n'est **pas** une publication V5.1. Exiger la CI et un essai utilisateur
  sur Windows avant tout packaging/installateur/versionnage V5.1 public.
- **Dépannage** : si « Aucune session » apparaît, vérifier que Codex est lancé
  localement, qu'il a persisté une session JSONL, et la variable `CODEX_HOME`.
  Ne jamais copier `auth.json` ni partager les journaux bruts. Pour revenir à la
  version stable, continuer à utiliser la Release officielle V5.0.0 sans installer
  cette candidate ; elle ne modifie aucune session Codex ni historique de quotas.
