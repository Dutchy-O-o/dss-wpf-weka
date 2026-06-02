using System;
using System.Collections.Generic;

namespace DSSAppWpf.Models
{
    // Arayüzden ayarlanabilen tek bir hiperparametre (örn. k-NN'de k, J48'de güven faktörü).
    public sealed class HyperParam
    {
        public string Name { get; set; }   // UI etiketi
        public double Min { get; set; }
        public double Max { get; set; }
        public double Step { get; set; }
        public double Value { get; set; }
        public bool IsInteger { get; set; }

        public string ValueText
        {
            get
            {
                return IsInteger
                    ? ((long)System.Math.Round(Value)).ToString()
                    : Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

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
        public List<HyperParam> HyperParams { get; set; }  // null = ayarlanabilir parametre yok
        public override string ToString() { return DisplayName; }
    }
}
