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

            var j48 = new AlgorithmDescriptor
            {
                Key = "J48",
                DisplayName = "J48 (C4.5)",
                Family = AlgorithmFamily.Trees,
                Preprocessing = PreprocessingMode.None,
                Description = "C4.5 karar ağacı. Veri tipi önemli değil.",
                IsSelected = true,
                HyperParams = new List<HyperParam>
                {
                    new HyperParam { Name = "Güven faktörü (C)", Min = 0.05, Max = 0.5, Step = 0.05, Value = 0.25, IsInteger = false },
                    new HyperParam { Name = "Min yaprak örneği (M)", Min = 1, Max = 20, Step = 1, Value = 2, IsInteger = true }
                }
            };
            j48.Builder = delegate
            {
                var c = new weka.classifiers.trees.J48();
                c.setConfidenceFactor((float)j48.HyperParams[0].Value);
                c.setMinNumObj((int)j48.HyperParams[1].Value);
                return c;
            };
            list.Add(j48);
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
            var rf = new AlgorithmDescriptor
            {
                Key = "RandomForest",
                DisplayName = "Random Forest",
                Family = AlgorithmFamily.Trees,
                Preprocessing = PreprocessingMode.None,
                Description = "Birden fazla rastgele ağacın topluluk (ensemble) yöntemi.",
                IsSelected = true,
                HyperParams = new List<HyperParam>
                {
                    new HyperParam { Name = "Ağaç sayısı (I)", Min = 10, Max = 500, Step = 10, Value = 100, IsInteger = true },
                    new HyperParam { Name = "Maks derinlik (0=sınırsız)", Min = 0, Max = 50, Step = 1, Value = 0, IsInteger = true }
                }
            };
            rf.Builder = delegate
            {
                var c = new weka.classifiers.trees.RandomForest();
                c.setNumTrees((int)rf.HyperParams[0].Value);
                c.setMaxDepth((int)rf.HyperParams[1].Value);
                return c;
            };
            list.Add(rf);
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
            var mlp = new AlgorithmDescriptor
            {
                Key = "MLP",
                DisplayName = "Multilayer Perceptron (ANN)",
                Family = AlgorithmFamily.Functions,
                Preprocessing = PreprocessingMode.NormalizeAndDummy,
                Description = "Yapay sinir ağı; sayısal ve normalize veri gerektirir.",
                IsSelected = true,
                HyperParams = new List<HyperParam>
                {
                    new HyperParam { Name = "Öğrenme oranı (L)", Min = 0.05, Max = 0.9, Step = 0.05, Value = 0.3, IsInteger = false },
                    new HyperParam { Name = "Momentum (M)", Min = 0.0, Max = 0.9, Step = 0.05, Value = 0.2, IsInteger = false },
                    new HyperParam { Name = "Eğitim adımı (N)", Min = 100, Max = 1000, Step = 100, Value = 500, IsInteger = true }
                }
            };
            mlp.Builder = delegate
            {
                var c = new weka.classifiers.functions.MultilayerPerceptron();
                c.setLearningRate(mlp.HyperParams[0].Value);
                c.setMomentum(mlp.HyperParams[1].Value);
                c.setTrainingTime((int)mlp.HyperParams[2].Value);
                return c;
            };
            list.Add(mlp);

            var smo = new AlgorithmDescriptor
            {
                Key = "SMO",
                DisplayName = "SMO (SVM)",
                Family = AlgorithmFamily.Functions,
                Preprocessing = PreprocessingMode.NormalizeAndDummy,
                Description = "Sequential Minimal Optimization ile Destek Vektör Makinesi.",
                IsSelected = true,
                HyperParams = new List<HyperParam>
                {
                    new HyperParam { Name = "Karmaşıklık (C)", Min = 0.1, Max = 10, Step = 0.1, Value = 1.0, IsInteger = false }
                }
            };
            smo.Builder = delegate
            {
                var c = new weka.classifiers.functions.SMO();
                c.setC(smo.HyperParams[0].Value);
                return c;
            };
            list.Add(smo);

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
