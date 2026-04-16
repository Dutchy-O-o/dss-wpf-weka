using System.Collections.Generic;
using DSSAppWpf.Models;

namespace DSSAppWpf.Services
{
    public static class ClassifierFactory
    {
        public static List<AlgorithmDescriptor> BuildCatalog()
        {
            List<AlgorithmDescriptor> list = new List<AlgorithmDescriptor>();

            list.Add(new AlgorithmDescriptor
            {
                Key = "ZeroR",
                DisplayName = "ZeroR (Baseline)",
                Family = AlgorithmFamily.Baseline,
                Preprocessing = PreprocessingMode.None,
                Description = "Çoğunluk sınıfını tahmin eder; diğer algoritmalar için kıyaslama tabanı.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.rules.ZeroR(); }
            });
            list.Add(new AlgorithmDescriptor
            {
                Key = "OneR",
                DisplayName = "OneR",
                Family = AlgorithmFamily.Rules,
                Preprocessing = PreprocessingMode.DiscretizeOnly,
                Description = "Tek bir öznitelik üzerinden en basit kural.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.rules.OneR(); }
            });
            list.Add(new AlgorithmDescriptor
            {
                Key = "NaiveBayes",
                DisplayName = "Naive Bayes",
                Family = AlgorithmFamily.Bayes,
                Preprocessing = PreprocessingMode.DiscretizeOnly,
                Description = "Bayes teoremi + bağımsızlık varsayımı. Nominal veri tercih edilir.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.bayes.NaiveBayes(); }
            });
            list.Add(new AlgorithmDescriptor
            {
                Key = "BayesNet",
                DisplayName = "BayesNet",
                Family = AlgorithmFamily.Bayes,
                Preprocessing = PreprocessingMode.DiscretizeOnly,
                Description = "Olasılıksal yönlü asiklik graf tabanlı Bayes ağı.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.bayes.BayesNet(); }
            });
            list.Add(new AlgorithmDescriptor
            {
                Key = "Logistic",
                DisplayName = "Logistic Regression",
                Family = AlgorithmFamily.Functions,
                Preprocessing = PreprocessingMode.NormalizeAndDummy,
                Description = "Ridge regularizasyonlu lojistik regresyon. Sayısal veri gerektirir.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.functions.Logistic(); }
            });

            AddKnn(list, 1);
            AddKnn(list, 3);
            AddKnn(list, 5);
            AddKnn(list, 7);
            AddKnn(list, 9);

            list.Add(new AlgorithmDescriptor
            {
                Key = "J48",
                DisplayName = "J48 (C4.5)",
                Family = AlgorithmFamily.Trees,
                Preprocessing = PreprocessingMode.None,
                Description = "C4.5 karar ağacı. Veri tipi önemli değil.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.trees.J48(); }
            });
            list.Add(new AlgorithmDescriptor
            {
                Key = "RandomTree",
                DisplayName = "Random Tree",
                Family = AlgorithmFamily.Trees,
                Preprocessing = PreprocessingMode.None,
                Description = "Rastgele özniteliklerle tek bir karar ağacı.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.trees.RandomTree(); }
            });
            list.Add(new AlgorithmDescriptor
            {
                Key = "RandomForest",
                DisplayName = "Random Forest",
                Family = AlgorithmFamily.Trees,
                Preprocessing = PreprocessingMode.None,
                Description = "Birden fazla rastgele ağacın topluluk (ensemble) yöntemi.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.trees.RandomForest(); }
            });
            list.Add(new AlgorithmDescriptor
            {
                Key = "REPTree",
                DisplayName = "REPTree",
                Family = AlgorithmFamily.Trees,
                Preprocessing = PreprocessingMode.None,
                Description = "Hızlı karar/regresyon ağacı; azaltılmış hata budama.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.trees.REPTree(); }
            });
            list.Add(new AlgorithmDescriptor
            {
                Key = "MLP",
                DisplayName = "Multilayer Perceptron (ANN)",
                Family = AlgorithmFamily.Functions,
                Preprocessing = PreprocessingMode.NormalizeAndDummy,
                Description = "Yapay sinir ağı; sayısal ve normalize veri gerektirir.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.functions.MultilayerPerceptron(); }
            });
            list.Add(new AlgorithmDescriptor
            {
                Key = "SMO",
                DisplayName = "SMO (SVM)",
                Family = AlgorithmFamily.Functions,
                Preprocessing = PreprocessingMode.NormalizeAndDummy,
                Description = "Sequential Minimal Optimization ile Destek Vektör Makinesi.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.functions.SMO(); }
            });

            return list;
        }

        private static void AddKnn(List<AlgorithmDescriptor> list, int k)
        {
            int kLocal = k;
            list.Add(new AlgorithmDescriptor
            {
                Key = "IBk_" + kLocal + "NN",
                DisplayName = kLocal + "-NN (IBk)",
                Family = AlgorithmFamily.Lazy,
                Preprocessing = PreprocessingMode.NormalizeAndDummy,
                Description = kLocal + " en yakın komşuya göre sınıflandırır; öklid uzaklığı için normalize gerekir.",
                IsSelected = true,
                Builder = delegate { return new weka.classifiers.lazy.IBk(kLocal); }
            });
        }
    }
}
