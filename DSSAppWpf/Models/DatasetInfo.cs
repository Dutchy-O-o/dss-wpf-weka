using System.Collections.Generic;
using System.Data;

namespace DSSAppWpf.Models
{
    public sealed class AttributeInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsNumeric { get; set; }
        public bool IsNominal { get; set; }
        public List<string> NominalValues { get; set; }
        public string ValuesText
        {
            get
            {
                if (NominalValues == null || NominalValues.Count == 0) return "";
                return "{" + string.Join(", ", NominalValues.ToArray()) + "}";
            }
        }
    }

    public sealed class DatasetInfo
    {
        public string FilePath { get; set; }
        public string RelationName { get; set; }
        public int InstanceCount { get; set; }
        public int AttributeCount { get; set; }
        public string ClassAttributeName { get; set; }
        public bool ClassIsNominal { get; set; }
        public int NumericAttributeCount { get; set; }
        public int NominalAttributeCount { get; set; }
        public int MissingValueCount { get; set; }
        public List<AttributeInfo> Attributes { get; set; }
        public Dictionary<string, int> ClassDistribution { get; set; }
        public DataTable PreviewTable { get; set; }
        public weka.core.Instances RawInstances { get; set; }

        public DatasetInfo()
        {
            Attributes = new List<AttributeInfo>();
            ClassDistribution = new Dictionary<string, int>();
        }
    }
}
