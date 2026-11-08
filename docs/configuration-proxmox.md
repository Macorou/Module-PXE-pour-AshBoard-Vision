# Configurer Proxmox Monitor

## 1. Préparer un accès de lecture

Dans Proxmox, crée un compte de monitoring dédié et activé, par exemple `monitoring@pve`. Le module ne demande pas son mot de passe : il utilisera un jeton API.

Dans **Datacenter → Permissions**, attribue à ce compte :

- Chemin : `/`.
- Rôle : `PVEAuditor`.
- Propagation : activée.

Dans **Permissions → API Tokens**, crée un jeton pour ce compte, par exemple nommé `ashboard`, avec **Privilege Separation** activée. Proxmox affiche son secret lors de sa création ; conserve-le pour la saisie dans le module.

Ajoute aussi une permission pour le jeton complet `monitoring@pve!ashboard` : même chemin `/`, même rôle `PVEAuditor`, propagation activée. Les permissions effectives sont l’intersection de celles du compte et du jeton. Une permission ajoutée au compte seul ne suffit donc pas pour un jeton séparé.

Le rôle de lecture permet de vérifier que les listes sont complètes. Les ACL sont lues pour détecter également les restrictions `NoAccess` que Proxmox peut masquer dans ses réponses. Le module ne crée ni compte, ni jeton, ni permission.

Références : [documentation officielle des jetons](https://github.com/proxmox/pve-docs/blob/master/pveum.adoc), [API de permissions](https://github.com/proxmox/pve-access-control/blob/master/src/PVE/API2/AccessControl.pm).

## 2. Remplir les paramètres du module

**Adresse du serveur**, par exemple :

```text
https://pve.example:8006
```

Remplace cet exemple par ton adresse réseau. Utilise HTTPS et le bon port ; aucun identifiant, mot de passe, paramètre d’URL ou chemin personnalisé. L’API doit être directement accessible sous `/api2/json/`. Un reverse proxy doit exposer ces routes à la racine, sans redirection. `localhost`, les boucles locales et le proxy système ne sont pas pris en charge dans cette version.

**Jeton API complet**, dans l’unique champ masqué :

```text
monitoring@pve!ashboard=VALEUR_DU_SECRET
```

La partie avant `=` est l’identifiant du jeton ; la partie après est son secret. Ne saisis pas le préfixe HTTP `Authorization:` ou `PVEAPIToken=`. Le logiciel conserve ce champ dans son coffre chiffré ; il ne voyage pas dans une disposition exportée.

## 3. Certificat HTTPS

Si le certificat est reconnu par Windows, laisse **Certificat auto-signé** désactivé.

Pour un certificat local non reconnu, active cette option puis lance **Tester la connexion**. Le test prépare d’abord la connexion sans jeton, mémorise l’empreinte par le SDK et vérifie sa persistance avant de s’authentifier. Un autre certificat non reconnu est ensuite refusé pour cette adresse.

Lors d’un renouvellement connu d’un certificat auto-signé, utilise **Oublier le certificat mémorisé**, confirme, puis relance le test. Cela invalide la liaison et n’efface ni ne remplace aucun certificat sur Proxmox. Un certificat devenu valide selon Windows reste accepté selon la politique du SDK.

## 4. Tester puis choisir le disque

Clique sur **Tester la connexion**. Le message de réussite identifie l’hôte et le nombre de stockages découverts. Enregistre les paramètres, puis ferme complètement et rouvre la fenêtre des paramètres d’AshBoard Vision pour charger la nouvelle liste. Choisis alors le disque si tu souhaites autre chose que **Système Proxmox**, par exemple **local-lvm**.

Le SDK charge cette liste à l’ouverture du champ ; le bouton de test ne la recharge pas automatiquement. Si aucun stockage n’était proposé avant le test, le bouton **Actualiser** permet aussi de demander la nouvelle liste. Après un changement de serveur ou une nouvelle découverte alors qu’une ancienne liste existe, ferme complètement et rouvre la fenêtre des paramètres ; changer d’onglet ne garantit pas un rechargement. Le choix **Système Proxmox** est fourni une seule fois par le SDK et remet la sélection au disque système.

Après une mise à jour depuis la bêta 1, une ancienne sélection système reste fonctionnelle mais peut ne plus être surlignée. Cliquer une fois sur **Système Proxmox** rétablit son indication de sélection.

La liste des stockages porte le nom de l’hôte testé. Après changement de serveur, teste de nouveau avant de choisir ; le module refuse d’afficher un stockage sélectionné sur une autre origine. Dans la vue Ressources, le système et les stockages sont toujours présentés séparément, avec rotation des longues listes.

Une connexion est partagée entre les tuiles. Tester une autre adresse change la connexion active ; les anciennes tuiles demandent une configuration concordante. La sélection d’un autre stockage ou d’une autre présentation ne change pas le serveur surveillé.

## 5. Vérifier les premières données

Compare avec l’interface Proxmox :

1. CPU/RAM de l’hôte et disque système.
2. Stockage choisi, occupation et capacité.
3. Liste des VM et des LXC, sans les modèles.
4. Nombre d’invités démarrés sur le total, séparément pour VM et conteneurs.

« En ligne » correspond à l’état de démarrage Proxmox, pas à un test de ping ou du service invité. Pause/suspension et données inconnues sont distinguées. Aucune commande de démarrage ou d’arrêt n’est envoyée.

## Résoudre un problème de connexion

| Message ou symptôme | Vérification |
| --- | --- |
| Connexion à tester | Saisir l’adresse et le jeton complet, puis tester/enregistrer |
| Jeton refusé / 401 | Vérifier l’identifiant complet, le secret, l’activation et l’expiration du compte/jeton |
| Droits insuffisants / 403 | PVEAuditor sur compte **et** jeton, avec propagation ; chercher une restriction enfant/pool |
| Plusieurs nœuds | Cette version demande un serveur autonome et ne gère pas les clusters |
| Certificat refusé | Vérifier le mode auto-signé ; oublier seulement une ancienne empreinte dont le renouvellement est connu, puis tester |
| Réseau injoignable | Vérifier URL, port, accessibilité depuis Windows et permissions réseau du module |
| Disque à sélectionner | Refaire le test puis choisir un stockage du serveur actif |
| Valeurs « — » et compteurs inconnus | Les mesures sont absentes ou périmées ; cela ne signifie pas que les invités sont arrêtés |
| Certificat/liaison impossible à mémoriser | Le coffre a une limite SDK de 64 clés/256 Kio ; aucune nouvelle liaison non persistée n’est utilisée |

Après un refus de jeton, de certificat ou de droits, le module attend un nouveau test réussi avant de reprendre les requêtes. Après une panne réseau temporaire, il réessaie automatiquement avec une attente progressive plafonnée à une minute.
