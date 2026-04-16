namespace DSSAppWpf.Models
{
    public enum EvaluationMethod
    {
        PercentageSplit, TenFoldCrossValidation
    }

    public sealed class EvaluationOptions
    {
        public EvaluationMethod Method { get; set; }
        public int PercentSplit { get; set; }
        public int Folds { get; set; }
        public int RandomSeed { get; set; }
    }
}
