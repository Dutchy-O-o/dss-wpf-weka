using System.IO;
using DSSAppWpf.Models;

namespace DSSAppWpf.Services
{
    public static class ModelPersistence
    {
        public static void Save(string filePath, weka.classifiers.Classifier classifier,
            AlgorithmDescriptor descriptor, weka.core.Instances header)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            object[] bundle = new object[]
            {
                classifier,
                new weka.core.Instances(header, 0),
                descriptor != null ? descriptor.Key : "Unknown"
            };
            weka.core.SerializationHelper.writeAll(filePath, bundle);
        }

        public static LoadedModel Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Model dosyası bulunamadı.", filePath);

            object[] bundle = weka.core.SerializationHelper.readAll(filePath);
            LoadedModel lm = new LoadedModel();
            if (bundle.Length > 0) lm.Classifier = (weka.classifiers.Classifier)bundle[0];
            if (bundle.Length > 1) lm.Header = (weka.core.Instances)bundle[1];
            if (bundle.Length > 2 && bundle[2] != null) lm.AlgorithmKey = bundle[2].ToString();
            return lm;
        }

        public sealed class LoadedModel
        {
            public weka.classifiers.Classifier Classifier { get; set; }
            public weka.core.Instances Header { get; set; }
            public string AlgorithmKey { get; set; }
        }
    }
}
