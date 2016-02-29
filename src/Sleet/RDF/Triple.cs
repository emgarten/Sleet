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
    public class Triple : IEquatable<Triple>
    {
        private readonly RDFDataset.Node _subNode;
        private readonly RDFDataset.Node _predNode;
        private readonly RDFDataset.Node _objNode;

        public Triple(Uri subjectURI, Uri predicateURI, string objectValue)
        {
            _subNode = new RDFDataset.IRI(subjectURI.AbsoluteUri);
            _predNode = new RDFDataset.IRI(predicateURI.AbsoluteUri);

            _objNode = new RDFDataset.Literal(
                objectValue,
                Constants.StringUri,
                language: null);
        }

        public Triple(Uri subjectURI, Uri predicateURI, int objectValue)
        {
            _subNode = new RDFDataset.IRI(subjectURI.AbsoluteUri);
            _predNode = new RDFDataset.IRI(predicateURI.AbsoluteUri);

            _objNode = new RDFDataset.Literal(
                string.Empty + objectValue,
                Constants.IntegerUri,
                language: null);
        }

        public Triple(Uri subjectURI, Uri predicateURI, bool objectValue)
        {
            _subNode = new RDFDataset.IRI(subjectURI.AbsoluteUri);
            _predNode = new RDFDataset.IRI(predicateURI.AbsoluteUri);

            _objNode = new RDFDataset.Literal(
                objectValue.ToString().ToLowerInvariant(),
                Constants.BooleanUri,
                language: null);
        }

        public Triple(Uri subjectURI, Uri predicateURI, DateTimeOffset objectValue)
        {
            var dt = objectValue.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

            _subNode = new RDFDataset.IRI(subjectURI.AbsoluteUri);
            _predNode = new RDFDataset.IRI(predicateURI.AbsoluteUri);
            _objNode = new RDFDataset.Literal(dt, Constants.DateTimeUri, null);
        }

        public Triple(Uri subjectURI, Uri predicateURI, Uri objectURI)
            : this(
                  new RDFDataset.IRI(subjectURI.AbsoluteUri),
                  new RDFDataset.IRI(predicateURI.AbsoluteUri),
                  new RDFDataset.IRI(objectURI.AbsoluteUri))
        {
        }

        public Triple(RDFDataset.Node subNode, RDFDataset.Node predNode, RDFDataset.Node objNode)
        {
            _subNode = subNode;
            _predNode = predNode;
            _objNode = objNode;
        }

        public RDFDataset.Node Subject
        {
            get
            {
                return _subNode;
            }
        }

        public RDFDataset.Node Predicate
        {
            get
            {
                return _predNode;
            }
        }

        public RDFDataset.Node Object
        {
            get
            {
                return _objNode;
            }
        }

        public bool Equals(Triple other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Subject.Equals(other.Subject) && Predicate.Equals(other.Predicate) && Object.Equals(other.Object);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.GetHashCode(_subNode, _predNode, _objNode);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Triple);
        }
    }
}
