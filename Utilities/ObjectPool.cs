using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedStreamChat.Utilities
{
    /// <summary>
    /// A dynamic pool of unity components of type T, that recycles old objects when possible, and allocates new objects when required.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObjectPool<T> : IDisposable where T : Component
    {
        private Queue<T> _freeObjects;
        private Action<T> FirstAlloc;
        private Action<T> OnAlloc;
        private Action<T> OnFree;
        private Func<T> Constructor;
        private object _lock = new object();

        /// <summary>
        /// ObjectPool constructor function, used to setup the initial pool size and callbacks.
        /// </summary>
        /// <param name="initialCount">The number of components of type T to allocate right away.</param>
        /// <param name="FirstAlloc">The callback function you want to occur only the first time when a new component of type T is allocated.</param>
        /// <param name="OnAlloc">The callback function to be called everytime ObjectPool.Alloc() is called.</param>
        /// <param name="OnFree">The callback function to be called everytime ObjectPool.Free() is called</param>
        public ObjectPool(int initialCount = 0, Func<T> Constructor = null, Action<T> FirstAlloc = null, Action<T> OnAlloc = null, Action<T> OnFree = null)
        {
            this.Constructor = Constructor;
            this.FirstAlloc = FirstAlloc;
            this.OnAlloc = OnAlloc;
            this.OnFree = OnFree;
            this._freeObjects = new Queue<T>();

            while (initialCount-- > 0)
            {
                _freeObjects.Enqueue(internalAlloc());
            }
        }

        ~ObjectPool()
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(false);
        }

        public void Dispose(bool immediate)
        {
            foreach (T obj in _freeObjects)
            {
                if (immediate)
                {
                    UnityEngine.Object.DestroyImmediate(obj.gameObject);
                }
                else
                {
                    UnityEngine.Object.Destroy(obj.gameObject);
                }
            }
            _freeObjects.Clear();
        }

        private T internalAlloc()
        {
            T newObj;
            if (Constructor is null)
            {
                newObj = new GameObject().AddComponent<T>();
            }
            else
            {
                newObj = Constructor.Invoke();
            }
            FirstAlloc?.Invoke(newObj);
            return newObj;
        }

        /// <summary>
        /// Allocates a component of type T from a pre-allocated pool, or instantiates a new one if required.
        /// </summary>
        /// <returns></returns>
        public T Alloc()
        {
            lock (_lock)
            {
                T obj = null;
                if (_freeObjects.Count > 0)
                    obj = _freeObjects.Dequeue();
                if (!obj)
                    obj = internalAlloc();
                OnAlloc?.Invoke(obj);
                return obj;
            }
        }

        /// <summary>
        /// Inserts a component of type T into the stack of free objects. Note: the component does *not* need to be allocated using ObjectPool.Alloc() to be freed with this function!
        /// </summary>
        /// <param name="obj"></param>
        public void Free(T obj)
        {
            lock (_lock)
            {
                if (obj == null) return;
                _freeObjects.Enqueue(obj);
                OnFree?.Invoke(obj);
            }
        }
    }
}
