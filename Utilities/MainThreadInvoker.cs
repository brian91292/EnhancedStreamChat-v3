using IPA.Utilities.Async;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedStreamChat.Utilities
{
    public class MainThreadInvoker
    {
        private static CancellationTokenSource _cancellationToken = new CancellationTokenSource();
        public static void ClearQueue()
        {
            _cancellationToken.Cancel();
            _cancellationToken = new CancellationTokenSource();
        }

        public static void Invoke(Action action)
        {
            if (action != null)
            {
                UnityMainThreadTaskScheduler.Factory.StartNew(action, _cancellationToken.Token);
            }
        }

        public static void Invoke<A>(Action<A> action, A a)
        {
            if (action != null)
            {
                UnityMainThreadTaskScheduler.Factory.StartNew(() => action?.Invoke(a), _cancellationToken.Token);
            }
        }

        public static void Invoke<A, B>(Action<A, B> action, A a, B b)
        {
            if (action != null)
            {
                UnityMainThreadTaskScheduler.Factory.StartNew(() => action?.Invoke(a, b), _cancellationToken.Token);
            }
        }
    }
}
