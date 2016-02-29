namespace Sleet
{
    internal sealed class HashCodeCombiner
    {
        private const long Seed = 0x1505L;
        private long _combinedHash = Seed;

        internal int CombinedHash
        {
            get { return _combinedHash.GetHashCode(); }
        }

        internal void AddInt(int i)
        {
            _combinedHash = ((_combinedHash << 5) + _combinedHash) ^ i;
        }

        internal void AddObject(object o)
        {
            if (o != null)
            {
                var asInt = o as int?;

                if (asInt != null)
                {
                    AddInt(asInt.Value);
                }
                else
                {
                    AddInt(o.GetHashCode());
                }
            }
            else
            {
                // Count nulls
                AddInt(1201);
            }
        }

        internal static int GetHashCode(params object[] objects)
        {
            var combiner = new HashCodeCombiner();

            foreach (var obj in objects)
            {
                combiner.AddObject(obj);
            }

            return combiner.CombinedHash;
        }
    }
}
