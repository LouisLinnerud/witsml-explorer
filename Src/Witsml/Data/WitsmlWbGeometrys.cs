using System.Collections.Generic;
using System.Xml.Serialization;

namespace Witsml.Data
{
    [XmlRoot("wbGeometrys", Namespace = "http://www.witsml.org/schemas/1series")]
    public class WitsmlWbGeometrys : IWitsmlQueryType
    {
        [XmlAttribute("version")]
        public string Version = "1.4.1.1";

        [XmlElement("wbGeometry")]
        public List<WitsmlWbGeometry> WbGeometrys { get; set; } = new List<WitsmlWbGeometry>();

        public string TypeName => "wbGeometry";
    }
}
