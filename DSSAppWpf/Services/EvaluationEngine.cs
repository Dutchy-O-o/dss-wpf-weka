using System;
using System.Diagnostics;
using DSSAppWpf.Models;

namespace DSSAppWpf.Services
{
    public static class EvaluationEngine
    {
        public static ClassifierResult Evaluate(
            weka.core.Instances sourceInstances,
            AlgorithmDescriptor descriptor,
            EvaluationOptions options)
        {
            ClassifierResult result = new ClassifierResult
            {
                AlgorithmKey = descriptor.Key,
                AlgorithmName = descriptor.DisplayName
            };

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                weka.core.Instances data = new weka.core.Instances(sourceInstances);
                data.setClassIndex(data.numAttributes() - 1);

                data = ApplyPreprocessing(data, descriptor.Preprocessing);
                data = Randomize(data, options.RandomSeed);

                weka.classifiers.Classifier classifier = descriptor.Builder();

                if (options.Method == EvaluationMethod.TenFoldCrossValidation)
                    FillFromCrossValidation(result, data, classifier, options);
                else
                    FillFromPercentageSplit(result, data, classifier, options);

                result.TrainedClassifier = classifier;
            }
            catch (java.lang.Exception jex)
            {
                result.ErrorMessage = jex.getMessage() ?? jex.ToString();
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                sw.Stop();
                result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            }
            return result;
        }

        private static weka.core.Instances ApplyPreprocessing(
            weka.core.Instances data, PreprocessingMode mode)
        {
            switch (mode)
            {
                case PreprocessingMode.NormalizeAndDummy:
                    data = ApplyFilter(data, new weka.filters.unsupervised.attribute.Normalize());
                    data = ApplyFilter(data, new weka.filters.unsupervised.attribute.NominalToBinary());
                    return data;
                case PreprocessingMode.DiscretizeOnly:
                    return ApplyFilter(data, new weka.filters.unsupervised.attribute.Discretize());
                case PreprocessingMode.DummyOnly:
                    return ApplyFilter(data, new weka.filters.unsupervised.attribute.NominalToBinary());
                default:
                    return data;
            }
        }

        private static weka.core.Instances ApplyFilter(
            weka.core.Instances data, weka.filters.Filter filter)
        {
            filter.setInputFormat(data);
            return weka.filters.Filter.useFilter(data, filter);
        }

        private static weka.core.Instances Randomize(weka.core.Instances data, int seed)
        {
            weka.filters.unsupervised.instance.Randomize rnd =
                new weka.filters.unsupervised.instance.Randomize();
            rnd.setRandomSeed(seed);
            return ApplyFilter(data, rnd);
        }

        private static void FillFromPercentageSplit(
            ClassifierResult result, weka.core.Instances data,
            weka.classifiers.Classifier classifier, EvaluationOptions options)
        {
            int trainSize = data.numInstances() * options.PercentSplit / 100;
            int testSize = data.numInstances() - trainSize;

            weka.core.Instances train = new weka.core.Instances(data, 0, trainSize);
            weka.core.Instances test = new weka.core.Instances(data, trainSize, testSize);

            classifier.buildClassifier(train);

            weka.classifiers.Evaluation eval = new weka.classifiers.Evaluation(train);
            eval.evaluateModel(classifier, test, new java.lang.Object[0]);

            result.TestSize = testSize;
            result.CorrectCount = (int)eval.correct();
            result.IncorrectCount = (int)eval.incorrect();
            result.Accuracy = eval.pctCorrect();
            result.KappaStatistic = eval.kappa();
            result.WeightedPrecision = eval.weightedPrecision();
            result.WeightedRecall = eval.weightedRecall();
            result.WeightedFMeasure = eval.weightedFMeasure();
        }

        private static void FillFromCrossValidation(
            ClassifierResult result, weka.core.Instances data,
            weka.classifiers.Classifier classifier, EvaluationOptions options)
        {
            weka.classifiers.Evaluation eval = new weka.classifiers.Evaluation(data);
            eval.crossValidateModel(classifier, data, options.Folds,
                new java.util.Random(options.RandomSeed),
                new java.lang.Object[0]);

            classifier.buildClassifier(data);

            result.TestSize = data.numInstances();
            result.CorrectCount = (int)eval.correct();
            result.IncorrectCount = (int)eval.incorrect();
            result.Accuracy = eval.pctCorrect();
            result.KappaStatistic = eval.kappa();
            result.WeightedPrecision = eval.weightedPrecision();
            result.WeightedRecall = eval.weightedRecall();
            result.WeightedFMeasure = eval.weightedFMeasure();
        }
    }
}
