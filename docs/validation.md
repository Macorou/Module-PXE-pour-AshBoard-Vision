# Validation de Proxmox Monitor 0.1.0

Cette fiche distingue les vérifications locales d’un essai sur une installation réelle.

## Promotion en version stable 0.1.0

La version 0.1.0 reprend le code fonctionnel de la bêta 2, avec sa correction du sélecteur de disque confirmée par l’utilisateur. Seuls le numéro de version, les métadonnées de distribution et les références de téléchargement changent pour la promotion stable demandée le 8 novembre 2026.

Le manifeste, la bibliothèque et le paquet portent tous la version `0.1.0`. La validation Release réussit avec **175/175 tests**, aucun ignoré, aucune erreur ni avertissement de compilation. Le contrôle officiel confirme la compatibilité SDK 1.0, les six PNG, le maintien en fonctionnement avec permissions réseau refusées et l’arrêt propre inférieur à 0,1 s. Les limites de validation Proxmox et les points mineurs documentés ci-dessous restent applicables.

## Correction du sélecteur de stockage — bêta 2

Un signalement utilisateur montrait deux choix « Système Proxmox » sans `local-lvm`. La lecture du catalogue non secret de l’installation confirme pourtant quatre stockages découverts, dont `local-lvm`. Aucun jeton ni coffre n’a été lu pendant ce diagnostic, et aucune requête n’a été faite au serveur par l’agent.

Cause recoupée avec `LookupFieldViewModel` du SDK : une liste non cherchable est chargée une fois à l’ouverture ; le bouton Actualiser n’est disponible que si le module ne retourne aucun résultat. Le module retournait son propre choix système en plus de `emptyLabel`, dupliquant la ligne et empêchant le rechargement après une première découverte.

Correction : le module retourne uniquement les stockages validés ; le SDK fournit le choix système. Avant découverte, la liste de résultats est vide et le bouton Actualiser reste disponible. Après découverte, tous les stockages sont retournés, dont `local-lvm`. L’aide et le guide expliquent aussi la réouverture des paramètres pour charger une nouvelle liste.

Deux tests de régression ont échoué avant correction puis passent ; les six tests des actions/recherches passent. Vérification Release complète : **175/175 tests réussis**, aucun ignoré, construction sans erreur ni avertissement. Le contrôle officiel déclare la bêta 2 compatible SDK 1.0 : première image en 0,3 s, six PNG, refus des permissions réseau supporté, dessin moyen 1,5 ms, CPU machine 0,1 %, mémoire privée maximale 19,4 Mio, arrêt propre inférieur à 0,1 s.

Relecture indépendante ciblée par GPT-6.1 en effort très élevé : aucun point critique ou important. Nuance mineure acceptée, expliquée dans le guide : une ancienne valeur explicite système reste fonctionnelle mais peut perdre son surlignage jusqu’à ce que l’utilisateur reclique le choix système. Aucun changement de format des réglages ni migration du coffre.

Le 8 novembre 2026, l’utilisateur a confirmé que la correction fonctionne dans son installation. Cette confirmation porte sur le problème signalé de sélection du disque ; elle ne remplace pas une comparaison de toutes les mesures ni une validation version par version de Proxmox.

## Référence de validation de la bêta 1

## Vérifications locales

- SDK officiel 1.0.0, commit Canary `69227f73ead5fc6406a6acd80edc9c2b056b4fae`.
- Construction Release C#/.NET 10 : 0 erreur, 0 avertissement ; dépendances verrouillées.
- Après corrections de la relecture : **173 tests réussis sur 173**, aucun ignoré, sur Windows le 8 novembre 2026. La suite comprenait 170 tests avant les trois tests de régression ajoutés ci-dessous.
- Tests des réglages, liaison serveur/jeton, droits complets/restreints, quotas, calculs, états, réponses partielles, cadence, annulation, fraîcheur et rendu.
- Deux essais du vrai client HTTP SDK avec serveurs TLS localhost : pin initial/remplacement de certificat et absence d’Authorization après redirection.
- PNG inspectés des quatre formats, thèmes SDK, contrastes clair/sombre de test, texte agrandi, état déconnecté et stockages paginés.
- Vérification du contenu du paquet : DLL, manifeste public, licence ; aucune configuration privée.

Le banc officiel `ashboard-module check` déclare le module **compatible avec AshBoard Vision (SDK 1.0)** : démarrage enfermé en 0,3 s, six PNG (deux tailles × trois thèmes), maintien en fonctionnement quand chacune des permissions réseau est refusée, arrêt propre inférieur à 0,1 s. Le passage complet après corrections mesure 1,4 ms par dessin en moyenne, 0,1 % de CPU machine et 19,7 Mio privés au maximum pendant 30,1 s. Ces mesures portent sur le module non configuré.

Le premier lancement du banc sur une sortie appartenant au compte isolé de Codex a échoué à l’attribution des droits AppContainer. Une copie neuve possédée par le compte Windows qui exécute le banc a résolu ce problème sans modifier l’application. `verify.ps1` effectue désormais cette copie dédiée.

## Relecture indépendante

Relecture du projet complet par **GPT-6.1, effort très élevé**, avec contexte neuf, en lecture seule. Aucun défaut critique identifié ; deux défauts importants corrigés après reproduction par des tests échouant avant correction :

- Le test de connexion capture désormais le jeton avant l’attente du verrou et revérifie son empreinte après cette attente et après la sonde TLS. Un remplacement pendant l’attente ou la sonde ne déclenche aucun envoi authentifié du test.
- Après validation des droits, l’expiration du budget global conserve les catégories déjà reçues ; seules les routes annulées deviennent indisponibles. Les horodatages des réponses réussies sont actualisés indépendamment. L’annulation lors de la fermeture continue d’empêcher la publication.

Les trois tests de régression ciblés passent. La vérification Release complète après corrections passe également : 173/173 tests et contrôle SDK officiel compatible, sans erreur ni avertissement. Aucune seconde relecture indépendante n’est revendiquée.

Point mineur reporté pour cette bêta : si l’enregistrement d’une nouvelle liaison échoue dans le coffre après remplacement du catalogue, la liste de choix des stockages de la liaison précédente peut ne plus être proposée. La vérification des révisions empêche d’utiliser le catalogue d’un autre serveur ; relancer un test réussi de la connexion active restaure cette liste.

## Limites actuelles

L’utilisateur a installé le module et un catalogue de découverte est présent sur son PC. La version exacte de son Proxmox et la concordance de toutes les mesures n’ont pas été vérifiées par l’agent. Les réponses réseau des tests métier restent fictives ; VE 8 et 9 ne sont donc pas déclarées validées en exploitation.

Le banc `ashboard-module check` utilise les paramètres vides : il vérifie le démarrage, le rendu non configuré, les permissions refusées et l’arrêt, sans mesurer une collecte authentifiée sur un hôte réel.

Le délai d’arrêt inférieur à cinq secondes est vérifié avec un SDK et un service parent fonctionnels. Un blocage externe des appels synchrones du coffre/IPC du SDK peut dépasser ce délai ; ce cas n’a pas été validé et n’est pas couvert par la mesure d’arrêt du banc.

Avant de déclarer une version Proxmox validée, noter sa version exacte, celle d’AshBoard Vision et du module, puis comparer host/stockages/VM/LXC/compteurs avec l’interface Proxmox. Cette comparaison reste en lecture seule.
