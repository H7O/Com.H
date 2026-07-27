using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Threading
{
    /// <summary>
    /// Provides methods for running actions and functions with cancellation support and timeout handling.
    /// </summary>
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
            Action? actionOnTimeout = null
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
        /// <summary>
        /// Runs an action with cancellation support and optional timeout.
        /// The action can be interrupted via the cancellation token.
        /// </summary>
        /// <param name="action">The action to run</param>
        /// <param name="token">Cancellation token to cancel the action</param>
        /// <param name="timeout">Optional timeout to wait before interrupting the action after cancellation is requested</param>
        /// <exception cref="ArgumentNullException">Thrown when action is null</exception>
        public static void CancellableRun(Action action, CancellationToken token, TimeSpan? timeout = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            bool done = false;
            try
            {
                Task.Run(() =>
                {
                    // The Register callback below runs on whichever thread calls
                    // token.Cancel() — interrupting Thread.CurrentThread there would
                    // interrupt the CANCELLER. Capture the worker so the timeout
                    // interrupt hits the (possibly hung) action instead.
                    var worker = Thread.CurrentThread;
                    using var reg = token.Register(() =>
                    {
                        try
                        {

                            if (!done && timeout != null)
                            {
                                // Monotonic on purpose: a wall-clock step (NTP correction,
                                // DST on DateTime.Now, VM restore) must not stretch the grace
                                // window, nor collapse it and interrupt a live worker early.
                                var grace = Stopwatch.StartNew();
                                while (grace.Elapsed < (TimeSpan)timeout && !done)
                                {
                                    Task.Delay(500).GetAwaiter().GetResult();
                                }

                            }
                            if (done)
                            {
                                return;
                            }

                            worker.Interrupt();
                        }
                        catch { }

                        //try
                        //{
                        //    // hard unsafe exit supported by older .net framework runtimes
                        //    if (EnableThreadAbort)
                        //        Thread.CurrentThread.Abort();
                        //}
                        //catch { }
                    }
                    );
                    try { action(); } finally { done = true; }
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
            catch (ThreadInterruptedException)
            {
            }
            catch
            {
                throw;
            }
            finally
            {
                done = true;
            }
        }

        public static T? CancellableRun<T>(Func<T?>? func, CancellationToken token, TimeSpan? timeout = null)
        {
            if (func is null) throw new ArgumentNullException(nameof(func));
            bool done = false;
            try
            {

                return Task.Run<T?>(() =>
                {
                    // See CancellableRun(Action): interrupt the worker, not the canceller.
                    var worker = Thread.CurrentThread;
                    using var reg = token.Register(() =>
                    {
                        try
                        {

                            if (!done && timeout != null)
                            {
                                // Monotonic on purpose: a wall-clock step (NTP correction,
                                // DST on DateTime.Now, VM restore) must not stretch the grace
                                // window, nor collapse it and interrupt a live worker early.
                                var grace = Stopwatch.StartNew();
                                while (grace.Elapsed < (TimeSpan)timeout && !done)
                                {
                                    Task.Delay(500).GetAwaiter().GetResult();
                                }

                            }
                            if (done)
                            {
                                return;
                            }

                            worker.Interrupt();
                        }
                        catch { }

                        //try
                        //{
                        //    // hard unsafe exit supported by older .net framework runtimes
                        //    if (EnableThreadAbort)
                        //        Thread.CurrentThread.Abort();
                        //}
                        //catch { }
                    });
                    try { return func(); } finally { done = true; }

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
            catch (ThreadInterruptedException)
            {
            }
            catch
            {
                throw;
            }
            finally
            {
                done = true;
            }

            return default;
        }
        /// <summary>
        /// Runs an action asynchronously with cancellation support and optional timeout.
        /// The action can be interrupted via the cancellation token.
        /// </summary>
        /// <param name="action">The action to run</param>
        /// <param name="token">Cancellation token to cancel the action</param>
        /// <param name="timeout">Optional timeout to wait before interrupting the action after cancellation is requested</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when action is null</exception>
        public static Task CancellableRunAsync(Action action, CancellationToken token, TimeSpan? timeout = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            // A task constructed with an already-cancelled token is born Canceled;
            // calling Start() on it throws InvalidOperationException. Return a
            // canceled task instead so callers racing a cancel don't blow up.
            if (token.IsCancellationRequested) return Task.FromCanceled(token);
            bool done = false;
            var t = new Task(() =>
            {
                // See CancellableRun(Action): interrupt the worker, not the canceller.
                var worker = Thread.CurrentThread;
                using var reg = token.Register(() =>
                {
                    try
                    {

                        if (!done && timeout != null)
                        {
                            // Monotonic on purpose: a wall-clock step (NTP correction,
                            // DST on DateTime.Now, VM restore) must not stretch the grace
                            // window, nor collapse it and interrupt a live worker early.
                            var grace = Stopwatch.StartNew();
                            while (grace.Elapsed < (TimeSpan)timeout && !done)
                            {
                                Task.Delay(500).GetAwaiter().GetResult();
                            }

                        }
                        if (done)
                        {
                            return;
                        }
                        worker.Interrupt();
                    }
                    catch { }

                    //try
                    //{
                    //    // hard exit supported by older .net framework runtimes
                    //    if (EnableThreadAbort)
                    //        Thread.CurrentThread.Abort();
                    //}
                    //catch{}
                });

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
                catch (ThreadInterruptedException)
                {
                }
                catch
                {
                    throw;
                }
                finally
                {
                    done = true;
                }


            }, token);

            t.ConfigureAwait(false);
            t.Start();
            return t;
        }

        /// <summary>
        /// Runs a function asynchronously with cancellation support and optional timeout, returning the result.
        /// The function can be interrupted via the cancellation token.
        /// </summary>
        /// <typeparam name="T">The return type of the function</typeparam>
        /// <param name="func">The function to run</param>
        /// <param name="token">Cancellation token to cancel the function</param>
        /// <param name="timeout">Optional timeout to wait before interrupting the function after cancellation is requested</param>
        /// <returns>A task representing the asynchronous operation with the result</returns>
        /// <exception cref="ArgumentNullException">Thrown when func is null</exception>
        public static Task<T?> CancellableRunAsync<T>(Func<T> func, CancellationToken token, TimeSpan? timeout = null)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            // See CancellableRunAsync(Action): don't Start a born-cancelled task.
            if (token.IsCancellationRequested) return Task.FromCanceled<T?>(token);
            bool done = false;
            var t = new Task<T?>(() =>
            {
                // See CancellableRun(Action): interrupt the worker, not the canceller.
                var worker = Thread.CurrentThread;
                using (var reg = token.Register(() =>
                {
                    try
                    {

                        if (!done && timeout != null)
                        {
                            // Monotonic on purpose: a wall-clock step (NTP correction,
                            // DST on DateTime.Now, VM restore) must not stretch the grace
                            // window, nor collapse it and interrupt a live worker early.
                            var grace = Stopwatch.StartNew();
                            while (grace.Elapsed < (TimeSpan)timeout && !done)
                            {
                                Task.Delay(500).GetAwaiter().GetResult();
                            }

                        }
                        if (done)
                        {
                            return;
                        }

                        worker.Interrupt();
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
                    catch (ThreadInterruptedException)
                    {
                    }

                    catch
                    {
                        throw;
                    }
                    finally
                    {
                        done = true;
                    }
                return default;

            }, token);

            t.ConfigureAwait(false);
            t.Start();
            return t;
        }




    }
}
