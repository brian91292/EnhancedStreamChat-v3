using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnhancedStreamChat.Utilities
{
    public class FixedSizedQueue<T> : ConcurrentQueue<T>
    {
        private readonly object _object = new object();

        public int Size { get; private set; }

        private event Action<T> OnFree;

        public FixedSizedQueue(int size, Action<T> OnFree)
        {
            Size = size;
            this.OnFree += OnFree;
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            lock (_object)
            {
                while (base.Count > Size)
                {
                    if(base.TryDequeue(out var outObj))
                    {
                        OnFree?.Invoke(outObj);
                    }
                }
            }
        }
    }
}
