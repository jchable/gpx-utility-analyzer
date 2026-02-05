# CLI Usage — gpx-analyzer

Documentation complète de toutes les commandes, flags et exemples d'utilisation.

---

## Table des matières

- [analyze — Analyser des fichiers GPX](#analyze--analyser-des-fichiers-gpx)
- [split — Découper un GPX par intervalles de temps](#split--découper-un-gpx-par-intervalles-de-temps)
- [merge — Fusionner plusieurs GPX](#merge--fusionner-plusieurs-gpx)
- [Statistiques calculées](#statistiques-calculées)
- [Correction d'élévation](#correction-délévation)
- [Algorithmes de calcul du dénivelé](#algorithmes-de-calcul-du-dénivelé---elevation-algo)
- [Lissage de la trace GPS](#lissage-de-la-trace-gps---track-smoothing)
- [Presets de détection d'arrêts](#presets-de-détection-darrêts)
- [Cas d'usage courants](#cas-dusage-courants)

---

## `analyze` — Analyser des fichiers GPX

Calcule les statistiques complètes d'un ou plusieurs fichiers GPX.

```bash
gpx-analyzer analyze [fichiers...] [flags]
```

**Entrées acceptées** : fichiers `.gpx`, répertoires (analyse tous les `.gpx` qu'ils contiennent), ou patterns glob (`*.gpx`).

### Flags

| Flag | Description | Défaut |
|------|------------|--------|
| `--format` | Format de sortie : `text` ou `json` | `text` |
| `--smoothing` | Lissage d'élévation : `none`, `light`, `medium`, `heavy` | `medium` |
| `--dem-dir` | Répertoire de tuiles SRTM `.hgt` pour correction DEM | _(désactivé)_ |
| `--dem-auto-download` | Télécharger automatiquement les tuiles SRTM manquantes | `true` |
| `--dem-cache` | Répertoire de cache pour les tuiles téléchargées | _(OS cache dir)_ |
| `--elevation-threshold` | Seuil minimum de changement d'élévation (mètres) | `2.0` |
| `--elevation-algo` | Algorithme de dénivelé : `threshold`, `douglas-peucker`, `segments` | `threshold` |
| `--track-smoothing` | Lissage lat/lon de la trace GPS : `none`, `light`, `medium`, `heavy` | `none` |
| `--dp-epsilon` | Douglas-Peucker : déviation verticale max tolérée (mètres) | `3.0` |
| `--seg-min-length` | Segments : longueur min d'un segment (mètres) | `200.0` |
| `--seg-max-deviation` | Segments : résidu RMS max par segment (mètres) | `2.0` |
| `--preset` | Preset de détection d'arrêts : `hiking`, `trail`, `cycling` | `hiking` |
| `--stop-speed` | Surcharge de la vitesse max pour un arrêt (m/s) | _(selon preset)_ |
| `--stop-duration` | Surcharge de la durée min pour un arrêt (ex: `2m`) | _(selon preset)_ |
| `--export` | Exporter les GPX retraités (DEM + lissage) dans ce répertoire | _(désactivé)_ |

### Exemples

**Analyse simple d'un fichier :**

```bash
gpx-analyzer analyze ma-rando.gpx
```

**Analyse de tous les GPX d'un dossier :**

```bash
gpx-analyzer analyze ./mes-traces/
```

**Sortie JSON pour intégration avec d'autres outils :**

```bash
gpx-analyzer analyze ma-rando.gpx --format json
```

```json
{
  "filename": "ma-rando.gpx",
  "total_distance_m": 24532.5,
  "total_distance_km": 24.5,
  "elevation_gain_m": 1250.0,
  "elevation_loss_m": 1180.0,
  "avg_speed_kmh": 4.2,
  ...
}
```

**Extraire une seule valeur avec `jq` :**

```bash
gpx-analyzer analyze ma-rando.gpx --format json | jq '.elevation_gain_m'
```

**Désactiver le lissage d'élévation (données brutes GPS) :**

```bash
gpx-analyzer analyze ma-rando.gpx --smoothing none
```

**Lissage fort pour des données GPS très bruitées :**

```bash
gpx-analyzer analyze trace-montre-gps.gpx --smoothing heavy
```

**Correction d'élévation par DEM (tuiles SRTM) :**

```bash
gpx-analyzer analyze pct.gpx --dem-dir ./srtm-tiles/
```

**Combiner DEM + lissage léger + seuil de 3m :**

```bash
gpx-analyzer analyze pct.gpx --dem-dir ./srtm-tiles/ --smoothing light --elevation-threshold 3
```

**Utiliser le preset vélo pour la détection d'arrêts :**

```bash
gpx-analyzer analyze sortie-velo.gpx --preset cycling
```

**Personnaliser les seuils d'arrêt (vitesse < 0.2 m/s pendant > 5 min) :**

```bash
gpx-analyzer analyze ultra-trail.gpx --stop-speed 0.2 --stop-duration 5m
```

**Analyser plusieurs fichiers avec des patterns glob :**

```bash
gpx-analyzer analyze vacances-*.gpx --format json
```

**Exporter le GPX avec altitudes corrigées par DEM :**

```bash
gpx-analyzer analyze ma-rando.gpx --export ./processed/
```

Produit `./processed/ma-rando_processed.gpx` avec les altitudes DEM + lissage appliqués.

**Exporter après retraitement complet (DEM + segments) :**

```bash
gpx-analyzer analyze pct.gpx --elevation-algo segments --export ./processed/
```

---

## `split` — Découper un GPX par intervalles de temps

Découpe un fichier GPX en segments temporels. Produit un fichier GPX par tranche et affiche les statistiques de chaque segment.

```
gpx-analyzer split <fichier> [flags]
```

### Flags

| Flag | Description | Défaut |
|------|------------|--------|
| `--interval` | Intervalle de découpe (ex: `24h`, `12h`, `30m`) | `24h` |
| `--output-dir` | Répertoire de sortie pour les fichiers GPX | `splits` |
| `--prefix` | Préfixe des noms de fichiers générés | `segment` |
| `--format` | Format de sortie des stats : `text` ou `json` | `text` |
| `--smoothing` | Lissage d'élévation | `medium` |
| `--dem-dir` | Répertoire de tuiles SRTM | _(désactivé)_ |
| `--dem-auto-download` | Télécharger automatiquement les tuiles SRTM manquantes | `true` |
| `--dem-cache` | Répertoire de cache pour les tuiles téléchargées | _(OS cache dir)_ |
| `--elevation-threshold` | Seuil de bruit d'élévation (mètres) | `2.0` |
| `--elevation-algo` | Algorithme de dénivelé : `threshold`, `douglas-peucker`, `segments` | `threshold` |
| `--track-smoothing` | Lissage lat/lon de la trace GPS | `none` |
| `--dp-epsilon` | Douglas-Peucker : déviation verticale max (mètres) | `3.0` |
| `--seg-min-length` | Segments : longueur min d'un segment (mètres) | `200.0` |
| `--seg-max-deviation` | Segments : résidu RMS max (mètres) | `2.0` |
| `--preset` | Preset de détection d'arrêts | `hiking` |
| `--stop-speed` | Surcharge vitesse max pour arrêt (m/s) | _(selon preset)_ |
| `--stop-duration` | Surcharge durée min pour arrêt | _(selon preset)_ |

### Exemples

**Découper une trace multi-jours en segments de 24h :**

```bash
gpx-analyzer split traversee-alpes.gpx
```

Produit :
```
splits/
  segment-001.gpx    # Jour 1
  segment-002.gpx    # Jour 2
  segment-003.gpx    # Jour 3
  ...
```

Chaque segment est accompagné de ses statistiques dans le terminal.

**Découper par demi-journées avec un préfixe personnalisé :**

```bash
gpx-analyzer split gr20.gpx --interval 12h --prefix etape --output-dir gr20-etapes
```

Produit :
```
gr20-etapes/
  etape-001.gpx
  etape-002.gpx
  ...
```

**Découper en tranches de 30 minutes (utile pour analyser un effort) :**

```bash
gpx-analyzer split marathon.gpx --interval 30m --preset trail
```

**Découper avec stats en JSON (pour un traitement automatisé) :**

```bash
gpx-analyzer split tour-du-mont-blanc.gpx --format json > etapes.json
```

**Découper un FKT avec lissage fort et DEM :**

```bash
gpx-analyzer split pct-karel-sabbe.gpx --interval 24h --dem-dir ./srtm/ --smoothing heavy
```

---

## `merge` — Fusionner plusieurs GPX

Combine plusieurs fichiers GPX en un seul. Les points sont triés par ordre chronologique par défaut.

```
gpx-analyzer merge [fichiers...] [flags]
```

### Flags

| Flag | Description | Défaut |
|------|------------|--------|
| `-o`, `--output` | Chemin du fichier de sortie | `merged.gpx` |
| `--sort` | Trier les points par temps | `true` |
| `--analyze` | Afficher les statistiques du résultat fusionné | `false` |
| `--format` | Format de sortie des stats (si `--analyze`) | `text` |
| `--smoothing` | Lissage d'élévation (si `--analyze`) | `medium` |
| `--dem-dir` | Répertoire de tuiles SRTM (si `--analyze`) | _(désactivé)_ |
| `--dem-auto-download` | Télécharger automatiquement les tuiles SRTM manquantes | `true` |
| `--dem-cache` | Répertoire de cache pour les tuiles téléchargées | _(OS cache dir)_ |
| `--elevation-threshold` | Seuil de bruit d'élévation (si `--analyze`) | `2.0` |
| `--elevation-algo` | Algorithme de dénivelé (si `--analyze`) | `threshold` |
| `--track-smoothing` | Lissage lat/lon de la trace GPS (si `--analyze`) | `none` |
| `--dp-epsilon` | Douglas-Peucker : déviation verticale max (si `--analyze`) | `3.0` |
| `--seg-min-length` | Segments : longueur min d'un segment (si `--analyze`) | `200.0` |
| `--seg-max-deviation` | Segments : résidu RMS max (si `--analyze`) | `2.0` |
| `--preset` | Preset de détection d'arrêts (si `--analyze`) | `hiking` |

### Exemples

**Fusionner plusieurs fichiers :**

```bash
gpx-analyzer merge jour1.gpx jour2.gpx jour3.gpx -o randonnee-complete.gpx
```

**Fusionner tous les GPX d'un dossier et afficher les stats :**

```bash
gpx-analyzer merge ./traces-vacances/ -o vacances.gpx --analyze
```

**Fusionner les segments d'un split précédent :**

```bash
gpx-analyzer merge ./splits/ -o reconstitue.gpx --analyze
```

**Fusionner sans trier (garder l'ordre des fichiers) :**

```bash
gpx-analyzer merge a.gpx b.gpx c.gpx -o concat.gpx --sort=false
```

**Fusionner avec analyse JSON et DEM :**

```bash
gpx-analyzer merge ./etapes/ -o complet.gpx --analyze --format json --dem-dir ./srtm/
```

---

## Statistiques calculées

| Catégorie | Statistiques |
|-----------|-------------|
| **Distance** | Distance totale 2D (Haversine), distance 3D (avec pente) |
| **Dénivelé** | D+ / D- (3 algorithmes au choix), altitude max, altitude min |
| **Temps** | Durée totale, temps en mouvement, temps à l'arrêt, date de début, date de fin |
| **Vitesse** | Vitesse moyenne, vitesse moyenne en mouvement, vitesse max |
| **Allure** | Allure moyenne (min/km), allure moyenne en mouvement |
| **Arrêts** | Nombre d'arrêts, durée totale, arrêt le plus long, durée moyenne |
| **Métadonnées** | Nombre de points, nombre de segments, densité de points par km |

---

## Correction d'élévation

Les altitudes GPS brutes sont souvent très bruitées (erreur de 10 à 50 mètres courant). Cela gonfle artificiellement le D+ et le D-. L'outil propose deux mécanismes de correction, cumulables.

### Lissage logiciel (`--smoothing`)

Filtre en deux passes appliqué aux données d'élévation avant tout calcul :

1. **Filtre médian** — supprime les spikes isolés (un point aberrant est remplacé par la valeur médiane de ses voisins)
2. **Moyenne glissante** — lisse le bruit haute fréquence restant

| Preset | Fenêtre médiane | Fenêtre moyenne | Usage recommandé |
|--------|----------------|-----------------|------------------|
| `none` | _(désactivé)_ | _(désactivé)_ | Données déjà propres ou debug |
| `light` | 3 points | 3 points | GPS de bonne qualité (Garmin récent) |
| `medium` | 5 points | 5 points | Usage général (défaut) |
| `heavy` | 7 points | 11 points | GPS très bruité (montre, téléphone) |

### Correction DEM/SRTM

Remplace les altitudes GPS par celles d'un modèle numérique de terrain (NASA SRTM). C'est la méthode la plus précise.

#### Téléchargement automatique (par défaut)

Par défaut, les tuiles SRTM manquantes sont **téléchargées automatiquement** depuis le service AWS Elevation Tiles (SRTM1, résolution 30m quand disponible). Les tuiles sont mises en cache localement :

- **Windows** : `%LOCALAPPDATA%\gpx-utility-analyzer\srtm\`
- **macOS** : `~/Library/Caches/gpx-utility-analyzer/srtm/`
- **Linux** : `~/.cache/gpx-utility-analyzer/srtm/`

```bash
# Fonctionne directement, les tuiles sont téléchargées à la volée
gpx-analyzer analyze ma-rando.gpx
```

Pour désactiver le téléchargement automatique :

```bash
gpx-analyzer analyze ma-rando.gpx --dem-auto-download=false
```

Pour changer le répertoire de cache :

```bash
gpx-analyzer analyze ma-rando.gpx --dem-cache /path/to/cache
```

#### Tuiles locales (`--dem-dir`)

Pour utiliser des tuiles SRTM1 (30m, plus précises) ou travailler hors-ligne :

1. Télécharger les tuiles SRTM couvrant votre trace depuis [NASA Earthdata](https://earthexplorer.usgs.gov/) ou [CGIAR-CSI](https://srtm.csi.cgiar.org/)
2. Placer les fichiers `.hgt` dans un dossier (ex: `./srtm/`)
3. Passer `--dem-dir ./srtm/`

```bash
gpx-analyzer analyze pct.gpx --dem-dir ./srtm-tiles/
```

Les fichiers sont au format HGT standard (SRTM1 à 30m ou SRTM3 à 90m de résolution). Le nommage suit la convention `N48W003.hgt` (coordonnées du coin sud-ouest de la tuile).

Quand `--dem-dir` est fourni avec `--dem-auto-download` (défaut), les tuiles locales sont prioritaires. Si une tuile est absente localement, elle est téléchargée dans le cache. Si le téléchargement échoue, l'altitude GPS est conservée avec un avertissement.

#### Limitations

- Le téléchargement automatique nécessite une connexion internet
- Le service AWS fournit des tuiles SRTM1 (30m) entre 60°N et 56°S, et SRTM3 (90m) ailleurs

**Exemple : impact sur une trace de 4000+ km (PCT de Karel Sabbe)**

| Configuration | D+ | Max altitude |
|--------------|-----|-------------|
| `--smoothing none` | 599 323 m | 7 583 m |
| `--smoothing medium` (défaut) | 226 908 m | 5 720 m |
| `--smoothing heavy` | 155 015 m | 5 645 m |
| DEM + `--smoothing medium` + seuil 5m | ~126 000 m | ~4 001 m |
| DEM + `--elevation-algo segments` | **~104 000 m** | ~4 001 m |

Le D+ réel du PCT est d'environ 96 000 m. L'algorithme `segments` combiné au DEM donne le résultat le plus proche.

---

## Algorithmes de calcul du dénivelé (`--elevation-algo`)

Trois algorithmes sont disponibles pour calculer le D+ et le D-. Ils s'appliquent après le lissage d'élévation (`--smoothing`) et la correction DEM.

### `threshold` (défaut)

Accumule le D+/D- uniquement quand le changement d'élévation depuis le dernier point de référence dépasse le seuil (`--elevation-threshold`). Simple et efficace pour filtrer le bruit GPS.

```bash
gpx-analyzer analyze trace.gpx --elevation-algo threshold --elevation-threshold 3
```

### `douglas-peucker`

Simplifie le profil altimétrique (distance cumulée, altitude) par l'algorithme de Douglas-Peucker, puis calcule le D+/D- sur les points retenus. L'epsilon (`--dp-epsilon`) contrôle la déviation verticale maximale tolérée en mètres.

```bash
gpx-analyzer analyze trace.gpx --elevation-algo douglas-peucker --dp-epsilon 3
```

Fonctionne bien sur des données GPS sans DEM. Avec DEM, le profil terrain conserve beaucoup de micro-variations légitimes, ce qui limite l'efficacité du filtre.

### `segments`

Découpe le profil en segments de pente quasi-constante par régression linéaire gloutonne. Le D+/D- est calculé sur les élévations ajustées (fitted) aux extrémités de chaque segment.

```bash
gpx-analyzer analyze trace.gpx --elevation-algo segments --seg-min-length 200 --seg-max-deviation 2
```

| Paramètre | Description | Défaut |
|-----------|------------|--------|
| `--seg-min-length` | Longueur horizontale minimale d'un segment (mètres) | `200.0` |
| `--seg-max-deviation` | Résidu RMS maximal avant de couper un segment (mètres) | `2.0` |

C'est l'algorithme le plus efficace avec des données DEM : il absorbe le bruit de grille SRTM et donne des résultats proches de la réalité terrain.

---

## Lissage de la trace GPS (`--track-smoothing`)

Applique une moyenne glissante sur les coordonnées lat/lon **avant** la correction DEM. Réduit le bruit horizontal GPS qui cause des oscillations artificielles d'altitude quand les points oscillent entre différentes cellules DEM.

| Preset | Fenêtre | Usage |
|--------|---------|-------|
| `none` | _(désactivé)_ | Défaut, pas de lissage lat/lon |
| `light` | 3 points | GPS de bonne qualité |
| `medium` | 5 points | GPS standard |
| `heavy` | 9 points | GPS très bruité |

```bash
gpx-analyzer analyze trace.gpx --track-smoothing medium --elevation-algo douglas-peucker
```

**Attention** : le lissage lat/lon modifie les coordonnées utilisées pour le calcul de distance et la détection d'arrêts. La distance totale sera légèrement réduite (le bruit horizontal est filtré).

### Pipeline complet

L'ordre de traitement est :

```
Track smoothing (lat/lon) → Correction DEM → Lissage élévation (--smoothing) → Calcul distances → Algorithme dénivelé
```

---

## Presets de détection d'arrêts

| Preset | Vitesse max | Durée min | Usage |
|--------|------------|-----------|-------|
| `hiking` | 0.3 m/s (1.1 km/h) | 2 min | Randonnée, marche |
| `trail` | 0.5 m/s (1.8 km/h) | 1 min | Trail, course en montagne |
| `cycling` | 1.0 m/s (3.6 km/h) | 30 sec | Vélo, VTT |

Un arrêt est détecté quand la vitesse calculée (distance entre points / temps écoulé) reste en dessous du seuil pendant au moins la durée minimum. Les seuils sont personnalisables avec `--stop-speed` et `--stop-duration`.

---

## Cas d'usage courants

### Analyser une randonnée à la journée

```bash
gpx-analyzer analyze rando-chartreuse.gpx
```

### Analyser un ultra-trail avec détection d'arrêts fine

```bash
gpx-analyzer analyze utmb.gpx --preset trail --stop-duration 30s
```

### Découper et analyser un trek multi-jours

```bash
# Découper en jours
gpx-analyzer split gr20-complet.gpx --interval 24h --output-dir gr20-jours

# Voir les stats de chaque jour séparément
gpx-analyzer analyze ./gr20-jours/

# Reconstituer et vérifier
gpx-analyzer merge ./gr20-jours/ -o gr20-verifie.gpx --analyze
```

### Comparer les stats avec et sans lissage

```bash
gpx-analyzer analyze trace.gpx --smoothing none
gpx-analyzer analyze trace.gpx --smoothing heavy
```

### Pipeline automatisé (JSON + jq)

```bash
# Extraire la distance de chaque fichier
for f in *.gpx; do
  dist=$(gpx-analyzer analyze "$f" --format json | jq '.total_distance_km')
  echo "$f: ${dist} km"
done

# Obtenir le D+ total d'un dossier
gpx-analyzer merge ./traces/ -o /dev/null --analyze --format json | jq '.elevation_gain_m'
```

### Obtenir le D+ le plus précis possible (DEM + segments)

```bash
gpx-analyzer analyze pct.gpx --elevation-algo segments
```

### Comparer les algorithmes de dénivelé

```bash
gpx-analyzer analyze trace.gpx --elevation-algo threshold --elevation-threshold 5
gpx-analyzer analyze trace.gpx --elevation-algo douglas-peucker --dp-epsilon 3
gpx-analyzer analyze trace.gpx --elevation-algo segments
```

### Réduire le bruit horizontal GPS avant correction DEM

```bash
gpx-analyzer analyze trace.gpx --track-smoothing medium --elevation-algo segments
```

### Exporter un GPX avec altitudes corrigées

```bash
# Exporter avec correction DEM pour utiliser dans un autre outil
gpx-analyzer analyze ma-rando.gpx --export ./processed/

# Exporter avec le meilleur retraitement possible
gpx-analyzer analyze pct.gpx --elevation-algo segments --smoothing medium --export ./clean/
```

Le fichier exporté contient les coordonnées et altitudes après l'ensemble du pipeline de retraitement (lissage lat/lon, correction DEM, lissage élévation). Il peut être importé dans n'importe quel outil compatible GPX.

### Analyser une sortie vélo

```bash
gpx-analyzer analyze sortie-col.gpx --preset cycling --smoothing light
```
