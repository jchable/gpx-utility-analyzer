# gpx-analyzer

Outil en ligne de commande pour analyser des fichiers GPX : distance, dénivelé, vitesse, détection d'arrêts, découpage temporel et fusion de fichiers.

## Installation

```bash
go install github.com/jchable/gpx-utility-analyzer@latest
```

Ou depuis les sources :

```bash
git clone https://github.com/jchable/gpx-utility-analyzer.git
cd gpx-utility-analyzer
go build -o gpx-analyzer .
```

## Utilisation

### Analyser un fichier GPX

```bash
gpx-analyzer analyze track.gpx
gpx-analyzer analyze track.gpx --format json
gpx-analyzer analyze *.gpx
gpx-analyzer analyze ./mes-traces/
```

### Options d'analyse

| Flag | Description | Défaut |
|------|------------|--------|
| `--format` | Format de sortie : `text` ou `json` | `text` |
| `--preset` | Preset de détection d'arrêts : `hiking`, `trail`, `cycling` | `hiking` |
| `--stop-speed` | Vitesse max pour un arrêt (m/s) | selon preset |
| `--stop-duration` | Durée min pour un arrêt (ex: `2m`) | selon preset |
| `--elevation-threshold` | Seuil de bruit pour le dénivelé (mètres) | `2.0` |

### Découper un GPX par intervalles de temps

```bash
gpx-analyzer split track.gpx --interval 24h
gpx-analyzer split track.gpx --interval 12h --output-dir jour-par-jour --prefix etape
```

Produit un fichier GPX par tranche + affiche les statistiques de chaque segment.

### Fusionner plusieurs GPX

```bash
gpx-analyzer merge jour1.gpx jour2.gpx jour3.gpx -o complet.gpx
gpx-analyzer merge ./splits/ -o complet.gpx --analyze
```

## Statistiques calculées

- **Distance** : 2D (Haversine) et 3D (avec pente)
- **Dénivelé** : D+ / D- avec filtre de bruit, altitude max/min
- **Temps** : durée totale, temps en mouvement, temps à l'arrêt
- **Vitesse** : moyenne, moyenne en mouvement, max
- **Allure** : min/km moyenne et en mouvement
- **Arrêts** : nombre, durée totale, arrêt le plus long, durée moyenne
- **Métadonnées** : nombre de points, segments, densité points/km

## Presets de détection d'arrêts

| Preset | Vitesse max | Durée min |
|--------|------------|-----------|
| `hiking` | 0.3 m/s (1.1 km/h) | 2 min |
| `trail` | 0.5 m/s (1.8 km/h) | 1 min |
| `cycling` | 1.0 m/s (3.6 km/h) | 30 sec |

## Tests

```bash
go test ./...
```
