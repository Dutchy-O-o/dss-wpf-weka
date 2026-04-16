namespace DSSAppWpf.Models
{
    public sealed class ClassifierResult
    {
        public string AlgorithmKey { get; set; }
        public string AlgorithmName { get; set; }
        public int CorrectCount { get; set; }
        public int IncorrectCount { get; set; }
        public int TestSize { get; set; }
        public double Accuracy { get; set; }
        public double WeightedPrecision { get; set; }
        public double WeightedRecall { get; set; }
        public double WeightedFMeasure { get; set; }
        public double KappaStatistic { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public string ErrorMessage { get; set; }
        public weka.classifiers.Classifier TrainedClassifier { get; set; }

        public bool IsSuccessful { get { return string.IsNullOrEmpty(ErrorMessage); } }

        public string AccuracyText
        {
            get { return IsSuccessful ? Accuracy.ToString("F2") + " %" : "—"; }
        }

        public string FMeasureText
        {
            get
            {
                if (!IsSuccessful || double.IsNaN(WeightedFMeasure)) return "—";
                return WeightedFMeasure.ToString("F4");
            }
        }

        public string PrecisionText
        {
            get
            {
                if (!IsSuccessful || double.IsNaN(WeightedPrecision)) return "—";
                return WeightedPrecision.ToString("F4");
            }
        }

        public string RecallText
        {
            get
            {
                if (!IsSuccessful || double.IsNaN(WeightedRecall)) return "—";
                return WeightedRecall.ToString("F4");
            }
        }

        public string KappaText
        {
            get
            {
                if (!IsSuccessful || double.IsNaN(KappaStatistic)) return "—";
                return KappaStatistic.ToString("F4");
            }
        }

        public string CountsText
        {
            get { return IsSuccessful ? CorrectCount + " / " + TestSize : "—"; }
        }

        public string TimeText
        {
            get { return ElapsedMilliseconds + " ms"; }
        }

        public string StatusText
        {
            get { return IsSuccessful ? "OK" : ("HATA: " + ErrorMessage); }
        }
    }
}
