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
    public abstract class BaseGraph
    {
        public void Assert(Triple triple)
        {
            Triples.Add(triple);
        }

        public void Assert(RDFDataset.Quad quad)
        {
            Triples.Add(new Triple(quad.GetSubject(), quad.GetPredicate(), quad.GetObject()));
        }

        public void Assert(RDFDataset.Node subNode, RDFDataset.Node predNode, RDFDataset.Node objNode)
        {
            Triples.Add(new Triple(subNode, predNode, objNode));
        }

        public virtual void Merge(BaseGraph graph)
        {
            foreach (var triple in graph.Triples)
            {
                Triples.Add(triple);
            }
        }

        public int Count
        {
            get
            {
                return Triples.Count;
            }
        }

        public HashSet<Triple> RecursiveDescribe(Uri entity)
        {
            HashSet<Triple> triples = new HashSet<Triple>();
            RecursiveDescribeInternal(entity.AbsoluteUri, this, triples);

            return triples;
        }

        private static void RecursiveDescribeInternal(string subject, BaseGraph graph, HashSet<Triple> triples)
        {
            bool needsRecurse = false;

            var children = graph.SelectSubject(subject);

            foreach (var triple in children)
            {
                if (triples.Add(triple))
                {
                    needsRecurse = true;
                }
            }

            if (needsRecurse)
            {
                foreach (var triple in children)
                {
                    RecursiveDescribeInternal(triple.Object.GetValue(), graph, triples);
                }
            }
        }

        public abstract HashSet<Triple> Triples
        {
            get;
        }

        public string NQuads
        {
            get
            {
                var builder = new StringBuilder();

                foreach (var triple in Triples)
                {
                    builder.Append(FormatQuadNode(triple.Subject) + " ");
                    builder.Append(FormatQuadNode(triple.Predicate) + " ");
                    builder.Append(FormatQuadNode(triple.Object) + " ." + Environment.NewLine);
                }

                return builder.ToString();
            }
        }

        private const string StringUri = "http://www.w3.org/2001/XMLSchema#string";
        private const string BooleanUri = "http://www.w3.org/2001/XMLSchema#boolean";
        private const string IntegerUri = "http://www.w3.org/2001/XMLSchema#integer";

        private static string FormatQuadNode(Node node)
        {
            if (node.IsIRI())
            {
                return string.Format(CultureInfo.InvariantCulture, "<{0}>", node.GetValue());
            }
            else if (node.IsLiteral())
            {
                var dataType = node.GetDatatype();
                var value = node.GetValue();

                // The string datatype is the default, it can be ommitted to save space.
                if (StringUri.Equals(dataType))
                {
                    value = value.Replace("\\", "\\\\");
                    value = value.Replace("\"", "\\\"");

                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "\"{0}\"",
                        value);
                }
                else
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "\"{0}\"^^<{1}>",
                        value,
                        dataType);
                }
            }

            return node.GetValue();
        }

        // Query
        public IEnumerable<Triple> SelectSubject(Uri subject)
        {
            return SelectSubject(subject.AbsoluteUri);
        }

        public IEnumerable<Triple> SelectSubject(string subject)
        {
            return Triples.Where(t => StringComparer.Ordinal.Equals(t.Subject.GetValue(), subject));
        }

        public IEnumerable<Triple> SelectPredicate(Uri predicate)
        {
            return SelectPredicate(predicate.AbsoluteUri);
        }

        public IEnumerable<Triple> SelectPredicate(string predicate)
        {
            return Triples.Where(t => StringComparer.Ordinal.Equals(t.Subject.GetValue(), predicate));
        }

    }
}
