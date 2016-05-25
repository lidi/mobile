using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Helpers
{
    public static class ReactiveExtensions
    {
        public class SimpleObserver<T> : IObserver<T>
        {
            readonly Action<T> onNext;
            public void OnCompleted() { }  // Do nothing
            public void OnError(Exception error) { }  // Do nothing
            public void OnNext(T value)
            {
                onNext(value);
            }
            public SimpleObserver(Action<T> onNext)
            {
                this.onNext = onNext;
            }
        }

        /// <summary>
        /// Help method when Rx is not available
        /// </summary>
        public static IDisposable SubscribeSimple<T> (this IObservable<T> observable, Action<T> action)
        {
            return observable.Subscribe(new SimpleObserver<T> (action));
        }

        /// <summary>
        /// Filters out null results from chooser function
        /// </summary>
        public static IObservable<U> Choose<T, U> (this IObservable<T> observable, Func<T, U> chooser)
        where U : class
        {
            return observable.Select(chooser).Where(x => x != null);
        }

        public static IObservable<T> TimedBuffer<T> (this IObservable<T> observable, int milliseconds)
        {
            if (milliseconds <= 0)
                throw new ArgumentException($"{nameof(milliseconds)} must be bigger than 0");

            var queue = new System.Collections.Concurrent.ConcurrentQueue<T>();
            var timer = new System.Timers.Timer(milliseconds) { AutoReset = true };

            return Observable.Create((IObserver<T> obs) =>
            {
                T next = default(T);
                timer.Elapsed += (sender, timestamp) =>
                {
                    if (queue.TryDequeue(out next))
                        obs.OnNext(next);
                };
                timer.Start();
                var disp = observable.Subscribe(queue.Enqueue);
                return () =>
                {
                    disp.Dispose();
                    timer.Dispose();
                };
            });
        }

        /// <summary>
        /// Processes async operations one after the other (SelectMany does it in parallel)
        /// </summary>
        public static IObservable<U> SelectAsync<T, U> (this IObservable<T> observable, Func<T, Task<U>> selector)
        {
            // SelectMany would process tasks in parallel, see https://goo.gl/eayv5N
            return observable.Select(x => Observable.FromAsync(() => selector(x))).Concat();
        }

        public static IObservable<Unit> SelectAsync<T> (this IObservable<T> observable, Func<T, Task> selector)
        {
            // SelectMany would process tasks in parallel, see https://goo.gl/eayv5N
            return observable.Select(x => Observable.FromAsync(() => selector(x))).Concat();
        }
    }
}

