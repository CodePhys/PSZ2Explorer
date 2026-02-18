using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;


namespace PSZ2Explorer
{
    public partial class MainWindow : Window
    {
        private List<ClusterRecord> _allClusters = new();
        private List<ClusterRecord> _filteredClusters = new();
        private List<ClusterRecord> _overlayClusters = new();
        private string? _overlayFilePath;
        private List<ClusterRecord> _overlayClusters2 = new();
        private string? _overlayFilePath2;

        // Binning e range fissi: grafici confrontabili al variare di SNR min (per tesi)
        private const int HistBinsZ = 25;
        private const double HistZMin = 0;
        private const double HistZMax = 1.0;

        private const int HistBinsMass = 25;
        private const double HistMassMin = 0.3;
        private const double HistMassMax = 25;
        // log10(M) per istogramma masse: range log10(1..25) ≈ 0..1.4
        private const double HistLogMassMin = 0.0;
        private const double HistLogMassMax = 1.6;

        private const int HistBinsSnr = 25;
        private const double HistSnrMin = 0;
        private const double HistSnrMaxDefault = 50;

        // y5r500: parametro Compton integrato entro 5 R_500 (§3.4.5 tesi)
        private const int HistBinsY5 = 20;
        private const double HistY5Min = 0;
        private const double HistY5Max = 0.05;

        // Range massa fisico per scatter M-z (unità 10^14 M_sun). Esclude outlier da colonna/unità errate.
        private const double ScatterMassMin = 0.1;
        private const double ScatterMassMax = 50;

        /// <summary>Numero massimo di punti overlay 2 (es. eROSITA) nel grafico M–z per mantenere leggibilità.</summary>
        private const int MaxOverlay2DisplayPoints = 600;

        // y5r500: range fisico tipico (PSZ2) ~0.001–0.05. Valori >0.2 o <1e-6 indicano colonna/CSV errati.
        private const double Y5R500PhysMin = 1e-6;
        private const double Y5R500PhysMax = 0.2;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV/TSV (*.csv;*.tsv)|*.csv;*.tsv|Tutti i file (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                _allClusters = LoadClustersFromCsv(dlg.FileName);
                ApplyFiltersAndUpdatePlot();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore nel caricamento del file: " + ex.Message);
            }
        }

        private void LoadOverlay_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV/TSV (*.csv;*.tsv)|*.csv;*.tsv|Tutti i file (*.*)|*.*",
                Title = "Carica secondo catalogo (overlay)"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                _overlayClusters = LoadClustersFromCsv(dlg.FileName);
                _overlayFilePath = dlg.FileName;
                if (FindName("OverlayLabel") is System.Windows.Controls.TextBlock overlayTb)
                    overlayTb.Text = System.IO.Path.GetFileName(_overlayFilePath);
                ApplyFiltersAndUpdatePlot();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore nel caricamento overlay: " + ex.Message);
            }
        }

        private void LoadOverlay2_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV/TSV (*.csv;*.tsv)|*.csv;*.tsv|Tutti i file (*.*)|*.*",
                Title = "Carica terzo catalogo (overlay 2, es. eROSITA)"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                _overlayClusters2 = LoadClustersFromCsv(dlg.FileName);
                _overlayFilePath2 = dlg.FileName;
                if (FindName("Overlay2Label") is System.Windows.Controls.TextBlock tb)
                    tb.Text = System.IO.Path.GetFileName(_overlayFilePath2);
                ApplyFiltersAndUpdatePlot();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore nel caricamento overlay 2: " + ex.Message);
            }
        }

        private static string GetOverlayTitle(string? filePath, string fallback)
        {
            if (string.IsNullOrEmpty(filePath)) return fallback;
            string fname = Path.GetFileNameWithoutExtension(filePath);
            if (fname.Contains("asu", StringComparison.OrdinalIgnoreCase) || fname.Contains("erosita", StringComparison.OrdinalIgnoreCase))
                return "eROSITA";
            if (fname.Contains("act", StringComparison.OrdinalIgnoreCase))
                return "ACT-DR5";
            return string.IsNullOrEmpty(fname) ? fallback : fname;
        }

        /// <summary>Converte RA in formato sessagesimale (hh mm ss o hh mm ss.ss) in gradi decimali.</summary>
        private static double? ParseSexagesimalRa(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var tokens = s.Trim().Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3) return null;
            if (!double.TryParse(tokens[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var h)) return null;
            if (!double.TryParse(tokens[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var m)) return null;
            if (!double.TryParse(tokens[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var sec)) return null;
            return 15.0 * (h + m / 60.0 + sec / 3600.0);  // ore -> gradi
        }

        /// <summary>Converte Dec in formato sessagesimale (dd mm ss con segno) in gradi decimali.</summary>
        private static double? ParseSexagesimalDec(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var tokens = s.Trim().Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3) return null;
            if (!double.TryParse(tokens[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return null;
            if (!double.TryParse(tokens[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var m)) return null;
            if (!double.TryParse(tokens[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var sec)) return null;
            int sign = d >= 0 ? 1 : -1;
            return sign * (Math.Abs(d) + m / 60.0 + sec / 3600.0);
        }

        private List<ClusterRecord> LoadClustersFromCsv(string path)
        {
            var list = new List<ClusterRecord>();
            // Encoding UTF-8 senza BOM per allineamento colonne (ESA usa ; e virgola decimale)
            using var reader = new StreamReader(path, System.Text.Encoding.UTF8);

            // Salta righe di commento o intestazione HEASARC (es. "Results from...", "Coordinate system...")
            string? header = null;
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null || string.IsNullOrWhiteSpace(line)) continue;
                var t = line.TrimStart();
                if (t.StartsWith("#", StringComparison.Ordinal)) continue;
                if (t.StartsWith("Results from", StringComparison.OrdinalIgnoreCase)) continue;
                if (t.StartsWith("Coordinate system", StringComparison.OrdinalIgnoreCase)) continue;
                header = line;
                break;
            }
            if (header == null)
                return list;

            // Rimuovi BOM UTF-8 se presente (altrimenti la prima colonna non fa match)
            if (header.Length > 0 && header[0] == '\uFEFF')
                header = header.Substring(1);

            // Catalogo PSZ2 ESA usa PUNTO E VIRGOLA (;) come separatore di colonne. Con la virgola i numeri (es. 0,00548159) si spezzano e i dati sono errati.
            bool looksLikePsz2 = header.IndexOf("y5r500", StringComparison.OrdinalIgnoreCase) >= 0
                || header.IndexOf("mass_sz", StringComparison.OrdinalIgnoreCase) >= 0
                || header.IndexOf("source_number", StringComparison.OrdinalIgnoreCase) >= 0;
            if (looksLikePsz2 && !header.Contains(";"))
            {
                MessageBox.Show(
                    "Questo file sembra il catalogo PSZ2 ma non usa il punto e virgola (;) come separatore di colonne.\n\n" +
                    "Per un caricamento corretto:\n" +
                    "• Usare il file Catalogo.csv dalla cartella Data del progetto (con ; come separatore),\n" +
                    "• Non riaprire e salvare il file da Excel come \"CSV (virgola)\", altrimenti i dati vengono interpretati male.",
                    "File non adatto",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return list;
            }

            // Separatore: | (HEASARC), ; (PSZ2/ESA), altrimenti ,
            char sep = header.Contains("|") ? '|' : (header.Contains(";") ? ';' : ',');
            var columns = header.Split(sep);
            for (int i = 0; i < columns.Length; i++)
                columns[i] = columns[i].Trim();

            // Nomi colonne in maiuscolo; rimuovi \r (fine riga Windows) per match affidabile
            string[] colsUpper = columns.Select(c => c.Trim().Replace("\r", "").Replace("\n", "").ToUpperInvariant()).ToArray();

            int idxName = Array.IndexOf(colsUpper, "NAME");
            int idxZ = Array.IndexOf(colsUpper, "REDSHIFT");
            int idxMass = Array.IndexOf(colsUpper, "MASS_SZ");
            int idxSnr = Array.IndexOf(colsUpper, "SNR");
            // y5r500: nel PSZ2 ESA è SEMPRE la colonna subito prima di "y5r500_error" — così si evita di leggere nn_quality_flag (0,9) per sbaglio
            int idxY5Err = -1;
            for (int i = 0; i < columns.Length; i++)
            {
                string col = columns[i].Trim().Replace("\r", "").Replace("\n", "").ToUpperInvariant();
                if (col == "Y5R500_ERROR" || (col.Contains("Y5R500") && col.Contains("ERROR")))
                { idxY5Err = i; break; }
            }
            int idxY5 = (idxY5Err > 0) ? idxY5Err - 1 : -1;
            if (idxY5 < 0)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    string col = columns[i].Trim().Replace("\r", "").Replace("\n", "");
                    if (col.Equals("y5r500", StringComparison.OrdinalIgnoreCase) && !col.Contains("error"))
                    { idxY5 = i; break; }
                }
            }
            int idxRa = Array.IndexOf(colsUpper, "RA");
            int idxDec = Array.IndexOf(colsUpper, "DEC");
            int idxVal = Array.IndexOf(colsUpper, "VALIDATION_STATUS");
            int idxCosmo = Array.IndexOf(colsUpper, "COSMOLOGY_SAMPLE_FLAG");
            // Colonne catalogo eROSITA (VizieR / Bulbul+ 2024)
            int idxRAJ2000 = Array.IndexOf(colsUpper, "RAJ2000");
            int idxDEJ2000 = Array.IndexOf(colsUpper, "DEJ2000");
            int idxZBest = Array.IndexOf(colsUpper, "ZBEST");
            int idxM500 = Array.IndexOf(colsUpper, "M500");  // massa in 10^13 M_sun → convertiamo in 10^14
            // Colonne HEASARC / ACT-DR5 (mass_500c già in 10^14 M_sun)
            int idxMass500c = Array.IndexOf(colsUpper, "MASS_500C");
            if (idxMass500c < 0) idxMass500c = Array.IndexOf(colsUpper, "MASS_500C_CAL");


            var culture = CultureInfo.InvariantCulture;
            bool isErositaFormat = idxM500 >= 0 || idxZBest >= 0;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(sep);
                for (int i = 0; i < parts.Length; i++)
                    parts[i] = parts[i].Trim();

                // Salta riga unità o separatore (es. " ;deg;deg;s" o "---;---;..." o |---| pipe table)
                if (parts.Length > 0 && (parts[0].Contains("deg", StringComparison.OrdinalIgnoreCase) ||
                        (parts[0].StartsWith("-", StringComparison.Ordinal) && parts[0].Contains("---"))))
                    continue;

                double? TryParseDouble(int idx)
                {
                    if (idx < 0 || idx >= parts.Length) return null;
                    var s = parts[idx];
                    if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                        return v;
                    // Catalogo ESA: virgola decimale (es. 0,00548159)
                    if (double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out v))
                        return v;
                    if (double.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("it-IT"), out v))
                        return v;
                    return null;
                }


                int? TryParseInt(int idx)
                {
                    if (idx < 0 || idx >= parts.Length) return null;
                    if (int.TryParse(parts[idx], NumberStyles.Any, culture, out var v))
                        return v;
                    return null;
                }

                double? redshift = TryParseDouble(idxZ);
                if (!redshift.HasValue && idxZBest >= 0) redshift = TryParseDouble(idxZBest);

                double? massSz = TryParseDouble(idxMass);
                if (!massSz.HasValue && idxM500 >= 0)
                {
                    var m500 = TryParseDouble(idxM500);
                    if (m500.HasValue) massSz = m500.Value / 10.0;  // M500 in 10^13 M_sun → 10^14
                }
                if (!massSz.HasValue && idxMass500c >= 0)
                    massSz = TryParseDouble(idxMass500c);  // ACT/HEASARC: mass_500c già in 10^14 M_sun

                double? ra = TryParseDouble(idxRa);
                if (!ra.HasValue && idxRAJ2000 >= 0) ra = TryParseDouble(idxRAJ2000);
                if (!ra.HasValue && idxRa >= 0 && idxRa < parts.Length && parts[idxRa].Contains(' '))
                    ra = ParseSexagesimalRa(parts[idxRa]);
                if (!ra.HasValue && idxRAJ2000 >= 0 && idxRAJ2000 < parts.Length && parts[idxRAJ2000].Contains(' '))
                    ra = ParseSexagesimalRa(parts[idxRAJ2000]);

                double? dec = TryParseDouble(idxDec);
                if (!dec.HasValue && idxDEJ2000 >= 0) dec = TryParseDouble(idxDEJ2000);
                if (!dec.HasValue && idxDec >= 0 && idxDec < parts.Length && parts[idxDec].Contains(' '))
                    dec = ParseSexagesimalDec(parts[idxDec]);
                if (!dec.HasValue && idxDEJ2000 >= 0 && idxDEJ2000 < parts.Length && parts[idxDEJ2000].Contains(' '))
                    dec = ParseSexagesimalDec(parts[idxDEJ2000]);

                double? snr = TryParseDouble(idxSnr);
                if (!snr.HasValue && isErositaFormat) snr = 999;  // eROSITA senza SNR: includi in filtri

                // y5r500: leggi dalla colonna "y5r500" (virgola o punto decimale); il grafico Y–M filtra per range 10⁻⁶–0,2
                double? y5 = idxY5 >= 0 ? TryParseDouble(idxY5) : null;

                var c = new ClusterRecord
                {
                    Name = idxName >= 0 && idxName < parts.Length ? parts[idxName] : "",
                    Redshift = redshift,
                    MassSz = massSz,
                    Snr = snr,
                    Y5R500 = y5,
                    Ra = ra,
                    Dec = dec,
                    ValidationStatus = TryParseInt(idxVal),
                    CosmologyFlag = TryParseInt(idxCosmo)
                };

                list.Add(c);
            }

            return list;
        }

        private void PlotSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ApplyFiltersAndUpdatePlot();
        }


        /// <summary>Interpola tra due colori (t in [0,1]). Per scale colori stile tesi.</summary>
        private static OxyColor InterpolateColor(OxyColor low, OxyColor high, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            byte r = (byte)(low.R + (high.R - low.R) * t);
            byte g = (byte)(low.G + (high.G - low.G) * t);
            byte b = (byte)(low.B + (high.B - low.B) * t);
            byte a = (byte)(low.A + (high.A - low.A) * t);
            return OxyColor.FromArgb(a, r, g, b);
        }

        private void PlotHistogramBars(
    List<double> values,
    string title,
    string xTitle,
    double xMin,
    double xMax,
    int bins)
        {
            if (values.Count == 0) return;

            if (xMax <= xMin) xMax = xMin + 1e-6;

            double dx = (xMax - xMin) / bins;
            var counts = new int[bins];

            foreach (var v in values)
            {
                if (v < xMin || v > xMax) continue;
                int bin = (int)((v - xMin) / dx);
                if (bin == bins) bin = bins - 1;
                if (bin >= 0 && bin < bins) counts[bin]++;
            }

            var model = new PlotModel { Title = title };

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = xTitle,
                Minimum = xMin,
                Maximum = xMax
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "N",
                Minimum = 0
            });

            // Scala colori stile tesi: da blu (basso) a rosso (alto) sull'asse x
            var colorLow = OxyColors.SteelBlue;
            var colorHigh = OxyColors.IndianRed;

            var bars = new RectangleBarSeries
            {
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1
            };

            for (int i = 0; i < bins; i++)
            {
                double t = (double)i / Math.Max(1, bins - 1);
                var barColor = InterpolateColor(colorLow, colorHigh, t);
                double left = xMin + i * dx;
                double right = left + dx;
                var item = new RectangleBarItem(left, 0, right, counts[i])
                {
                    Color = barColor
                };
                bars.Items.Add(item);
            }

            model.Series.Add(bars);
            PlotView.Model = model;
        }



        private void ApplyFiltersAndUpdatePlot()
        {
            if (_allClusters.Count == 0)
                return;

            _filteredClusters = GetFilteredSample().ToList();
            SampleInfoTextBlock.Text = $"Cluster selezionati: {_filteredClusters.Count}";
            UpdatePlot();
        }
        private IEnumerable<ClusterRecord> GetFilteredSample()
        {
            double snrMin = 0;
            double.TryParse(SnrMinTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out snrMin);

            var sample = _allClusters
                .Where(c => c.Snr.HasValue && c.Snr.Value >= snrMin)   // taglio in SNR
                .Where(c => c.Redshift.HasValue && c.Redshift.Value > 0)
                .Where(c => c.MassSz.HasValue && c.MassSz.Value > 0);

            // validation_status
            sample = sample.Where(c => !c.ValidationStatus.HasValue || c.ValidationStatus.Value > 0);

            // Solo campione cosmologico (cosmology_sample_flag = 1)
            if (CosmologyOnlyCheckBox?.IsChecked == true)
                sample = sample.Where(c => c.CosmologyFlag.HasValue && c.CosmologyFlag.Value == 1);

            return sample.ToList();
        }

        private void UpdatePlot()
        {
            var selected = (PlotSelector.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
            selected ??= "HistZ";

            switch (selected)
            {
                case "HistZ":
                    PlotRedshiftHistogram();
                    break;
                case "HistMass":
                    PlotMassHistogram();
                    break;
                case "ScatterMZ":
                    PlotMassRedshiftScatter();
                    break;
                case "HistSnr":
                    PlotSnrHistogram();
                    break;
                case "HistY5":
                    PlotY5R500Histogram();
                    break;
                case "ScatterY5M":
                    PlotY5R500MassScatter();
                    break;
                case "ScatterZY5":
                    PlotRedshiftY5R500Scatter();
                    break;
                case "HeatMapMZ":
                    PlotHeatMapMassRedshift();
                    break;
                case "SkyMap":
                    PlotSkyMapAitoff(colorByRedshift: false);
                    break;
                case "SkyMapZ":
                    PlotSkyMapAitoff(colorByRedshift: true);
                    break;
            }
        }

        // ---------------- GRAFICI ----------------

        private void PlotRedshiftHistogram()
        {
            var zValues = _filteredClusters
                .Where(c => c.Redshift.HasValue)
                .Select(c => c.Redshift!.Value)
                .ToList();

            PlotHistogramBars(zValues, "Distribuzione dei redshift", "z",
                HistZMin, HistZMax, HistBinsZ);
        }


        /// <summary>Istogramma in log10(M): distribuzione masse è molto sbilanciata, in log è leggibile (tesi).</summary>
        private void PlotMassHistogram()
        {
            var logMValues = _filteredClusters
                .Where(c => c.MassSz.HasValue && c.MassSz.Value > 0)
                .Select(c => Math.Log10(c.MassSz!.Value))
                .ToList();

            PlotHistogramBars(logMValues, "Distribuzione delle masse SZ (log₁₀)",
                "log₁₀(M_{500}^{SZ} / (10^{14} M_\\odot))", HistLogMassMin, HistLogMassMax, HistBinsMass);
        }


        private void PlotMassRedshiftScatter()
        {
            double snrMin = 0;
            double.TryParse(SnrMinTextBox?.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out snrMin);

            var points = _filteredClusters
                .Where(c => c.Redshift.HasValue && c.MassSz.HasValue &&
                            c.MassSz.Value >= ScatterMassMin && c.MassSz.Value <= ScatterMassMax)
                .ToList();

            // Stesso taglio SNR per entrambi gli overlay: confronto omogeneo
            var overlayPoints = _overlayClusters
                .Where(c => c.Snr.HasValue && c.Snr.Value >= snrMin)
                .Where(c => c.Redshift.HasValue && c.Redshift.Value > 0 && c.MassSz.HasValue &&
                            c.MassSz.Value >= ScatterMassMin && c.MassSz.Value <= ScatterMassMax)
                .ToList();

            var overlayPoints2 = _overlayClusters2
                .Where(c => c.Snr.HasValue && c.Snr.Value >= snrMin)
                .Where(c => c.Redshift.HasValue && c.Redshift.Value > 0 && c.MassSz.HasValue &&
                            c.MassSz.Value >= ScatterMassMin && c.MassSz.Value <= ScatterMassMax)
                .ToList();

            // "Confrontabile": limita gli overlay allo stesso range (z, M) del catalogo principale
            bool comparable = ComparableOverlayCheckBox?.IsChecked == true && points.Count > 0;
            if (comparable)
            {
                double psz2ZMin = points.Min(c => c.Redshift!.Value);
                double psz2ZMax = points.Max(c => c.Redshift!.Value);
                double psz2MMin = points.Min(c => c.MassSz!.Value);
                double psz2MMax = points.Max(c => c.MassSz!.Value);
                overlayPoints = overlayPoints
                    .Where(c => c.Redshift!.Value >= psz2ZMin && c.Redshift.Value <= psz2ZMax &&
                                c.MassSz!.Value >= psz2MMin && c.MassSz.Value <= psz2MMax)
                    .ToList();
                overlayPoints2 = overlayPoints2
                    .Where(c => c.Redshift!.Value >= psz2ZMin && c.Redshift.Value <= psz2ZMax &&
                                c.MassSz!.Value >= psz2MMin && c.MassSz.Value <= psz2MMax)
                    .ToList();
            }

            // Solo i cataloghi con checkbox attivo vengono mostrati
            bool showPsz2 = ShowPsz2CheckBox?.IsChecked == true;
            bool showO1 = ShowOverlay1CheckBox?.IsChecked == true;
            bool showO2 = ShowOverlay2CheckBox?.IsChecked == true;
            var visiblePts = showPsz2 ? points : new List<ClusterRecord>();
            var visibleOverlay = showO1 ? overlayPoints : new List<ClusterRecord>();
            var visibleOverlay2 = showO2 ? overlayPoints2 : new List<ClusterRecord>();

            if (visiblePts.Count == 0 && visibleOverlay.Count == 0 && visibleOverlay2.Count == 0)
            {
                PlotView.Model = new PlotModel { Title = "Massa SZ vs redshift" };
                return;
            }

            double mMin = double.MaxValue;
            double mMax = double.MinValue;
            if (visiblePts.Count > 0)
            {
                mMin = Math.Min(mMin, visiblePts.Min(c => c.MassSz!.Value));
                mMax = Math.Max(mMax, visiblePts.Max(c => c.MassSz!.Value));
            }
            if (visibleOverlay.Count > 0)
            {
                mMin = Math.Min(mMin, visibleOverlay.Min(c => c.MassSz!.Value));
                mMax = Math.Max(mMax, visibleOverlay.Max(c => c.MassSz!.Value));
            }
            if (visibleOverlay2.Count > 0)
            {
                mMin = Math.Min(mMin, visibleOverlay2.Min(c => c.MassSz!.Value));
                mMax = Math.Max(mMax, visibleOverlay2.Max(c => c.MassSz!.Value));
            }
            if (mMax <= mMin) mMax = mMin + 0.1;

            double zMin = 0;
            double zMax = 0.5;
            if (visiblePts.Count > 0)
            {
                zMin = visiblePts.Min(c => c.Redshift!.Value);
                zMax = visiblePts.Max(c => c.Redshift!.Value);
            }
            if (visibleOverlay.Count > 0)
            {
                double ozMin = visibleOverlay.Min(c => c.Redshift!.Value);
                double ozMax = visibleOverlay.Max(c => c.Redshift!.Value);
                zMin = visiblePts.Count > 0 ? Math.Min(zMin, ozMin) : ozMin;
                zMax = visiblePts.Count > 0 ? Math.Max(zMax, ozMax) : ozMax;
            }
            if (visibleOverlay2.Count > 0)
            {
                double o2zMin = visibleOverlay2.Min(c => c.Redshift!.Value);
                double o2zMax = visibleOverlay2.Max(c => c.Redshift!.Value);
                zMin = (visiblePts.Count > 0 || visibleOverlay.Count > 0) ? Math.Min(zMin, o2zMin) : o2zMin;
                zMax = (visiblePts.Count > 0 || visibleOverlay.Count > 0) ? Math.Max(zMax, o2zMax) : o2zMax;
            }
            if (zMax <= zMin) zMax = zMin + 0.1;

            var model = new PlotModel
            {
                Title = comparable
                    ? "Massa SZ vs redshift (stesso range M–z: confrontabile)"
                    : "Massa SZ vs redshift",
                IsLegendVisible = false
            };

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "z"
            });

            model.Axes.Add(new LogarithmicAxis
            {
                Position = AxisPosition.Left,
                Title = "M_{500} [10^{14} M\u2609]",
                TitleFontSize = 14
            });

            // Nomi leggibili per overlay (es. asu.tsv → eROSITA, ACT file → ACT)
            string overlayTitle = GetOverlayTitle(_overlayFilePath, "Overlay 1");
            string overlay2Title = GetOverlayTitle(_overlayFilePath2, "Overlay 2");

            // Overlay 2 (es. eROSITA): al massimo MaxOverlay2DisplayPoints per grafico leggibile; marker più piccoli
            List<ClusterRecord> toDrawO2 = visibleOverlay2;
            if (visibleOverlay2.Count > MaxOverlay2DisplayPoints)
            {
                double step = (double)visibleOverlay2.Count / MaxOverlay2DisplayPoints;
                toDrawO2 = new List<ClusterRecord>();
                for (int i = 0; i < MaxOverlay2DisplayPoints; i++)
                {
                    int idx = Math.Min((int)(i * step), visibleOverlay2.Count - 1);
                    toDrawO2.Add(visibleOverlay2[idx]);
                }
            }
            if (toDrawO2.Count > 0)
            {
                var scatterOverlay2 = new ScatterSeries
                {
                    Title = overlay2Title + (visibleOverlay2.Count > MaxOverlay2DisplayPoints
                        ? string.Format(CultureInfo.InvariantCulture, " (n={0}, mostrati {1})", visibleOverlay2.Count, toDrawO2.Count)
                        : ""),
                    MarkerType = MarkerType.Square,
                    MarkerSize = 3,
                    MarkerFill = OxyColors.DarkGreen,
                    MarkerStroke = OxyColors.White,
                    MarkerStrokeThickness = 0.5,
                    TrackerFormatString = "{0}\nNome: {Tag}\nz = {2:0.000}\nM_500 = {4:0.00} × 10^14 M_⊙"
                };
                foreach (var c in toDrawO2)
                {
                    double z = c.Redshift!.Value;
                    double m = c.MassSz!.Value;
                    scatterOverlay2.Points.Add(new ScatterPoint(z, m) { Tag = c.Name });
                }
                model.Series.Add(scatterOverlay2);
            }

            if (visibleOverlay.Count > 0)
            {
                var scatterOverlay = new ScatterSeries
                {
                    Title = overlayTitle,
                    MarkerType = MarkerType.Triangle,
                    MarkerSize = 5,
                    MarkerFill = OxyColors.DarkOrange,
                    MarkerStroke = OxyColors.White,
                    MarkerStrokeThickness = 0.8,
                    TrackerFormatString = "{0}\nNome: {Tag}\nz = {2:0.000}\nM_500 = {4:0.00} × 10^14 M_⊙"
                };
                foreach (var c in visibleOverlay)
                {
                    double z = c.Redshift!.Value;
                    double m = c.MassSz!.Value;
                    scatterOverlay.Points.Add(new ScatterPoint(z, m) { Tag = c.Name });
                }
                model.Series.Add(scatterOverlay);
            }

            if (visiblePts.Count > 0)
            {
                var scatterPsz2 = new ScatterSeries
                {
                    Title = "PSZ2",
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 5,
                    MarkerFill = OxyColors.DarkBlue,
                    MarkerStroke = OxyColors.White,
                    MarkerStrokeThickness = 0.8,
                    TrackerFormatString = "{0}\nNome: {Tag}\nz = {2:0.000}\nM_500 = {4:0.00} × 10^14 M_⊙"
                };
                foreach (var c in visiblePts)
                {
                    double z = c.Redshift!.Value;
                    double m = c.MassSz!.Value;
                    scatterPsz2.Points.Add(new ScatterPoint(z, m) { Tag = c.Name });
                }
                model.Series.Add(scatterPsz2);
            }

            // Legenda sul bordo destro in alto: nomi cataloghi con simbolo e colore
            double zLegRight = zMin + (zMax - zMin) * 0.98;
            double logMmin = Math.Log10(mMin);
            double logMmax = Math.Log10(mMax);
            double mLeg1 = Math.Pow(10, logMmin + (logMmax - logMmin) * 0.92);
            double mLeg2 = Math.Pow(10, logMmin + (logMmax - logMmin) * 0.82);
            double mLeg3 = Math.Pow(10, logMmin + (logMmax - logMmin) * 0.72);

            if (visiblePts.Count > 0)
            {
                model.Annotations.Add(new OxyPlot.Annotations.TextAnnotation
                {
                    Text = "● PSZ2",
                    TextPosition = new DataPoint(zLegRight, mLeg1),
                    TextColor = OxyColors.DarkBlue,
                    FontSize = 12,
                    FontWeight = 600,
                    TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Right,
                    TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle
                });
            }
            if (visibleOverlay.Count > 0)
            {
                model.Annotations.Add(new OxyPlot.Annotations.TextAnnotation
                {
                    Text = "▲ " + overlayTitle,
                    TextPosition = new DataPoint(zLegRight, mLeg2),
                    TextColor = OxyColors.DarkOrange,
                    FontSize = 12,
                    FontWeight = 600,
                    TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Right,
                    TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle
                });
            }
            if (toDrawO2.Count > 0)
            {
                model.Annotations.Add(new OxyPlot.Annotations.TextAnnotation
                {
                    Text = "■ " + overlay2Title,
                    TextPosition = new DataPoint(zLegRight, mLeg3),
                    TextColor = OxyColors.DarkGreen,
                    FontSize = 12,
                    FontWeight = 600,
                    TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Right,
                    TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle
                });
            }

            // In modalità confrontabile: annotazione con conteggi e range per citazione in tesi
            if (comparable && (points.Count > 0 || overlayPoints.Count > 0 || overlayPoints2.Count > 0))
            {
                double psz2ZMin = points.Count > 0 ? points.Min(c => c.Redshift!.Value) : 0;
                double psz2ZMax = points.Count > 0 ? points.Max(c => c.Redshift!.Value) : 0;
                double psz2MMin = points.Count > 0 ? points.Min(c => c.MassSz!.Value) : 0;
                double psz2MMax = points.Count > 0 ? points.Max(c => c.MassSz!.Value) : 0;
                string rangeText = string.Format(CultureInfo.InvariantCulture,
                    "N_PSZ2 = {0},  N_{1} = {2},  N_{3} = {4}   |   z in [{5:F2}, {6:F2}],  M500 in [{7:F2}, {8:F2}] x10^14 Msun",
                    points.Count, overlayTitle, overlayPoints.Count, overlay2Title, overlayPoints2.Count,
                    psz2ZMin, psz2ZMax, psz2MMin, psz2MMax);
                double mLeg0 = Math.Pow(10, logMmin + (logMmax - logMmin) * 0.02);
                model.Annotations.Add(new OxyPlot.Annotations.TextAnnotation
                {
                    Text = rangeText,
                    TextPosition = new DataPoint(zMin + (zMax - zMin) * 0.02, mLeg0),
                    TextColor = OxyColors.DarkGray,
                    FontSize = 9,
                    TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                    TextVerticalAlignment = OxyPlot.VerticalAlignment.Top
                });
            }

            PlotView.Model = model;
        }

        /// <summary>Range da SNRmin (UI) a max osservato: mostra dove si accumulano le rivelazioni (picco vicino alla soglia).</summary>
        private void PlotSnrHistogram()
        {
            var sValues = _filteredClusters
                .Where(c => c.Snr.HasValue)
                .Select(c => c.Snr!.Value)
                .ToList();

            double snrMin = HistSnrMin;
            double.TryParse(SnrMinTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out snrMin);
            double snrMax = sValues.Count > 0 ? Math.Ceiling(sValues.Max()) : snrMin + 1;
            snrMax = Math.Max(snrMin + 1, Math.Min(snrMax, HistSnrMaxDefault));

            PlotHistogramBars(sValues, "Distribuzione del rapporto segnale/rumore", "SNR",
                snrMin, snrMax, HistBinsSnr);
        }

        /// <summary>§3.4.5 tesi: distribuzione del parametro Compton y entro 5 R_500 (eq. 1.8).</summary>
        private void PlotY5R500Histogram()
        {
            var yValues = _filteredClusters
                .Where(c => c.Y5R500.HasValue && c.Y5R500.Value > 0)
                .Select(c => c.Y5R500!.Value)
                .ToList();

            if (yValues.Count == 0)
            {
                PlotView.Model = new PlotModel
                {
                    Title = "Distribuzione y_{5R500} — Nessun dato (filtrato). Verificare CSV e colonna 'y5r500'."
                };
                return;
            }

            double yMin = yValues.Min();
            double yMax = yValues.Max();
            // Range tipico PSZ2: 0.001–0.05. Se dati chiaramente sbagliati (yMax > 0.1) avvisa.
            bool usePhysicalRange = yMin >= HistY5Min && yMax <= HistY5Max;
            double rangeMin = usePhysicalRange ? HistY5Min : yMin;
            double rangeMax = usePhysicalRange ? HistY5Max : Math.Max(yMax, yMin * 1.1);
            if (rangeMax <= rangeMin) rangeMax = rangeMin + 1;

            PlotHistogramBars(yValues,
                "Distribuzione del parametro Compton y (5 R_{500})",
                "y_{5R500}",
                rangeMin, rangeMax, HistBinsY5);

            // Avviso solo se valori chiaramente fuori (es. colonna sbagliata: 10, 100, ...)
            bool clearlyWrong = yMax > 0.1;
            if (clearlyWrong)
            {
                var model = PlotView.Model;
                if (model != null)
                    model.Annotations.Add(new OxyPlot.Annotations.TextAnnotation
                    {
                        Text = string.Format(CultureInfo.InvariantCulture,
                            "Attenzione: valori fuori range tipico (0,001–0,05).\nValori letti come y5r500: min = {0:F0}, max = {1:F0}.\n→ Il file caricato usa probabilmente la VIRGOLA come separatore di colonne.\nUsare il file con PUNTO E VIRGOLA (;) come separatore (es. Catalogo.csv dalla cartella Data, senza riaprirlo da Excel come CSV con virgola).",
                            yMin, yMax),
                        TextPosition = new DataPoint(rangeMin, 0),
                        TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                        TextVerticalAlignment = OxyPlot.VerticalAlignment.Top,
                        FontSize = 9,
                        TextColor = OxyColors.DarkRed
                    });
            }
        }

        /// <summary>§3.4.5 tesi: relazione Y–M in scala log-log (power law → retta). Fit: log Y = A + α log M.</summary>
        private void PlotY5R500MassScatter()
        {
            // Solo punti con y5r500 nel range fisico (0.001–0.05): esclude righe mis-parse o CSV con colonne sbagliate
            var points = _filteredClusters
                .Where(c => c.Y5R500.HasValue && c.Y5R500.Value >= Y5R500PhysMin && c.Y5R500.Value <= Y5R500PhysMax &&
                            c.MassSz.HasValue && c.MassSz.Value >= ScatterMassMin && c.MassSz.Value <= ScatterMassMax)
                .ToList();
            if (points.Count == 0)
            {
                int conY5 = _filteredClusters.Count(c => c.Y5R500.HasValue);
                int conY5InRange = _filteredClusters.Count(c => c.Y5R500.HasValue && c.Y5R500.Value >= Y5R500PhysMin && c.Y5R500.Value <= Y5R500PhysMax);
                string hint = conY5 == 0
                    ? "Il CSV deve contenere la colonna 'y5r500' (parametro Compton entro 5 R₅₀₀). Verificare nome colonna e separatore decimale (virgola/punto)."
                    : conY5InRange == 0
                        ? "Nessun punto con y5r500 nel range 10⁻⁶–0,2. Verificare che si usi la colonna y5r500 (non y5r500_error) e che i decimali usino la virgola."
                        : "Nessun punto con sia y5r500 che massa nel range. Verificare filtri (SNR, campione cosmologico) e range massa 0,1–50.";
                PlotView.Model = new PlotModel
                {
                    Title = "y_{5R500} vs Massa SZ — Nessun dato. " + hint
                };
                return;
            }

            double mMin = points.Min(c => c.MassSz!.Value);
            double mMax = points.Max(c => c.MassSz!.Value);
            if (mMax <= mMin) mMax = mMin + 0.1;
            double yMin = points.Min(c => c.Y5R500!.Value);
            double yMax = points.Max(c => c.Y5R500!.Value);
            if (yMax <= yMin) yMax = yMin * 1.1;

            var model = new PlotModel { Title = "y_{5R500} vs Massa SZ (Y–M, log-log)" };

            // Scala log-log: power law Y ∝ M^α diventa retta
            model.Axes.Add(new LogarithmicAxis
            {
                Position = AxisPosition.Bottom,
                Title = "y_{5R500}"
            });
            model.Axes.Add(new LogarithmicAxis
            {
                Position = AxisPosition.Left,
                Title = "M_{500}^{SZ} [10^{14} M_\\odot]"
            });

            var colorAxis = new OxyPlot.Axes.LinearColorAxis
            {
                Position = AxisPosition.Right,
                Title = "M_{500}^{SZ} [10^{14} M_\\odot]",
                Key = "MassColor",
                Minimum = mMin,
                Maximum = mMax,
                LowColor = OxyColors.DarkBlue,
                HighColor = OxyColors.DarkRed
            };
            model.Axes.Add(colorAxis);

            var scatter = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                ColorAxisKey = "MassColor",
                TrackerFormatString = "y_5R500 = {1:0.00000}\nM_500^SZ = {2:0.00} × 10^14 M_sun"
            };

            foreach (var c in points)
                scatter.Points.Add(new ScatterPoint(c.Y5R500!.Value, c.MassSz!.Value, 3, c.MassSz.Value) { Tag = c.Name });

            model.Series.Add(scatter);

            // Regressione lineare su log10(Y) vs log10(M): log10(Y) = A + α*log10(M)
            var logY = points.Select(c => Math.Log10(c.Y5R500!.Value)).ToList();
            var logM = points.Select(c => Math.Log10(c.MassSz!.Value)).ToList();
            int n = logY.Count;
            double meanLogY = logY.Average();
            double meanLogM = logM.Average();
            double ssM = 0, ssY = 0, sp = 0;
            for (int i = 0; i < n; i++)
            {
                double dm = logM[i] - meanLogM;
                double dy = logY[i] - meanLogY;
                ssM += dm * dm;
                ssY += dy * dy;
                sp += dm * dy;
            }
            double alpha = ssM > 0 ? sp / ssM : 0;  // pendenza
            double intercept = meanLogY - alpha * meanLogM;  // log10(Y0)
            double ssRes = 0;
            for (int i = 0; i < n; i++)
            {
                double pred = intercept + alpha * logM[i];
                double err = logY[i] - pred;
                ssRes += err * err;
            }
            double r2 = (ssY > 0) ? 1 - ssRes / ssY : 0;
            double rmsLog = n > 0 ? Math.Sqrt(ssRes / n) : 0;

            // Retta di fit in spazio (Y, M): Y = 10^intercept * M^alpha
            var fitLine = new LineSeries
            {
                Title = "fit: log Y = A + α log M",
                Color = OxyColors.DarkOrange,
                StrokeThickness = 2
            };
            int numSeg = 50;
            for (int i = 0; i <= numSeg; i++)
            {
                double m = mMin * Math.Pow(mMax / mMin, (double)i / numSeg);
                double yFit = Math.Pow(10, intercept) * Math.Pow(m, alpha);
                fitLine.Points.Add(new DataPoint(yFit, m));
            }
            model.Series.Add(fitLine);

            // Legenda: retta arancione = fit log Y = A + α log M. Se α≈0 la retta è ~verticale (Y≈cost).
            string fitText = string.Format(CultureInfo.InvariantCulture,
                "Retta di fit (arancione): log Y = A + α log M\nα = {0:F3}   R² = {1:F3}   σ(log Y) = {2:F3} dex",
                alpha, r2, rmsLog);
            model.Annotations.Add(new OxyPlot.Annotations.TextAnnotation
            {
                Text = fitText,
                TextPosition = new DataPoint(yMin, mMax * 0.85),
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Top,
                FontSize = 11,
                TextColor = OxyColors.DarkOrange
            });

            PlotView.Model = model;
        }

        /// <summary>Redshift vs y5r500: atteso calo del segnale SZ con la distanza. Accetta tutti i y5r500 > 0 per mostrare il grafico anche se CSV ha separatore sbagliato.</summary>
        private void PlotRedshiftY5R500Scatter()
        {
            var points = _filteredClusters
                .Where(c => c.Redshift.HasValue && c.Redshift.Value > 0 &&
                            c.Y5R500.HasValue && c.Y5R500.Value > 0)
                .ToList();
            if (points.Count == 0)
            {
                PlotView.Model = new PlotModel
                {
                    Title = "Redshift vs y_{5R500} — Nessun dato (z > 0 e y5r500 > 0). Verificare il CSV."
                };
                return;
            }

            double yMin = points.Min(c => c.Y5R500!.Value);
            double yMax = points.Max(c => c.Y5R500!.Value);
            if (yMax <= yMin) yMax = yMin * 1.1;
            bool inPhysicalRange = yMin >= Y5R500PhysMin && yMax <= Y5R500PhysMax;

            var model = new PlotModel { Title = "Redshift vs y_{5R500}" };
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "z" });
            model.Axes.Add(new LogarithmicAxis { Position = AxisPosition.Left, Title = "y_{5R500}", Minimum = yMin * 0.9, Maximum = yMax * 1.1 });

            var colorAxis = new OxyPlot.Axes.LinearColorAxis
            {
                Position = AxisPosition.Right,
                Title = "M_{500}^{SZ} [10^{14} M_\\odot]",
                Key = "MassColor",
                Minimum = points.Where(c => c.MassSz.HasValue).Select(c => c.MassSz!.Value).DefaultIfEmpty(1).Min(),
                Maximum = points.Where(c => c.MassSz.HasValue).Select(c => c.MassSz!.Value).DefaultIfEmpty(10).Max(),
                LowColor = OxyColors.DarkBlue,
                HighColor = OxyColors.DarkRed
            };
            model.Axes.Add(colorAxis);

            var scatter = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                ColorAxisKey = "MassColor",
                TrackerFormatString = "Nome: {Tag}\nz = {2:0.000}\ny_{5R500} = {4:0.00000}\nM = {6:0.00} ×10^14 M_⊙"
            };
            double mMin = colorAxis.Minimum;
            double mMax = colorAxis.Maximum;
            if (mMax <= mMin) mMax = mMin + 1;
            foreach (var c in points)
            {
                double z = c.Redshift!.Value;
                double y = c.Y5R500!.Value;
                double m = c.MassSz ?? (mMin + mMax) * 0.5;
                scatter.Points.Add(new ScatterPoint(z, y, 3, m) { Tag = c.Name });
            }
            model.Series.Add(scatter);
            string caption = "Colore = M_500^SZ. Atteso: y diminuisce con z (segnale SZ con distanza).";
            if (!inPhysicalRange)
                caption += "\nValori y5r500 fuori range tipico (0,001–0,05): verificare che il CSV usi ; come separatore.";
            model.Annotations.Add(new OxyPlot.Annotations.TextAnnotation
            {
                Text = caption,
                TextPosition = new DataPoint(0, 0),
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom,
                FontSize = 9,
                TextColor = OxyColors.Gray
            });
            PlotView.Model = model;
        }

        /// <summary>Heatmap 2D: densità nel piano massa–redshift (binning). Dove si concentrano gli oggetti.</summary>
        private void PlotHeatMapMassRedshift()
        {
            var points = _filteredClusters
                .Where(c => c.Redshift.HasValue && c.MassSz.HasValue &&
                            c.MassSz.Value >= ScatterMassMin && c.MassSz.Value <= ScatterMassMax)
                .ToList();
            if (points.Count == 0)
            {
                PlotView.Model = new PlotModel { Title = "Heatmap massa–redshift" };
                return;
            }

            const int nz = 25;
            const int nM = 25;
            double zMin = 0;
            double zMax = Math.Min(1.0, points.Max(c => c.Redshift!.Value) * 1.01);
            if (zMax <= zMin) zMax = 0.5;
            double mMin = ScatterMassMin;
            double mMax = ScatterMassMax;
            double dz = (zMax - zMin) / nz;
            double dm = (mMax - mMin) / nM;

            // HeatMapSeries Data[rows, cols] = Data[Y, X] = Data[nM, nz]
            var counts = new double[nM, nz];
            foreach (var c in points)
            {
                double z = c.Redshift!.Value;
                double m = c.MassSz!.Value;
                if (z < zMin || z > zMax || m < mMin || m > mMax) continue;
                int iz = (int)((z - zMin) / dz);
                int im = (int)((m - mMin) / dm);
                if (iz >= nz) iz = nz - 1;
                if (im >= nM) im = nM - 1;
                if (iz >= 0 && im >= 0) counts[im, iz]++;
            }

            var model = new PlotModel { Title = "Heatmap massa–redshift (densità)" };
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "z", Minimum = zMin, Maximum = zMax });
            model.Axes.Add(new LogarithmicAxis { Position = AxisPosition.Left, Title = "M_{500}^{SZ} [10^{14} M_\\odot]", Minimum = mMin, Maximum = mMax });

            var heatMap = new HeatMapSeries
            {
                X0 = zMin,
                X1 = zMax,
                Y0 = mMin,
                Y1 = mMax,
                Data = counts,
                Interpolate = false,
                RenderMethod = HeatMapRenderMethod.Rectangles
            };
            model.Series.Add(heatMap);
            model.Axes.Add(new OxyPlot.Axes.LinearColorAxis
            {
                Position = AxisPosition.Right,
                Title = "N",
                Palette = OxyPlot.OxyPalettes.Viridis(256)
            });
            PlotView.Model = model;
        }

        /// <summary>Mappa celeste: Aitoff (RA, Dec). Colore = massa oppure redshift (vista RA-Dec-z con OxyPlot 2D).</summary>
        private void PlotSkyMapAitoff(bool colorByRedshift)
        {
            var points = _filteredClusters
                .Where(c => c.Ra.HasValue && c.Dec.HasValue)
                .ToList();
            if (colorByRedshift)
                points = points.Where(c => c.Redshift.HasValue && c.Redshift.Value > 0).ToList();
            if (points.Count == 0)
            {
                PlotView.Model = new PlotModel
                {
                    Title = "Mappa celeste — Nessun dato con RA/Dec" + (colorByRedshift ? " e redshift" : "") + ". Verificare il CSV."
                };
                return;
            }

            const double deg2rad = Math.PI / 180.0;
            double mMin = points.Where(c => c.MassSz.HasValue).Select(c => c.MassSz!.Value).DefaultIfEmpty(1).Min();
            double mMax = points.Where(c => c.MassSz.HasValue).Select(c => c.MassSz!.Value).DefaultIfEmpty(10).Max();
            if (mMax <= mMin) mMax = mMin + 1;

            double zMin = 0.0;
            double zMax = 1.0;
            if (colorByRedshift)
            {
                zMin = points.Where(c => c.Redshift.HasValue).Select(c => c.Redshift!.Value).DefaultIfEmpty(0).Min();
                zMax = points.Where(c => c.Redshift.HasValue).Select(c => c.Redshift!.Value).DefaultIfEmpty(0.5).Max();
                if (zMax <= zMin) zMax = zMin + 0.1;
            }

            string title = colorByRedshift ? "Mappa RA–Dec–z (Aitoff, colore = redshift)" : "Mappa celeste (Aitoff) — cluster PSZ2";
            if (CosmologyOnlyCheckBox?.IsChecked == true)
                title += " — solo campione cosmologico";
            var model = new PlotModel { Title = title };

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Aitoff x",
                Minimum = -2.05,
                Maximum = 2.05
            });
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Aitoff y",
                Minimum = -1.05,
                Maximum = 1.05
            });

            var colorAxis = new OxyPlot.Axes.LinearColorAxis
            {
                Position = AxisPosition.Right,
                Title = colorByRedshift ? "z (redshift)" : "M_{500}^{SZ} [10^{14} M_\\odot]",
                Key = "MassColor",
                Minimum = colorByRedshift ? zMin : mMin,
                Maximum = colorByRedshift ? zMax : mMax,
                LowColor = OxyColors.DarkBlue,
                HighColor = OxyColors.DarkRed
            };
            model.Axes.Add(colorAxis);

            double sizeMin = 1.2;
            double sizeMax = 5.5;
            double massRange = mMax - mMin;
            if (massRange < 1e-6) massRange = 1;

            var scatter = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                ColorAxisKey = "MassColor",
                TrackerFormatString = colorByRedshift
                    ? "Nome: {Tag}\nAitoff (x,y) = ({2:0.2f}, {4:0.2f})\nz = {6:0.3f} (colore). Dimensione ∝ M_500^SZ."
                    : "Nome: {Tag}\nAitoff (x,y) = ({2:0.2f}, {4:0.2f})\nM_500^SZ = {6:0.00} × 10^14 M_⊙"
            };

            foreach (var c in points)
            {
                double raDeg = c.Ra!.Value;
                double decDeg = c.Dec!.Value;
                double lon = (raDeg - 180.0) * deg2rad;
                double lat = decDeg * deg2rad;
                double cosLat = Math.Cos(lat);
                double cosHalfLon = Math.Cos(lon * 0.5);
                double alpha = Math.Acos(Math.Max(-1, Math.Min(1, cosLat * cosHalfLon)));
                double sinc = (Math.Abs(alpha) < 1e-10) ? 1.0 : (Math.Sin(alpha) / alpha);
                double x = 2.0 * cosLat * Math.Sin(lon * 0.5) / sinc;
                double y = Math.Sin(lat) / sinc;
                double mass = c.MassSz ?? (mMin + mMax) * 0.5;
                double size = sizeMin + (sizeMax - sizeMin) * (mass - mMin) / massRange;
                double colorValue = colorByRedshift ? (c.Redshift ?? (zMin + zMax) * 0.5) : mass;
                scatter.Points.Add(new ScatterPoint(x, y, size, colorValue) { Tag = c.Name });
            }

            model.Series.Add(scatter);

            string caption = colorByRedshift
                ? "Vista RA–Dec–z: posizione = cielo (Aitoff), colore = redshift (distanza). Size ∝ massa.\nUtile per allineamenti e struttura a grande scala."
                : "Punti più grandi = cluster più massicci. Colore = M_500^SZ.\nDistribuzione angolare; assenza di copertura = maschera galattica Planck.";
            model.Annotations.Add(new OxyPlot.Annotations.TextAnnotation
            {
                Text = caption,
                TextPosition = new DataPoint(-2, -0.95),
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom,
                FontSize = 9,
                TextColor = OxyColors.Gray
            });

            PlotView.Model = model;
        }

        private void SnrMinTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_allClusters.Count > 0)
                ApplyFiltersAndUpdatePlot();
        }

        private void CosmologyOnlyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_allClusters.Count > 0)
                ApplyFiltersAndUpdatePlot();
        }

        private void ComparableOverlayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePlot();
        }

        private void ShowCatalog_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePlot();
        }


    }
}
