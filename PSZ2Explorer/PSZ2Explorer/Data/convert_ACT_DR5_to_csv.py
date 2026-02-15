"""
Converte il catalogo ACT-DR5 (FITS) in CSV con colonne compatibili con PSZ2Explorer.
Scarica prima: https://lambda.gsfc.nasa.gov/data/suborbital/ACT/ACT_dr5/DR5_cluster-catalog_v1.1.fits
Salva il file FITS in questa cartella come DR5_cluster-catalog_v1.1.fits, poi:
  python convert_ACT_DR5_to_csv.py
Oppure: python convert_ACT_DR5_to_csv.py /path/to/DR5_cluster-catalog_v1.1.fits
Output: ACT_DR5_for_PSZ2Explorer.csv (separatore ;, decimali .)
"""
import sys
from pathlib import Path

try:
    from astropy.table import Table
except ImportError:
    print("Serve astropy: pip install astropy")
    sys.exit(1)

SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_FITS = SCRIPT_DIR / "DR5_cluster-catalog_v1.1.fits"
OUTPUT_CSV = SCRIPT_DIR / "ACT_DR5_for_PSZ2Explorer.csv"

def main():
    fits_path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_FITS
    if not fits_path.is_file():
        print(f"File non trovato: {fits_path}")
        print("Scarica da: https://lambda.gsfc.nasa.gov/data/suborbital/ACT/ACT_dr5/DR5_cluster-catalog_v1.1.fits")
        sys.exit(1)

    t = Table.read(fits_path, format="fits")
    # Colonne PSZ2Explorer: name;ra;dec;redshift;mass_sz;snr (M500c già in 10^14 Msun)
    mass_col = "M500cCal" if "M500cCal" in t.colnames else "M500c"
    cols = ["name", "RADeg", "decDeg", "redshift", mass_col, "SNR"]
    for c in cols:
        if c not in t.colnames:
            raise SystemExit(f"Colonna mancante nel FITS: {c}")

    out = t[cols].copy()
    out.rename_columns(["RADeg", "decDeg", mass_col], ["ra", "dec", "mass_sz"])
    out.write(OUTPUT_CSV, format="csv", delimiter=";", overwrite=True)
    print(f"Scritto: {OUTPUT_CSV} ({len(out)} righe)")

if __name__ == "__main__":
    main()
