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

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
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

        private List<ClusterRecord> LoadClustersFromCsv(string path)
        {
            var list = new List<ClusterRecord>();
            using var reader = new StreamReader(path);

            string? header = reader.ReadLine();
            if (header == null)
                return list;

            // Assumo separatore ';' – cambia se necessario
            char sep = header.Contains(";") ? ';' : ',';
            var columns = header.Split(sep);
            for (int i = 0; i < columns.Length; i++)
                columns[i] = columns[i].Trim();

            string[] colsUpper = columns.Select(c => c.ToUpperInvariant()).ToArray();

            int idxName = Array.IndexOf(colsUpper, "NAME");
            int idxZ = Array.IndexOf(colsUpper, "REDSHIFT");        // o Z, se nel file è così
            int idxMass = Array.IndexOf(colsUpper, "MASS_SZ");         // o MSZ, M500, ecc.
            int idxSnr = Array.IndexOf(colsUpper, "SNR");
            int idxVal = Array.IndexOf(colsUpper, "VALIDATION_STATUS");
            int idxCosmo = Array.IndexOf(colsUpper, "COSMOLOGY_SAMPLE_FLAG");


            var culture = CultureInfo.InvariantCulture;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(sep);
                for (int i = 0; i < parts.Length; i++)
                    parts[i] = parts[i].Trim();

                double? TryParseDouble(int idx)
                {
                    if (idx < 0 || idx >= parts.Length) return null;
                    if (double.TryParse(parts[idx], NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
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

                var c = new ClusterRecord
                {
                    Name = idxName >= 0 && idxName < parts.Length ? parts[idxName] : "",
                    Redshift = TryParseDouble(idxZ),
                    MassSz = TryParseDouble(idxMass),
                    Snr = TryParseDouble(idxSnr) / 1e5,
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

            // se hai validation_status
            sample = sample.Where(c => !c.ValidationStatus.HasValue || c.ValidationStatus.Value > 0);

            // se vuoi usare solo il campione cosmologico:
            // sample = sample.Where(c => c.CosmologyFlag.HasValue && c.CosmologyFlag.Value == 1);

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
            }
        }

        // ---------------- GRAFICI ----------------

        private void PlotRedshiftHistogram()
        {
            var zValues = _filteredClusters
                .Where(c => c.Redshift.HasValue)
                .Select(c => c.Redshift!.Value)
                .ToList();

            if (zValues.Count == 0)
                return;

            int bins = 15;
            double zMin = zValues.Min();
            double zMax = zValues.Max();
            double dz = (zMax - zMin) / bins;
            if (dz <= 0) dz = 0.01;

            int[] counts = new int[bins];
            foreach (var z in zValues)
            {
                int bin = (int)((z - zMin) / dz);
                if (bin < 0) bin = 0;
                if (bin >= bins) bin = bins - 1;
                counts[bin]++;
            }

            var model = new PlotModel { Title = "Distribuzione dei redshift" };

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "z",
                Minimum = zMin,
                Maximum = zMax
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "N"
            });

            // Istogramma come curva a scalini
            var line = new LineSeries { MarkerType = MarkerType.None };

            double x = zMin;
            for (int i = 0; i < bins; i++)
            {
                double xNext = zMin + (i + 1) * dz;

                // punto iniziale del gradino
                line.Points.Add(new DataPoint(x, counts[i]));
                // punto finale del gradino (stessa N)
                line.Points.Add(new DataPoint(xNext, counts[i]));

                x = xNext;
            }

            model.Series.Add(line);
            PlotView.Model = model;
        }


        private void PlotMassHistogram()
        {
            var mValues = _filteredClusters
                .Where(c => c.MassSz.HasValue)
                .Select(c => c.MassSz!.Value)
                .ToList();

            if (mValues.Count == 0)
                return;

            int bins = 15;
            double mMin = mValues.Min();
            double mMax = mValues.Max();
            double dm = (mMax - mMin) / bins;
            if (dm <= 0) dm = 0.1;

            int[] counts = new int[bins];
            foreach (var m in mValues)
            {
                int bin = (int)((m - mMin) / dm);
                if (bin < 0) bin = 0;
                if (bin >= bins) bin = bins - 1;
                counts[bin]++;
            }

            var model = new PlotModel { Title = "Distribuzione delle masse SZ" };

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "M_{500}^{SZ} [10^{14} M_\\odot]",
                Minimum = mMin,
                Maximum = mMax
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "N"
            });

            var line = new LineSeries { MarkerType = MarkerType.None };

            double x = mMin;
            for (int i = 0; i < bins; i++)
            {
                double xNext = mMin + (i + 1) * dm;

                line.Points.Add(new DataPoint(x, counts[i]));
                line.Points.Add(new DataPoint(xNext, counts[i]));

                x = xNext;
            }

            model.Series.Add(line);
            PlotView.Model = model;
        }


        private void PlotMassRedshiftScatter()
        {
            var model = new PlotModel { Title = "Massa SZ vs redshift" };

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "z"
            });

            model.Axes.Add(new LogarithmicAxis
            {
                Position = AxisPosition.Left,
                Title = "M_{500}^{SZ} [10^{14} M_\\odot]"
            });

            var scatter = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 2,
                TrackerFormatString = "Nome: {Tag}\nz = {2:0.000}\nM_500^{SZ} = {4:0.00} × 10^{14} M☉"
            };

            foreach (var c in _filteredClusters.Where(c => c.Redshift.HasValue && c.MassSz.HasValue))
            {
                scatter.Points.Add(new ScatterPoint(c.Redshift!.Value, c.MassSz!.Value)
                {
                    Tag = c.Name
                });
            }

            model.Series.Add(scatter);
            PlotView.Model = model;
        }

        private void PlotSnrHistogram()
        {
            var sValues = _filteredClusters
                .Where(c => c.Snr.HasValue)
                .Select(c => c.Snr!.Value)
                .ToList();

            if (sValues.Count == 0)
                return;

            int bins = 15;
            double sMin = sValues.Min();
            double sMax = sValues.Max();
            double ds = (sMax - sMin) / bins;
            if (ds <= 0) ds = 0.5;

            int[] counts = new int[bins];
            foreach (var s in sValues)
            {
                int bin = (int)((s - sMin) / ds);
                if (bin < 0) bin = 0;
                if (bin >= bins) bin = bins - 1;
                counts[bin]++;
            }

            var model = new PlotModel { Title = "Distribuzione del rapporto segnale/rumore" };

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "SNR",
                Minimum = sMin,
                Maximum = sMax
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "N"
            });


            var line = new LineSeries { MarkerType = MarkerType.None };

            double x = sMin;
            for (int i = 0; i < bins; i++)
            {
                double xNext = sMin + (i + 1) * ds;

                line.Points.Add(new DataPoint(x, counts[i]));
                line.Points.Add(new DataPoint(xNext, counts[i]));

                x = xNext;
            }

            model.Series.Add(line);
            PlotView.Model = model;
        }
        private void SnrMinTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // evitiamo di esplodere se il CSV non è ancora stato caricato
            if (_allClusters.Count > 0)
                ApplyFiltersAndUpdatePlot();
        }


    }
}
