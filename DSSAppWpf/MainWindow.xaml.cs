using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DSSAppWpf.Models;
using DSSAppWpf.Services;
using Microsoft.Win32;
using IOPath = System.IO.Path;
using File = System.IO.File;
using StreamWriter = System.IO.StreamWriter;

namespace DSSAppWpf
{
    public partial class MainWindow : Window
    {
        private DatasetInfo _dataset;
        private List<AlgorithmDescriptor> _catalog;
        private ObservableCollection<AlgoVm> _algoVms;
        private List<ClassifierResult> _results = new List<ClassifierResult>();
        private ClassifierResult _bestResult;
        private AlgorithmDescriptor _bestDescriptor;
        private weka.classifiers.Classifier _activeModel;
        private weka.core.Instances _activeHeader;
        private AlgorithmDescriptor _activeDescriptor;
        private List<AlgorithmDescriptor> _resultDescriptors = new List<AlgorithmDescriptor>();
        private List<ModelChoice> _modelChoices = new List<ModelChoice>();
        private bool _suppressModelSelectorEvent;
        private readonly BackgroundWorker _worker;

        // Tahmin sayfasındaki "model seç" açılır listesinin tek bir öğesi.
        private sealed class ModelChoice
        {
            public AlgorithmDescriptor Descriptor;
            public weka.classifiers.Classifier Classifier;
            public weka.core.Instances Header;
            public ClassifierResult Result;       // confusion matrix vb. için (diskten yüklemede null)
            public string Display { get; set; }   // ComboBox'ta görünen metin
            public string StatusLine { get; set; } // altta lblActiveModel'de görünen metin
        }

        public MainWindow()
        {
            InitializeComponent();

            _catalog = ClassifierFactory.BuildCatalog();
            _algoVms = new ObservableCollection<AlgoVm>(
                _catalog.Select(d => new AlgoVm(d)));
            algoItems.ItemsSource = _algoVms;

            _worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = false
            };
            _worker.DoWork += Worker_DoWork;
            _worker.ProgressChanged += Worker_ProgressChanged;
            _worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string defaultPath = IOPath.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "iris.arff");
            if (File.Exists(defaultPath))
                LoadDataset(defaultPath);
        }

        // ============= NAVIGATION =============
        private void navList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (page0 == null || page1 == null || page2 == null || page3 == null
                || page4 == null || lblPageTitle == null || lblPageSub == null)
                return;

            int idx = navList.SelectedIndex;
            if (idx < 0) return;

            Grid[] pages = { page0, page1, page2, page3, page4 };
            string[] titles = { "Veri Seti", "Algoritmalar", "Sonuçlar", "Tahmin",
                                "Analiz & Görselleştirme" };
            string[] subs =
            {
                "Bir .arff veya .csv dosyası yükleyin ve incelenin",
                "Değerlendirme yöntemini seçin ve algoritmaları çalıştırın",
                "16 algoritma karşılaştırması, en iyi seçim, metrikler",
                "En iyi (veya yüklenen) model ile tek bir örnekte tahmin",
                "Confusion matrix, öznitelik önemi ve dağılım grafiği"
            };

            for (int i = 0; i < pages.Length; i++)
                pages[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;

            lblPageTitle.Text = titles[idx];
            lblPageSub.Text = subs[idx];

            if (idx == 4) RefreshAnalysisPage();
        }

        // ============= STATUS HELPER =============
        private void SetStatus(string text, string state)
        {
            lblStatus.Text = text;
            switch (state)
            {
                case "running": statusDot.Fill = (Brush)FindResource("B.Warning"); break;
                case "error": statusDot.Fill = (Brush)FindResource("B.Danger"); break;
                default: statusDot.Fill = (Brush)FindResource("B.Success"); break;
            }
        }

        // ============= PAGE 1: DATASET =============
        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Veri Seti (*.arff;*.csv)|*.arff;*.csv|Tüm dosyalar (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) == true)
                LoadDataset(dlg.FileName);
        }

        private void LoadDataset(string path)
        {
            try
            {
                SetStatus("Yükleniyor...", "running");
                _dataset = DatasetLoader.Load(path);
                txtPath.Text = path;
                lblChipDataset.Text = IOPath.GetFileName(path);
                datasetChip.Visibility = Visibility.Visible;

                statInstances.Text = _dataset.InstanceCount.ToString();
                statAttributes.Text = _dataset.AttributeCount.ToString();
                statClasses.Text = _dataset.ClassDistribution.Count.ToString();
                statMissing.Text = _dataset.MissingValueCount.ToString();

                BuildClassDistribution();
                dgAttributes.ItemsSource = _dataset.Attributes;
                dgPreview.ItemsSource = _dataset.PreviewTable.DefaultView;

                BuildPredictionInputs();

                // Yeni veri setiyle eski eğitilmiş modeller geçersiz; seçim listesini sıfırla.
                _results.Clear();
                _resultDescriptors.Clear();
                _modelChoices.Clear();
                _activeModel = null;
                RefreshModelCombo(-1);
                lblActiveModel.Text = "Model yüklü değil. Önce algoritmaları çalıştırın ya da diskten yükleyin.";

                SetStatus("Hazır — " + _dataset.InstanceCount + " örnek", "ok");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Veri seti yüklenemedi:\n" + ex.Message,
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Hata", "error");
            }
        }

        private void BuildClassDistribution()
        {
            int max = 1;
            foreach (var v in _dataset.ClassDistribution.Values)
                if (v > max) max = v;

            var list = new List<ClassDistVm>();
            foreach (var kv in _dataset.ClassDistribution)
            {
                double pct = _dataset.InstanceCount == 0 ? 0
                    : 100.0 * kv.Value / _dataset.InstanceCount;
                list.Add(new ClassDistVm
                {
                    Label = kv.Key,
                    CountText = kv.Value + " (" + pct.ToString("F1") + "%)",
                    BarWidth = Math.Max(4, 240.0 * kv.Value / max)
                });
            }
            classDistList.ItemsSource = list;
        }

        private void ShowAttrsTab_Click(object sender, RoutedEventArgs e)
        {
            dgAttributes.Visibility = Visibility.Visible;
            dgPreview.Visibility = Visibility.Collapsed;
            tabAttrBtn.BorderBrush = (Brush)FindResource("B.Primary");
            tabAttrBtn.Foreground = (Brush)FindResource("B.Primary");
            tabPreviewBtn.BorderBrush = (Brush)FindResource("B.CardBorder");
            tabPreviewBtn.Foreground = (Brush)FindResource("B.TextPrimary");
        }

        private void ShowPreviewTab_Click(object sender, RoutedEventArgs e)
        {
            dgAttributes.Visibility = Visibility.Collapsed;
            dgPreview.Visibility = Visibility.Visible;
            tabPreviewBtn.BorderBrush = (Brush)FindResource("B.Primary");
            tabPreviewBtn.Foreground = (Brush)FindResource("B.Primary");
            tabAttrBtn.BorderBrush = (Brush)FindResource("B.CardBorder");
            tabAttrBtn.Foreground = (Brush)FindResource("B.TextPrimary");
        }

        // ============= PAGE 2: ALGORITHMS =============
        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var v in _algoVms) v.IsSelected = true;
            algoItems.Items.Refresh();
        }

        private void btnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var v in _algoVms) v.IsSelected = false;
            algoItems.Items.Refresh();
        }

        private void AlgoRow_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;
            var d = border.Tag as AlgorithmDescriptor;
            if (d == null) return;

            lblAlgoName.Text = d.DisplayName;
            lblAlgoFamily.Text = d.Family.ToString();
            lblAlgoPrep.Text = PrepLabel(d.Preprocessing);
            try { lblAlgoClass.Text = d.Builder().GetType().FullName; }
            catch { lblAlgoClass.Text = "(N/A)"; }
            lblAlgoDesc.Text = d.Description;

            BuildParamControls(d);
        }

        // Seçili algoritmanın ayarlanabilir hiperparametreleri için kaydırıcılar üretir.
        private void BuildParamControls(AlgorithmDescriptor d)
        {
            paramHost.Children.Clear();
            bool has = d.HyperParams != null && d.HyperParams.Count > 0;
            lblParamTitle.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            lblParamHint.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            if (!has) return;

            foreach (var p in d.HyperParams)
            {
                HyperParam param = p; // closure için yerel kopya

                var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

                var lbl = new TextBlock
                {
                    Text = param.Name,
                    Style = (Style)FindResource("T.Body"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(lbl, 0);
                row.Children.Add(lbl);

                var slider = new Slider
                {
                    Minimum = param.Min,
                    Maximum = param.Max,
                    Value = param.Value,
                    TickFrequency = param.Step,
                    IsSnapToTickEnabled = true,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(slider, 1);
                row.Children.Add(slider);

                var valText = new TextBlock
                {
                    Text = param.ValueText,
                    Style = (Style)FindResource("T.Body"),
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(valText, 2);
                row.Children.Add(valText);

                slider.ValueChanged += delegate
                {
                    param.Value = param.IsInteger ? Math.Round(slider.Value) : slider.Value;
                    valText.Text = param.ValueText;
                };

                paramHost.Children.Add(row);
            }
        }

        private static string PrepLabel(PreprocessingMode m)
        {
            switch (m)
            {
                case PreprocessingMode.NormalizeAndDummy: return "Normalize + NominalToBinary";
                case PreprocessingMode.DiscretizeOnly: return "Discretize";
                case PreprocessingMode.DummyOnly: return "NominalToBinary";
                default: return "(yok)";
            }
        }

        // ============= RUN =============
        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_dataset == null)
            {
                MessageBox.Show(this, "Önce bir veri seti yükleyin.",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                navList.SelectedIndex = 0;
                return;
            }

            var selected = _algoVms.Where(v => v.IsSelected).Select(v => v.Descriptor).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "En az bir algoritma seçin.",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int seed;
            if (!int.TryParse(txtSeed.Text, out seed) || seed < 1) seed = 1;

            var opts = new EvaluationOptions
            {
                Method = radCV.IsChecked == true
                    ? EvaluationMethod.TenFoldCrossValidation
                    : EvaluationMethod.PercentageSplit,
                PercentSplit = (int)sliSplit.Value,
                Folds = 10,
                RandomSeed = seed
            };

            btnRun.IsEnabled = false;
            prog.Visibility = Visibility.Visible;
            prog.Maximum = selected.Count;
            prog.Value = 0;
            lblRunStatus.Text = "Hazırlanıyor...";
            SetStatus("Çalışıyor", "running");

            _worker.RunWorkerAsync(new RunCtx
            {
                Algorithms = selected,
                Options = opts,
                SourceInstances = _dataset.RawInstances
            });
        }

        private sealed class RunCtx
        {
            public List<AlgorithmDescriptor> Algorithms;
            public EvaluationOptions Options;
            public weka.core.Instances SourceInstances;
        }

        private sealed class ProgressInfo
        {
            public int Current;
            public int Total;
            public string AlgorithmName;
        }

        private sealed class RunOutcome
        {
            public List<ClassifierResult> Results;
            public List<AlgorithmDescriptor> Descriptors;
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var ctx = (RunCtx)e.Argument;
            var results = new List<ClassifierResult>();
            int counter = 0;
            foreach (var d in ctx.Algorithms)
            {
                counter++;
                _worker.ReportProgress(counter, new ProgressInfo
                {
                    Current = counter,
                    Total = ctx.Algorithms.Count,
                    AlgorithmName = d.DisplayName
                });
                var r = EvaluationEngine.Evaluate(ctx.SourceInstances, d, ctx.Options);
                results.Add(r);
            }
            e.Result = new RunOutcome
            {
                Results = results,
                Descriptors = ctx.Algorithms
            };
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var p = (ProgressInfo)e.UserState;
            prog.Value = Math.Min(p.Current, prog.Maximum);
            lblRunStatus.Text = "Çalışıyor: " + p.AlgorithmName + " (" + p.Current + "/" + p.Total + ")";
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnRun.IsEnabled = true;
            prog.Visibility = Visibility.Collapsed;

            if (e.Error != null)
            {
                MessageBox.Show(this, "Çalışma sırasında hata:\n" + e.Error.Message,
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Hata", "error");
                return;
            }

            var outcome = (RunOutcome)e.Result;
            _results = outcome.Results;
            _resultDescriptors = outcome.Descriptors;
            PickBest(outcome);
            PopulateModelSelector();
            PopulateResultsGrid();
            PopulateBestHero();
            DrawChart();

            lblRunStatus.Text = "Tamamlandı — " + _results.Count + " algoritma";
            SetStatus("Bitti", "ok");
            navList.SelectedIndex = 2;
        }

        private void PickBest(RunOutcome outcome)
        {
            _bestResult = null;
            _bestDescriptor = null;
            double best = -1;
            for (int i = 0; i < outcome.Results.Count; i++)
            {
                var r = outcome.Results[i];
                if (!r.IsSuccessful) continue;
                if (r.Accuracy > best)
                {
                    best = r.Accuracy;
                    _bestResult = r;
                    _bestDescriptor = outcome.Descriptors[i];
                }
            }
            if (_bestResult != null)
            {
                _activeModel = _bestResult.TrainedClassifier;
                _activeHeader = _dataset.RawInstances;
                _activeDescriptor = _bestDescriptor;
                lblActiveModel.Text =
                    "Algoritma: " + _bestDescriptor.DisplayName +
                    "   |   Accuracy: " + _bestResult.Accuracy.ToString("F2") + "%" +
                    "   |   Doğru: " + _bestResult.CorrectCount + " / " + _bestResult.TestSize;
            }
        }

        // Çalıştırılan tüm (başarılı) algoritmaları Tahmin sayfasındaki açılır listeye
        // doldurur. Varsayılan olarak en iyi accuracy'li model seçili gelir; kullanıcı
        // istediği algoritmayı seçerek o modelle tahmin yapabilir.
        private void PopulateModelSelector()
        {
            _modelChoices = new List<ModelChoice>();
            int bestIdx = -1;

            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                if (!r.IsSuccessful || r.TrainedClassifier == null) continue;

                AlgorithmDescriptor d = (i < _resultDescriptors.Count) ? _resultDescriptors[i] : null;
                if (d == null) d = _catalog.FirstOrDefault(c => c.Key == r.AlgorithmKey);
                string name = d != null ? d.DisplayName : r.AlgorithmName;

                if (r == _bestResult) bestIdx = _modelChoices.Count;

                _modelChoices.Add(new ModelChoice
                {
                    Descriptor = d,
                    Classifier = r.TrainedClassifier,
                    Header = _dataset != null ? _dataset.RawInstances : _activeHeader,
                    Result = r,
                    Display = name + "   —   Accuracy " + r.Accuracy.ToString("F2") + "%"
                              + (r == _bestResult ? "   ⭐ en iyi" : ""),
                    StatusLine = "Algoritma: " + name +
                                 "   |   Accuracy: " + r.Accuracy.ToString("F2") + "%" +
                                 "   |   Doğru: " + r.CorrectCount + " / " + r.TestSize
                });
            }

            RefreshModelCombo(bestIdx >= 0 ? bestIdx : 0);
        }

        // Açılır listeyi _modelChoices ile tazeler ve verilen öğeyi seçili yapar.
        private void RefreshModelCombo(int selectedIndex)
        {
            _suppressModelSelectorEvent = true;
            cmbActiveModel.ItemsSource = null;
            cmbActiveModel.ItemsSource = _modelChoices;
            cmbActiveModel.Visibility = _modelChoices.Count > 0
                ? Visibility.Visible : Visibility.Collapsed;
            cmbActiveModel.SelectedIndex = _modelChoices.Count > 0
                ? (selectedIndex >= 0 && selectedIndex < _modelChoices.Count ? selectedIndex : 0)
                : -1;
            _suppressModelSelectorEvent = false;

            var sel = cmbActiveModel.SelectedItem as ModelChoice;
            if (sel != null) ApplyModelChoice(sel);
        }

        // Seçilen modeli aktif model yapar ve tahmin alanlarını yeniden kurar.
        private void ApplyModelChoice(ModelChoice c)
        {
            if (c == null) return;
            _activeModel = c.Classifier;
            _activeHeader = c.Header;
            _activeDescriptor = c.Descriptor;
            lblActiveModel.Text = c.StatusLine;
            BuildPredictionInputs();
        }

        private void cmbActiveModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressModelSelectorEvent) return;
            ApplyModelChoice(cmbActiveModel.SelectedItem as ModelChoice);
        }

        // ============= PAGE 3: RESULTS =============
        private void PopulateResultsGrid()
        {
            var ordered = _results
                .OrderByDescending(r => r.IsSuccessful ? r.Accuracy : -1)
                .ToList();
            dgResults.ItemsSource = ordered;
        }

        private void PopulateBestHero()
        {
            if (_bestResult == null || _bestDescriptor == null)
            {
                lblBestName.Text = "(Sonuç yok)";
                lblBestSub.Text = "";
                return;
            }
            lblBestName.Text = _bestDescriptor.DisplayName;
            lblBestSub.Text = string.Format(CultureInfo.InvariantCulture,
                "Doğru tahmin: {0} / {1}     •     Accuracy: {2:F2}%     •     F-Measure: {3}     •     Süre: {4} ms",
                _bestResult.CorrectCount,
                _bestResult.TestSize,
                _bestResult.Accuracy,
                _bestResult.FMeasureText,
                _bestResult.ElapsedMilliseconds);
        }

        private void chartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawChart();
        }

        private void DrawChart()
        {
            chartCanvas.Children.Clear();
            if (_results == null) return;

            var data = _results.Where(r => r.IsSuccessful)
                .OrderByDescending(r => r.Accuracy).ToList();
            if (data.Count == 0) return;

            double w = chartCanvas.ActualWidth;
            double h = chartCanvas.ActualHeight;
            if (w < 50 || h < 50) return;

            const double leftPad = 36;
            const double bottomPad = 56;
            const double topPad = 12;
            const double rightPad = 12;

            double plotW = w - leftPad - rightPad;
            double plotH = h - topPad - bottomPad;

            // Y grid lines (0..100 by 25)
            Brush gridBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));
            Brush axisText = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            for (int p = 0; p <= 100; p += 25)
            {
                double y = topPad + plotH * (1 - p / 100.0);
                Line line = new Line
                {
                    X1 = leftPad, X2 = leftPad + plotW,
                    Y1 = y, Y2 = y,
                    Stroke = gridBrush, StrokeThickness = 1
                };
                if (p > 0 && p < 100)
                {
                    line.StrokeDashArray = new DoubleCollection { 2, 3 };
                }
                chartCanvas.Children.Add(line);

                TextBlock yLab = new TextBlock
                {
                    Text = p.ToString(),
                    Foreground = axisText,
                    FontSize = 10
                };
                Canvas.SetLeft(yLab, 4);
                Canvas.SetTop(yLab, y - 7);
                chartCanvas.Children.Add(yLab);
            }

            int n = data.Count;
            double slot = plotW / n;
            double barGroupW = Math.Min(slot * 0.65, 60);
            double barW = barGroupW / 2 - 2;

            Brush primary = (Brush)FindResource("B.Primary");
            Brush accent2 = (Brush)FindResource("B.Accent2");
            Brush bestPrimary = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
            Brush bestAccent = new SolidColorBrush(Color.FromRgb(0xF5, 0x7C, 0x00));

            for (int i = 0; i < n; i++)
            {
                var r = data[i];
                bool isBest = (r == _bestResult);

                double cx = leftPad + slot * i + slot / 2;
                double accX = cx - barGroupW / 2;
                double fX = accX + barW + 4;

                double accH = plotH * (r.Accuracy / 100.0);
                double f = double.IsNaN(r.WeightedFMeasure) ? 0 : r.WeightedFMeasure * 100.0;
                double fH = plotH * (f / 100.0);

                Rectangle accBar = new Rectangle
                {
                    Width = barW, Height = Math.Max(2, accH),
                    Fill = isBest ? bestPrimary : primary,
                    RadiusX = 3, RadiusY = 3,
                    ToolTip = r.AlgorithmName + "\nAccuracy: " + r.Accuracy.ToString("F2") + "%" +
                              "\nDoğru: " + r.CorrectCount + " / " + r.TestSize
                };
                Canvas.SetLeft(accBar, accX);
                Canvas.SetTop(accBar, topPad + plotH - accH);
                chartCanvas.Children.Add(accBar);

                Rectangle fBar = new Rectangle
                {
                    Width = barW, Height = Math.Max(2, fH),
                    Fill = isBest ? bestAccent : accent2,
                    RadiusX = 3, RadiusY = 3,
                    ToolTip = r.AlgorithmName + "\nF-Measure: " + r.FMeasureText +
                              "\nPrecision: " + r.PrecisionText +
                              "\nRecall: " + r.RecallText
                };
                Canvas.SetLeft(fBar, fX);
                Canvas.SetTop(fBar, topPad + plotH - fH);
                chartCanvas.Children.Add(fBar);

                // X label (rotated)
                TextBlock xLab = new TextBlock
                {
                    Text = ShortLabel(r.AlgorithmName),
                    Foreground = axisText,
                    FontSize = 10,
                    RenderTransform = new RotateTransform(-30),
                    RenderTransformOrigin = new Point(0, 0)
                };
                Canvas.SetLeft(xLab, cx - 10);
                Canvas.SetTop(xLab, topPad + plotH + 10);
                chartCanvas.Children.Add(xLab);

                if (isBest)
                {
                    TextBlock val = new TextBlock
                    {
                        Text = r.Accuracy.ToString("F1"),
                        FontSize = 10, FontWeight = FontWeights.Bold,
                        Foreground = bestPrimary
                    };
                    Canvas.SetLeft(val, accX - 4);
                    Canvas.SetTop(val, topPad + plotH - accH - 16);
                    chartCanvas.Children.Add(val);
                }
            }
        }

        private static string ShortLabel(string name)
        {
            return name.Length > 14 ? name.Substring(0, 13) + "…" : name;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_results.Count == 0)
            {
                MessageBox.Show(this, "Dışa aktarılacak sonuç yok.",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = "results.csv"
            };
            if (dlg.ShowDialog(this) != true) return;

            using (var w = new StreamWriter(dlg.FileName, false, Encoding.UTF8))
            {
                w.WriteLine("Algorithm;Correct;Incorrect;TestSize;Accuracy;Precision;Recall;FMeasure;Kappa;TimeMs;Status");
                foreach (var r in _results)
                {
                    w.WriteLine(string.Join(";", new[] {
                        r.AlgorithmName,
                        r.IsSuccessful ? r.CorrectCount.ToString() : "",
                        r.IsSuccessful ? r.IncorrectCount.ToString() : "",
                        r.IsSuccessful ? r.TestSize.ToString() : "",
                        r.IsSuccessful ? r.Accuracy.ToString("F4", CultureInfo.InvariantCulture) : "",
                        r.IsSuccessful ? r.WeightedPrecision.ToString("F4", CultureInfo.InvariantCulture) : "",
                        r.IsSuccessful ? r.WeightedRecall.ToString("F4", CultureInfo.InvariantCulture) : "",
                        r.IsSuccessful ? r.WeightedFMeasure.ToString("F4", CultureInfo.InvariantCulture) : "",
                        r.IsSuccessful ? r.KappaStatistic.ToString("F4", CultureInfo.InvariantCulture) : "",
                        r.ElapsedMilliseconds.ToString(),
                        r.IsSuccessful ? "OK" : ("ERROR: " + r.ErrorMessage)
                    }));
                }
            }
            SetStatus("CSV kaydedildi", "ok");
        }

        // ============= MODEL SAVE / LOAD =============
        private void btnSaveModel_Click(object sender, RoutedEventArgs e)
        {
            if (_activeModel == null || _activeDescriptor == null)
            {
                MessageBox.Show(this, "Kaydedilecek model yok.\nÖnce algoritmaları çalıştırın.",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dlg = new SaveFileDialog
            {
                Filter = "WEKA Model (*.model)|*.model",
                DefaultExt = "model",
                FileName = _activeDescriptor.Key + ".model"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                ModelPersistence.Save(dlg.FileName, _activeModel, _activeDescriptor, _activeHeader);
                SetStatus("Model kaydedildi", "ok");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Model kaydedilemedi:\n" + ex.Message,
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnLoadModel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "WEKA Model (*.model)|*.model|Tüm dosyalar (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var lm = ModelPersistence.Load(dlg.FileName);
                _activeModel = lm.Classifier;
                _activeHeader = lm.Header;
                _activeDescriptor = _catalog.FirstOrDefault(d => d.Key == lm.AlgorithmKey)
                    ?? _catalog[0];

                if (_dataset == null || _dataset.RawInstances == null)
                {
                    _dataset = new DatasetInfo { RawInstances = lm.Header };
                    PopulateAttributesFromHeader(lm.Header);
                }

                // Yüklenen modeli açılır listeye ekle ve seçili yap.
                string fileName = IOPath.GetFileName(dlg.FileName);
                _modelChoices.Add(new ModelChoice
                {
                    Descriptor = _activeDescriptor,
                    Classifier = lm.Classifier,
                    Header = lm.Header,
                    Display = "Yüklenen: " + fileName + "   —   " + _activeDescriptor.DisplayName,
                    StatusLine = "Yüklendi: " + fileName +
                                 "  |  Algoritma: " + _activeDescriptor.DisplayName
                });
                RefreshModelCombo(_modelChoices.Count - 1);

                navList.SelectedIndex = 3;
                SetStatus("Model yüklendi", "ok");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Model yüklenemedi:\n" + ex.Message,
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulateAttributesFromHeader(weka.core.Instances header)
        {
            _dataset.Attributes.Clear();
            for (int i = 0; i < header.numAttributes(); i++)
            {
                var a = header.attribute(i);
                var ai = new AttributeInfo
                {
                    Index = i, Name = a.name(),
                    IsNumeric = a.isNumeric(), IsNominal = a.isNominal(),
                    NominalValues = new List<string>()
                };
                if (a.isNominal())
                    for (int v = 0; v < a.numValues(); v++) ai.NominalValues.Add(a.value(v));
                ai.Type = a.isNumeric() ? "Numeric" : (a.isNominal() ? "Nominal" : "Other");
                _dataset.Attributes.Add(ai);
            }
            _dataset.ClassAttributeName = header.classAttribute().name();
            _dataset.ClassIsNominal = header.classAttribute().isNominal();
        }

        // ============= PAGE 4: PREDICT =============
        private void BuildPredictionInputs()
        {
            predictInputs.Children.Clear();
            if (_dataset == null || _dataset.Attributes.Count == 0) return;

            int classIdx = _dataset.RawInstances != null
                ? _dataset.RawInstances.classIndex()
                : _dataset.Attributes.Count - 1;

            foreach (var ai in _dataset.Attributes)
            {
                if (ai.Index == classIdx) continue;

                var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var lbl = new TextBlock
                {
                    Text = ai.Name,
                    Style = (Style)FindResource("T.Body"),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold
                };
                Grid.SetColumn(lbl, 0);
                row.Children.Add(lbl);

                Control input;
                if (ai.IsNominal)
                {
                    var cb = new ComboBox
                    {
                        Style = (Style)FindResource("S.ComboBox"),
                        Tag = ai
                    };
                    foreach (var v in ai.NominalValues) cb.Items.Add(v);
                    if (cb.Items.Count > 0) cb.SelectedIndex = 0;
                    input = cb;
                }
                else
                {
                    var tb = new TextBox
                    {
                        Style = (Style)FindResource("S.TextBox"),
                        Tag = ai,
                        Text = "0"
                    };
                    input = tb;
                }
                Grid.SetColumn(input, 1);
                row.Children.Add(input);

                var typeBadge = new Border
                {
                    Background = ai.IsNumeric
                        ? (Brush)FindResource("B.Primary")
                        : (Brush)FindResource("B.Accent3"),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(10, 0, 0, 0),
                    Child = new TextBlock
                    {
                        Text = ai.IsNumeric ? "NUMERIC" : "NOMINAL",
                        Foreground = Brushes.White,
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold
                    }
                };
                Grid.SetColumn(typeBadge, 2);
                row.Children.Add(typeBadge);

                predictInputs.Children.Add(row);
            }
        }

        private void btnPredict_Click(object sender, RoutedEventArgs e)
        {
            if (_activeModel == null || _activeHeader == null)
            {
                MessageBox.Show(this, "Önce bir model oluşturun veya yükleyin.",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var single = BuildNewInstanceFromInputs();
                single.setClassIndex(single.numAttributes() - 1);
                var filtered = ApplySamePreprocessing(single);
                double predicted = _activeModel.classifyInstance(filtered.instance(0));

                string label = filtered.classAttribute().isNominal()
                    ? filtered.classAttribute().value((int)predicted)
                    : predicted.ToString("F4", CultureInfo.InvariantCulture);

                lblPredictionResult.Text = label;
                ShowClassProbabilities(filtered, (int)predicted);
                SetStatus("Tahmin: " + label, "ok");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Tahmin sırasında hata:\n" + ex.Message,
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private weka.core.Instances BuildNewInstanceFromInputs()
        {
            var empty = new weka.core.Instances(_activeHeader, 0);
            empty.setClassIndex(empty.numAttributes() - 1);
            double[] vals = new double[empty.numAttributes()];
            var inputMap = CollectInputControls();

            for (int i = 0; i < empty.numAttributes(); i++)
            {
                var attr = empty.attribute(i);
                if (i == empty.classIndex()) { vals[i] = double.NaN; continue; }
                if (!inputMap.ContainsKey(i)) { vals[i] = double.NaN; continue; }

                var c = inputMap[i];
                if (attr.isNumeric())
                {
                    string txt = (c is TextBox ? ((TextBox)c).Text : "").Trim().Replace(',', '.');
                    double d;
                    if (!double.TryParse(txt, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out d))
                        throw new FormatException(attr.name() + " için geçersiz sayı: " + txt);
                    vals[i] = d;
                }
                else if (attr.isNominal())
                {
                    string sel = c is ComboBox
                        ? Convert.ToString(((ComboBox)c).SelectedItem)
                        : "";
                    int idx = attr.indexOfValue(sel);
                    if (idx < 0) throw new FormatException(
                        attr.name() + " için geçersiz nominal değer: " + sel);
                    vals[i] = idx;
                }
                else vals[i] = double.NaN;
            }

            var inst = new weka.core.Instance(1.0, vals);
            inst.setDataset(empty);
            empty.add(inst);
            return empty;
        }

        private Dictionary<int, Control> CollectInputControls()
        {
            var map = new Dictionary<int, Control>();
            foreach (var child in predictInputs.Children)
            {
                var grid = child as Grid;
                if (grid == null) continue;
                foreach (var c in grid.Children)
                {
                    var ctrl = c as Control;
                    if (ctrl == null) continue;
                    var ai = ctrl.Tag as AttributeInfo;
                    if (ai != null) map[ai.Index] = ctrl;
                }
            }
            return map;
        }

        private weka.core.Instances ApplySamePreprocessing(weka.core.Instances single)
        {
            var mode = _activeDescriptor != null
                ? _activeDescriptor.Preprocessing
                : InferPreprocessingFromClassifier(_activeModel);

            switch (mode)
            {
                case PreprocessingMode.NormalizeAndDummy:
                    single = ApplyFilter(single, new weka.filters.unsupervised.attribute.Normalize());
                    single = ApplyFilter(single, new weka.filters.unsupervised.attribute.NominalToBinary());
                    return single;
                case PreprocessingMode.DiscretizeOnly:
                    return ApplyFilter(single, new weka.filters.unsupervised.attribute.Discretize());
                case PreprocessingMode.DummyOnly:
                    return ApplyFilter(single, new weka.filters.unsupervised.attribute.NominalToBinary());
                default:
                    return single;
            }
        }

        private static weka.core.Instances ApplyFilter(
            weka.core.Instances data, weka.filters.Filter f)
        {
            f.setInputFormat(data);
            return weka.filters.Filter.useFilter(data, f);
        }

        private static PreprocessingMode InferPreprocessingFromClassifier(
            weka.classifiers.Classifier c)
        {
            if (c == null) return PreprocessingMode.None;
            string name = c.GetType().FullName;
            if (name == "weka.classifiers.lazy.IBk" ||
                name == "weka.classifiers.functions.MultilayerPerceptron" ||
                name == "weka.classifiers.functions.SMO" ||
                name == "weka.classifiers.functions.Logistic")
                return PreprocessingMode.NormalizeAndDummy;
            if (name == "weka.classifiers.bayes.NaiveBayes" ||
                name == "weka.classifiers.bayes.BayesNet" ||
                name == "weka.classifiers.rules.OneR")
                return PreprocessingMode.DiscretizeOnly;
            return PreprocessingMode.None;
        }

        // ============= TAHMİN: SINIF OLASILIKLARI =============
        private void ShowClassProbabilities(weka.core.Instances filtered, int predictedIdx)
        {
            predictProbs.ItemsSource = null;
            lblProbTitle.Visibility = Visibility.Collapsed;

            var cls = filtered.classAttribute();
            if (cls == null || !cls.isNominal()) return; // sayısal sınıfta olasılık yok

            double[] dist;
            try { dist = _activeModel.distributionForInstance(filtered.instance(0)); }
            catch { return; }
            if (dist == null || dist.Length == 0) return;

            double max = 0;
            for (int i = 0; i < dist.Length; i++) if (dist[i] > max) max = dist[i];
            if (max <= 0) max = 1;

            var list = new List<ProbVm>();
            for (int i = 0; i < dist.Length && i < cls.numValues(); i++)
            {
                bool top = (i == predictedIdx);
                list.Add(new ProbVm
                {
                    Label = cls.value(i),
                    RawValue = dist[i],
                    PctText = (dist[i] * 100.0).ToString("F1", CultureInfo.InvariantCulture) + " %",
                    BarWidth = Math.Max(2.0, 420.0 * dist[i] / max),
                    Brush = top ? (Brush)FindResource("B.Success") : (Brush)FindResource("B.Primary"),
                    Weight = top ? FontWeights.Bold : FontWeights.Normal
                });
            }
            list.Sort(delegate (ProbVm a, ProbVm b) { return b.RawValue.CompareTo(a.RawValue); });
            predictProbs.ItemsSource = list;
            lblProbTitle.Visibility = Visibility.Visible;
        }

        // ============= PAGE 5: ANALIZ =============
        private void RefreshAnalysisPage()
        {
            // Confusion matrix için model listesi (çalıştırılmış modeller)
            object prev = cmbCmModel.SelectedItem;
            cmbCmModel.ItemsSource = null;
            cmbCmModel.ItemsSource = _modelChoices;
            if (_modelChoices.Count > 0)
            {
                int idx = prev != null ? _modelChoices.IndexOf(prev as ModelChoice) : -1;
                cmbCmModel.SelectedIndex = idx >= 0 ? idx : 0;
            }
            else
            {
                DrawConfusionMatrix(null);
            }

            ComputeFeatureImportance();
            PopulateScatterCombos();
            DrawScatter();
        }

        private void SetAnalysisTab(int tab)
        {
            cmPanel.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
            featPanel.Visibility = tab == 1 ? Visibility.Visible : Visibility.Collapsed;
            scatterPanel.Visibility = tab == 2 ? Visibility.Visible : Visibility.Collapsed;

            Brush on = (Brush)FindResource("B.Primary");
            Brush off = (Brush)FindResource("B.CardBorder");
            Brush onTxt = (Brush)FindResource("B.Primary");
            Brush offTxt = (Brush)FindResource("B.TextPrimary");

            tabCmBtn.BorderBrush = tab == 0 ? on : off;
            tabCmBtn.Foreground = tab == 0 ? onTxt : offTxt;
            tabFeatBtn.BorderBrush = tab == 1 ? on : off;
            tabFeatBtn.Foreground = tab == 1 ? onTxt : offTxt;
            tabScatterBtn.BorderBrush = tab == 2 ? on : off;
            tabScatterBtn.Foreground = tab == 2 ? onTxt : offTxt;

            if (tab == 2) DrawScatter();
        }

        private void ShowCmTab_Click(object sender, RoutedEventArgs e) { SetAnalysisTab(0); }
        private void ShowFeatTab_Click(object sender, RoutedEventArgs e) { SetAnalysisTab(1); }
        private void ShowScatterTab_Click(object sender, RoutedEventArgs e) { SetAnalysisTab(2); }

        private void cmbCmModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DrawConfusionMatrix(cmbCmModel.SelectedItem as ModelChoice);
        }

        // Confusion matrix'i renk yoğunluklu bir tablo olarak çizer.
        private void DrawConfusionMatrix(ModelChoice choice)
        {
            if (cmGrid == null) return;
            cmGrid.Children.Clear();
            cmGrid.RowDefinitions.Clear();
            cmGrid.ColumnDefinitions.Clear();

            var result = choice != null ? choice.Result : null;
            if (result == null || result.ConfusionMatrix == null || result.ClassLabels == null)
            {
                var msg = new TextBlock
                {
                    Text = "Bu model için confusion matrix yok. Önce algoritmaları çalıştırın.",
                    Style = (Style)FindResource("T.Muted"),
                    Margin = new Thickness(2)
                };
                cmGrid.RowDefinitions.Add(new RowDefinition());
                cmGrid.ColumnDefinitions.Add(new ColumnDefinition());
                cmGrid.Children.Add(msg);
                return;
            }

            double[][] cm = result.ConfusionMatrix;
            string[] labels = result.ClassLabels;
            int n = labels.Length;

            double maxCell = 1;
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                    if (cm[r][c] > maxCell) maxCell = cm[r][c];

            const double cell = 104;
            const double headerW = 150;

            cmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(headerW) });
            for (int c = 0; c < n; c++)
                cmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cell) });
            cmGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int r = 0; r < n; r++)
                cmGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cell) });

            // Sol üst köşe
            AddCmText(0, 0, "gerçek ＼ tahmin", true, false);

            // Sütun başlıkları (tahmin edilen)
            for (int c = 0; c < n; c++)
                AddCmText(0, c + 1, labels[c], true, false);

            for (int r = 0; r < n; r++)
            {
                AddCmText(r + 1, 0, labels[r], true, true);   // satır başlığı (gerçek)
                for (int c = 0; c < n; c++)
                {
                    double v = cm[r][c];
                    bool diag = (r == c);
                    double t = v / maxCell;                  // 0..1 yoğunluk
                    Color baseCol = diag
                        ? Color.FromRgb(0x16, 0xA3, 0x4A)    // doğru = yeşil
                        : Color.FromRgb(0xEF, 0x44, 0x44);   // hata = kırmızı
                    byte alpha = (byte)(v <= 0 ? 12 : 40 + 200 * t);
                    var border = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(alpha, baseCol.R, baseCol.G, baseCol.B)),
                        BorderBrush = (Brush)FindResource("B.CardBorder"),
                        BorderThickness = new Thickness(0.5),
                        Child = new TextBlock
                        {
                            Text = ((long)Math.Round(v)).ToString(),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            FontWeight = diag ? FontWeights.Bold : FontWeights.Normal,
                            FontSize = 14,
                            Foreground = t > 0.55 ? Brushes.White : (Brush)FindResource("B.TextPrimary")
                        }
                    };
                    Grid.SetRow(border, r + 1);
                    Grid.SetColumn(border, c + 1);
                    cmGrid.Children.Add(border);
                }
            }
        }

        private void AddCmText(int row, int col, string text, bool header, bool rowHeader)
        {
            var tb = new TextBlock
            {
                Text = text,
                HorizontalAlignment = rowHeader ? HorizontalAlignment.Right : HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 4, 6, 4),
                FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)FindResource("B.TextSecondary")
            };
            Grid.SetRow(tb, row);
            Grid.SetColumn(tb, col);
            cmGrid.Children.Add(tb);
        }

        // ============= ÖZNİTELİK ÖNEMİ (Information Gain) =============
        private void ComputeFeatureImportance()
        {
            if (featList == null) return;
            featList.ItemsSource = null;

            if (_dataset == null || _dataset.RawInstances == null)
            {
                lblFeatInfo.Text = "Önce bir veri seti yükleyin.";
                return;
            }

            try
            {
                var data = new weka.core.Instances(_dataset.RawInstances);
                data.setClassIndex(data.numAttributes() - 1);

                if (!data.classAttribute().isNominal())
                {
                    lblFeatInfo.Text = "Bilgi kazancı yalnızca nominal (kategorik) sınıf için hesaplanır.";
                    return;
                }

                var evalr = new weka.attributeSelection.InfoGainAttributeEval();
                evalr.buildEvaluator(data);

                int classIdx = data.classIndex();
                var raw = new List<FeatVm>();
                double max = 0;
                for (int i = 0; i < data.numAttributes(); i++)
                {
                    if (i == classIdx) continue;
                    double ig = evalr.evaluateAttribute(i);
                    if (ig > max) max = ig;
                    raw.Add(new FeatVm
                    {
                        Label = data.attribute(i).name(),
                        RawValue = ig
                    });
                }
                if (max <= 0) max = 1;
                foreach (var f in raw)
                {
                    f.BarWidth = Math.Max(2.0, 360.0 * f.RawValue / max);
                    f.ValueText = f.RawValue.ToString("F4", CultureInfo.InvariantCulture);
                }
                raw.Sort(delegate (FeatVm a, FeatVm b) { return b.RawValue.CompareTo(a.RawValue); });

                featList.ItemsSource = raw;
                lblFeatInfo.Text = "Her özniteliğin sınıfı belirlemedeki bilgi kazancı " +
                    "(yüksek = daha ayırt edici). Sınıf: " + data.classAttribute().name();
            }
            catch (Exception ex)
            {
                lblFeatInfo.Text = "Bilgi kazancı hesaplanamadı: " + ex.Message;
            }
        }

        // ============= DAĞILIM GRAFİĞİ (SCATTER) =============
        private void PopulateScatterCombos()
        {
            if (cmbScatterX == null || _dataset == null || _dataset.RawInstances == null) return;
            var inst = _dataset.RawInstances;

            // Zaten dolu ve aynı veri setiyse tekrar doldurma (seçim korunur)
            if (cmbScatterX.Items.Count == inst.numAttributes() && cmbScatterX.SelectedIndex >= 0)
                return;

            int prevX = cmbScatterX.SelectedIndex;
            int prevY = cmbScatterY.SelectedIndex;
            cmbScatterX.Items.Clear();
            cmbScatterY.Items.Clear();
            for (int i = 0; i < inst.numAttributes(); i++)
            {
                cmbScatterX.Items.Add(inst.attribute(i).name());
                cmbScatterY.Items.Add(inst.attribute(i).name());
            }
            int n = inst.numAttributes();
            cmbScatterX.SelectedIndex = prevX >= 0 && prevX < n ? prevX : 0;
            cmbScatterY.SelectedIndex = prevY >= 0 && prevY < n ? prevY : (n > 1 ? 1 : 0);
        }

        private void Scatter_AxisChanged(object sender, SelectionChangedEventArgs e) { DrawScatter(); }
        private void scatterCanvas_SizeChanged(object sender, SizeChangedEventArgs e) { DrawScatter(); }

        private void DrawScatter()
        {
            if (scatterCanvas == null) return;
            scatterCanvas.Children.Clear();
            if (scatterLegend != null) scatterLegend.Children.Clear();

            if (_dataset == null || _dataset.RawInstances == null) return;
            if (cmbScatterX.SelectedIndex < 0 || cmbScatterY.SelectedIndex < 0) return;

            var inst = _dataset.RawInstances;
            int xi = cmbScatterX.SelectedIndex;
            int yi = cmbScatterY.SelectedIndex;
            int ci = inst.classIndex();
            if (ci < 0) ci = inst.numAttributes() - 1;

            double w = scatterCanvas.ActualWidth;
            double h = scatterCanvas.ActualHeight;
            if (w < 100 || h < 100) return;

            const double padL = 64, padR = 24, padT = 16, padB = 50;
            double x0 = padL, x1 = w - padR;   // sol -> sağ
            double y0 = h - padB, y1 = padT;   // alt -> üst

            var xAttr = inst.attribute(xi);
            var yAttr = inst.attribute(yi);
            bool xNom = xAttr.isNominal();
            bool yNom = yAttr.isNominal();

            // Eksen aralıkları (nominal'de 0..n-1 indeks, ±0.5 boşlukla)
            double xmin, xmax, ymin, ymax;
            if (xNom) { xmin = -0.5; xmax = Math.Max(1, xAttr.numValues() - 1) + 0.5; }
            else
            {
                ComputeMinMax(inst, xi, out xmin, out xmax);
                if (double.IsNaN(xmin)) return;
                if (xmax - xmin < 1e-9) { xmin -= 1; xmax += 1; }
            }
            if (yNom) { ymin = -0.5; ymax = Math.Max(1, yAttr.numValues() - 1) + 0.5; }
            else
            {
                ComputeMinMax(inst, yi, out ymin, out ymax);
                if (double.IsNaN(ymin)) return;
                if (ymax - ymin < 1e-9) { ymin -= 1; ymax += 1; }
            }

            Func<double, double> sx = delegate (double v) { return x0 + (v - xmin) / (xmax - xmin) * (x1 - x0); };
            Func<double, double> sy = delegate (double v) { return y0 - (v - ymin) / (ymax - ymin) * (y0 - y1); };

            // --- Dikey grid + X tick etiketleri ---
            if (xNom)
            {
                for (int t = 0; t < xAttr.numValues(); t++)
                {
                    double px = sx(t);
                    scatterCanvas.Children.Add(MakeLine(px, y0, px, y1, "#FFEEF2F6"));
                    AddTick(px, y0 + 6, xAttr.value(t));
                }
            }
            else
            {
                for (int t = 0; t <= 5; t++)
                {
                    double val = xmin + (xmax - xmin) * t / 5.0;
                    double px = sx(val);
                    scatterCanvas.Children.Add(MakeLine(px, y0, px, y1, "#FFEEF2F6"));
                    AddTick(px, y0 + 6, val.ToString("0.##", CultureInfo.InvariantCulture));
                }
            }

            // --- Yatay grid + Y tick etiketleri ---
            if (yNom)
            {
                for (int t = 0; t < yAttr.numValues(); t++)
                {
                    double py = sy(t);
                    scatterCanvas.Children.Add(MakeLine(x0, py, x1, py, "#FFEEF2F6"));
                    AddTickRight(x0 - 8, py - 8, yAttr.value(t));
                }
            }
            else
            {
                for (int t = 0; t <= 5; t++)
                {
                    double val = ymin + (ymax - ymin) * t / 5.0;
                    double py = sy(val);
                    scatterCanvas.Children.Add(MakeLine(x0, py, x1, py, "#FFEEF2F6"));
                    AddTickRight(x0 - 8, py - 8, val.ToString("0.##", CultureInfo.InvariantCulture));
                }
            }

            // Eksen çizgileri (daha koyu)
            scatterCanvas.Children.Add(MakeLine(x0, y0, x1, y0, "#FF94A3B8"));
            scatterCanvas.Children.Add(MakeLine(x0, y0, x0, y1, "#FFCBD5E1"));

            // Eksen başlıkları
            AddCanvasText(scatterCanvas, xAttr.name(), (x0 + x1) / 2 - 40, h - 20, "#FF475569");
            AddCanvasText(scatterCanvas, yAttr.name(), 6, 0, "#FF475569");

            var clsAttr = inst.attribute(ci);
            bool nominalClass = clsAttr.isNominal();
            int numClasses = nominalClass ? clsAttr.numValues() : 1;

            // Lejant (üst bardaki alan)
            if (scatterLegend != null && nominalClass)
            {
                for (int cidx = 0; cidx < numClasses; cidx++)
                {
                    var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 0) };
                    sp.Children.Add(new System.Windows.Shapes.Ellipse
                    {
                        Width = 11, Height = 11, Fill = ClassBrush(cidx),
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0)
                    });
                    sp.Children.Add(new TextBlock
                    {
                        Text = clsAttr.value(cidx),
                        Style = (Style)FindResource("T.Muted"),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    scatterLegend.Children.Add(sp);
                }
            }

            // Noktalar — nominal eksende üst üste binmeyi açmak için sabit tohumlu jitter
            var rnd = new Random(20260602);
            double jx = xNom ? 0.16 : 0;
            double jy = yNom ? 0.16 : 0;
            int count = inst.numInstances();
            for (int k = 0; k < count; k++)
            {
                var row = inst.instance(k);
                if (row.isMissing(xi) || row.isMissing(yi)) continue;
                double xv = row.value(xi) + (jx > 0 ? (rnd.NextDouble() - 0.5) * 2 * jx : 0);
                double yv = row.value(yi) + (jy > 0 ? (rnd.NextDouble() - 0.5) * 2 * jy : 0);
                double px = sx(xv);
                double py = sy(yv);
                int cls = nominalClass && !row.isMissing(ci) ? (int)row.value(ci) : 0;
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 9, Height = 9,
                    Fill = ClassBrush(cls), Opacity = 0.72,
                    Stroke = Brushes.White, StrokeThickness = 1
                };
                Canvas.SetLeft(dot, px - 4.5);
                Canvas.SetTop(dot, py - 4.5);
                scatterCanvas.Children.Add(dot);
            }
        }

        private static void ComputeMinMax(weka.core.Instances inst, int idx, out double min, out double max)
        {
            min = double.NaN; max = double.NaN;
            double lo = double.MaxValue, hi = double.MinValue;
            bool any = false;
            int n = inst.numInstances();
            for (int k = 0; k < n; k++)
            {
                var r = inst.instance(k);
                if (r.isMissing(idx)) continue;
                double v = r.value(idx);
                if (v < lo) lo = v;
                if (v > hi) hi = v;
                any = true;
            }
            if (any) { min = lo; max = hi; }
        }

        // X ekseni altına, tık konumunda ortalanmış etiket
        private void AddTick(double cx, double top, string text)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = 10,
                Foreground = (Brush)FindResource("B.TextMuted"),
                TextAlignment = TextAlignment.Center, Width = 90,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(tb, cx - 45);
            Canvas.SetTop(tb, top);
            scatterCanvas.Children.Add(tb);
        }

        // Y ekseni soluna, sağa yaslı etiket (rightX bitiş noktası)
        private void AddTickRight(double rightX, double top, string text)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = 10,
                Foreground = (Brush)FindResource("B.TextMuted"),
                TextAlignment = TextAlignment.Right, Width = 54,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(tb, rightX - 54);
            Canvas.SetTop(tb, top);
            scatterCanvas.Children.Add(tb);
        }

        private static System.Windows.Shapes.Line MakeLine(double x1, double y1, double x2, double y2, string hex)
        {
            return new System.Windows.Shapes.Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = (Brush)new BrushConverter().ConvertFromString(hex),
                StrokeThickness = 1
            };
        }

        private static void AddCanvasText(Canvas canvas, string text, double left, double top, string hex)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = 11,
                Foreground = (Brush)new BrushConverter().ConvertFromString(hex)
            };
            Canvas.SetLeft(tb, left);
            Canvas.SetTop(tb, top);
            canvas.Children.Add(tb);
        }

        private static readonly Color[] _classPalette =
        {
            Color.FromRgb(0x25, 0x63, 0xEB), Color.FromRgb(0x16, 0xA3, 0x4A),
            Color.FromRgb(0xF5, 0x9E, 0x0B), Color.FromRgb(0x8B, 0x5C, 0xF6),
            Color.FromRgb(0xEC, 0x48, 0x99), Color.FromRgb(0x06, 0xB6, 0xD4),
            Color.FromRgb(0xEF, 0x44, 0x44), Color.FromRgb(0x64, 0x74, 0x8B)
        };

        private static Brush ClassBrush(int idx)
        {
            if (idx < 0) idx = 0;
            return new SolidColorBrush(_classPalette[idx % _classPalette.Length]);
        }
    }

    // ============================ VIEWMODELS ============================

    public sealed class AlgoVm : INotifyPropertyChanged
    {
        public AlgorithmDescriptor Descriptor { get; private set; }
        public string DisplayName { get { return Descriptor.DisplayName; } }
        public string FamilyText { get { return Descriptor.Family.ToString().ToUpperInvariant(); } }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    Descriptor.IsSelected = value;
                    OnChanged("IsSelected");
                }
            }
        }

        public Brush FamilyBrush
        {
            get
            {
                switch (Descriptor.Family)
                {
                    case AlgorithmFamily.Baseline: return new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                    case AlgorithmFamily.Bayes: return new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6));
                    case AlgorithmFamily.Lazy: return new SolidColorBrush(Color.FromRgb(0x06, 0xB6, 0xD4));
                    case AlgorithmFamily.Functions: return new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    case AlgorithmFamily.Trees: return new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
                    case AlgorithmFamily.Rules: return new SolidColorBrush(Color.FromRgb(0xEC, 0x48, 0x99));
                    default: return new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                }
            }
        }

        public AlgoVm(AlgorithmDescriptor d)
        {
            Descriptor = d;
            _isSelected = d.IsSelected;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string n)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(n));
        }
    }

    public sealed class ClassDistVm
    {
        public string Label { get; set; }
        public string CountText { get; set; }
        public double BarWidth { get; set; }
    }

    // Tahmin ekranındaki sınıf olasılığı barı
    public sealed class ProbVm
    {
        public string Label { get; set; }
        public string PctText { get; set; }
        public double BarWidth { get; set; }
        public double RawValue { get; set; }
        public Brush Brush { get; set; }
        public FontWeight Weight { get; set; }
    }

    // Öznitelik önemi (information gain) barı
    public sealed class FeatVm
    {
        public string Label { get; set; }
        public string ValueText { get; set; }
        public double BarWidth { get; set; }
        public double RawValue { get; set; }
    }
}
