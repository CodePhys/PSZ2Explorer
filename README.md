# PSZ2 Explorer

Applicazione desktop **WPF** (.NET 8) per esplorare cataloghi di ammassi di galassie: **PSZ2** (Planck), **ACT DR5** e **eROSITA**, con distribuzioni, grafici massa–redshift e mappe celesti.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows)
![OxyPlot](https://img.shields.io/badge/OxyPlot-2.2-blue)

---

## Requisiti

- **Windows** (WPF)
- **.NET 8 SDK**
- Nessuna installazione aggiuntiva: le dipendenze (OxyPlot.Wpf) sono gestite via NuGet

---

## Compilazione e avvio

```bash
# Dalla cartella della solution (PSZ2Explorer)
dotnet restore
dotnet build

# Avvio
dotnet run --project PSZ2Explorer\PSZ2Explorer\PSZ2Explorer.csproj
```

Oppure apri `PSZ2Explorer.sln` in Visual Studio e avvia il progetto **PSZ2Explorer**.

---

## Utilizzo

1. **Carica catalogo…** — Carica il catalogo principale PSZ2 (es. CSV ESA con colonne `name`, `ra`, `dec`, `redshift`, `mass_sz`, `snr`, `y5r500`, `validation_status`, `cosmology_sample_flag`).
2. **Overlay 1 (es. ACT)…** — Carica un secondo catalogo (es. ACT DR5 da HEASARC, formato pipe `|`) per confronto nel grafico **Massa vs redshift**.
3. **Overlay 2 (es. eROSITA)…** — Carica un terzo catalogo (es. eROSITA da VizieR, TSV con `RAJ2000`, `DEJ2000`, `zBest`, `M500`) per sovrapporlo allo stesso grafico.

### Filtri

- **SNR min** — Taglio sul rapporto segnale/rumore (applicato a PSZ2 e overlay che hanno la colonna SNR).
- **Solo campione cosmologico** — Restringe il catalogo PSZ2 ai cluster con `cosmology_sample_flag = 1`.
- **Confrontabile (stesso range M–z)** — Nel grafico Massa vs redshift, limita gli overlay allo stesso intervallo in redshift e massa del campione PSZ2 filtrato (per confronti omogenei e conteggi citabili).
- **In grafico M–z:** tre checkbox per mostrare/nascondere **PSZ2**, **Overlay 1** e **Overlay 2**.

### Tipi di grafico

- Istogramma **redshift**
- Istogramma **masse SZ** (log₁₀)
- **Massa vs redshift** (scatter: PSZ2, ACT, eROSITA con simboli e colori diversi)
- Istogramma **SNR**
- Istogramma **y5r500**
- **y5r500 vs Massa**, **Redshift vs y5r500**
- **Heatmap** massa–redshift
- **Mappa celeste** (Aitoff) e **RA–Dec–z** (colore = redshift)

*(Gli istogrammi e gli altri grafici oltre “Massa vs redshift” usano solo il catalogo principale PSZ2 filtrato.)*

---

## Struttura del repository

```
PSZ2Explorer/
├── README.md
├── PSZ2Explorer.sln
└── PSZ2Explorer/
    └── PSZ2Explorer/
        ├── PSZ2Explorer.csproj
        ├── MainWindow.xaml / MainWindow.xaml.cs
        └── Data/
            ├── Catalogo.csv          (esempio PSZ2)
            ├── ACT DR5.txt           (esempio ACT, HEASARC)
            ├── asu.tsv               (esempio eROSITA, VizieR)
            ├── RIEPILOGO_COLONNE_E_PARAMETRI_TESI.md
            └── convert_ACT_DR5_to_csv.py  (utility opzionale)
```

---

## Dati e colonne

I file di esempio in `Data/` sono opzionali. È possibile usare propri CSV/TSV purché contengano le colonne attese (nomi case-insensitive).

| Catalogo | Redshift | Massa M₅₀₀ | Note |
|----------|----------|------------|------|
| **PSZ2** | `redshift` | `mass_sz` (10¹⁴ M☉) | SNR, y5r500, validation_status, cosmology_sample_flag |
| **ACT DR5** | `redshift` | `mass_500c` o `mass_500c_cal` (10¹⁴ M☉) | RA/Dec anche sessagesimali |
| **eROSITA** | `zBest` | `M500` (10¹³ M☉ nel catalogo → convertito in 10¹⁴) | RAJ2000, DEJ2000; nessun SNR nel TSV ASU |

Per **dettaglio completo delle colonne, unità e parametri** (adatto a tesi o report) vedi:
**[Data/RIEPILOGO_COLONNE_E_PARAMETRI_TESI.md](PSZ2Explorer/PSZ2Explorer/PSZ2Explorer/Data/RIEPILOGO_COLONNE_E_PARAMETRI_TESI.md)**.

---

## Riferimenti

- **PSZ2:** catalogo ESA Planck Legacy (Sunyaev–Zel’dovich).
- **ACT DR5:** Hilton et al. 2021, ApJS 253, 3; catalogo HEASARC “Atacama Cosmology Telescope DR5 Sunyaev-Zeldovich Cluster Catalog”.
- **eROSITA:** Bulbul et al. 2024, A&A 685, A106; VizieR J/A+A/685/A106 (eRASS, cluster e gruppi).

---

## Licenza

Da definire dall’autore del repository.
