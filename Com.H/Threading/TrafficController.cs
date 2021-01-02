using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

namespace Com.H.Threading
{
    public class TrafficController
    {
        #region queue calls
        private readonly Dictionary<object, LockKey> queueLocks;
        private readonly object queueLockObj;

        public TrafficController() =>
            (this.queueLocks, this.queueLockObj) =
            (new Dictionary<object, LockKey>(), new object());

        /// <summary>
        /// Controls the concurrent / multi-threaded calls to a single Action 
        /// by queuing multi-threaded calls and
        /// bouncing off (not executing) extra calls that exceed the queueLength
        /// </summary>
        /// <param name="action"></param>
        /// <param name="key">By default, this method could automatically tell 
        /// the unique signature of the Action to determine whether or not 
        /// another thread is currently executing it.
        /// However, when the Action has input variables that aren't final 
        /// (e.g. SomeAction(5,2) <= final values, SomeAction(x,y) <= non   final), 
        /// then a unique key is needed to identify the Action signature,
        /// otherwise this method would always default to allowing unlimited multi-threaded calls to 
        /// execute the Action if it couldn't identify its unique signature (e.g. any unique string can be used here). </param>
        /// <param name="queueLength">Maximum queue length, default is 0 (i.e. no queue, only one call is allowed, any extra concurrent calls are ignored)</param>
        public void QueueCall(Action action, int queueLength = 0, object key = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (key == null) key = action;
            LockKey lockKey = null;
            lock (queueLockObj)
            {
                if (this.queueLocks.ContainsKey(key)) lockKey = this.queueLocks[key];

                if (lockKey != null && lockKey.Count > queueLength)
                    return;

                if (lockKey == null)
                {
                    lockKey = new LockKey() { Key = key, Count = 1 };
                    this.queueLocks[key] = lockKey;
                }
                else
                    lockKey.Count++;
            }
            action();
            lock (queueLockObj)
                queueLocks.Remove(key);
        }



        /// <summary>
        /// Controls the concurrent / multi-threaded calls to a single Action 
        /// by queuing multi-threaded calls and
        /// bouncing off (not executing) extra calls that exceed the queueLength value.
        /// The action that gets executed is done asynchronously on this method.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="key">By default, this method could automatically tell 
        /// the unique signature of the Action to determine whether or not 
        /// another thread is currently executing it.
        /// However, when the Action has input variables that aren't final 
        /// (e.g. SomeAction(5,2) <= final values, SomeAction(x,y) <= non   final), 
        /// then a unique key is needed to identify the Action signature,
        /// otherwise this method would always default to allowing unlimited multi-threaded calls to 
        /// execute the Action if it couldn't identify its unique signature. </param>
        /// <param name="queueLength">Maximum queue length, default is 0 (i.e. no queue, only one call is allowed, any extra concurrent calls are ignored)</param>
        public Task QueueCallAsync(Action action, int queueLength = 0, object key = null)
        {
            if (key == null) key = action;
            LockKey lockKey = null;
            lock (queueLockObj)
            {
                if (this.queueLocks.ContainsKey(key)) lockKey = this.queueLocks[key];

                if (lockKey != null && lockKey.Count > queueLength)
                    return Task.CompletedTask;

                if (lockKey == null)
                {
                    lockKey = new LockKey() { Key = key, Count = 1 };
                    this.queueLocks[key] = lockKey;
                }
                else
                    lockKey.Count++;
            }

            return Task.Run(()=>action()).ContinueWith(_=>
            {
                lock (queueLockObj)
                    queueLocks.Remove(key);
            },TaskScheduler.Default);
        }


        /// <summary>
        /// Controls the concurrent / multi-threaded calls to a single Func 
        /// by queuing multi-threaded calls and
        /// bouncing off (not executing) extra calls that exceed the queue_length
        /// </summary>
        /// <typeparam name="T">Result value of the the Func.</typeparam>
        /// <param name="func">The Func delegate to be called</param>
        /// <param name="defaultValue">Default value of T that gets returned in the event of a calls is bounced off the queue</param>
        /// <param name="key">By default, this method could automatically tell 
        /// the unique signature of the Func to determine whether or not 
        /// another thread is currently executing it.
        /// However, when the Func has input variable that aren't final 
        /// (e.g. SomeFunc(5,2) <= final values, SomeFunc(x,y) <= non final), 
        /// then a unique key is needed to identify the Func signature,
        /// as otherwise this method would always default to allowing unlimited multi-threaded calls to 
        /// execute the Func if it couldn't identify its unique signature. 
        /// </param>
        /// <param name="queueLength">Maximum queue length, default is 0 (i.e. no queue, only one call is allowed, any extra concurrent calls are ignored)</param>
        /// <returns>T</returns>
        public T QueueCall<T>(Func<T> func,
            T defaultValue = default,
            int queueLength = 0,
            object key = null
            )
        {
            if (func == null) return defaultValue;
            if (key == null) key = func;
            LockKey lockKey = null;
            lock (queueLockObj)
            {
                if (this.queueLocks.ContainsKey(key)) lockKey = this.queueLocks[key];

                if (lockKey != null && lockKey.Count > queueLength)
                    return defaultValue;

                if (lockKey == null)
                {
                    lockKey = new LockKey() { Key = key, Count = 1 };
                    this.queueLocks[key] = lockKey;
                }
                else
                    lockKey.Count++;
                
            }
            T result = func();
            lock (queueLockObj)
            {
                queueLocks.Remove(key);
                return result;
            }

        }

        /// <summary>
        /// Controls the concurrent / multi-threaded calls to a single Func 
        /// by queuing multi-threaded calls and
        /// bouncing off (not executing) extra calls that exceed the queueLength value.
        /// The func that gets executed is done asynchronously on this method.
        /// </summary>
        /// <typeparam name="T">Result value of the the Func.</typeparam>
        /// <param name="func">The Func delegate to be called</param>
        /// <param name="defaultValue">Default value of T that gets returned in the event of a calls is bounced off the queue</param>
        /// <param name="key">By default, this method could automatically tell 
        /// the unique signature of the Func to determine whether or not 
        /// another thread is currently executing it.
        /// However, when the Func has input variable that aren't final 
        /// (e.g. SomeFunc(5,2) <= final values, SomeFunc(x,y) <= non final), 
        /// then a unique key is needed to identify the Func signature,
        /// as otherwise this method would always default to allowing unlimited multi-threaded calls to 
        /// execute the Func if it couldn't identify its unique signature. 
        /// </param>
        /// <param name="queueLength">Maximum queue length, default is 0 (i.e. no queue, only one call is allowed, any extra concurrent calls are ignored)</param>
        /// <returns>T</returns>
        public Task<T> QueueCallAsync<T>(Func<T> func,
            T defaultValue = default,
            int queueLength = 0,
            object key = null
            )
        {
            if (func == null) return Task.FromResult(defaultValue);
            if (key == null) key = func;
            LockKey lockKey = null;
            lock (queueLockObj)
            {
                if (this.queueLocks.ContainsKey(key)) lockKey = this.queueLocks[key];

                if (lockKey != null && lockKey.Count > queueLength)
                    return Task.FromResult(defaultValue);

                if (lockKey == null)
                {
                    lockKey = new LockKey() { Key = key, Count = 1 };
                    this.queueLocks[key] = lockKey;
                }
                else
                    lockKey.Count++;
            }
            var result = Task.Run<T>(() => func());
            result.ConfigureAwait(false);
            result?.ContinueWith((_) =>
                {
                    lock (queueLockObj)
                    {
                        queueLocks.Remove(key);
                    }
                }, TaskScheduler.Default);

            return result;

        }


        #endregion

    }
}
