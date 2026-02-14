# Colonne e unità — `browse_resultsNoLimitVirgola.csv` (PSZ2)

File usato dall’app: **separatore `;`**, **virgola come decimale** (es. `0,00548159`).

## Intestazione e indici (0-based)

| Indice | Nome colonna CSV | Usata dall’app come | Unità / note |
|--------|-------------------|----------------------|---------------|
| 0 | `source_number` | — | ID numerico |
| 1 | `name` | **Name** | Nome cluster (es. PSZ2 G000.04+45.13) |
| 2 | **`ra`** | **Ra** | Ascensione retta (gradi, 0–360). Usata per mappa celeste (Aitoff). |
| 3 | **`dec`** | **Dec** | Declinazione (gradi, -90–90). Usata per mappa celeste (Aitoff). |
| 4 | `snr` | **Snr** | Rapporto segnale/rumore (adimensionale) |
| 5 | `ir_contam_flag` | — | Flag contaminazione IR |
| 6 | `nn_quality_flag` | — | Qualità nearest-neighbour |
| 7 | **`y5r500`** | **Y5R500** | Parametro Compton y integrato entro 5×R₅₀₀. **Unità:** adimensionale (valori tipici **0,001–0,05**). Es. `0,00548159` |
| 8 | `y5r500_error` | — | Errore su y5r500 |
| 9 | `validation_status` | **ValidationStatus** | Stato validazione (>0 = validato) |
| 10 | **`redshift`** | **Redshift** | Redshift (adimensionale). Es. `0.119800` |
| 11 | `redshift_source_name` | — | Nome sorgente redshift |
| 12 | **`mass_sz`** | **MassSz** | Massa M₅₀₀ da SZ. **Unità: 10¹⁴ M⊙** (solare). Es. `3.962411` → 3,96×10¹⁴ M⊙ |
| 13 | `mass_sz_pos_err` | — | Errore positivo su massa |
| 14 | `cosmology_sample_flag` | **CosmologyFlag** | 1 = nel campione cosmologico |

## Riepilogo per l’app

- **Colonne lette:** `name`, `ra`, `dec`, `redshift`, `mass_sz`, `snr`, `y5r500`, `validation_status`, `cosmology_sample_flag`.
- **y5r500:** colonna **`y5r500`** (indice 7). Valori nel file tipo `0,00548159` → range fisico **~0,001–0,05**. Se in grafico vedi 10⁵–10⁶, il file caricato non è questo o la colonna è sbagliata.
- **Massa:** colonna **`mass_sz`** in **10¹⁴ M⊙**. Range tipico nel file ~1–25 (10¹⁴ M⊙).
- **Decimali:** il codice accetta sia punto che virgola (es. `10,356931` per `mass_sz`).

## Per la tesi

Puoi scrivere ad esempio: *«I dati sono stati letti dal catalogo PSZ2 in formato CSV (colonne: name, ra, dec, redshift, mass_sz, snr, y5r500, validation_status, cosmology_sample_flag). Il parametro y₅R₅₀₀ è adimensionale (ordine 10⁻³–10⁻²); la massa M₅₀₀^SZ è in unità di 10¹⁴ M⊙.»*

---

## Mappa celeste — testo per tesi

**Cosa fa l’app:** Proiezione Aitoff (RA, Dec → x,y). Colore = M₅₀₀^SZ; **dimensione del punto ∝ massa** (cluster massivi = punti più grandi). Opzione *Solo campione cosmologico* filtra per `cosmology_sample_flag = 1`.

**Paragrafo tipo per la tesi:**

*La mappa mostra la distribuzione angolare dei cluster SZ nel cielo osservato da Planck. L’assenza di copertura completa è compatibile con la maschera galattica e le limitazioni osservative del satellite. I cluster massivi appaiono come punti più grandi e più chiari (rosso), distribuiti in modo non uniforme. Alcune regioni mostrano addensamenti, potenzialmente compatibili con sovrastrutture a grande scala. Questa rappresentazione è utile per identificare regioni interessanti per follow-up o studi a più lunghezze d’onda. Confrontando con l’opzione «solo campione cosmologico» si può verificare come la distribuzione angolare dipenda dalla selezione del campione.*
