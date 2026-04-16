using System;

namespace DSSAppWpf.Models
{
    public enum AlgorithmFamily
    {
        Baseline, Bayes, Lazy, Functions, Trees, Rules, Meta
    }

    public enum PreprocessingMode
    {
        None, NormalizeAndDummy, DiscretizeOnly, DummyOnly
    }

    public sealed class AlgorithmDescriptor
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public AlgorithmFamily Family { get; set; }
        public PreprocessingMode Preprocessing { get; set; }
        public string Description { get; set; }
        public Func<weka.classifiers.Classifier> Builder { get; set; }
        public bool IsSelected { get; set; }
        public override string ToString() { return DisplayName; }
    }
}
