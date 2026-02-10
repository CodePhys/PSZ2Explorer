using System;

namespace PSZ2Explorer
{
    public class ClusterRecord
    {
        public string Name { get; set; }

        // redshift
        public double? Redshift { get; set; }

        // Massa SZ in unità di 1e14 M_sun
        public double? MassSz { get; set; }

        // Rapporto segnale/rumore
        public double? Snr { get; set; }

        // Stato di validazione (positivo = ammasso confermato)
        public int? ValidationStatus { get; set; }

        // Flag di appartenenza al campione cosmologico Planck
        public int? CosmologyFlag { get; set; }
    }
}
