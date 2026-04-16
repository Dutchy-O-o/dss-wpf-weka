using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using DSSAppWpf.Models;

namespace DSSAppWpf.Services
{
    public static class DatasetLoader
    {
        private const int PreviewRowLimit = 50;

        public static DatasetInfo Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Dataset dosyası bulunamadı.", filePath);

            weka.core.Instances instances = LoadInstances(filePath);
            instances.setClassIndex(instances.numAttributes() - 1);

            DatasetInfo info = new DatasetInfo
            {
                FilePath = filePath,
                RelationName = instances.relationName(),
                InstanceCount = instances.numInstances(),
                AttributeCount = instances.numAttributes(),
                RawInstances = instances
            };

            PopulateAttributes(info, instances);
            PopulateClassDistribution(info, instances);
            info.PreviewTable = BuildPreviewTable(instances);
            info.MissingValueCount = CountMissing(instances);

            return info;
        }

        private static weka.core.Instances LoadInstances(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".arff")
            {
                using (java.io.FileReader reader = new java.io.FileReader(filePath))
                    return new weka.core.Instances(reader);
            }
            if (ext == ".csv")
            {
                weka.core.converters.CSVLoader loader = new weka.core.converters.CSVLoader();
                loader.setSource(new java.io.File(filePath));
                return loader.getDataSet();
            }
            throw new NotSupportedException("Sadece .arff ve .csv dosyaları desteklenir.");
        }

        private static void PopulateAttributes(DatasetInfo info, weka.core.Instances instances)
        {
            int classIdx = instances.classIndex();
            for (int i = 0; i < instances.numAttributes(); i++)
            {
                weka.core.Attribute attr = instances.attribute(i);
                AttributeInfo ai = new AttributeInfo
                {
                    Index = i,
                    Name = attr.name(),
                    IsNumeric = attr.isNumeric(),
                    IsNominal = attr.isNominal(),
                    NominalValues = new List<string>()
                };

                if (attr.isNumeric()) { ai.Type = "Numeric"; info.NumericAttributeCount++; }
                else if (attr.isNominal())
                {
                    ai.Type = "Nominal";
                    info.NominalAttributeCount++;
                    for (int v = 0; v < attr.numValues(); v++)
                        ai.NominalValues.Add(attr.value(v));
                }
                else if (attr.isString()) ai.Type = "String";
                else if (attr.isDate()) ai.Type = "Date";
                else ai.Type = "Unknown";

                info.Attributes.Add(ai);

                if (i == classIdx)
                {
                    info.ClassAttributeName = attr.name();
                    info.ClassIsNominal = attr.isNominal();
                }
            }
        }

        private static void PopulateClassDistribution(DatasetInfo info, weka.core.Instances instances)
        {
            weka.core.Attribute cls = instances.classAttribute();
            if (!cls.isNominal()) return;

            for (int v = 0; v < cls.numValues(); v++)
                info.ClassDistribution[cls.value(v)] = 0;

            for (int i = 0; i < instances.numInstances(); i++)
            {
                weka.core.Instance inst = instances.instance(i);
                if (inst.classIsMissing()) continue;
                string label = cls.value((int)inst.classValue());
                if (info.ClassDistribution.ContainsKey(label))
                    info.ClassDistribution[label]++;
                else
                    info.ClassDistribution[label] = 1;
            }
        }

        private static DataTable BuildPreviewTable(weka.core.Instances instances)
        {
            DataTable table = new DataTable(instances.relationName());
            for (int i = 0; i < instances.numAttributes(); i++)
                table.Columns.Add(instances.attribute(i).name(), typeof(string));

            int rowCount = Math.Min(PreviewRowLimit, instances.numInstances());
            for (int r = 0; r < rowCount; r++)
            {
                weka.core.Instance inst = instances.instance(r);
                object[] row = new object[instances.numAttributes()];
                for (int c = 0; c < instances.numAttributes(); c++)
                {
                    if (inst.isMissing(c)) { row[c] = "?"; continue; }
                    weka.core.Attribute a = instances.attribute(c);
                    if (a.isNumeric()) row[c] = inst.value(c).ToString("G6");
                    else if (a.isNominal()) row[c] = a.value((int)inst.value(c));
                    else row[c] = inst.toString(c);
                }
                table.Rows.Add(row);
            }
            return table;
        }

        private static int CountMissing(weka.core.Instances instances)
        {
            int total = 0;
            for (int i = 0; i < instances.numInstances(); i++)
            {
                weka.core.Instance inst = instances.instance(i);
                for (int c = 0; c < instances.numAttributes(); c++)
                    if (inst.isMissing(c)) total++;
            }
            return total;
        }
    }
}
