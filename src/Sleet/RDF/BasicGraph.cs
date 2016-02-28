using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonLD.Core;
using Node = JsonLD.Core.RDFDataset.Node;
using System.Globalization;

namespace Sleet
{
    public class BasicGraph : BaseGraph
    {
        private readonly HashSet<Triple> _triples;

        public BasicGraph()
            : this(new HashSet<Triple>())
        {

        }

        public BasicGraph(IEnumerable<Triple> triples)
        {
            _triples = new HashSet<Triple>(triples);
        }

        public override HashSet<Triple> Triples
        {
            get
            {
                return _triples;
            }
        }
    }
}
