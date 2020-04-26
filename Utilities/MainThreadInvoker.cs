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
        private static TaskFactory _taskFactory = new TaskFactory(_cancellationToken.Token, TaskCreationOptions.None, TaskContinuationOptions.None, UnityMainThreadTaskScheduler.Default);
        public static void ClearQueue()
        {
            _cancellationToken.Cancel();
            _cancellationToken = new CancellationTokenSource();
            _taskFactory = new TaskFactory(_cancellationToken.Token, TaskCreationOptions.None, TaskContinuationOptions.None, UnityMainThreadTaskScheduler.Default);
        }

        public static void Invoke(Action action)
        {
            if (action != null)
            {
                _taskFactory.StartNew(action);
            }
        }

        public static void Invoke<A>(Action<A> action, A a)
        {
            if (action != null)
            {
                _taskFactory.StartNew(() => action?.Invoke(a));
            }
        }

        public static void Invoke<A, B>(Action<A, B> action, A a, B b)
        {
            if (action != null)
            {
                _taskFactory.StartNew(() => action?.Invoke(a, b));
            }
        }
    }
}
