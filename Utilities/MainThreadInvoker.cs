using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedStreamChat.Utilities
{
    public class MainThreadInvoker : PersistentSingleton<MainThreadInvoker>
    {
        //public static int MAX_INVOKES_PER_FRAME = 10;
        private static ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();
        private void Update()
        {
            //int count = 0;
            //while(count++ < MAX_INVOKES_PER_FRAME && _actions.TryDequeue(out var action))
            if(_actions.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch(Exception ex)
                {
                    Logger.log.Error(ex);
                }
            }
        }

        public static void ClearQueue()
        {
            _actions = new ConcurrentQueue<Action>();
        }

        public static void Invoke(Action action)
        {
            if (action != null)
            {
                _actions.Enqueue(action);
            }
        }

        public static void Invoke<A>(Action<A> action, A a)
        {
            if (action != null)
            {
                _actions.Enqueue(() => action?.Invoke(a));
            }
        }

        public static void Invoke<A, B>(Action<A, B> action, A a, B b)
        {
            if (action != null)
            {
                _actions.Enqueue(() => action?.Invoke(a, b));
            }
        }
    }
}
