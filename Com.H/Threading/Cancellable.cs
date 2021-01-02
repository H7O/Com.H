using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Threading
{
    public static class Cancellable
    {
        /// <summary>
        /// Attempts Thread.Abort() for older .NET 4.x if set to true for Cancellable
        /// </summary>
        public static bool EnableThreadAbort { get; set; }
        /// <summary>
        /// Waits for a task completion with timeout limit option.
        /// If the task doesn't finish within the timeout limit, the actionOnTimeout Action is called.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task">The task for which to wait.</param>
        /// <param name="timeout">Timeout in miliseconds</param>
        /// <param name="token">Optional cancellation token that cancels the execution and calls actionOnTimeout Action</param>
        /// <param name="actionOnTimeout">an Action that gets called on task timedout or cancellation requested</param>
        public static void CancellableWait<T>(
            this Task<T> task,
            int? timeout = null,
            CancellationToken? token = null,
            Action actionOnTimeout = null
            )
        {
            timeout ??= -1;
            var delayTask = token == null ?
                Task.Delay((int)timeout) :
                Task.Delay((int)timeout, (CancellationToken)token);
            var result = Task.WhenAny(task, delayTask).GetAwaiter().GetResult();

            if (actionOnTimeout != null
                && result == delayTask
                && delayTask.IsCompleted
                ) actionOnTimeout();
        }
        public static void CancellableRun(Action action, CancellationToken token)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            try
            {
                Task.Run(() =>
                {
                    using (var reg = token.Register(() =>
                    {
                        try
                        {
                            Thread.CurrentThread.Interrupt();
                        }
                        catch { }

                        //try
                        //{
                        //    // hard unsafe exit supported by older .net framework runtimes
                        //    if (EnableThreadAbort)
                        //        Thread.CurrentThread.Abort();
                        //}
                        //catch { }
                    }))
                    action();
                }, token).GetAwaiter().GetResult();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (TaskCanceledException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (ThreadAbortException)
            {
            }
            catch
            {
                throw;
            }
        }

        public static T CancellableRun<T>(Func<T> func, CancellationToken token)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            try
            {
                return Task.Run<T>(() =>
                {
                    using (var reg = token.Register(() =>
                    {
                        try
                        {
                            Thread.CurrentThread.Interrupt();
                        }
                        catch { }

                        //try
                        //{
                        //    // hard unsafe exit supported by older .net framework runtimes
                        //    if (EnableThreadAbort)
                        //        Thread.CurrentThread.Abort();
                        //}
                        //catch { }
                    }))
                    {
                        return func();
                    }

                }, token).GetAwaiter().GetResult();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (TaskCanceledException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (ThreadAbortException)
            {
            }
            catch
            {
                throw;
            }

            return default;
        }
        public static Task CancellableRunAsync(Action action, CancellationToken token)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var t = new Task(() =>
            {
                using (var reg = token.Register(() =>
                {
                    try
                    {
                        Thread.CurrentThread.Interrupt();
                    }
                    catch { }

                    //try
                    //{
                    //    // hard exit supported by older .net framework runtimes
                    //    if (EnableThreadAbort)
                    //        Thread.CurrentThread.Abort();
                    //}
                    //catch{}
                }))

                    try
                    {
                        action();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (TaskCanceledException)
                    {
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (ThreadAbortException)
                    {
                    }
                    catch
                    {
                        throw;
                    }

            }, token);

            t.ConfigureAwait(false);
            t.Start();
            return t;
        }

        public static Task<T> CancellableRunAsync<T>(Func<T> func, CancellationToken token)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            var t = new Task<T>(() =>
            {
                using (var reg = token.Register(() =>
                {
                    try
                    {
                        Thread.CurrentThread.Interrupt();
                    }
                    catch { }

                    //try
                    //{
                    //    // hard exit supported by older .net framework runtimes
                    //    if (EnableThreadAbort)
                    //        Thread.CurrentThread.Abort();
                    //}
                    //catch{}
                }))

                    try
                    {
                        return func();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (TaskCanceledException)
                    {
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (ThreadAbortException)
                    {
                    }
                    catch
                    {
                        throw;
                    }
                return default;

            }, token);

            t.ConfigureAwait(false);
            t.Start();
            return t;
        }

        


    }
}
