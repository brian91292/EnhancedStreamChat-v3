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
        private static ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();
        private void FixedUpdate()
        {
            //int actionCount = 0;
            float startTime = Time.realtimeSinceStartup;
            while (_actions.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                    //actionCount++;
                }
                catch(Exception ex)
                {
                    Logger.log.Error(ex);
                }
                if(Time.realtimeSinceStartup - startTime >= 0.0005f)
                {
                    break;
                }
            }
            //if (actionCount > 0)
            //{
            //    Logger.log.Debug($"Executed {actionCount} actions.");
            //}
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
